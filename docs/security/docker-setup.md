<!--{"sort_order": 2, "name": "docker-setup", "label": "Docker Environment Setup"}-->
# Docker Environment Setup

## Overview

This guide documents the procedure to set up the WebVella ERP application and its PostgreSQL 16 dependency using Docker Compose for security scanning. The WebVella ERP repository does not ship with a `Dockerfile` or `docker-compose.yml`, so this guide includes the creation of both files from scratch.

The containerized environment provides an isolated, reproducible target for running OWASP ZAP and Nuclei security scans against the full WebVella API surface. Once the containers are running and the health check passes, you can proceed to [Authentication](authentication.md) for JWT token acquisition.

> **Source**: The repository URL is `https://github.com/WebVella/WebVella-ERP` and the project targets ASP.NET Core 9 with PostgreSQL 16 (`Source: README.md`).

---

## Prerequisites

Before starting, ensure the following tools and resources are available on your host machine:

| Prerequisite | Minimum Version | Purpose |
|---|---|---|
| Docker Engine | 24.0+ | Container runtime for all services |
| Docker Compose | V2 (2.20+) | Multi-container orchestration |
| Git | 2.30+ | Repository cloning |
| Available RAM | 4 GB | Minimum for web + database containers |
| Port 5000 | Free | WebVella ERP HTTP endpoint |
| Port 5432 | Free | PostgreSQL database |

Verify your Docker installation:

```bash
docker --version
docker compose version

```text

---

## Step 1: Repository Cloning

Clone the WebVella ERP repository from GitHub and navigate into the project directory:

```bash
git clone https://github.com/WebVella/WebVella-ERP.git
cd WebVella-ERP

```

> **Source**: Repository URL from `README.md`. The README describes WebVella ERP as "a free and open-source web software" targeting ASP.NET Core 9 and PostgreSQL 16.

---

## Step 2: Dockerfile Creation

Create a `Dockerfile` at the repository root. This uses a multi-stage build to produce an optimized runtime image.

**Build stage**: Uses the .NET 9.0 SDK image (`mcr.microsoft.com/dotnet/sdk:9.0`) to restore, build, and publish the application.

**Runtime stage**: Uses the ASP.NET Core 9.0 runtime image (`mcr.microsoft.com/dotnet/aspnet:9.0`) for a minimal production footprint.

Key project details informing the Dockerfile:

- **Target framework**: `net9.0` (`Source: WebVella.Erp.Site/WebVella.Erp.Site.csproj:L4`)
- **Hosting model**: InProcess (`Source: WebVella.Erp.Site/WebVella.Erp.Site.csproj:L15`)
- **Project references**: The site project references `WebVella.Erp.Plugins.SDK`, `WebVella.Erp.Web`, and `WebVella.Erp` (`Source: WebVella.Erp.Site/WebVella.Erp.Site.csproj:L42-44`), so the entire repository must be copied into the build context.

Create the file `Dockerfile` in the repository root with the following content:

```dockerfile
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

# Create a non-root user for defense-in-depth.
# Running containers as a non-root user limits the blast radius of any
# container escape exploit. The 'appuser' account has no login shell and
# owns the /app working directory.
RUN groupadd --gid 1000 appuser \
    && useradd --uid 1000 --gid appuser --shell /bin/false --create-home appuser

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

# Ensure the non-root user owns the application directory so it can
# read the published assemblies and write to config.json if needed.
RUN chown -R appuser:appuser /app

# Switch to the non-root user for runtime execution.
# This follows Docker security best practice — the application process
# runs with minimal privileges, reducing risk from container escape exploits.
USER appuser

# Launch the WebVella ERP application.
# Assembly name 'WebVella.Erp.Site' is defined in the .csproj at line 6:
#   <AssemblyName>WebVella.Erp.Site</AssemblyName>
# The application uses WebHost.CreateDefaultBuilder with Kestrel as the
# web server (Program.cs), configured through Startup.cs.
ENTRYPOINT ["dotnet", "WebVella.Erp.Site.dll"]

```text

### Dockerfile Explanation

| Feature | Details |
|---|---|
| **Multi-stage build** | Stage 1 (`build`) uses `.NET 9.0 SDK` for compilation; Stage 2 uses the lightweight `ASP.NET 9.0 runtime` image |
| **Layer caching** | Individual `.csproj` files are copied before `COPY . .` so the NuGet restore layer is cached when only source files change — significantly faster rebuilds |
| **Case-sensitivity handling** | `ln -s WebVella.Erp WebVella.ERP` resolves cross-platform casing differences. Several `.csproj` files reference `../WebVella.ERP/` (uppercase) while the Linux filesystem directory is `WebVella.Erp` (mixed case). Without this symlink, the build fails on Linux (`Source: WebVella.Erp.Web/WebVella.Erp.Web.csproj`, `WebVella.Erp.Plugins.SDK/WebVella.Erp.Plugins.SDK.csproj`) |
| **Config.json copy** | `cp WebVella.Erp.Site/Config.json /app/publish/config.json` ensures the configuration file has the lowercase casing required by `Startup.cs:L42` on case-sensitive Linux filesystems |
| **curl installation** | Installs `curl` in the runtime image so the Docker Compose health check (`curl -sf http://localhost:5000/api/v3/en_US/meta`) can execute inside the container |
| **Non-root user** | Creates `appuser` (UID 1000) and runs the application as a non-root user for defense-in-depth, reducing the blast radius of container escape exploits |
| **ASPNETCORE_ENVIRONMENT** | Defaults to `Development` for the security scanning environment, enabling detailed error pages and developer exception middleware (`Source: WebVella.Erp.Site/Startup.cs:L147-150`) |
| **Port 5000** | `EXPOSE 5000` and `ASPNETCORE_URLS=http://+:5000` configure the application to listen on port 5000 on all network interfaces |

> **Note**: The `.csproj` files are copied individually before `COPY . .` to leverage Docker layer caching. When only source code changes (not project references or NuGet package versions), Docker reuses the cached restore layer, significantly speeding up rebuilds. The dependency chain is: `WebVella.Erp.Site` → `WebVella.Erp.Plugins.SDK` → `WebVella.Erp.Web` → `WebVella.Erp` (`Source: WebVella.Erp.Site/WebVella.Erp.Site.csproj:L42-44`).

---

## Step 3: Docker Compose Configuration

Create a `docker-compose.yml` file at the repository root to orchestrate the WebVella ERP web application and its PostgreSQL 16 database dependency.

```yaml
services:
  db:
    image: postgres:16
    container_name: webvella-db
    environment:
      POSTGRES_USER: webvella
      POSTGRES_PASSWORD: webvella
      POSTGRES_DB: erp3
    ports:

      - "5432:5432"
    volumes:

      - pgdata:/var/lib/postgresql/data
    networks:

      - webvella-net
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U webvella -d erp3"]
      interval: 10s
      timeout: 5s
      retries: 5
    restart: unless-stopped

  web:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: webvella-web
    ports:

      - "5000:5000"
    depends_on:
      db:
        condition: service_healthy
    environment:
      ASPNETCORE_URLS: "http://+:5000"
      ASPNETCORE_ENVIRONMENT: "Development"
    entrypoint: ["/bin/bash", "-c"]
    command:

      - >-
        sed -i
        's|Server=192.168.0.190;Port=5436;User Id=test;Password=test|Server=db;Port=5432;User Id=webvella;Password=webvella|g'
        /app/config.json &&
        exec dotnet WebVella.Erp.Site.dll
    networks:

      - webvella-net
    healthcheck:
      test: ["CMD-SHELL", "curl -sf http://localhost:5000/api/v3/en_US/meta || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 10
      start_period: 60s
    restart: unless-stopped

volumes:
  pgdata:
    driver: local

networks:
  webvella-net:
    driver: bridge

```

### Docker Compose Service Details

| Service | Image | Port | Purpose |
|---|---|---|---|
| `web` | Built from local `Dockerfile` | 5000 | WebVella ERP ASP.NET Core 9 application |
| `db` | `postgres:16` | 5432 | PostgreSQL 16 database backend |

**Key configuration points**:

- **`depends_on` with health check**: The `web` service waits for PostgreSQL to report healthy via `pg_isready -U webvella -d erp3` before starting. This prevents the application from failing due to a database connection timeout on first startup.
- **Connection string override via `sed`**: The application loads configuration exclusively from `config.json` via a custom `ConfigurationBuilder` that does **not** call `AddEnvironmentVariables()` (`Source: WebVella.Erp.Site/Startup.cs:L42-43`). Therefore, the `web` service overrides the Docker `ENTRYPOINT` to run a `sed` command that patches the connection string at container startup, replacing the default development server address (`192.168.0.190:5436`, `test/test`) with the Docker service hostname (`db:5432`) and the `webvella/webvella` credentials. No manual `Config.json` editing is required before building.
- **Named volume `pgdata`**: PostgreSQL data persists across container restarts. Use `docker compose down -v` to reset the database completely.
- **Network `webvella-net`**: Both services share a dedicated bridge network so that the web application can resolve the database service by hostname `db`.
- **PostgreSQL credentials**: The `db` service uses `POSTGRES_USER=webvella`, `POSTGRES_PASSWORD=webvella`, and `POSTGRES_DB=erp3` — matching the connection string injected by the `sed` command in the `web` service.

---

## Step 4: Environment Variables and Configuration

The WebVella ERP application reads its configuration from `config.json` (lowercase) located in the working directory at startup (`Source: WebVella.Erp.Site/Startup.cs:L42`). Importantly, the `ConfigurationBuilder` does **not** call `AddEnvironmentVariables()`, so environment variables alone cannot override settings like the connection string.

### Connection String Override (Automatic via `sed`)

The `docker-compose.yml` handles the connection string override **automatically at container startup** — no manual `Config.json` editing is required before building. The `web` service's `command` field runs a `sed` command that patches `config.json` inside the container before launching the application:

```bash
# Executed automatically by the web service entrypoint:
sed -i \
  's|Server=192.168.0.190;Port=5436;User Id=test;Password=test|Server=db;Port=5432;User Id=webvella;Password=webvella|g' \
  /app/config.json

```text

This replaces the default development server connection details with the Docker Compose service values:

| Parameter | Original (Config.json default) | Docker Override |
|---|---|---|
| Server | `192.168.0.190` | `db` (Docker DNS hostname) |
| Port | `5436` | `5432` (PostgreSQL default) |
| User Id | `test` | `webvella` |
| Password | `test` | `webvella` |
| Database | `erp3` | `erp3` (unchanged) |

The resulting connection string after the `sed` replacement is:

```

Server=db;Port=5432;User Id=webvella;Password=webvella;Database=erp3;Pooling=true;MinPoolSize=1;MaxPoolSize=100;CommandTimeout=120;

```text

> **Source**: `docker-compose.yml` — `web.command` field. The `sed` command runs before `exec dotnet WebVella.Erp.Site.dll`, so the patched `config.json` is loaded by the application at startup.

> **Note**: The `Server=db` hostname resolves to the PostgreSQL container within the `webvella-net` Docker Compose network. The credentials (`webvella`/`webvella`) match the `POSTGRES_USER` and `POSTGRES_PASSWORD` values defined in the `db` service environment.

### Full Configuration Reference

The following table documents all security-relevant settings in `Config.json`:

| Setting | Path | Default Value | Source |
|---|---|---|---|
| ConnectionString | `Settings.ConnectionString` | `Server=192.168.0.190;Port=5436;...` | `Config.json:L3` |
| EncryptionKey | `Settings.EncryptionKey` | `BC93B776A42877CFEE808823BA8B37C83B6B0AD23198AC3AF2B5A54DCB647658` | `Config.json:L4` |
| DevelopmentMode | `Settings.DevelopmentMode` | `true` | `Config.json:L9` |
| EnableBackgroundJobs | `Settings.EnableBackgroundJobs` | `false` | `Config.json:L10` |
| JWT Key | `Settings.Jwt.Key` | `ThisIsMySecretKeyThisIsMySecretKeyThisIsMySecretKey` | `Config.json:L24` |
| JWT Issuer | `Settings.Jwt.Issuer` | `webvella-erp` | `Config.json:L25` |
| JWT Audience | `Settings.Jwt.Audience` | `webvella-erp` | `Config.json:L26` |

> **Security Note**: The default `Config.json` contains hardcoded secrets including the JWT signing key and encryption key. These values are acceptable for a local security scanning environment but must never be used in production. See the [Remediation Guide](remediation-guide.md) for secrets management recommendations.

---

## Step 5: Container Startup Procedure

Build and start all containers in detached mode:

```bash
docker compose up -d --build

```

### What Happens During Startup

1. **Docker builds the `web` image** using the multi-stage `Dockerfile`. The build stage restores NuGet packages and publishes the application. This may take 2–5 minutes on first build depending on network speed.
2. **PostgreSQL starts first** — the `db` service launches immediately and begins accepting connections. Docker Compose monitors the `pg_isready` health check.
3. **Web application waits for database** — the `depends_on` condition ensures the `web` container does not start until PostgreSQL reports healthy.
4. **First-time database initialization** — on the first startup, WebVella ERP creates the database schema, seed data, and default admin account. This process may take 30–60 seconds.

### Monitoring Startup Progress

Watch the container logs in real time:

```bash
# Follow all container logs
docker compose logs -f

# Follow only the web application logs
docker compose logs -f web

# Follow only the database logs
docker compose logs -f db

```text

### Verifying Container Status

```bash
docker compose ps

```

Expected output when both services are running:

```text
NAME                    SERVICE   STATUS          PORTS
webvella-erp-web-1      web       Up (healthy)    0.0.0.0:5000->5000/tcp
webvella-erp-db-1       db        Up (healthy)    0.0.0.0:5432->5432/tcp

```

---

## Step 6: Health Check Validation

After the containers are running, validate that the WebVella ERP API is fully initialized by polling the metadata endpoint:

```text
GET /api/v3/en_US/meta

```

### Automated Polling Script

Use the following shell script to poll until the API returns HTTP 200:

```bash
until curl -sf http://localhost:5000/api/v3/en_US/meta > /dev/null 2>&1; do
  echo "Waiting for WebVella ERP to start..."
  sleep 5
done
echo "WebVella ERP is ready!"

```text

### Polling with Retry Limit

For CI/CD environments or scripted setups, use a bounded retry loop:

```bash
MAX_ATTEMPTS=30
SLEEP_INTERVAL=10

for i in $(seq 1 $MAX_ATTEMPTS); do
  if curl -sf http://localhost:5000/api/v3/en_US/meta > /dev/null 2>&1; then
    echo "Health check passed on attempt $i"
    exit 0
  fi
  echo "Attempt $i/$MAX_ATTEMPTS: Waiting for WebVella ERP..."
  sleep $SLEEP_INTERVAL
done

echo "ERROR: WebVella ERP did not start within $((MAX_ATTEMPTS * SLEEP_INTERVAL)) seconds"
exit 1

```

### Expected Health Check Response

A successful health check returns HTTP 200 with a JSON body containing entity metadata in the standard WebVella response envelope:

```json
{
  "success": true,
  "message": "...",
  "timestamp": "2025-01-01T00:00:00.000Z",
  "errors": [],
  "object": { ... }
}

```text

> **Source**: The response envelope format follows the structure documented in `docs/developer/web-api/response.md` with `success`, `message`, `timestamp`, `errors`, and `object` fields.

---

## Troubleshooting

### PostgreSQL Connection Refused

**Symptom**: The web application logs show `Npgsql.NpgsqlException: Failed to connect to 192.168.0.190:5436` or similar connection errors.

**Cause**: The `sed` command in the `web` service's `command` field may not have matched the connection string pattern in `config.json`, leaving the original development server address (`192.168.0.190:5436`) in place.

**Resolution**: Verify the `sed` command in `docker-compose.yml` matches the actual `Config.json` connection string pattern. The `web` service automatically patches the connection string at container startup to use `Server=db;Port=5432;User Id=webvella;Password=webvella`. The hostname `db` resolves to the PostgreSQL container within the `webvella-net` Docker Compose network. If the pattern has changed, update the `sed` command in `docker-compose.yml` accordingly.

```bash
# Verify the current connection string
grep -i "connectionstring" WebVella.Erp.Site/Config.json

```

### Port 5000 Already in Use

**Symptom**: `docker compose up` fails with `Error: address already in use`.

**Cause**: Another process is already listening on port 5000.

**Resolution**: Either stop the conflicting process or change the port mapping in `docker-compose.yml`:

```yaml
ports:

  - "8080:5000"  # Map host port 8080 to container port 5000

```text

Then access the application at `http://localhost:8080` instead.

### Database Initialization Failures

**Symptom**: The web application starts but immediately crashes with database-related errors.

**Cause**: PostgreSQL may not have fully initialized, or there may be schema conflicts from a previous incomplete startup.

**Resolution**:

```bash
# Check PostgreSQL container logs
docker compose logs db

# Reset the database volume (WARNING: deletes all data)
docker compose down -v
docker compose up -d --build

```

### Application Startup Timeout

**Symptom**: The health check polling loop times out after 5 minutes.

**Cause**: First-time database schema initialization can take 30–60 seconds. If the application is initializing entities, seed data, and default accounts, it may appear unresponsive during this period.

**Resolution**:

```bash
# Check the web application logs for initialization progress
docker compose logs -f web

# Look for lines indicating schema creation or seed data insertion
docker compose logs web | grep -i "init\|seed\|creat\|migrat"

```text

### Config.json Not Found at Startup

**Symptom**: Application crashes with `FileNotFoundException` for `config.json`.

**Cause**: The configuration file must be named `config.json` (lowercase) and located in the application's working directory. The filename is hardcoded in `Startup.cs` (`Source: WebVella.Erp.Site/Startup.cs:L42`):

```csharp
string configPath = "config.json";
Configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile(configPath)
    .Build();

```

**Resolution**: Verify the `Config.json` file exists and has the correct casing. On case-sensitive file systems (Linux), the file must be lowercase `config.json`. In the repository, it is stored as `Config.json` — the .NET `AddJsonFile` method is case-sensitive on Linux.

```bash
# Check file casing in the publish output
docker compose exec web ls -la /app/ | grep -i config

```text

If the file is missing or has wrong casing, rebuild with a Dockerfile modification:

```dockerfile
# Add after COPY --from=build /app/publish .
RUN if [ -f Config.json ] && [ ! -f config.json ]; then cp Config.json config.json; fi

```

### Scanner Cannot Reach the Application

**Symptom**: OWASP ZAP or Nuclei cannot connect to `http://localhost:5000`.

**Cause**: If the scanner runs inside a Docker container, `localhost` refers to the scanner's own container, not the host machine.

**Resolution**: Run the scanner containers with `--network host` so they share the host's network namespace:

```bash
# ZAP with host networking
docker run --network host ghcr.io/zaproxy/zaproxy:stable ...

# Nuclei with host networking
docker run --network host projectdiscovery/nuclei:latest ...

```text

Alternatively, connect the scanner container to the `webvella-net` network (Docker Compose prefixes network names with the project directory name — use `docker network ls` to find the exact name):

```bash
docker run --network webvella-erp_webvella-net \
  projectdiscovery/nuclei:latest -u http://web:5000 ...

```

---

## Container Architecture

The following diagram illustrates the Docker Compose deployment topology and network connectivity for the security scanning environment:

```mermaid
graph LR
    subgraph "Docker Network: webvella-net"
        A["web<br/>WebVella ERP<br/>.NET 9.0<br/>Port: 5000"] <-->|"PostgreSQL Protocol<br/>Port: 5432"| B["db<br/>PostgreSQL 16<br/>Port: 5432"]
    end
    C["Host Machine"] -->|"HTTP :5000"| A
    C -->|"PostgreSQL :5432"| B
    D["ZAP / Nuclei<br/>--network host"] -->|"HTTP :5000"| A

```text

**Network flow**:

1. The **Host Machine** accesses the WebVella ERP API on port 5000 for manual verification and health checks.
2. The **web** service communicates with the **db** service over the internal `webvella-net` bridge network using the hostname `db` on port 5432.
3. **Security scanners** (ZAP and Nuclei) use `--network host` mode to access the web application on `localhost:5000`, enabling them to reach the mapped container port directly.

---

## Container Lifecycle Commands

Quick reference for managing the Docker Compose environment:

| Command | Purpose |
|---|---|
| `docker compose up -d --build` | Build images and start all containers in detached mode |
| `docker compose ps` | Show running container status |
| `docker compose logs -f` | Follow all container logs |
| `docker compose logs -f web` | Follow web application logs only |
| `docker compose logs -f db` | Follow database logs only |
| `docker compose stop` | Stop containers without removing them |
| `docker compose down` | Stop and remove containers (preserves volumes) |
| `docker compose down -v` | Stop, remove containers, and delete volumes (full reset) |
| `docker compose build --no-cache web` | Rebuild web image without cache (after code changes) |
| `docker compose restart web` | Restart only the web container |

---

## Build Reproducibility

The `Dockerfile` and `docker-compose.yml` use rolling major/minor tags (`sdk:9.0`, `aspnet:9.0`, `postgres:16`) for Docker base images. Rolling tags automatically receive security patches when you run `docker pull` or `docker compose build`, which is convenient for development. However, rolling tags **sacrifice build reproducibility** — the same tag can resolve to different image digests over time, meaning two builds on different dates may produce different images.

For **production security scans** and **audited environments** where build reproducibility is required, pin images to either specific patch-version tags or sha256 digests.

### Option A: Pin to Specific Patch-Version Tags

Replace rolling tags with explicit patch-level versions. As of the latest patched releases:

| Image | Rolling Tag | Pinned Tag | Patches Applied |
|---|---|---|---|
| .NET SDK | `mcr.microsoft.com/dotnet/sdk:9.0` | `mcr.microsoft.com/dotnet/sdk:9.0.14` | CVE-2026-26130, CVE-2026-26127, CVE-2026-21218, CVE-2025-55315 |
| ASP.NET Runtime | `mcr.microsoft.com/dotnet/aspnet:9.0` | `mcr.microsoft.com/dotnet/aspnet:9.0.14` | CVE-2026-26130, CVE-2026-26127, CVE-2026-21218, CVE-2025-55315 |
| PostgreSQL | `postgres:16` | `postgres:16.13` | CVE-2026-2006, CVE-2026-2004, CVE-2026-2005 |

Update the `Dockerfile`:

```dockerfile
# Pinned to specific patch versions for reproducible builds
FROM mcr.microsoft.com/dotnet/sdk:9.0.14 AS build
# ...
FROM mcr.microsoft.com/dotnet/aspnet:9.0.14

```

Update `docker-compose.yml`:

```yaml
services:
  db:
    image: postgres:16.13

```text

### Option B: Pin to sha256 Digests (Strongest Reproducibility)

For the strongest reproducibility guarantee, pin images to their sha256 digests. Retrieve the current digest with:

```bash
docker pull mcr.microsoft.com/dotnet/sdk:9.0.14
docker inspect --format='{{index .RepoDigests 0}}' mcr.microsoft.com/dotnet/sdk:9.0.14

docker pull mcr.microsoft.com/dotnet/aspnet:9.0.14
docker inspect --format='{{index .RepoDigests 0}}' mcr.microsoft.com/dotnet/aspnet:9.0.14

docker pull postgres:16.13
docker inspect --format='{{index .RepoDigests 0}}' postgres:16.13

```

Then reference the digest directly in the `Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0.14@sha256:<digest> AS build
# ...
FROM mcr.microsoft.com/dotnet/aspnet:9.0.14@sha256:<digest>

```

### Recommendation

- **For local development and iterative security scanning**: Use rolling tags (`:9.0`, `:16`) to automatically receive patches. This is the default configuration in the provided `Dockerfile` and `docker-compose.yml`.
- **For audited or compliance-driven environments**: Pin to patch-version tags (Option A) or sha256 digests (Option B) and document the pinned versions in your scan report for traceability.
- **When upgrading pinned images**: Check for new patch releases periodically using `docker pull` and update the pinned tags or digests accordingly. Monitor Microsoft's .NET Docker image release notes and PostgreSQL's official Docker Hub page for security advisories.

---

## Next Steps

Once the health check confirms the application is running:

- **[Authentication](authentication.md)**: Acquire a JWT Bearer token using the default admin credentials (`erp@webvella.com` / `erp`) for authenticated security scanning.
- **[Attack Surface Inventory](attack-surface-inventory.md)**: Review the complete API endpoint inventory before configuring scan scopes.
- **[Back to Overview](README.md)**: Return to the security assessment workflow overview.

For a manual (non-Docker) setup alternative, see **[Getting Started](../developer/introduction/getting-started.md)** in the developer documentation.
