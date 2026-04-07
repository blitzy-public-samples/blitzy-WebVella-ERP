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
```

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
# Stage 1: Build
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy the entire solution (required for project references)
COPY . .

# Restore NuGet dependencies for the site project and all referenced projects
RUN dotnet restore WebVella.Erp.Site/WebVella.Erp.Site.csproj

# Publish the application in Release configuration
RUN dotnet publish WebVella.Erp.Site/WebVella.Erp.Site.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# =============================================================================
# Stage 2: Runtime
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Copy published output from build stage
COPY --from=build /app/publish .

# Configure ASP.NET Core to listen on port 5000
ENV ASPNETCORE_URLS=http://+:5000

# Expose the application port
EXPOSE 5000

# Start the application
ENTRYPOINT ["dotnet", "WebVella.Erp.Site.dll"]
```

### Dockerfile Explanation

| Stage | Base Image | Purpose |
|---|---|---|
| `build` | `mcr.microsoft.com/dotnet/sdk:9.0` | Restores NuGet packages, compiles all projects, publishes optimized output |
| `runtime` | `mcr.microsoft.com/dotnet/aspnet:9.0` | Runs the published application with minimal image size |

> **Note**: The entire repository is copied during the build stage because `WebVella.Erp.Site.csproj` has `ProjectReference` entries to sibling projects (`WebVella.Erp.Plugins.SDK`, `WebVella.Erp.Web`, `WebVella.Erp`). A selective copy approach would require all referenced `.csproj` files to be staged individually.

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
```

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
```

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
```

### Verifying Container Status

```bash
docker compose ps
```

Expected output when both services are running:

```
NAME                    SERVICE   STATUS          PORTS
webvella-erp-web-1      web       Up (healthy)    0.0.0.0:5000->5000/tcp
webvella-erp-db-1       db        Up (healthy)    0.0.0.0:5432->5432/tcp
```

---

## Step 6: Health Check Validation

After the containers are running, validate that the WebVella ERP API is fully initialized by polling the metadata endpoint:

```
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
```

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
```

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
```

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
```

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
```

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
```

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
```

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

## Next Steps

Once the health check confirms the application is running:

- **[Authentication](authentication.md)**: Acquire a JWT Bearer token using the default admin credentials (`erp@webvella.com` / `erp`) for authenticated security scanning.
- **[Attack Surface Inventory](attack-surface-inventory.md)**: Review the complete API endpoint inventory before configuring scan scopes.
- **[Back to Overview](README.md)**: Return to the security assessment workflow overview.

For a manual (non-Docker) setup alternative, see **[Getting Started](../developer/introduction/getting-started.md)** in the developer documentation.
