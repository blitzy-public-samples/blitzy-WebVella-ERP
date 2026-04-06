<!--{"sort_order": 6, "name": "nuclei-scan-config", "label": "Nuclei Scan Configuration"}-->
# Nuclei Scan Configuration

## Overview

Configure [Nuclei](https://github.com/projectdiscovery/nuclei) **v3.7.1** with community templates **v10.3.9** (9,821+ templates) for authenticated vulnerability scanning of WebVella ERP. Nuclei performs template-based detection of misconfigurations, known CVEs, and common vulnerability patterns specific to ASP.NET Core applications and REST APIs. This scan runs **in parallel** alongside the OWASP ZAP active scan to maximize coverage and minimize total scan time.

> **Cross-reference**: See [Authentication](authentication.md) for obtaining the JWT Bearer token used throughout this guide.

> **Cross-reference**: See [Attack Surface Inventory](attack-surface-inventory.md) for the complete list of 70 endpoints across 17 functional groups that Nuclei will probe.

> **Cross-reference**: See [ZAP Scan Configuration](zap-scan-config.md) for the parallel OWASP ZAP scan that runs alongside Nuclei.

---

## Prerequisites

Before configuring and running the Nuclei scan, ensure the following are in place:

| Prerequisite | Requirement | Reference |
|---|---|---|
| **Docker Engine** | Version 24.0 or later | Required for Docker-based Nuclei execution (recommended) |
| **Docker Compose** | V2 (2.20+) | Required only if running WebVella ERP via Docker Compose |
| **OR Nuclei Binary** | Go >= 1.24.2 for source installation | Alternative to Docker-based execution |
| **WebVella ERP** | Running and healthy on `http://localhost:5000` | See [Docker Environment Setup](docker-setup.md) |
| **JWT Bearer Token** | Valid token from `POST /api/v3/en_US/auth/jwt/token` | See [Authentication](authentication.md) |
| **Nuclei Version** | **v3.7.1** | Verified stable release |
| **Templates Version** | **v10.3.9** (9,821+ templates) | Auto-downloaded on first run |

> **Source**: `WebVella.Erp.Web/Controllers/WebApiController.cs:L36` — `[Authorize]` class-level attribute confirms that authenticated scanning is required to reach protected endpoints.

---

## Nuclei Installation

### Docker Method (Recommended)

Pull the official Nuclei Docker image from Docker Hub:

```bash
docker pull projectdiscovery/nuclei:latest
```

Verify the installed version:

```bash
docker run --rm projectdiscovery/nuclei:latest -version
```

Expected output should confirm **Nuclei v3.7.1** with templates **v10.3.9**.

> The Docker method is recommended because it bundles the correct Go runtime, Nuclei binary, and template engine in a single portable image. No local Go installation or PATH configuration is required.

### Binary Installation Alternative

For environments where Docker is unavailable, install Nuclei directly from the official releases:

```bash
# Download and install via Go (requires Go >= 1.24.2)
go install -v github.com/projectdiscovery/nuclei/v3/cmd/nuclei@latest

# Verify installation
nuclei -version
```

Alternatively, download a pre-compiled binary from the [Nuclei GitHub Releases](https://github.com/projectdiscovery/nuclei/releases) page for your target platform (Linux, macOS, Windows).

### Initial Template Download

On first run, Nuclei automatically downloads the community templates repository. To explicitly fetch or update templates:

```bash
# Docker-based template update
docker run --rm projectdiscovery/nuclei:latest -update-templates

# Binary-based template update
nuclei -update-templates
```

After updating, verify the templates version is **v10.3.9** with **9,821+** templates:

```bash
docker run --rm projectdiscovery/nuclei:latest -version
```

> **Source**: [Nuclei Templates Repository](https://github.com/projectdiscovery/nuclei-templates) — Community-maintained templates v10.3.9 covering CVE detection, misconfiguration, exposed panels, default credentials, and technology-specific checks.

---

## Template Pack Selection

Nuclei uses tag-based template selection to scope scans to relevant vulnerability categories. For WebVella ERP, two template packs are combined to cover both ASP.NET Core-specific and general REST API vulnerabilities.

### ASP.NET Core Templates (`-tags aspnet`)

The `aspnet` tag selects templates targeting ASP.NET Core-specific vulnerabilities and misconfigurations:

| Check Category | Description | WebVella ERP Relevance |
|---|---|---|
| Debug/Trace endpoints | Detects exposed `/_framework/`, `/elmah.axd`, `/trace.axd` debugging endpoints | WebVella uses `UseDeveloperExceptionPage()` in Development mode (`Source: WebVella.Erp.Site/Startup.cs:L149`) |
| Server header disclosure | Identifies `X-Powered-By: ASP.NET` and `Server: Kestrel` response headers | Leaks technology stack information |
| Authentication misconfigurations | Checks for default ASP.NET Core auth cookie settings, missing `SameSite` | WebVella sets `erp_auth_base` cookie with `HttpOnly=true` but no explicit `SameSite` policy (`Source: WebVella.Erp.Site/Startup.cs:L95-96`) |
| Error page information disclosure | Detects verbose error pages exposing stack traces | WebVella leaks `e.Message + e.StackTrace` in 15+ endpoints (`Source: WebVella.Erp.Web/Controllers/WebApiController.cs:L4287`) |
| CORS misconfigurations | Identifies permissive CORS policies | WebVella uses `AllowAnyOrigin()` + `AllowAnyMethod()` + `AllowAnyHeader()` (`Source: WebVella.Erp.Site/Startup.cs:L58-64`) |
| Known CVEs | Detects known ASP.NET Core CVEs by version fingerprint | Checks against .NET 9.0 runtime |

### Generic API Templates (`-tags api`)

The `api` tag selects templates targeting common REST API vulnerabilities:

| Check Category | Description | WebVella ERP Relevance |
|---|---|---|
| Authentication bypass | Tests for unauthenticated access to protected endpoints | WebVella class-level `[Authorize]` with `[AllowAnonymous]` overrides on 3 endpoints (`Source: WebApiController.cs:L36, L4273, L4292`) |
| IDOR / BOLA | Insecure Direct Object Reference testing via ID manipulation | Record CRUD endpoints accept `{entityName}/{recordId}` with no controller-level permission checks (`Source: WebApiController.cs:L2504-2517`) |
| Injection patterns | SQL injection, command injection, LDAP injection templates | EQL execution endpoint at `POST /api/v3/en_US/eql` accepts user-supplied query strings (`Source: WebApiController.cs:L63-95`) |
| Information disclosure | API key exposure, verbose errors, debug information | Stack trace leakage in error responses across 15+ endpoints |
| Rate limiting | Tests for missing rate limits on authentication endpoints | JWT token endpoint has no observed rate limiting |
| HTTP method fuzzing | Checks for unexpected HTTP method handling | 60+ endpoints across GET, POST, PUT, PATCH, DELETE methods |

### Combined Tag Usage

Use both template packs together for comprehensive coverage:

```bash
-tags aspnet,api
```

This configures Nuclei to execute all templates tagged with either `aspnet` OR `api`, providing both technology-specific and general API vulnerability detection in a single scan pass.

---

## Custom Header Configuration (Bearer Token Authentication)

WebVella ERP's `WebApiController` is decorated with `[Authorize]` at the class level, requiring authentication for all endpoints except three explicitly marked `[AllowAnonymous]` (static CSS, JWT token, JWT refresh). To scan the full 70-endpoint attack surface, Nuclei must inject the JWT Bearer token into every HTTP request.

> **Source**: `WebVella.Erp.Web/Controllers/WebApiController.cs:L36` — `[Authorize]` on the controller class.

> **Source**: `WebVella.Erp.Site/Startup.cs:L115-125` — The `JWT_OR_COOKIE` policy scheme routes requests with `Authorization: Bearer ...` headers to the JWT Bearer handler.

### Header Injection Flag

Use the `-H` flag to inject the Bearer token as a custom HTTP header:

```bash
-H "Authorization: Bearer <TOKEN>"
```

Replace `<TOKEN>` with the JWT token obtained from the authentication endpoint. For example, if you stored the token in a shell variable:

```bash
-H "Authorization: Bearer $TOKEN"
```

### Token Acquisition (Quick Reference)

Obtain the token before running the scan:

```bash
TOKEN=$(curl -sf -X POST http://localhost:5000/api/v3/en_US/auth/jwt/token \
  -H "Content-Type: application/json" \
  -d '{"email":"erp@webvella.com","password":"erp"}' | jq -r '.object.token')

echo "Token acquired: ${TOKEN:0:20}..."
```

> **Cross-reference**: See [Authentication](authentication.md) for the complete token acquisition flow, error handling, and token refresh procedure.

### Authentication Verification

Before launching the full scan, verify that the token grants authenticated access:

```bash
curl -sf -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/v3/en_US/meta/entity/list | jq '.success'
```

Expected output: `true`. If this returns `false` or an error, the token may be expired or invalid. Re-acquire the token before proceeding.

---

## Target URL Configuration

### Single Target

Point Nuclei at the WebVella ERP base URL:

```bash
-u http://localhost:5000
```

Nuclei will automatically discover and crawl API paths from the base URL, applying each selected template against the target.

### Port and Host Notes

| Configuration | Value | Notes |
|---|---|---|
| Default port | `5000` | WebVella ERP default in Docker configuration |
| Protocol | `http` | Default local development configuration; HTTPS not configured by default |
| Docker network | `--network host` | Required when running Nuclei in Docker to access the host's `localhost:5000` |

> The WebVella ERP application exposes **70 unique action methods** across **60+ route patterns** under `/api/v3/`, `/api/v3.0/`, `/fs/`, and `/ckeditor/` paths. Nuclei templates will test against the base URL and its discovered sub-paths.

> **Cross-reference**: See [Attack Surface Inventory](attack-surface-inventory.md) for the complete endpoint inventory with route patterns, HTTP methods, authorization requirements, and risk classifications.

---

## Severity Filtering

Filter scan output to report only **CRITICAL** and **HIGH** severity findings, as specified in the security assessment requirements:

```bash
-severity critical,high
```

### Severity Level Definitions

| Level | Description | Example Findings |
|---|---|---|
| **critical** | Direct exploitation possible; immediate remediation required | Remote code execution, SQL injection, authentication bypass |
| **high** | Significant security impact; requires prompt remediation | IDOR/BOLA, unrestricted file upload, path traversal, information disclosure |
| **medium** | Moderate impact; scheduled remediation acceptable | CORS misconfiguration, missing security headers, verbose errors |
| **low** | Minimal direct impact; informational findings | Technology fingerprinting, cookie attribute warnings |

> Filtering to `critical,high` aligns with the security assessment requirement to focus remediation effort on findings with the highest impact. Medium and low findings can be captured separately by running a second pass without the severity filter if needed.

### Capturing All Severities (Optional)

To also capture MEDIUM and LOW findings for a comprehensive baseline, omit the severity filter or specify all levels:

```bash
# Capture all severity levels
-severity critical,high,medium,low,info
```

---

## Output Format Selection

### JSONL Output (Recommended)

Use JSONL (JSON Lines) format for machine-parseable output that integrates directly with the finding analysis pipeline:

```bash
-jsonl -o nuclei-results.jsonl
```

Each line in the output file is a self-contained JSON object representing a single finding. This format is ideal for:

- Automated parsing with `jq` in the finding deduplication pipeline
- Cross-scanner correlation with OWASP ZAP JSON output
- Programmatic severity filtering and reporting

### Example JSONL Output Entry

A single finding entry in `nuclei-results.jsonl` has the following structure:

```json
{
  "template-id": "aspnetcore-debug-enabled",
  "info": {
    "name": "ASP.NET Core Debug Enabled",
    "severity": "high",
    "tags": ["aspnet", "misconfig"],
    "classification": {
      "cwe-id": ["CWE-215"],
      "cvss-score": 7.5
    }
  },
  "type": "http",
  "host": "http://localhost:5000",
  "matched-at": "http://localhost:5000/api/v3/en_US/eql",
  "timestamp": "2024-01-01T12:00:00.000Z"
}
```

Key fields for finding analysis:

| Field | Purpose |
|---|---|
| `template-id` | Unique template identifier for deduplication |
| `info.severity` | Severity level (critical, high, medium, low, info) |
| `info.classification.cwe-id` | CWE reference(s) for standardized classification |
| `host` | Target base URL |
| `matched-at` | Exact URL where the vulnerability was detected |
| `timestamp` | Detection timestamp |

> **Cross-reference**: See [Finding Analysis](finding-analysis.md) for the complete JSONL parsing procedure and cross-scanner deduplication algorithm using `jq`.

---

## Complete Scan Execution Command

### Basic Command (Inline Output)

The primary Nuclei scan command targeting WebVella ERP with authenticated scanning, ASP.NET Core and API templates, and severity filtering:

```bash
docker run --network host projectdiscovery/nuclei:latest \
  -u http://localhost:5000 \
  -tags aspnet,api -severity critical,high \
  -H "Authorization: Bearer <TOKEN>" \
  -jsonl -o nuclei-results.jsonl
```

Replace `<TOKEN>` with the JWT Bearer token obtained from the authentication endpoint.

### Persistent Results with Volume Mount (Recommended)

When running Nuclei in Docker, scan results written inside the container are lost when the container exits. Use a volume mount to persist the output to the host filesystem:

```bash
docker run --network host -v $(pwd)/nuclei-work:/output \
  projectdiscovery/nuclei:latest \
  -u http://localhost:5000 \
  -tags aspnet,api -severity critical,high \
  -H "Authorization: Bearer <TOKEN>" \
  -jsonl -o /output/nuclei-results.jsonl
```

This mounts the local `./nuclei-work/` directory into the container at `/output/`, ensuring `nuclei-results.jsonl` is available on the host after the scan completes.

### Complete Command with Token Variable

Combining the token acquisition with the scan execution in a single workflow:

```bash
# Step 1: Acquire JWT token
TOKEN=$(curl -sf -X POST http://localhost:5000/api/v3/en_US/auth/jwt/token \
  -H "Content-Type: application/json" \
  -d '{"email":"erp@webvella.com","password":"erp"}' | jq -r '.object.token')

# Step 2: Create output directory
mkdir -p nuclei-work

# Step 3: Run Nuclei scan with authenticated headers
docker run --network host -v $(pwd)/nuclei-work:/output \
  projectdiscovery/nuclei:latest \
  -u http://localhost:5000 \
  -tags aspnet,api -severity critical,high \
  -H "Authorization: Bearer $TOKEN" \
  -jsonl -o /output/nuclei-results.jsonl

# Step 4: Verify results
echo "Scan complete. Results:"
wc -l nuclei-work/nuclei-results.jsonl
cat nuclei-work/nuclei-results.jsonl | jq -r '.info.severity' | sort | uniq -c | sort -rn
```

### Command-Line Flag Reference

| Flag | Value | Purpose |
|---|---|---|
| `--network host` | — | Share host network so Nuclei can reach `localhost:5000` |
| `-v $(pwd)/nuclei-work:/output` | Host:Container path | Persist scan results to host filesystem |
| `-u` | `http://localhost:5000` | WebVella ERP target URL |
| `-tags` | `aspnet,api` | Select ASP.NET Core and API vulnerability templates |
| `-severity` | `critical,high` | Report only critical and high findings |
| `-H` | `"Authorization: Bearer <TOKEN>"` | Inject JWT Bearer token for authenticated scanning |
| `-jsonl` | — | Output in JSON Lines format for machine parsing |
| `-o` | `/output/nuclei-results.jsonl` | Write results to the specified output file |

---

## Parallel Execution Strategy (Alongside ZAP)

Per the security assessment requirements, OWASP ZAP and Nuclei scans **must run in parallel** (not sequentially) to minimize total scan time. Both scanners target the same WebVella ERP instance simultaneously.

### Concurrent Execution Script

Run both scanners as background processes and wait for both to complete:

```bash
#!/usr/bin/env bash
set -euo pipefail

# Acquire JWT token
TOKEN=$(curl -sf -X POST http://localhost:5000/api/v3/en_US/auth/jwt/token \
  -H "Content-Type: application/json" \
  -d '{"email":"erp@webvella.com","password":"erp"}' | jq -r '.object.token')

if [ -z "$TOKEN" ] || [ "$TOKEN" = "null" ]; then
  echo "[!] Failed to acquire JWT token."
  exit 1
fi

echo "[+] Token acquired: ${TOKEN:0:20}..."

# Create output directories
mkdir -p zap-work nuclei-work

# Terminal 1: Start ZAP scan (background)
echo "[*] Starting ZAP active scan..."
docker run --network host -v $(pwd)/zap-work:/zap/wrk \
  ghcr.io/zaproxy/zaproxy:stable zap-full-scan.py \
  -t http://localhost:5000 -J zap-report.json \
  -z "-config replacer.full_list(0).matchtype=REQ_HEADER \
      -config replacer.full_list(0).matchstr=Authorization \
      -config replacer.full_list(0).replacement='Bearer $TOKEN'" &
ZAP_PID=$!

# Terminal 2: Start Nuclei scan (background, simultaneously)
echo "[*] Starting Nuclei template scan..."
docker run --network host -v $(pwd)/nuclei-work:/output \
  projectdiscovery/nuclei:latest \
  -u http://localhost:5000 \
  -tags aspnet,api -severity critical,high \
  -H "Authorization: Bearer $TOKEN" \
  -jsonl -o /output/nuclei-results.jsonl &
NUCLEI_PID=$!

# Wait for both scanners to complete
echo "[*] Waiting for both scanners to complete..."
wait $ZAP_PID
ZAP_EXIT=$?
wait $NUCLEI_PID
NUCLEI_EXIT=$?

# Report completion status
echo ""
echo "========================================="
echo "  Scan Execution Summary"
echo "========================================="
echo "  ZAP exit code:    $ZAP_EXIT"
echo "  Nuclei exit code: $NUCLEI_EXIT"
echo ""
echo "  ZAP results:      zap-work/zap-report.json"
echo "  Nuclei results:   nuclei-work/nuclei-results.jsonl"
echo "========================================="

# Summarize findings
if [ -f nuclei-work/nuclei-results.jsonl ]; then
  echo ""
  echo "Nuclei Finding Summary:"
  cat nuclei-work/nuclei-results.jsonl | jq -r '.info.severity' | sort | uniq -c | sort -rn
fi
```

Save this script as `run-parallel-scans.sh` and execute:

```bash
chmod +x run-parallel-scans.sh
./run-parallel-scans.sh
```

### Resource Considerations

Running ZAP and Nuclei simultaneously imposes additional load on both the scanning host and the WebVella ERP target:

| Resource | ZAP Impact | Nuclei Impact | Combined Recommendation |
|---|---|---|---|
| **CPU** | High (active scan with fuzzing) | Moderate (template matching) | Minimum 4 CPU cores recommended |
| **Memory** | 2–4 GB (Java-based, headless browser) | 512 MB–1 GB (Go-based, lightweight) | Minimum 8 GB total RAM recommended |
| **Network** | High (active fuzzing, spidering) | Moderate (template-based HTTP requests) | Both use `--network host` to share host networking |
| **Target Load** | High (hundreds of fuzzing requests) | Moderate (focused template probes) | Monitor WebVella ERP for resource exhaustion |
| **Disk I/O** | Moderate (ZAP session database) | Low (JSONL line-append writes) | SSD recommended for ZAP work directory |

> **Tip**: If the target system becomes unresponsive under combined scanner load, reduce Nuclei concurrency with the `-rate-limit` flag (e.g., `-rate-limit 50` to cap at 50 requests per second) or run the scans sequentially instead.

### Sequential Execution Alternative

If parallel execution causes resource contention or scan interference, run the scanners sequentially:

```bash
# Run ZAP first
docker run --network host -v $(pwd)/zap-work:/zap/wrk \
  ghcr.io/zaproxy/zaproxy:stable zap-full-scan.py \
  -t http://localhost:5000 -J zap-report.json \
  -z "-config replacer.full_list(0).matchtype=REQ_HEADER \
      -config replacer.full_list(0).matchstr=Authorization \
      -config replacer.full_list(0).replacement='Bearer $TOKEN'"

# Then run Nuclei
docker run --network host -v $(pwd)/nuclei-work:/output \
  projectdiscovery/nuclei:latest \
  -u http://localhost:5000 \
  -tags aspnet,api -severity critical,high \
  -H "Authorization: Bearer $TOKEN" \
  -jsonl -o /output/nuclei-results.jsonl
```

> **Cross-reference**: See [ZAP Scan Configuration](zap-scan-config.md) for the complete ZAP command-line details and Automation Framework YAML plan.

---

## Template Update Procedure

Nuclei templates are regularly updated by the ProjectDiscovery community to add new CVE detections, misconfiguration checks, and vulnerability patterns. Before each scan, update templates to ensure the latest coverage.

### Update Command (Docker)

```bash
docker run --rm projectdiscovery/nuclei:latest -update-templates
```

### Update Command (Binary)

```bash
nuclei -update-templates
```

### Version Verification

After updating, confirm the templates version:

```bash
docker run --rm projectdiscovery/nuclei:latest -version
```

Expected output should include:

```
Nuclei Engine Version: v3.7.1
Nuclei Templates Version: v10.3.9
```

> **Note**: Templates version **v10.3.9** includes **9,821+ community templates** covering CVE detection, misconfiguration, exposed panels, default credentials, technology fingerprinting, and fuzzing payloads. The `aspnet` and `api` tags select a relevant subset focused on ASP.NET Core and REST API vulnerabilities.

### Template Version Pinning

For reproducible security assessments, pin the templates version to avoid result variance between scan runs:

```bash
# Clone specific templates version
git clone --branch v10.3.9 --depth 1 \
  https://github.com/projectdiscovery/nuclei-templates.git \
  ./nuclei-templates-v10.3.9

# Run Nuclei with pinned templates directory
docker run --network host \
  -v $(pwd)/nuclei-templates-v10.3.9:/root/nuclei-templates \
  -v $(pwd)/nuclei-work:/output \
  projectdiscovery/nuclei:latest \
  -u http://localhost:5000 \
  -tags aspnet,api -severity critical,high \
  -H "Authorization: Bearer $TOKEN" \
  -jsonl -o /output/nuclei-results.jsonl
```

This ensures every scan run uses the exact same template definitions, producing consistent and comparable results across assessments.

---

## Troubleshooting

### Common Issues

| Issue | Cause | Resolution |
|---|---|---|
| `Could not connect to target` | WebVella ERP not running or wrong port | Verify with `curl -sf http://localhost:5000/api/v3/en_US/meta` — should return HTTP 200. See [Docker Environment Setup](docker-setup.md). |
| `0 results found` | Authentication failure — token not injected or expired | Re-acquire token: `TOKEN=$(curl -sf ... \| jq -r '.object.token')`. Verify with `curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/v3/en_US/meta/entity/list`. See [Authentication](authentication.md). |
| `network host not supported` | Docker Desktop on macOS/Windows | Replace `--network host` with `-p 5000:5000` or use the Docker bridge network with the container IP instead of `localhost`. |
| `permission denied: /output/nuclei-results.jsonl` | Volume mount permission mismatch | Run `chmod 777 nuclei-work/` on the host before mounting, or run Docker with `--user $(id -u):$(id -g)`. |
| Templates version mismatch | Cached older templates in Docker image | Pull latest image: `docker pull projectdiscovery/nuclei:latest` then run `-update-templates`. |
| Scan takes excessively long | Too many templates selected or high concurrency | Add `-rate-limit 100` to cap requests per second, or narrow scope with more specific tags. |

### Verifying Scan Results

After the scan completes, verify the output file exists and contains findings:

```bash
# Check file exists and has content
ls -la nuclei-work/nuclei-results.jsonl

# Count total findings
wc -l nuclei-work/nuclei-results.jsonl

# Summarize findings by severity
cat nuclei-work/nuclei-results.jsonl | jq -r '.info.severity' | sort | uniq -c | sort -rn

# Summarize findings by template
cat nuclei-work/nuclei-results.jsonl | jq -r '.template-id' | sort | uniq -c | sort -rn

# View first finding in detail
head -1 nuclei-work/nuclei-results.jsonl | jq '.'
```

---

## Next Steps

After the Nuclei scan completes (alongside the parallel ZAP scan), proceed to finding analysis:

- **[Finding Analysis](finding-analysis.md)** — Parse Nuclei JSONL output, correlate with ZAP JSON results, deduplicate findings across scanners, and triage by severity.
- **[Remediation Guide](remediation-guide.md)** — Apply ASP.NET Core secure coding patterns for each HIGH and CRITICAL finding.
- **[Security Report](security-report.md)** — Generate the final per-finding report with CWE references, before/after code, and scanner confirmation.

---

> **Navigation**: [← ZAP Scan Configuration](zap-scan-config.md) | [Security Assessment Overview](README.md) | [Finding Analysis →](finding-analysis.md)
