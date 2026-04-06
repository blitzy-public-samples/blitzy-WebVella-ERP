# =============================================================================
# Multi-Stage Dockerfile for WebVella ERP
# =============================================================================
#
# This Dockerfile builds and packages the WebVella ERP application — an
# open-source ASP.NET Core 9 ERP platform backed by PostgreSQL 16.
#
# Build stage:  mcr.microsoft.com/dotnet/sdk:9.0
#   - Restores NuGet packages with Docker layer caching (.csproj-first pattern)
#   - Compiles and publishes the WebVella.Erp.Site project in Release mode
#
# Runtime stage: mcr.microsoft.com/dotnet/aspnet:9.0
#   - Lightweight image containing only the published application
#   - Includes curl for Docker Compose health check support
#   - Listens on port 5000
#
# Usage:
#   docker build -t webvella-erp .
#   docker run -p 5000:5000 webvella-erp
#
# Used by the companion docker-compose.yml for the security scanning environment
# alongside a PostgreSQL 16 database container.
#
# Source: WebVella.Erp.Site/WebVella.Erp.Site.csproj (net9.0, AssemblyName=WebVella.Erp.Site)
# Source: WebVella.Erp.Site/Program.cs (entry point using WebHost.CreateDefaultBuilder)
# =============================================================================

# ---------------------------------------------------------------------------
# Stage 1: Build
# ---------------------------------------------------------------------------
# Uses the .NET 9.0 SDK image to restore NuGet packages, compile, and publish
# the WebVella.Erp.Site project and all its transitive project dependencies:
#   - WebVella.Erp          (core library)
#   - WebVella.Erp.Web      (web layer, Razor components, controllers)
#   - WebVella.Erp.Plugins.SDK (SDK plugin)
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files first for efficient Docker layer caching.
# When only source code changes (not project references or NuGet packages),
# Docker reuses the cached restore layer, significantly speeding up rebuilds.
#
# Dependency chain (from WebVella.Erp.Site.csproj ProjectReference entries):
#   WebVella.Erp.Site → WebVella.Erp.Plugins.SDK → WebVella.Erp.Web → WebVella.Erp
#   WebVella.Erp.Site → WebVella.Erp.Web → WebVella.Erp
#   WebVella.Erp.Site → WebVella.Erp
COPY WebVella.Erp/WebVella.Erp.csproj WebVella.Erp/
COPY WebVella.Erp.Web/WebVella.Erp.Web.csproj WebVella.Erp.Web/
COPY WebVella.Erp.Plugins.SDK/WebVella.Erp.Plugins.SDK.csproj WebVella.Erp.Plugins.SDK/
COPY WebVella.Erp.Site/WebVella.Erp.Site.csproj WebVella.Erp.Site/

# Handle cross-platform case-sensitivity difference.
# The solution was developed on Windows (case-insensitive filesystem). Several
# .csproj files reference '../WebVella.ERP/WebVella.Erp.csproj' (uppercase ERP),
# but the actual directory on the Linux filesystem is 'WebVella.Erp' (mixed case).
# On Linux, 'WebVella.ERP' != 'WebVella.Erp', so dotnet restore and build would
# fail without this symbolic link.
# Affected files:
#   - WebVella.Erp.Web/WebVella.Erp.Web.csproj:      ../WebVella.ERP/WebVella.Erp.csproj
#   - WebVella.Erp.Plugins.SDK/WebVella.Erp.Plugins.SDK.csproj: ../WebVella.ERP/WebVella.Erp.csproj
RUN ln -s WebVella.Erp WebVella.ERP

# Restore NuGet packages for the Site project and all transitive dependencies.
# This is the cached layer — it only re-runs when .csproj files change.
RUN dotnet restore WebVella.Erp.Site/WebVella.Erp.Site.csproj

# Copy the entire source tree for compilation.
# This layer is invalidated whenever any source file changes, but the restore
# layer above remains cached as long as project files are unchanged.
COPY . .

# Re-create the symbolic link after the full COPY.
# Docker COPY is additive and does not remove existing files, but we re-create
# the symlink defensively to guarantee the case-sensitive project references
# resolve correctly during the build phase.
RUN ln -sf WebVella.Erp WebVella.ERP

# Publish the WebVella.Erp.Site project in Release configuration.
# --no-restore: skips restore since packages were already restored above.
# -o /app/publish: outputs the self-contained publish artifacts to /app/publish.
# Source: WebVella.Erp.Site/WebVella.Erp.Site.csproj — TargetFramework: net9.0
RUN dotnet publish WebVella.Erp.Site/WebVella.Erp.Site.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Ensure Config.json is present in the publish output with correct casing.
# Startup.cs (line 42) loads "config.json" (lowercase) via ConfigurationBuilder,
# but the repository file is "Config.json" (title case). On Linux these are
# different filenames, so we copy it with the lowercase name the app expects.
# Source: WebVella.Erp.Site/Startup.cs:L42-43
RUN cp WebVella.Erp.Site/Config.json /app/publish/config.json

# ---------------------------------------------------------------------------
# Stage 2: Runtime
# ---------------------------------------------------------------------------
# Uses the lightweight ASP.NET Core 9.0 runtime image.
# Contains only the published application — no SDK, no source code, no build
# tools — resulting in a significantly smaller and more secure final image.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0

WORKDIR /app

# Install curl for Docker Compose health check support.
# The docker-compose.yml health check polls the metadata endpoint:
#   curl -sf http://localhost:5000/api/v3/en_US/meta
# This verifies the application has started and can serve API requests.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Copy the published application from the build stage
COPY --from=build /app/publish .

# Expose port 5000 for HTTP traffic.
# The application is configured to listen on this port via ASPNETCORE_URLS.
EXPOSE 5000

# Configure ASP.NET Core to listen on port 5000 on all network interfaces.
# The '+' wildcard binds to 0.0.0.0 so the application is reachable from
# outside the container (required for Docker networking and port mapping).
ENV ASPNETCORE_URLS=http://+:5000

# Default to Development environment for security scanning.
# This enables detailed error pages and developer exception middleware,
# which is appropriate for the security assessment scanning environment.
# Override via docker-compose.yml or docker run -e for other environments.
ENV ASPNETCORE_ENVIRONMENT=Development

# Launch the WebVella ERP application.
# Assembly name 'WebVella.Erp.Site' is defined in the .csproj at line 6:
#   <AssemblyName>WebVella.Erp.Site</AssemblyName>
# The application uses WebHost.CreateDefaultBuilder with Kestrel as the
# web server (Program.cs), configured through Startup.cs.
ENTRYPOINT ["dotnet", "WebVella.Erp.Site.dll"]
