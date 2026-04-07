<!--{"sort_order": 5, "name": "zap-scan-config", "label": "ZAP Scan Configuration"}-->
# OWASP ZAP Scan Configuration

## Overview

This document provides a complete guide for configuring and running an OWASP ZAP 2.17.0 authenticated active scan against the WebVella ERP API. The scan targets 60+ REST API endpoints exposed by `WebApiController.cs`, using JWT Bearer token authentication and scanning for IDOR, BOLA, SQL/EQL injection, XSS, CSRF, path traversal, and unrestricted file upload vulnerabilities.

ZAP is executed as a Docker container with scan scope precisely mapped to the WebVella ERP attack surface. Two execution methods are documented: the quick-start `zap-full-scan.py` wrapper script and the more configurable ZAP Automation Framework YAML plan.

> **Cross-reference**: See [Authentication](authentication.md) for Bearer token acquisition.
> **Cross-reference**: See [Attack Surface Inventory](attack-surface-inventory.md) for the complete 70-endpoint security classification.
> **Cross-reference**: See [Nuclei Scan Configuration](nuclei-scan-config.md) for the parallel Nuclei scanner that runs alongside ZAP.

---

## Prerequisites

Before configuring the ZAP scan, ensure the following requirements are met:

| Requirement | Detail |
|---|---|
| Docker Engine | Version 24.0+ installed and running |
| WebVella ERP | Running and accessible at `http://localhost:5000` (see [Docker Environment Setup](docker-setup.md)) |
| Health Check | `GET /api/v3/en_US/meta` returns HTTP 200 |
| JWT Bearer Token | Valid token obtained from `POST /api/v3/en_US/auth/jwt/token` (see [Authentication](authentication.md)) |
| OWASP ZAP Version | **2.17.0** (stable release, December 15, 2025) |
| Docker Image | `ghcr.io/zaproxy/zaproxy:stable` |
| Working Directory | A local `zap-work/` directory for scan output |

> **Token Prerequisite**: Acquire the JWT token before proceeding. The token is required for authenticated scanning of all protected endpoints. Run the token acquisition script from [Authentication — Full Authentication Script](authentication.md#full-authentication-script) to populate the `AUTH_TOKEN` environment variable.

---

## ZAP Docker Setup

Pull the official OWASP ZAP Docker image:

```bash
docker pull ghcr.io/zaproxy/zaproxy:stable
```

Verify the installed version:

```bash
docker run --rm ghcr.io/zaproxy/zaproxy:stable zap.sh -version
```

Expected output: `ZAP 2.17.0`

**Key capabilities of ZAP 2.17.0**:
- Java 17+ runtime (included in the Docker image — no host installation required)
- Automation Framework for CI/CD integration via YAML plans
- Browser-Based Authentication and Client Spider for modern SPA applications
- Replacer add-on for global header injection (used for Bearer token authentication)
- JSON and HTML report generation

Create the working directory for scan output:

```bash
mkdir -p zap-work
```

This directory is volume-mounted into the ZAP container at `/zap/wrk` and receives the scan report output.

---

## Authentication Context (Bearer Token)

ZAP requires authentication context to scan protected endpoints. WebVella ERP uses a `JWT_OR_COOKIE` dual authentication policy scheme: when an `Authorization: Bearer <token>` header is present, the request is routed to the JWT Bearer handler; otherwise, it falls back to cookie-based authentication.

> Source: `WebVella.Erp.Site/Startup.cs:L115-125`

For security scanning, configure ZAP's **Replacer add-on** to inject the Bearer token as a global request header on every outgoing request:

```bash
-z "-config replacer.full_list(0).matchtype=REQ_HEADER \
    -config replacer.full_list(0).matchstr=Authorization \
    -config replacer.full_list(0).replacement='Bearer <TOKEN>'"
```

Replace `<TOKEN>` with the actual JWT token value obtained from `POST /api/v3/en_US/auth/jwt/token`.

> Source: `WebVella.Erp.Web/Controllers/WebApiController.cs:L4273-4290` — JWT token issuance endpoint.

**How the Replacer works**: The Replacer add-on operates at the proxy level within ZAP. It intercepts every outgoing HTTP request (from the spider, AJAX spider, and active scanner) and adds or replaces the `Authorization` header with the configured Bearer token value. This ensures that all 60+ authenticated endpoints are reachable during the scan.

**Token lifetime consideration**: The JWT token has a configurable expiration (default: 24 hours). For scans exceeding this duration, use the token refresh endpoint to obtain a new token before the original expires. See [Authentication — Token Refresh Procedure](authentication.md#token-refresh-procedure).

> **CORS Note**: WebVella ERP configures `AllowAnyOrigin()` + `AllowAnyMethod()` + `AllowAnyHeader()` as the default CORS policy, so ZAP will not encounter CORS restrictions when scanning API endpoints.
> Source: `WebVella.Erp.Site/Startup.cs:L58-64`

---

## Scan Scope Definition

Define the exact URL scope patterns based on the WebVella ERP API attack surface. The scope limits ZAP's spider and active scanner to only the relevant API endpoints, preventing out-of-scope scanning of external resources or unrelated services.

### Scope Inclusion Patterns

The following URL patterns cover the complete WebVella ERP API surface as documented in the [Attack Surface Inventory](attack-surface-inventory.md):

| Scope Pattern | Endpoints Covered | Priority |
|---|---|---|
| `http://localhost:5000/api/v3/.*` | All REST API endpoints (EQL, entity meta, record CRUD, auth, scheduling, pages, snippets) | P1 |
| `http://localhost:5000/fs/.*` | File download, upload, move, delete operations | P2 |
| `http://localhost:5000/ckeditor/.*` | CKEditor file upload handlers | P2 |

### Scope Inclusion — Detailed Endpoint Mapping

Per the security assessment requirements, the scan scope encompasses these specific endpoint groups:

1. **`/api/v3/`** — All REST API endpoints. The `WebApiController` exposes 60+ routes across EQL execution, entity management, record CRUD, file operations, scheduling, authentication, and system administration.
   > Source: `WebVella.Erp.Web/Controllers/WebApiController.cs:L36-37` — class-level `[Authorize]` attribute

2. **`/api/v3/en_US/entity/`** — Entity management CRUD endpoints. Prioritize IDOR (Insecure Direct Object Reference) and Broken Object-Level Authorization (BOLA) testing on entity metadata modification.
   > Source: `WebVella.Erp.Web/Controllers/WebApiController.cs:L1436-1587` — entity meta CRUD

3. **`/api/v3/en_US/user/`** — Authentication-related endpoints. Test for privilege escalation and horizontal access control bypass.

4. **`/fs/upload/`** — Single file upload handler. No content-type validation or file size limits enforced in the controller.
   > Source: `WebVella.Erp.Web/Controllers/WebApiController.cs:L3327-3345`

5. **`/fs/upload-user-file-multiple/`** — Multi-file upload to user-scoped storage. No content-type or extension filtering.
   > Source: `WebVella.Erp.Web/Controllers/WebApiController.cs:L4041-4132`

6. **`/fs/upload-file-multiple/`** — Multi-file upload to temporary storage. No content-type or extension filtering.
   > Source: `WebVella.Erp.Web/Controllers/WebApiController.cs:L4134-4214`

7. **`/ckeditor/drop-upload-url`** — CKEditor drag-and-drop file upload. Creates file in `tmp/` then moves to user file storage.
   > Source: `WebVella.Erp.Web/Controllers/WebApiController.cs:L3962-4006`

8. **`/ckeditor/image-upload-url`** — CKEditor image upload. Returns HTML with inline JavaScript — potential XSS vector if `CKEditorFuncNum` query parameter is not sanitized.
   > Source: `WebVella.Erp.Web/Controllers/WebApiController.cs:L4009-4039`

### Scope Inclusion Regex (for ZAP Context Configuration)

```
http://localhost:5000/api/v3/.*
http://localhost:5000/fs/.*
http://localhost:5000/ckeditor/.*
```

### Scope Exclusion Patterns

Exclude non-security-relevant static resources and the anonymous CSS endpoint:

```
http://localhost:5000/api/v3.0/p/core/styles.css
http://localhost:5000/_framework/.*
http://localhost:5000/_content/.*
http://localhost:5000/lib/.*
```

> The `styles.css` endpoint is the only non-static `[AllowAnonymous]` endpoint aside from the JWT authentication routes, and it serves dynamically generated CSS with no security-relevant attack surface.
> Source: `WebVella.Erp.Web/Controllers/WebApiController.cs:L1038-1063`

---

## Automation Framework YAML Plan

The ZAP Automation Framework provides a YAML-based declarative configuration for reproducible scans. Save the following configuration as `zap-automation.yaml` in your project root:

```yaml
---
env:
  contexts:
    - name: "WebVella ERP API"
      urls:
        - "http://localhost:5000"
      includePaths:
        - "http://localhost:5000/api/v3/.*"
        - "http://localhost:5000/fs/.*"
        - "http://localhost:5000/ckeditor/.*"
      excludePaths:
        - "http://localhost:5000/api/v3.0/p/core/styles.css"
      sessionManagement:
        method: "headers"
        parameters:
          - "Authorization: Bearer <TOKEN>"
  parameters:
    failOnError: true
    progressToStdout: true

jobs:
  - type: spider
    parameters:
      context: "WebVella ERP API"
      maxDuration: 10
  - type: spiderAjax
    parameters:
      context: "WebVella ERP API"
      maxDuration: 5
  - type: activeScan
    parameters:
      context: "WebVella ERP API"
      maxRuleDurationInMins: 5
      maxScanDurationInMins: 60
    policyDefinition:
      rules:
        - id: 40018  # SQL Injection
          strength: HIGH
        - id: 40014  # Cross Site Scripting (Persistent)
          strength: HIGH
        - id: 40012  # Cross Site Scripting (Reflected)
          strength: HIGH
        - id: 40003  # CRLF Injection
          strength: MEDIUM
        - id: 6      # Path Traversal
          strength: HIGH
        - id: 30001  # Buffer Overflow
          strength: LOW
  - type: report
    parameters:
      template: "json-plus"
      reportDir: "/zap/wrk"
      reportFile: "zap-report.json"
```

**YAML Configuration Breakdown**:

| Section | Purpose |
|---|---|
| `env.contexts` | Defines the scan target URL and scope inclusion/exclusion patterns |
| `env.contexts.sessionManagement` | Configures header-based session management, injecting the Bearer token into every request |
| `env.parameters.failOnError` | Fail the scan job if ZAP encounters errors (useful for CI/CD gates) |
| `env.parameters.progressToStdout` | Stream scan progress to standard output for monitoring |
| `jobs[0]: spider` | Traditional spider to discover linked endpoints (10 min max) |
| `jobs[1]: spiderAjax` | AJAX spider for JavaScript-rendered content discovery (5 min max) |
| `jobs[2]: activeScan` | Active vulnerability scanning with custom policy rules (60 min max) |
| `jobs[2].policyDefinition.rules` | Per-rule strength overrides for priority vulnerability categories |
| `jobs[3]: report` | JSON report generation to the mounted volume |

> **Important**: Replace `<TOKEN>` in the YAML file with the actual JWT Bearer token value before execution. Use `sed` for inline replacement:
> ```bash
> sed -i "s/<TOKEN>/$TOKEN/g" zap-automation.yaml
> ```

---

## Active Scan Policy

The active scan policy defines which vulnerability categories ZAP tests against, mapped to the WebVella ERP attack surface. Rule strengths control the aggressiveness of testing: `HIGH` sends more payloads and variations, while `LOW` performs minimal checks.

### Vulnerability Category Mapping

| Vulnerability Category | ZAP Scanner Rule ID | Target Endpoints | Priority | OWASP Top 10 |
|---|---|---|---|---|
| SQL/EQL Injection | 40018 | `POST /api/v3/en_US/eql`, `POST /api/v3/en_US/eql-ds`, `POST /api/v3/en_US/eql-ds-select2`, all record CRUD endpoints | HIGH | A03:2021 Injection |
| XSS (Reflected) | 40012 | All API endpoints returning user-supplied input, `/ckeditor/image-upload-url` (`CKEditorFuncNum` injection) | HIGH | A03:2021 Injection |
| XSS (Persistent/Stored) | 40014 | Record create (`POST /api/v3/en_US/record/{entityName}`), record update (`PUT/PATCH`), page node options | HIGH | A03:2021 Injection |
| CRLF Injection | 40003 | All endpoints returning custom headers or processing user-controlled header values | MEDIUM | A03:2021 Injection |
| Path Traversal | 6 | `/fs/*` (file download, move, delete), `DELETE {*filepath}` wildcard route | HIGH | A01:2021 Broken Access Control |
| Buffer Overflow | 30001 | File upload endpoints (oversized payloads) | LOW | A06:2021 Vulnerable Components |
| CSRF | 4 | All state-changing (POST/PUT/PATCH/DELETE) endpoints | MEDIUM | A01:2021 Broken Access Control |
| IDOR (manual check) | Custom | Entity/record CRUD with UUID params (`/record/{entityName}/{recordId}`) | CRITICAL | A01:2021 Broken Access Control |

> **Note on EQL Injection**: WebVella ERP uses Entity Query Language (EQL) rather than raw SQL. The EQL execution endpoint at `POST /api/v3/en_US/eql` accepts user-supplied query strings via `model.Eql` and executes them through `EqlCommand`. While EQL has its own parser and is not direct SQL, injection testing is critical because the `eql-ds` endpoint also supports `CodeDataSource` execution paths that invoke dynamically compiled C# code.
> Source: `WebVella.Erp.Web/Controllers/WebApiController.cs:L63-95` (EQL endpoint), `L97-188` (DataSource endpoint)

### Recommended Additional Rule Overrides

For comprehensive coverage of the WebVella ERP attack surface, consider enabling these additional active scan rules:

| Rule ID | Rule Name | Relevance to WebVella |
|---|---|---|
| 40016 | Cross-Site Scripting (Persistent) — Prime | Covers stored XSS via record field values |
| 40017 | Cross-Site Scripting (Persistent) — Spider | Detects stored XSS during spidering |
| 90019 | Server Side Code Injection | Relevant to `/api/v3.0/datasource/code-compile` endpoint |
| 90020 | Remote OS Command Injection | Relevant to code execution paths |
| 40009 | Server Side Include | File inclusion via upload paths |
| 10010 | Cookie No HttpOnly Flag | Verify `erp_auth_base` cookie `HttpOnly` setting |
| 10011 | Cookie Without Secure Flag | Verify cookie security flags |

---

## IDOR-Specific Configuration

Insecure Direct Object Reference (IDOR) testing requires manual configuration beyond ZAP's automated active scan rules, because IDOR exploitation involves substituting one user's resource identifiers with another user's.

### Target Endpoints for IDOR Testing

Record CRUD endpoints are the primary BOLA (Broken Object-Level Authorization) surface. The controller does not implement record-level authorization checks — it relies on `RecordManager` internal logic.

> Source: `WebVella.Erp.Web/Controllers/WebApiController.cs:L2504-2517` — `GET` record by `{recordId}` with no controller-level permission check.

| HTTP Method | Route | IDOR Test Strategy |
|---|---|---|
| GET | `/api/v3/en_US/record/{entityName}/{recordId}` | Replace `{recordId}` with another user's record GUID |
| PUT | `/api/v3/en_US/record/{entityName}/{recordId}` | Attempt full update on another user's record |
| PATCH | `/api/v3/en_US/record/{entityName}/{recordId}` | Attempt partial update on another user's record |
| DELETE | `/api/v3/en_US/record/{entityName}/{recordId}` | Attempt deletion of another user's record |
| GET | `/api/v3/en_US/meta/entity/id/{entityId}` | Replace `{entityId}` with system entity GUIDs |
| PATCH | `/api/v3/en_US/meta/entity/{StringId}` | Attempt entity schema modification with non-admin token |

### IDOR Testing Procedure

1. **Create two user sessions**: Obtain JWT tokens for two different user accounts (e.g., the default admin `erp@webvella.com` and a newly created regular user).

2. **Identify target record GUIDs**: Using the admin token, list records from a target entity:
   ```bash
   curl -H "Authorization: Bearer $ADMIN_TOKEN" \
     "http://localhost:5000/api/v3/en_US/record/user/list"
   ```

3. **Cross-account access test**: Using the regular user's token, attempt to access admin-owned records:
   ```bash
   curl -H "Authorization: Bearer $USER_TOKEN" \
     "http://localhost:5000/api/v3/en_US/record/user/<ADMIN_RECORD_GUID>"
   ```

4. **ZAP Fuzzer integration**: Use ZAP's Fuzzer tool to iterate over a list of known record GUIDs in the `{recordId}` parameter position. Configure the fuzzer payload as a file containing one GUID per line.

5. **Expected behavior**: Requests for records outside the user's authorization scope should return HTTP 403 (Forbidden) or HTTP 404 (Not Found). Any HTTP 200 response with record data indicates a BOLA vulnerability (CWE-639: Authorization Bypass Through User-Controlled Key).

### Entity-Level IDOR Considerations

Entity meta endpoints (`/api/v3/en_US/meta/entity/*`) require `[Authorize(Roles = "administrator")]`. Test that a non-admin JWT token correctly receives HTTP 403 when accessing these routes. ZAP can be configured with two separate contexts (admin and regular user) to automate this comparison.

> Source: `WebVella.Erp.Web/Controllers/WebApiController.cs:L1436-1587` — entity meta endpoints with `[Authorize(Roles = "administrator")]`

---

## File Upload Scan Configuration

File upload endpoints are a high-priority scan target in WebVella ERP because none of the five upload handlers validate content type, file extension, or file size at the controller level.

> Source: `WebVella.Erp.Web/Controllers/WebApiController.cs:L3327-3345` (upload), `L4041-4132` (multi-upload user), `L4134-4214` (multi-upload), `L3962-4006` (CKEditor drop), `L4009-4039` (CKEditor image)

### Target Upload Endpoints

| Route | HTTP Method | Content-Type | Handler |
|---|---|---|---|
| `/fs/upload/` | POST | `multipart/form-data` | `UploadFile([FromForm] IFormFile file)` |
| `/fs/upload-user-file-multiple/` | POST | `multipart/form-data` | `UploadUserFileMultiple([FromForm] List<IFormFile> files)` |
| `/fs/upload-file-multiple/` | POST | `multipart/form-data` | `UploadFileMultiple([FromForm] List<IFormFile> files)` |
| `/ckeditor/drop-upload-url` | POST | `multipart/form-data` | `UploadDropCKEditor(IFormFile upload)` |
| `/ckeditor/image-upload-url` | POST | `multipart/form-data` | `UploadFileManagerCKEditor(IFormFile upload)` |

### ZAP File Upload Testing

Configure ZAP to test file upload handlers with the following payload categories:

**1. Web Shell Upload (CWE-434: Unrestricted Upload of File with Dangerous Type)**

Upload files with executable extensions to verify the server does not allow web shell deployment:
- `shell.aspx` — ASP.NET web shell
- `shell.cshtml` — Razor view file
- `shell.config` — Configuration file overwrite
- `shell.exe` — Windows executable

**2. Path Traversal Filenames (CWE-22: Improper Limitation of a Pathname)**

Upload files with path traversal sequences in the filename:
- `../../../etc/passwd` — Unix path traversal
- `..\..\web.config` — Windows path traversal
- `%2e%2e%2f%2e%2e%2fetc/passwd` — URL-encoded traversal

> The `/fs/move/` endpoint at L3347-3368 accepts `source` and `target` paths directly from the request body with no sanitization — this is an additional path traversal surface.
> Source: `WebVella.Erp.Web/Controllers/WebApiController.cs:L3347-3368`

**3. Oversized File Upload**

Upload files exceeding reasonable size limits to test for denial of service:
- The upload handlers read the entire file into memory via `ReadFully()` (L3385-3397) without size validation
- Test with 10MB, 50MB, and 100MB payloads

**4. CKEditor XSS via `CKEditorFuncNum` (CWE-79: Cross-Site Scripting)**

The `/ckeditor/image-upload-url` endpoint injects the `CKEditorFuncNum` query parameter directly into an inline `<script>` tag without sanitization:

```csharp
var vOutput = @"<html><body><script>window.parent.CKEDITOR.tools.callFunction("
    + CKEditorFuncNum + ", \"" + url + "\", \"" + vMessage + "\");</script></body></html>";
```

> Source: `WebVella.Erp.Web/Controllers/WebApiController.cs:L4029`

Test by setting `CKEditorFuncNum` to XSS payloads:
```bash
curl -X POST "http://localhost:5000/ckeditor/image-upload-url?CKEditorFuncNum=1);alert('XSS');//" \
  -H "Authorization: Bearer $TOKEN" \
  -F "upload=@test-image.png"
```

### Wildcard Delete Route

The `DELETE {*filepath}` route matches **any URL path** for file deletion. The `filepath` parameter is lowercased but passed directly to `DbFileRepository.Delete()` with no validation:

```bash
# Test wildcard path deletion — should be rejected for non-file paths
curl -X DELETE "http://localhost:5000/../../sensitive-file" \
  -H "Authorization: Bearer $TOKEN"
```

> Source: `WebVella.Erp.Web/Controllers/WebApiController.cs:L3370-3383`

---

## Output Configuration

Configure ZAP to generate a JSON report for machine parsing and integration with the finding analysis pipeline.

### JSON Report Format

Use the `-J` flag for the `zap-full-scan.py` wrapper:

```bash
-J zap-report.json
```

Or configure the `report` job in the Automation Framework YAML:

```yaml
- type: report
  parameters:
    template: "json-plus"
    reportDir: "/zap/wrk"
    reportFile: "zap-report.json"
```

The report is written to `/zap/wrk/zap-report.json` inside the container, which maps to `./zap-work/zap-report.json` on the host via the volume mount.

### Report Contents

The ZAP JSON report contains:
- **Site**: Target URL and host information
- **Alerts**: Array of findings, each containing:
  - `alert`: Finding name
  - `riskcode`: Severity (0=Informational, 1=Low, 2=Medium, 3=High)
  - `confidence`: Detection confidence level
  - `cweid`: CWE identifier for the finding
  - `wascid`: WASC identifier
  - `uri`: Affected URL
  - `param`: Vulnerable parameter name
  - `attack`: Payload that triggered the finding
  - `evidence`: Response evidence confirming the vulnerability
  - `solution`: Recommended remediation
  - `reference`: External reference URLs

### Viewing the Report

After the scan completes, verify the report was generated:

```bash
ls -la zap-work/zap-report.json
```

Preview the report summary using `jq`:

```bash
# Count findings by risk level
cat zap-work/zap-report.json | jq '.site[].alerts | group_by(.riskcode) | map({risk: .[0].riskcode, count: length})'
```

```bash
# List HIGH and CRITICAL findings
cat zap-work/zap-report.json | jq '.site[].alerts[] | select(.riskcode >= 3) | {alert: .alert, risk: .riskcode, cweid: .cweid, uri: .instances[0].uri}'
```

> **Next Step**: See [Finding Analysis](finding-analysis.md) for the complete output parsing procedure, cross-scanner deduplication with Nuclei results, and source code location methodology.

---

## Complete Scan Execution Commands

### Method 1: Quick-Start Full Scan (`zap-full-scan.py`)

The `zap-full-scan.py` script provides a single-command execution that spiders the target and runs an active scan:

```bash
docker run --network host -v $(pwd)/zap-work:/zap/wrk \
  ghcr.io/zaproxy/zaproxy:stable zap-full-scan.py \
  -t http://localhost:5000 -J zap-report.json \
  -z "-config replacer.full_list(0).matchtype=REQ_HEADER \
      -config replacer.full_list(0).matchstr=Authorization \
      -config replacer.full_list(0).replacement='Bearer <TOKEN>'"
```

**Command breakdown**:

| Flag | Purpose |
|---|---|
| `--network host` | Use host networking so ZAP can reach `localhost:5000` |
| `-v $(pwd)/zap-work:/zap/wrk` | Mount local `zap-work/` directory for report output |
| `zap-full-scan.py` | ZAP full scan wrapper script (spider + active scan) |
| `-t http://localhost:5000` | Target URL |
| `-J zap-report.json` | JSON report output filename |
| `-z "..."` | Pass ZAP command-line options (Replacer configuration for Bearer token) |

> **Replace `<TOKEN>`** with the actual JWT token value. Example using a shell variable:
> ```bash
> docker run --network host -v $(pwd)/zap-work:/zap/wrk \
>   ghcr.io/zaproxy/zaproxy:stable zap-full-scan.py \
>   -t http://localhost:5000 -J zap-report.json \
>   -z "-config replacer.full_list(0).matchtype=REQ_HEADER \
>       -config replacer.full_list(0).matchstr=Authorization \
>       -config replacer.full_list(0).replacement='Bearer $TOKEN'"
> ```

### Method 2: Automation Framework Execution

For greater control over scan scope, policies, and job sequencing, use the Automation Framework YAML plan:

```bash
docker run --network host -v $(pwd)/zap-work:/zap/wrk \
  -v $(pwd)/zap-automation.yaml:/zap/wrk/automation.yaml \
  ghcr.io/zaproxy/zaproxy:stable zap.sh \
  -cmd -autorun /zap/wrk/automation.yaml
```

**Command breakdown**:

| Flag | Purpose |
|---|---|
| `--network host` | Use host networking for `localhost:5000` access |
| `-v $(pwd)/zap-work:/zap/wrk` | Mount output directory |
| `-v $(pwd)/zap-automation.yaml:/zap/wrk/automation.yaml` | Mount the Automation Framework YAML plan |
| `zap.sh -cmd` | Run ZAP in command-line (headless) mode |
| `-autorun /zap/wrk/automation.yaml` | Execute the Automation Framework plan |

> **Recommendation**: Use Method 2 (Automation Framework) for production security assessments. It provides explicit scope control, custom scan policies, and reproducible configurations that can be version-controlled alongside the project.

### Parallel Execution with Nuclei

ZAP and Nuclei scans can run in parallel since they operate independently against the same target. Start both scanners simultaneously:

```bash
# Run ZAP and Nuclei in parallel
docker run --network host -v $(pwd)/zap-work:/zap/wrk \
  ghcr.io/zaproxy/zaproxy:stable zap-full-scan.py \
  -t http://localhost:5000 -J zap-report.json \
  -z "-config replacer.full_list(0).matchtype=REQ_HEADER \
      -config replacer.full_list(0).matchstr=Authorization \
      -config replacer.full_list(0).replacement='Bearer $TOKEN'" &

docker run --network host -v $(pwd)/nuclei-work:/tmp/output \
  projectdiscovery/nuclei:latest \
  -u http://localhost:5000 \
  -tags aspnet,api -severity critical,high \
  -H "Authorization: Bearer $TOKEN" \
  -jsonl -o /tmp/output/nuclei-results.jsonl &

# Wait for both scanners to complete
wait
echo "[+] Both ZAP and Nuclei scans complete."
```

> **Cross-reference**: See [Nuclei Scan Configuration](nuclei-scan-config.md) for the full Nuclei command syntax and template pack details.

> **Performance Note**: Running both scanners concurrently doubles the request volume to the WebVella ERP instance. Ensure the Docker environment has sufficient CPU and memory resources. Monitor the WebVella container for errors during parallel scanning:
> ```bash
> docker compose logs -f web
> ```

---

## Scan Duration Estimates

Scan duration varies based on the number of endpoints discovered by the spider and the active scan policy strength:

| Scan Phase | Estimated Duration | Notes |
|---|---|---|
| Spider (traditional) | 5–10 minutes | Discovers linked API endpoints from seed URL |
| AJAX Spider | 3–5 minutes | JavaScript-rendered content discovery |
| Active Scan (default policy) | 30–60 minutes | Tests all discovered endpoints with configured rules |
| Active Scan (HIGH strength, all rules) | 60–120 minutes | More payload variations per endpoint |
| Report Generation | < 1 minute | JSON report written to volume mount |

> **Tip**: For initial reconnaissance, run a passive scan only (spider without active scan) to inventory discovered endpoints before committing to a full active scan. Use the `passiveScan-wait` job type in the Automation Framework YAML.

---

## Troubleshooting

### ZAP Cannot Reach Target

**Symptom**: ZAP reports "Failed to connect to localhost:5000" or scan discovers zero URLs.

**Resolution**:
1. Verify WebVella ERP is running: `curl -sf http://localhost:5000/api/v3/en_US/meta`
2. Ensure `--network host` is used in the Docker run command (ZAP needs host networking to reach `localhost`)
3. On macOS/Windows with Docker Desktop, `--network host` may not work as expected — use the host's IP address instead of `localhost`

### Authentication Failures (401 Responses)

**Symptom**: ZAP finds endpoints but receives HTTP 401 on all protected routes.

**Resolution**:
1. Verify the JWT token is valid: `curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/v3/en_US/meta/entity/list`
2. Check that the Replacer configuration syntax is correct — quotation marks around `Bearer <TOKEN>` must match exactly
3. Verify the token has not expired — refresh using `POST /api/v3/en_US/auth/jwt/token/refresh`
4. Review ZAP's History tab (in GUI mode) to confirm the `Authorization` header is being injected

### Scan Takes Too Long

**Symptom**: Active scan exceeds `maxScanDurationInMins` and is terminated prematurely.

**Resolution**:
1. Increase `maxScanDurationInMins` in the Automation Framework YAML
2. Reduce scan scope to focus on CRITICAL and HIGH priority endpoints first
3. Lower rule strength from `HIGH` to `MEDIUM` for non-priority vulnerability categories
4. Exclude low-risk endpoints from the active scan context

### Empty Report

**Symptom**: `zap-report.json` exists but contains no alerts.

**Resolution**:
1. This may indicate the scan completed successfully with no findings — verify by checking ZAP's scan log
2. Ensure the spider discovered endpoints within the scope (check `spider` job output)
3. Verify the active scan policy includes the correct rule IDs
4. Check that the API returns valid JSON responses — ZAP may not parse non-standard response formats

---

## Next Steps

- **[Nuclei Scan Configuration](nuclei-scan-config.md)** — Configure the parallel Nuclei scanner with ASP.NET Core templates for complementary coverage.
- **[Finding Analysis](finding-analysis.md)** — Parse ZAP JSON output, deduplicate across scanners, and locate vulnerable source code.
- **[Remediation Guide](remediation-guide.md)** — Apply ASP.NET Core secure coding patterns to resolve findings.
- **[Security Report](security-report.md)** — Generate the final per-finding report with CWE references and scanner confirmation.

---

> **Navigation**: [← Attack Surface Inventory](attack-surface-inventory.md) | [Security Assessment Overview](README.md) | [Nuclei Scan Configuration →](nuclei-scan-config.md)
