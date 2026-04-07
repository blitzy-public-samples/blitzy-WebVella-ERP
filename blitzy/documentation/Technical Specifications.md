# Technical Specification

# 0. Agent Action Plan

## 0.1 Intent Clarification

### 0.1.1 Core Documentation Objective

Based on the provided requirements, the Blitzy platform understands that the documentation objective is to **create comprehensive documentation** for a full dynamic security validation workflow on the WebVella ERP platform — an open-source ASP.NET Core 9 ERP system backed by PostgreSQL 16. This is a **new documentation creation** initiative that will produce a complete security assessment report and accompanying procedural documentation.

The documentation type is a hybrid of: **Security Assessment Report**, **Technical Procedure Guide**, and **Remediation Documentation**.

The documentation requirements, restated with enhanced clarity:

- **Docker Environment Setup Documentation**: Document the procedure to clone the WebVella ERP repository from GitHub (`https://github.com/WebVella/WebVella-ERP.git`), stand up the application and its PostgreSQL 16 dependency via Docker Compose, and validate health by polling the metadata endpoint `GET /api/v3/en_US/meta` until an HTTP 200 response is returned. The repository does not contain a Docker Compose file, so one must be authored.

- **Authentication Procedure Documentation**: Document the REST API authentication flow using the WebVella JWT token endpoint at `POST /api/v3/en_US/auth/jwt/token` with the default admin credentials (`erp@webvella.com` / `erp`). Capture the Bearer token extraction process from the JSON response and configure it as an `Authorization: Bearer <token>` header for all subsequent API requests.

- **OWASP ZAP Scan Configuration Documentation**: Document the configuration of an OWASP ZAP (v2.17.0) authenticated active scan using the extracted Bearer token. The scan scope encompasses:
  - `/api/v3/` — all REST API endpoints (the `WebApiController` exposes 60+ routes)
  - `/api/v3/en_US/entity/` — entity management CRUD endpoints (prioritize IDOR and Broken Object-Level Authorization)
  - `/api/v3/en_US/user/` — authentication endpoints (test for privilege escalation)
  - `/fs/upload/`, `/fs/upload-user-file-multiple/`, `/fs/upload-file-multiple/`, `/ckeditor/drop-upload-url`, `/ckeditor/image-upload-url` — all file upload handlers present in `WebVella.Erp.Web/Controllers/WebApiController.cs`

- **Nuclei Scan Configuration Documentation**: Document a parallel Nuclei (v3.7.1) scan using the ASP.NET Core and generic API template packs against the same base URL, with templates version v10.3.9.

- **Finding Analysis and Remediation Documentation**: Document the process of parsing ZAP JSON and Nuclei outputs, deduplicating findings across scanners, and for each HIGH or CRITICAL finding: locating the vulnerable file and line number in the WebVella source, generating a remediation patch using ASP.NET Core secure coding patterns (parameterized EF Core / EQL queries for SQLi, proper `[Authorize]` attributes and role checks for IDOR, output encoding for XSS), applying the patch, rebuilding the Docker image, and re-running the specific scan check to confirm resolution.

- **Final Security Report Documentation**: Produce a structured report per finding containing: CWE reference, vulnerable code before patch, remediated code after patch, and scanner confirmation of resolution.

**Inferred Documentation Needs** (implicit requirements surfaced from codebase analysis):

- The repository lacks a `Dockerfile` and `docker-compose.yml` at the project root — documentation must include the creation of these files and their contents
- The security architecture uses DES encryption (legacy) and MD5 password hashing (unsalted) — these are known pre-existing weaknesses documented in Section 6.4 of the tech spec that scanners will likely flag
- The CORS policy in `WebVella.Erp.Site/Startup.cs` allows any origin (`AllowAnyOrigin()`) — this will likely appear as a finding
- The JWT error response in `WebApiController.cs` line 4287 leaks stack traces (`e.Message + e.StackTrace`) — this information disclosure pattern needs remediation documentation
- Multiple `[AllowAnonymous]` attributes are commented out but present in the codebase, indicating security surface boundaries that need documentation
- The `Config.json` contains hardcoded secrets (JWT key, encryption key, database credentials) — scanners may flag configuration exposure

### 0.1.2 Special Instructions and Constraints

- **Docker Compose Authoring**: Since the repository contains no `docker-compose.yml`, the documentation must include the full file content targeting .NET 9.0 SDK, PostgreSQL 16, and the `WebVella.Erp.Site` project
- **Default Credentials**: The default admin credentials are `erp@webvella.com` / `erp` as documented in `docs/developer/introduction/getting-started.md`
- **Scanner Parallelism**: ZAP and Nuclei scans are to run in parallel — documentation must address concurrent scan orchestration
- **Remediation Scope**: Patches must follow ASP.NET Core secure coding patterns specifically — parameterized queries via EQL (not raw SQL), proper `[Authorize]` attributes, and output encoding
- **Rebuild-and-Verify Cycle**: Each remediation must include a Docker image rebuild and targeted re-scan to confirm resolution

### 0.1.3 Technical Interpretation

These documentation requirements translate to the following technical documentation strategy:

- To **document the Docker environment setup**, we will **create** `docs/security/docker-setup.md` containing Docker Compose configuration for the .NET 9.0 application and PostgreSQL 16, health check polling procedures, and troubleshooting guidance
- To **document the authentication procedure**, we will **create** `docs/security/authentication.md` detailing the JWT token acquisition flow via `POST /api/v3/en_US/auth/jwt/token`, token extraction from the JSON response envelope, and header configuration for authenticated scanning
- To **document the ZAP scan configuration**, we will **create** `docs/security/zap-scan-config.md` specifying the authenticated active scan setup, scope definitions mapped to the `WebApiController.cs` route inventory, and scan policy parameters
- To **document the Nuclei scan configuration**, we will **create** `docs/security/nuclei-scan-config.md` covering template pack selection, ASP.NET Core-specific templates, and parallel execution alongside ZAP
- To **document finding analysis and remediation**, we will **create** `docs/security/finding-analysis.md` and `docs/security/remediation-guide.md` covering the deduplication algorithm, vulnerable code location process, and ASP.NET Core remediation patterns
- To **produce the final security report**, we will **create** `docs/security/security-report.md` with a per-finding structure: CWE reference, before/after code, and scanner confirmation

### 0.1.4 Inferred Documentation Needs

Based on code analysis:
- `WebVella.Erp.Web/Controllers/WebApiController.cs` (4313 lines) contains 60+ API routes with security-relevant patterns including raw EQL execution, file upload handling without content-type validation, and stack trace leakage in error responses — all require security assessment documentation
- `WebVella.Erp.Web/Security/` contains authentication primitives (AuthToken, AuthCache, AuthorizeAttribute, WebSecurityUtil) where implementations are largely commented out — this unusual state needs documentation as it affects the security posture
- `WebVella.Erp/Utilities/CryptoUtility.cs` uses DES encryption and `PasswordUtil.cs` uses unsalted MD5 — known weaknesses requiring remediation documentation
- `WebVella.Erp.Site/Startup.cs` configures `AllowAnyOrigin()` CORS policy — a common scanner finding requiring documentation
- `WebVella.Erp.Site/Config.json` contains hardcoded JWT signing key (`ThisIsMySecretKeyThisIsMySecretKey...`) and database credentials — configuration exposure requiring documentation

Based on user journey:
- Security teams need a complete end-to-end runbook from environment setup through scan execution to remediation verification
- Each finding needs traceable documentation from scanner output to source code location to remediation patch to re-scan confirmation

## 0.2 Documentation Discovery and Analysis

### 0.2.1 Existing Documentation Infrastructure Assessment

Repository analysis reveals a structured developer documentation framework under `docs/developer/` with **14 topical sections**, but **zero security assessment or vulnerability documentation** exists. The documentation uses a folder.json manifest convention with HTML-comment JSON front-matter for metadata.

**Documentation Framework**: Static HTML/Markdown files served directly — no documentation generator (MkDocs, Docusaurus, Sphinx) detected. All documentation is plain Markdown with inline HTML comment metadata blocks.

**Documentation Generator Configuration**: None detected. No `mkdocs.yml`, `docusaurus.config.js`, `sphinx/conf.py`, or `.readthedocs.yml` found in the repository.

**API Documentation Tools**: No automated API documentation tools (Swagger/OpenAPI, JSDoc, XML doc comments) detected. The existing `docs/developer/web-api/` section provides manual REST API documentation covering base URL conventions, CORS behavior, ISO 8601 date formatting, and the JSON response envelope structure (`success`, `message`, `timestamp`, `errors`, `object`).

**Diagram Tools**: No Mermaid, PlantUML, or other diagram tooling detected. Documentation relies on text descriptions only.

**Current Documentation Structure**:

```
docs/
└── developer/
    ├── applications/       (Application management concepts)
    ├── background-jobs/    (Job scheduling documentation)
    ├── components/         (UI component reference)
    ├── data-sources/       (Data source configuration)
    ├── entities/           (Entity modeling reference - comprehensive)
    ├── hooks/              (API hooks, page hooks, render hooks)
    ├── introduction/       (Getting started guide - references .NET Core 2.1, outdated)
    ├── pages/              (Page system documentation)
    ├── plugins/            (Plugin architecture)
    ├── server-api/         (EntityManager, RecordManager, SecurityManager C# examples)
    ├── system-log/         (Logging documentation)
    ├── tag-helpers/        (Razor tag helper reference)
    ├── users-and-roles/    (User management, role system: Administrator/Regular/Guest)
    └── web-api/            (REST API overview, response format)
```

**Documentation Conventions Observed**:
- Each topic folder contains a `folder.json` manifest with `name`, `page`, and `nav` properties
- Individual pages use HTML-comment JSON front-matter: `<!-- {"name": "...", "tag": "..."} -->`
- Content is plain Markdown with occasional inline HTML
- No standardized template across sections
- Version references are outdated (introduction references .NET Core 2.1, while codebase targets .NET 9.0)

### 0.2.2 Repository Code Analysis for Documentation

Search patterns employed for code requiring security documentation:

- **Controllers**: `find . -name "*.cs" -path "*/Controllers/*"` → 4 controllers found:
  - `WebVella.Erp.Web/Controllers/WebApiController.cs` (4313 lines, 60+ routes — primary API surface)
  - `WebVella.Erp.Web/Controllers/ApiControllerBase.cs` (base controller)
  - `WebVella.Erp.Web/Controllers/AdminController.cs` (admin functionality)
  - `WebVella.Erp.Web/Controllers/ProjectController.cs` (project management)

- **Security Files**: `find . -name "*.cs" -path "*Security*"` → 11 files found:
  - `WebVella.Erp.Web/Security/AuthCache.cs` (in-process GUID cache, 5-min TTL)
  - `WebVella.Erp.Web/Security/AuthToken.cs` (DES-encrypted token Create/Encrypt/Decrypt/Verify)
  - `WebVella.Erp.Web/Security/AuthorizeAttribute.cs` (ActionFilterAttribute — no role checks implemented)
  - `WebVella.Erp.Web/Security/ErpIdentity.cs` (ClaimsIdentity wrapper)
  - `WebVella.Erp.Web/Security/ErpPrincipal.cs` (ClaimsPrincipal wrapper)
  - `WebVella.Erp.Web/Security/HttpForbiddenResult.cs`
  - `WebVella.Erp.Web/Security/HttpUnauthorizedResult.cs`
  - `WebVella.Erp.Web/Security/WebSecurityUtil.cs` (AUTH_TOKEN_KEY=`erp-auth`, token expiry 2/30 days)
  - `WebVella.Erp/Api/Security/QuerySecurity.cs`
  - `WebVella.Erp/Api/Security/SecurityContext.cs` (AsyncLocal scoping)
  - `WebVella.Erp/Api/SecurityManager.cs` (user CRUD, EQL-based queries)

- **Configuration Files**: `WebVella.Erp.Site/Config.json` — contains connection string, encryption key, JWT key, development mode flag

- **File Upload Handlers**: Located in `WebApiController.cs` at routes `/fs/upload/`, `/fs/upload-user-file-multiple/`, `/fs/upload-file-multiple/`, `/ckeditor/drop-upload-url`, `/ckeditor/image-upload-url` — no content-type validation or file size limits observed

- **Authentication Endpoints**: JWT token endpoint at `POST /api/v3/en_US/auth/jwt/token` (lines 4270-4300) and refresh at `POST /api/v3/en_US/auth/jwt/token/refresh` (lines 4302-4314)

**Key Directories Examined**:
- `WebVella.Erp/` — Core library (SecurityManager, CryptoUtility, PasswordUtil)
- `WebVella.Erp.Web/` — Web layer (Controllers, Security, Middleware)
- `WebVella.Erp.Site/` — Host site (Startup.cs, Config.json)
- `docs/developer/` — All 14 documentation subsections
- `WebVella.Erp.Plugins.*` — 6 plugin projects (SDK, Next, CRM, Project, Marketplace, Duatec)

**Related Documentation Found**:
- `docs/developer/web-api/overview.md` — REST API conventions (base URL, CORS, authorization cookie)
- `docs/developer/web-api/response.md` — JSON response envelope format
- `docs/developer/server-api/overview.md` — C# server-side API reference (EntityManager, RecordManager, SecurityManager)
- `docs/developer/users-and-roles/` — Users.md (user management), Roles.md (3-tier role system with GUIDs), Overview.md
- `docs/developer/entities/` — Entity field definitions, relation modeling, CRUD API patterns
- `docs/developer/hooks/` — API hooks (pre/post CRUD), page hooks, render hooks

### 0.2.3 Web Search Research Conducted

- **OWASP ZAP latest version**: ZAP 2.17.0 (stable, released December 15, 2025). Docker image: `ghcr.io/zaproxy/zaproxy:stable`. Requires Java 17+. Supports Automation Framework for CI/CD integration, Browser-Based Authentication, and Client Spider.

- **Nuclei latest version**: Nuclei v3.7.1 (latest, released March 5, 2026). Templates version v10.3.9 with 9,821+ templates. Requires Go >= 1.24.2 for source installation. Docker image: `projectdiscovery/nuclei:latest`. MIT licensed. Supports ASP.NET Core and generic API template packs.

- **Best practices for security scanning ASP.NET Core applications**: Authenticated DAST scanning with token-based auth, scope containment to API surface areas, IDOR testing on entity-level endpoints, file upload handler fuzzing, and configuration exposure checks.

- **Documentation structure conventions for security assessment reports**: CWE-referenced findings, before/after code snippets, severity classification (CRITICAL/HIGH/MEDIUM/LOW), scanner-confirmed remediation verification, and OWASP Top 10 mapping.

## 0.3 Documentation Scope Analysis

### 0.3.1 Code-to-Documentation Mapping

**Modules Requiring Security Assessment Documentation:**

- **Module**: `WebVella.Erp.Web/Controllers/WebApiController.cs`
  - Public APIs: 60+ REST endpoints including EQL execution, entity meta CRUD, record CRUD, file upload/move/delete, schedule plans, system log, JWT authentication, and page/node management
  - Current documentation: Partial — `docs/developer/web-api/` covers base URL and response format only; no security documentation exists
  - Documentation needed: Complete API attack surface inventory, endpoint-level security classification, scan scope definition, per-finding remediation documentation

- **Module**: `WebVella.Erp.Web/Security/AuthToken.cs`
  - Public APIs: `Create()`, `Encrypt()`, `Decrypt()`, `Verify()` — DES-encrypted URL-safe token management
  - Current documentation: None
  - Documentation needed: Security assessment finding for DES encryption weakness, remediation patch to upgrade to AES-256-GCM

- **Module**: `WebVella.Erp.Web/Security/AuthorizeAttribute.cs`
  - Public APIs: Custom `ActionFilterAttribute` applied at controller level
  - Current documentation: None
  - Documentation needed: Security finding for missing role-based checks, remediation patch to add proper `[Authorize(Roles = "...")]` enforcement

- **Module**: `WebVella.Erp.Web/Security/WebSecurityUtil.cs`
  - Public APIs: Central security orchestrator with `AUTH_TOKEN_KEY` constant, token expiration configuration (2/30 days), IMemoryCache identity management
  - Current documentation: None
  - Documentation needed: Token management security findings, SameSite cookie configuration remediation

- **Module**: `WebVella.Erp/Api/SecurityManager.cs`
  - Public APIs: `GetUser(Guid)`, `GetUser(email)`, `GetUserByUsername(string)`, `GetUser(email, password)` — all use EQL queries with `SecurityContext.OpenSystemScope()`
  - Current documentation: Partial — `docs/developer/server-api/overview.md` provides C# examples
  - Documentation needed: EQL injection assessment, system scope privilege escalation assessment

- **Module**: `WebVella.Erp.Site/Startup.cs`
  - Configuration points: CORS policy (`AllowAnyOrigin()`), authentication scheme (`JWT_OR_COOKIE`), Razor Page authorization folder rules
  - Current documentation: None
  - Documentation needed: CORS misconfiguration finding documentation, authorization scheme security assessment

- **Module**: `WebVella.Erp.Site/Config.json`
  - Configuration options: ConnectionString, EncryptionKey, JWT Key, JWT Issuer/Audience, DevelopmentMode flag
  - Current documentation: None
  - Documentation needed: Hardcoded secrets finding, configuration exposure remediation (environment variables, secrets manager)

- **Module**: File upload handlers in `WebApiController.cs` (routes: `/fs/upload/`, `/fs/upload-user-file-multiple/`, `/fs/upload-file-multiple/`, `/ckeditor/*`)
  - Public APIs: `UploadFile()`, `UploadUserFileMultiple()`, `UploadFileMultiple()`, `CkEditorDropUploadUrl()`, `CkEditorImageUploadUrl()`
  - Current documentation: None
  - Documentation needed: File upload vulnerability assessment (unrestricted file type, path traversal via `MoveFile()`, missing size limits), remediation patches

- **Module**: `WebVella.Erp/Api/CryptoUtility.cs`
  - Public APIs: DES encryption/decryption utilities
  - Current documentation: None
  - Documentation needed: Cryptographic weakness findings, AES-256 upgrade remediation

- **Module**: `WebVella.Erp/Api/PasswordUtil.cs`
  - Public APIs: MD5-based password hashing (unsalted)
  - Current documentation: None
  - Documentation needed: Password hashing weakness finding, bcrypt/Argon2 upgrade remediation

### 0.3.2 Configuration Options Requiring Documentation

| Config File | Options Documented | Options Total | Missing Documentation |
|---|---|---|---|
| `Config.json` | 0 | 8+ | ConnectionString, EncryptionKey, JwtKey, JwtIssuer, JwtAudience, DevelopmentMode, EnableBackgroundJobs, EmailSettings |
| `Startup.cs` (CORS) | 0 | 3 | AllowAnyOrigin, AllowAnyMethod, AllowAnyHeader |
| `Startup.cs` (Auth) | 0 | 4 | Cookie name, HttpOnly, Login path, JWT_OR_COOKIE scheme |

### 0.3.3 Documentation Gap Analysis

Given the requirements and repository analysis, documentation gaps include:

**Undocumented Security Surface (Complete Gap)**:
- No security assessment documentation of any kind exists in the repository
- No Docker infrastructure documentation (no Dockerfile or docker-compose.yml in repository root)
- No scanner configuration documentation (ZAP, Nuclei, or any DAST tooling)
- No vulnerability remediation templates or patterns documented
- No CWE-referenced security findings archive

**Undocumented Public APIs Requiring Security Documentation**:
- `POST /api/v3/en_US/eql` — EQL query execution (SQL injection surface)
- `POST /api/v3/en_US/eql-ds` — Datasource query execution
- `GET/POST/PUT/PATCH/DELETE /api/v3/en_US/meta/entity/*` — Full entity meta CRUD (IDOR surface)
- `GET/POST/PUT/PATCH/DELETE /api/v3/en_US/record/{entityName}/*` — Record CRUD (BOLA surface)
- `POST /api/v3/en_US/auth/jwt/token` — JWT issuance (stack trace leakage, brute-force surface)
- `POST /api/v3/en_US/auth/jwt/token/refresh` — Token refresh (token reuse surface)
- `POST /fs/upload/` — File upload (unrestricted upload surface)
- `POST /fs/move/` — File move (path traversal surface)
- `DELETE /{*filepath}` — File delete (wildcard path deletion surface)
- `GET /fs/{fileName}` — File download (path traversal read surface)
- `POST /api/v3.0/datasource/code-compile` — Dynamic code compilation (RCE surface)

**Outdated Documentation Requiring Update**:
- `docs/developer/introduction/getting-started.md` — References .NET Core 2.1 while codebase targets .NET 9.0

**Missing Architecture Security Documentation**:
- No threat model documentation
- No security zone boundary documentation
- No data flow diagrams showing trust boundaries
- No incident response or vulnerability disclosure procedures

## 0.4 Documentation Implementation Design

### 0.4.1 Documentation Structure Planning

The security validation workflow documentation will be organized under a new `docs/security/` directory, parallel to the existing `docs/developer/` structure. Each phase of the workflow corresponds to a dedicated documentation file.

```
docs/
├── developer/                          (existing — unchanged)
│   ├── introduction/
│   ├── web-api/
│   ├── server-api/
│   ├── users-and-roles/
│   ├── entities/
│   ├── hooks/
│   └── ... (10 other sections)
└── security/
    ├── README.md                       (Security assessment overview and quick start)
    ├── folder.json                     (Navigation manifest following existing convention)
    ├── docker-setup.md                 (Docker environment setup and health validation)
    ├── authentication.md               (JWT token acquisition and scan authentication)
    ├── attack-surface-inventory.md     (Complete API endpoint security classification)
    ├── zap-scan-config.md              (OWASP ZAP authenticated active scan setup)
    ├── nuclei-scan-config.md           (Nuclei template-based scan configuration)
    ├── finding-analysis.md             (Output parsing, deduplication, triage)
    ├── remediation-guide.md            (ASP.NET Core secure coding patterns)
    ├── security-report.md              (Final per-finding report with CWE references)
    └── diagrams/
        ├── scan-workflow.md            (Mermaid: end-to-end scan workflow)
        ├── attack-surface.md           (Mermaid: API route security classification)
        └── remediation-flow.md         (Mermaid: patch-rebuild-verify cycle)
```

### 0.4.2 Content Generation Strategy

**Information Extraction Approach**:
- Extract the full API route inventory from `WebVella.Erp.Web/Controllers/WebApiController.cs` (lines 1-4313) by parsing `[HttpGet]`, `[HttpPost]`, `[HttpPut]`, `[HttpPatch]`, `[HttpDelete]` attributes and their route templates
- Extract security configuration from `WebVella.Erp.Site/Startup.cs` (CORS policy, authentication scheme, cookie configuration, authorization folder rules)
- Extract cryptographic implementations from `WebVella.Erp/Api/CryptoUtility.cs` (DES encryption) and `WebVella.Erp/Api/PasswordUtil.cs` (MD5 hashing)
- Extract authentication flow from `WebVella.Erp.Web/Security/WebSecurityUtil.cs` (token creation, identity caching, session management)
- Extract file upload handling from `WebApiController.cs` lines 3320-3500 (upload, move, delete handlers)
- Generate remediation examples by analyzing ASP.NET Core 9 secure coding patterns (parameterized queries, authorization policies, output encoding via `HtmlEncoder`)
- Create Mermaid diagrams by mapping controller route dependencies, security filter pipelines, and scan workflow sequences

**Template Application**:
- Follow the existing `docs/developer/` convention: `folder.json` manifest with `name`, `page`, and `nav` properties per folder
- Individual page metadata via HTML-comment JSON front-matter: `<!-- {"name": "...", "tag": "..."} -->`
- Maintain consistent Markdown header hierarchy (# for page title, ## for major sections, ### for subsections)

**Documentation Standards**:
- Markdown formatting with proper headers (`# ## ### ####`)
- Mermaid diagram integration using triple-backtick `mermaid` fenced code blocks for all workflow visualizations
- Code examples using triple-backtick fenced blocks with syntax highlighting (`csharp`, `json`, `bash`, `yaml`)
- Source citations as inline references: `Source: WebVella.Erp.Web/Controllers/WebApiController.cs:L4287`
- Tables for endpoint inventories, finding summaries, and configuration option descriptions
- Consistent terminology: "finding" (not "vulnerability"), "remediation" (not "fix"), "scan target" (not "attack target")

### 0.4.3 Diagram and Visual Strategy

**Mermaid Diagrams to Create**:

- **End-to-End Scan Workflow** (`docs/security/diagrams/scan-workflow.md`): Flowchart showing the complete sequence from Docker environment setup → health check → authentication → parallel ZAP + Nuclei scans → output parsing → deduplication → remediation → re-scan verification → report generation

- **API Attack Surface Classification** (`docs/security/diagrams/attack-surface.md`): Graph diagram categorizing all 60+ `WebApiController` endpoints by security risk level (Critical: EQL execution, file uploads; High: entity/record CRUD; Medium: page/node management; Low: static resources)

- **Remediation Patch-Rebuild-Verify Cycle** (`docs/security/diagrams/remediation-flow.md`): Sequence diagram showing the iterative cycle: identify finding → locate source file/line → generate patch → apply patch → rebuild Docker image → re-run targeted scan → verify resolution

- **Authentication Flow Diagram** (inline in `docs/security/authentication.md`): Sequence diagram showing `POST /api/v3/en_US/auth/jwt/token` request → `AuthService.GetTokenAsync()` → JWT token response → Bearer header configuration

- **Security Architecture Overview** (inline in `docs/security/attack-surface-inventory.md`): Class diagram showing `WebApiController` → `SecurityManager` → `SecurityContext` → `AuthorizeAttribute` relationships and the `JWT_OR_COOKIE` authentication pipeline

## 0.5 Documentation File Transformation Mapping

### 0.5.1 File-by-File Documentation Plan

| Target Documentation File | Transformation | Source Code/Docs | Content/Changes |
|---|---|---|---|
| `docs/security/README.md` | CREATE | `README.md`, `docs/developer/introduction/getting-started.md` | Security assessment overview, workflow summary, prerequisites, quick-start guide linking to all sub-documents |
| `docs/security/folder.json` | CREATE | `docs/developer/folder.json` (style reference) | Navigation manifest with `name`, `page`, `nav` properties following existing docs/developer convention |
| `docs/security/docker-setup.md` | CREATE | `WebVella.Erp.Site/WebVella.Erp.Site.csproj`, `WebVella.Erp.Site/Startup.cs`, `WebVella.Erp.Site/Config.json` | Docker Compose authoring for .NET 9.0 + PostgreSQL 16, Dockerfile creation, health check polling `/api/v3/en_US/meta`, environment variable configuration, container networking |
| `docs/security/authentication.md` | CREATE | `WebVella.Erp.Web/Controllers/WebApiController.cs` (lines 4270-4314), `WebVella.Erp.Web/Security/WebSecurityUtil.cs`, `docs/developer/users-and-roles/users.md` | JWT token acquisition via `POST /api/v3/en_US/auth/jwt/token` with default credentials (`erp@webvella.com` / `erp`), Bearer header configuration, token refresh procedure, Mermaid auth flow diagram |
| `docs/security/attack-surface-inventory.md` | CREATE | `WebVella.Erp.Web/Controllers/WebApiController.cs` (all 4313 lines), `WebVella.Erp.Web/Controllers/AdminController.cs`, `WebVella.Erp.Web/Controllers/ApiControllerBase.cs` | Complete endpoint inventory table with route, HTTP method, authorization requirement, security risk classification; categorized by EQL/entity/record/file/auth/system groups |
| `docs/security/zap-scan-config.md` | CREATE | `WebVella.Erp.Web/Controllers/WebApiController.cs` (route inventory), `WebVella.Erp.Site/Startup.cs` (CORS config) | OWASP ZAP 2.17.0 Docker configuration, Automation Framework YAML plan, Bearer token auth context, scan scope definitions for `/api/v3/`, `/api/v3/en_US/entity/`, `/api/v3/en_US/user/`, file upload handlers; scan policy for IDOR, BOLA, XSS, SQLi |
| `docs/security/nuclei-scan-config.md` | CREATE | `WebVella.Erp.Web/Controllers/WebApiController.cs`, `WebVella.Erp.Site/Startup.cs` | Nuclei v3.7.1 configuration, ASP.NET Core template pack (`-tags aspnet`), generic API templates (`-tags api`), custom header injection for Bearer token, parallel execution alongside ZAP, output format selection (`-jsonl`) |
| `docs/security/finding-analysis.md` | CREATE | `WebVella.Erp.Web/Controllers/WebApiController.cs`, `WebVella.Erp.Web/Security/*.cs`, `WebVella.Erp/Api/SecurityManager.cs`, `WebVella.Erp/Api/CryptoUtility.cs`, `WebVella.Erp/Api/PasswordUtil.cs` | ZAP JSON output parsing procedure, Nuclei JSONL output parsing, cross-scanner deduplication algorithm (by CWE + URL + parameter), severity filtering for HIGH/CRITICAL, source code location methodology (file + line mapping), finding triage workflow |
| `docs/security/remediation-guide.md` | CREATE | `WebVella.Erp.Web/Controllers/WebApiController.cs`, `WebVella.Erp.Web/Security/AuthToken.cs`, `WebVella.Erp.Web/Security/AuthorizeAttribute.cs`, `WebVella.Erp/Api/CryptoUtility.cs`, `WebVella.Erp/Api/PasswordUtil.cs`, `WebVella.Erp.Site/Startup.cs` | ASP.NET Core 9 secure coding patterns: parameterized EQL queries (SqlParameter binding), `[Authorize(Roles)]` attribute enforcement, `HtmlEncoder.Default.Encode()` for XSS output encoding, `AesGcm` for encryption upgrade, `BCrypt.Net` for password hashing, CORS origin whitelisting, `SameSite=Strict` cookie policy, error response sanitization; per-pattern before/after code examples |
| `docs/security/security-report.md` | CREATE | All source files referenced in finding-analysis.md and remediation-guide.md | Final report template with per-finding structure: Finding ID, CWE Reference, Severity, Scanner Source (ZAP/Nuclei/Both), Affected File:Line, Vulnerable Code Before, Remediated Code After, Scanner Re-Scan Confirmation, OWASP Top 10 Mapping |
| `docs/security/diagrams/scan-workflow.md` | CREATE | N/A (synthesized from workflow) | Mermaid flowchart: Docker setup → health poll → JWT auth → parallel ZAP+Nuclei → parse → dedup → remediate → rebuild → re-scan → report |
| `docs/security/diagrams/attack-surface.md` | CREATE | `WebVella.Erp.Web/Controllers/WebApiController.cs` | Mermaid graph: endpoint classification by risk level (Critical/High/Medium/Low) with route groupings |
| `docs/security/diagrams/remediation-flow.md` | CREATE | N/A (synthesized from workflow) | Mermaid sequence diagram: patch-rebuild-verify iterative cycle per finding |
| `docker-compose.yml` | CREATE | `WebVella.Erp.Site/WebVella.Erp.Site.csproj`, `WebVella.Erp.Site/Config.json` | Docker Compose file at repository root defining `web` (.NET 9.0 app) and `db` (PostgreSQL 16) services with health checks, volume mounts, environment variables, and network configuration |
| `Dockerfile` | CREATE | `WebVella.Erp.Site/WebVella.Erp.Site.csproj`, `global.json` | Multi-stage Dockerfile at repository root: build stage (mcr.microsoft.com/dotnet/sdk:9.0), runtime stage (mcr.microsoft.com/dotnet/aspnet:9.0), exposing port 5000 |
| `docs/developer/introduction/getting-started.md` | UPDATE | `docs/developer/introduction/getting-started.md` | Update .NET Core 2.1 references to .NET 9.0; add link to new `docs/security/` documentation; note Docker-based setup alternative |

### 0.5.2 New Documentation Files Detail

```
File: docs/security/README.md
Type: Overview / Quick Start
Source Code: README.md, docs/developer/introduction/getting-started.md
Sections:
    - Overview (purpose of security validation workflow)
    - Prerequisites (Docker, OWASP ZAP, Nuclei, .NET 9.0 SDK)
    - Quick Start (condensed 6-step procedure)
    - Document Index (links to all sub-documents)
    - Conventions (terminology, severity levels, CWE references)
Diagrams:
    - End-to-end workflow overview (Mermaid flowchart)
Key Citations: README.md, WebVella.Erp.Site/WebVella.Erp.Site.csproj
```

```
File: docs/security/docker-setup.md
Type: Procedure Guide
Source Code: WebVella.Erp.Site/WebVella.Erp.Site.csproj, WebVella.Erp.Site/Startup.cs, WebVella.Erp.Site/Config.json
Sections:
    - Prerequisites (Docker Engine 24+, Docker Compose V2)
    - Repository Cloning (git clone from GitHub)
    - Dockerfile Creation (multi-stage build for .NET 9.0)
    - Docker Compose Configuration (web + db services)
    - Environment Variables (connection string, JWT key, encryption key)
    - Container Startup Procedure (docker compose up -d)
    - Health Check Validation (polling GET /api/v3/en_US/meta)
    - Troubleshooting (common startup failures, PostgreSQL connectivity)
Diagrams:
    - Container architecture (Mermaid: web ↔ db network)
Key Citations: WebVella.Erp.Site/Startup.cs:L28-42, Config.json
```

```
File: docs/security/authentication.md
Type: Procedure Guide
Source Code: WebVella.Erp.Web/Controllers/WebApiController.cs:L4270-4314, WebVella.Erp.Web/Security/WebSecurityUtil.cs
Sections:
    - Default Credentials (erp@webvella.com / erp from docs/developer/introduction/getting-started.md)
    - JWT Token Request (POST /api/v3/en_US/auth/jwt/token with JwtTokenLoginModel)
    - Response Parsing (extract token from JSON envelope)
    - Bearer Header Configuration (Authorization: Bearer <token>)
    - Token Refresh (POST /api/v3/en_US/auth/jwt/token/refresh)
    - Cookie-Based Authentication Alternative (erp_auth_base cookie)
    - Scanner Authentication Setup (configuring ZAP and Nuclei with token)
Diagrams:
    - JWT authentication sequence (Mermaid sequence diagram)
Key Citations: WebApiController.cs:L4270-4314, WebSecurityUtil.cs, docs/developer/users-and-roles/users.md
```

```
File: docs/security/attack-surface-inventory.md
Type: API Reference / Security Classification
Source Code: WebVella.Erp.Web/Controllers/WebApiController.cs (all 4313 lines)
Sections:
    - Endpoint Inventory Table (route, method, auth requirement, risk level)
    - EQL Execution Endpoints (POST /api/v3/en_US/eql, /eql-ds, /eql-ds-select2)
    - Entity Meta CRUD Endpoints (GET/POST/PATCH/DELETE /api/v3/en_US/meta/entity/*)
    - Record CRUD Endpoints (GET/POST/PUT/PATCH/DELETE /api/v3/en_US/record/*)
    - File System Endpoints (GET/POST/DELETE /fs/*)
    - Authentication Endpoints (POST /api/v3/en_US/auth/jwt/*)
    - Scheduling Endpoints (GET/PUT/POST /api/v3/en_US/scheduleplan/*)
    - Anonymous Endpoints (GET /api/v3.0/p/core/styles.css)
    - Security Risk Classification Matrix
Diagrams:
    - Attack surface classification graph (Mermaid)
Key Citations: WebApiController.cs (full file), AdminController.cs
```

```
File: docs/security/zap-scan-config.md
Type: Configuration Guide
Source Code: WebApiController.cs (route inventory), Startup.cs (CORS config)
Sections:
    - ZAP Docker Setup (ghcr.io/zaproxy/zaproxy:stable)
    - Automation Framework Plan (YAML configuration)
    - Authentication Context (Bearer token header injection)
    - Scan Scope Definition (included/excluded URL patterns)
    - Active Scan Policy (IDOR, BOLA, SQLi, XSS, CSRF, path traversal)
    - IDOR-Specific Configuration (entity UUID parameter fuzzing)
    - File Upload Scan Configuration (multipart form testing)
    - Output Configuration (JSON report format)
    - Scan Execution Commands
Diagrams: None (configuration-focused)
Key Citations: WebApiController.cs route inventory, Startup.cs:L34-36 (CORS)
```

```
File: docs/security/nuclei-scan-config.md
Type: Configuration Guide
Source Code: WebApiController.cs, Startup.cs
Sections:
    - Nuclei Installation (Docker or binary)
    - Template Pack Selection (ASP.NET Core: -tags aspnet, API: -tags api)
    - Custom Header Configuration (-H "Authorization: Bearer <token>")
    - Target URL Configuration (-u http://localhost:5000)
    - Severity Filtering (-severity critical,high)
    - Output Format (-jsonl for machine parsing)
    - Parallel Execution Strategy (alongside ZAP)
    - Template Update Procedure (-update-templates)
Diagrams: None (configuration-focused)
Key Citations: WebApiController.cs (target surface), Startup.cs (server configuration)
```

```
File: docs/security/finding-analysis.md
Type: Procedure Guide
Source Code: WebApiController.cs, Security/*.cs, SecurityManager.cs, CryptoUtility.cs, PasswordUtil.cs
Sections:
    - ZAP JSON Output Parsing (jq-based extraction)
    - Nuclei JSONL Output Parsing (jq-based extraction)
    - Cross-Scanner Deduplication Algorithm (CWE + URL + parameter matching)
    - Severity Filtering (CRITICAL and HIGH only per requirements)
    - Source Code Location Methodology (grep/ripgrep for vulnerable patterns)
    - Finding Classification (CWE mapping, OWASP Top 10 alignment)
    - Pre-Existing Vulnerability Catalog (known issues from tech spec 6.4)
Diagrams:
    - Finding triage flowchart (Mermaid)
Key Citations: All security-relevant source files
```

```
File: docs/security/remediation-guide.md
Type: Technical Guide
Source Code: WebApiController.cs, AuthToken.cs, AuthorizeAttribute.cs, CryptoUtility.cs, PasswordUtil.cs, Startup.cs
Sections:
    - SQL Injection Remediation (parameterized EQL queries)
    - IDOR/BOLA Remediation (entity-level authorization checks via CanRead/CanUpdate/CanDelete role GUIDs)
    - XSS Remediation (HtmlEncoder output encoding)
    - Cryptographic Weakness Remediation (DES → AES-256-GCM migration)
    - Password Hashing Remediation (MD5 → bcrypt/Argon2 migration)
    - CORS Misconfiguration Remediation (origin whitelisting)
    - Information Disclosure Remediation (error response sanitization)
    - File Upload Remediation (content-type validation, size limits, path sanitization)
    - Docker Rebuild Procedure (docker compose build --no-cache)
    - Re-Scan Verification (targeted ZAP/Nuclei re-scan per finding)
Diagrams:
    - Patch-rebuild-verify cycle (Mermaid sequence diagram)
Key Citations: All remediated source files with before/after line references
```

```
File: docs/security/security-report.md
Type: Security Report Template
Source Code: All findings from ZAP and Nuclei scans
Sections:
    - Executive Summary (total findings by severity, scan coverage)
    - Finding Detail Template (repeating per finding):
        - Finding ID, CWE Reference, Severity
        - Scanner Source (ZAP/Nuclei/Both)
        - Affected File:Line Number
        - Vulnerable Code (before patch)
        - Remediated Code (after patch)
        - Remediation Pattern Applied
        - Scanner Re-Scan Confirmation (pass/fail)
    - OWASP Top 10 Coverage Matrix
    - Residual Risk Assessment
    - Recommendations for Further Hardening
Diagrams:
    - Finding severity distribution (table/chart)
Key Citations: All finding source files
```

### 0.5.3 Documentation Files to Update Detail

- `docs/developer/introduction/getting-started.md` — Update .NET Core 2.1 references to .NET 9.0
  - Update: Change SDK version reference from 2.1 to 9.0
  - Addition: Add "Security Assessment" section linking to `docs/security/README.md`
  - Addition: Add Docker-based setup instructions as alternative to manual setup
  - Source citation: `WebVella.Erp.Site/WebVella.Erp.Site.csproj` (net9.0 TFM)

### 0.5.4 Documentation Configuration Updates

- `docs/security/folder.json` — New navigation manifest for the security documentation folder following the existing `docs/developer/folder.json` convention with `name: "Security Assessment"`, ordered `nav` array linking all 9 documentation pages

### 0.5.5 Cross-Documentation Dependencies

- **Navigation Links**: `docs/security/README.md` links to all 9 sub-documents; each sub-document links back to README and to the next document in the workflow sequence
- **Forward References**: `docker-setup.md` → `authentication.md` → `attack-surface-inventory.md` → `zap-scan-config.md` / `nuclei-scan-config.md` → `finding-analysis.md` → `remediation-guide.md` → `security-report.md`
- **Cross-References to Existing Docs**: `authentication.md` references `docs/developer/users-and-roles/users.md` for credential details; `attack-surface-inventory.md` references `docs/developer/web-api/overview.md` for API conventions
- **Root README Update**: `README.md` should add a link to `docs/security/README.md` under a new "Security" section

## 0.6 Dependency Inventory

### 0.6.1 Documentation Dependencies

The following tools and packages are relevant to producing and validating the security assessment documentation. All versions are verified from web search and repository analysis.

| Registry | Package Name | Version | Purpose |
|---|---|---|---|
| Docker Hub | `ghcr.io/zaproxy/zaproxy:stable` | 2.17.0 | OWASP ZAP dynamic application security scanner (DAST); Docker image for authenticated active scanning |
| Docker Hub | `projectdiscovery/nuclei:latest` | v3.7.1 | Nuclei template-based vulnerability scanner; Docker image for ASP.NET Core and API template scanning |
| Docker Hub | `mcr.microsoft.com/dotnet/sdk:9.0` | 9.0 | .NET 9.0 SDK for building WebVella ERP Docker image (build stage) |
| Docker Hub | `mcr.microsoft.com/dotnet/aspnet:9.0` | 9.0 | ASP.NET Core 9.0 runtime for WebVella ERP Docker image (runtime stage) |
| Docker Hub | `postgres:16` | 16 | PostgreSQL 16 database container for WebVella ERP data storage |
| GitHub | `projectdiscovery/nuclei-templates` | v10.3.9 | Community-maintained vulnerability detection templates (9,821+ templates); auto-downloaded by Nuclei |
| NuGet | `Microsoft.AspNetCore.Authentication.JwtBearer` | 9.0.10 | JWT Bearer authentication middleware (existing project dependency) |
| NuGet | `Npgsql` | 9.0.4 | PostgreSQL .NET data provider (existing project dependency) |
| NuGet | `System.IdentityModel.Tokens.Jwt` | 8.14.0 | JWT token handling library (existing project dependency) |
| NuGet | `Newtonsoft.Json` | 13.0.4 | JSON serialization for API responses (existing project dependency) |
| System | `jq` | 1.7+ | Command-line JSON processor for parsing ZAP and Nuclei scan outputs |
| System | `curl` | 8.0+ | HTTP client for health check polling and API authentication requests |
| System | `Docker Engine` | 24.0+ | Container runtime for running WebVella, PostgreSQL, ZAP, and Nuclei |
| System | `Docker Compose` | V2 (2.20+) | Multi-container orchestration for the WebVella + PostgreSQL stack |

### 0.6.2 WebVella ERP Project Dependencies (Security-Relevant)

These are existing NuGet dependencies from the WebVella codebase that are directly relevant to the security assessment documentation:

| NuGet Package | Version | Project | Security Relevance |
|---|---|---|---|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 9.0.10 | WebVella.Erp.Site | JWT authentication scheme — scanner target for token-based auth testing |
| `System.IdentityModel.Tokens.Jwt` | 8.14.0 | WebVella.Erp.Web | JWT token creation/validation — weak HMAC-SHA256 key in Config.json |
| `Npgsql` | 9.0.4 | WebVella.Erp | PostgreSQL driver — relevant for SQL injection assessment of EQL queries |
| `Storage.Net` | 9.3.0 | WebVella.Erp | Cloud/filesystem storage abstraction — relevant for file upload security |
| `HtmlAgilityPack` | 1.12.4 | WebVella.Erp.Web | HTML parsing — relevant for XSS output encoding assessment |
| `Microsoft.CodeAnalysis.CSharp.Scripting` | 4.14.0 | WebVella.Erp.Web | Dynamic C# compilation — relevant for code injection assessment at `/api/v3.0/datasource/code-compile` |
| `AutoMapper` | 14.0.0 | WebVella.Erp | Object mapping — relevant for IDOR assessment (object property exposure) |

### 0.6.3 Remediation Dependencies (To Be Added)

These packages may be recommended in the remediation documentation for upgrading security primitives:

| NuGet Package | Recommended Version | Purpose |
|---|---|---|
| `BCrypt.Net-Next` | 4.0.3 | Replace unsalted MD5 password hashing with bcrypt |
| `Microsoft.AspNetCore.DataProtection` | 9.0.x | Replace DES encryption with modern data protection APIs |

### 0.6.4 Documentation Reference Updates

No existing documentation links require transformation since the security documentation is entirely new. However, the following cross-references should be established:

- `docs/security/README.md` → `docs/developer/web-api/overview.md` (API conventions reference)
- `docs/security/authentication.md` → `docs/developer/users-and-roles/users.md` (default credentials)
- `docs/security/attack-surface-inventory.md` → `docs/developer/entities/overview.md` (entity structure reference)
- `docs/developer/introduction/getting-started.md` → `docs/security/README.md` (new security section link)

## 0.7 Coverage and Quality Targets

### 0.7.1 Documentation Coverage Metrics

**Current Coverage Analysis**:

| Coverage Area | Currently Documented | Total Required | Coverage % |
|---|---|---|---|
| Security assessment procedures | 0 / 6 procedures | 6 procedures | 0% |
| API endpoint security classification | 0 / 60+ endpoints | 60+ endpoints | 0% |
| Scanner configuration guides | 0 / 2 scanners | 2 scanners (ZAP, Nuclei) | 0% |
| Remediation patterns | 0 / 8 patterns | 8 ASP.NET Core patterns | 0% |
| Docker infrastructure setup | 0 / 2 files | 2 files (Dockerfile, docker-compose.yml) | 0% |
| Finding analysis procedures | 0 / 3 procedures | 3 (parse, dedup, triage) | 0% |
| Security report template | 0 / 1 template | 1 per-finding template | 0% |

**Target Coverage**: 100% documentation coverage of all 6 workflow phases specified in the user requirements.

**Coverage Gaps to Address**:

- **Docker Environment** (currently 0%): No Docker Compose file exists in the repository; documentation must author both `Dockerfile` and `docker-compose.yml` from scratch and document the complete setup procedure
- **Authentication Procedure** (currently 0%): JWT token endpoint exists at `POST /api/v3/en_US/auth/jwt/token` but is undocumented for scanner use; existing `docs/developer/web-api/overview.md` only mentions cookie-based authorization
- **Attack Surface Inventory** (currently 0%): `WebApiController.cs` contains 60+ endpoints across 4313 lines; no security classification exists; each endpoint must be inventoried with method, route, authorization requirement, and risk level
- **Scanner Configuration** (currently 0%): No ZAP or Nuclei configuration documentation exists; both require authenticated scan setup with Bearer token injection
- **Finding Analysis** (currently 0%): No deduplication, parsing, or triage procedures documented; the cross-scanner correlation algorithm must be defined
- **Remediation Patterns** (currently 0%): No ASP.NET Core secure coding pattern documentation exists; 8 remediation patterns needed (SQLi, IDOR, XSS, crypto, password hashing, CORS, info disclosure, file upload)
- **Security Report** (currently 0%): No per-finding report template exists; must include CWE reference, before/after code, scanner confirmation

### 0.7.2 Documentation Quality Criteria

**Completeness Requirements**:
- All 6 workflow phases from the user requirements are documented with step-by-step procedures
- Every public API endpoint in `WebApiController.cs` has a security classification entry in the attack surface inventory
- All scanner configuration parameters are documented with exact command-line syntax or YAML configuration
- Each ASP.NET Core remediation pattern includes before/after code examples with source file citations
- The Docker setup procedure includes health check validation with expected HTTP response
- The final report template includes every required field: CWE reference, vulnerable code, remediated code, scanner confirmation

**Accuracy Validation**:
- All code examples reference actual source files with line numbers (e.g., `WebApiController.cs:L4287`)
- API endpoints match the actual routes discovered in the `WebApiController.cs` codebase analysis
- Scanner commands use verified tool versions: ZAP 2.17.0, Nuclei v3.7.1, Templates v10.3.9
- Docker images use exact tags: `mcr.microsoft.com/dotnet/sdk:9.0`, `mcr.microsoft.com/dotnet/aspnet:9.0`, `postgres:16`
- Default credentials match `docs/developer/introduction/getting-started.md`: `erp@webvella.com` / `erp`
- Configuration values match `Config.json` analysis (JWT key, encryption key, database credentials)

**Clarity Standards**:
- Technical accuracy with accessible language suitable for security engineers and DevOps teams
- Progressive disclosure: overview documents link to detailed procedure documents
- Consistent terminology: "finding" (scanner output), "remediation" (code fix), "verification" (re-scan confirmation)
- Each document follows a consistent structure: Overview → Prerequisites → Procedure → Verification → Troubleshooting

**Maintainability**:
- Source citations on every technical claim (file path:line number format)
- Clear scanner version dependencies (ZAP 2.17.0, Nuclei v3.7.1) documented in prerequisites
- Template-based report structure allows adding new findings without restructuring
- Navigation manifest (`folder.json`) follows existing documentation convention for discoverability

### 0.7.3 Example and Diagram Requirements

| Document | Minimum Code Examples | Required Diagrams | Verification Method |
|---|---|---|---|
| `docker-setup.md` | 3 (Dockerfile, docker-compose.yml, health check curl) | 1 (container architecture) | Docker Compose up + health endpoint 200 |
| `authentication.md` | 2 (login curl, token extraction) | 1 (auth sequence diagram) | JWT token in response body |
| `attack-surface-inventory.md` | 0 (table-based) | 1 (risk classification graph) | Route count matches codebase |
| `zap-scan-config.md` | 3 (Docker run, Automation Framework YAML, scope config) | 0 | ZAP scan completes without error |
| `nuclei-scan-config.md` | 2 (nuclei command, custom header) | 0 | Nuclei scan completes without error |
| `finding-analysis.md` | 3 (jq parse ZAP, jq parse Nuclei, dedup script) | 1 (triage flowchart) | Deduplicated output produced |
| `remediation-guide.md` | 16 (8 patterns × 2 before/after) | 1 (patch-rebuild-verify cycle) | Re-scan shows finding resolved |
| `security-report.md` | 2 (report template, sample finding) | 0 | Report renders correctly |

## 0.8 Scope Boundaries

### 0.8.1 Exhaustively In Scope

**New Documentation Files** (with trailing patterns):
- `docs/security/README.md` — Security assessment overview and workflow quick start
- `docs/security/folder.json` — Navigation manifest for security documentation
- `docs/security/docker-setup.md` — Docker environment setup and health validation
- `docs/security/authentication.md` — JWT token acquisition and scan authentication
- `docs/security/attack-surface-inventory.md` — Complete API endpoint security classification
- `docs/security/zap-scan-config.md` — OWASP ZAP authenticated active scan configuration
- `docs/security/nuclei-scan-config.md` — Nuclei template-based scan configuration
- `docs/security/finding-analysis.md` — Output parsing, deduplication, and triage
- `docs/security/remediation-guide.md` — ASP.NET Core secure coding pattern documentation
- `docs/security/security-report.md` — Final per-finding report with CWE references
- `docs/security/diagrams/*.md` — Mermaid workflow, attack surface, and remediation diagrams

**Infrastructure Files** (new):
- `Dockerfile` — Multi-stage build for WebVella ERP (.NET 9.0 SDK build, ASP.NET 9.0 runtime)
- `docker-compose.yml` — Docker Compose orchestration for web + PostgreSQL 16 services

**Documentation File Updates**:
- `docs/developer/introduction/getting-started.md` — Update .NET version references (2.1 → 9.0), add security documentation link

**Documentation Assets**:
- `docs/security/diagrams/scan-workflow.md` — End-to-end scan workflow Mermaid diagram
- `docs/security/diagrams/attack-surface.md` — API endpoint risk classification Mermaid diagram
- `docs/security/diagrams/remediation-flow.md` — Patch-rebuild-verify cycle Mermaid diagram

**Source Code Files Referenced for Documentation** (read-only analysis, no modifications):
- `WebVella.Erp.Web/Controllers/WebApiController.cs` — Full API route inventory extraction
- `WebVella.Erp.Web/Controllers/AdminController.cs` — Admin endpoint analysis
- `WebVella.Erp.Web/Controllers/ApiControllerBase.cs` — Base controller patterns
- `WebVella.Erp.Web/Security/AuthToken.cs` — DES encryption token analysis
- `WebVella.Erp.Web/Security/AuthorizeAttribute.cs` — Authorization attribute analysis
- `WebVella.Erp.Web/Security/WebSecurityUtil.cs` — Security utility configuration analysis
- `WebVella.Erp.Web/Security/AuthCache.cs` — Authentication cache analysis
- `WebVella.Erp.Web/Security/ErpIdentity.cs` — Identity model analysis
- `WebVella.Erp.Web/Security/ErpPrincipal.cs` — Principal model analysis
- `WebVella.Erp/Api/SecurityManager.cs` — Security manager EQL query analysis
- `WebVella.Erp/Api/Security/SecurityContext.cs` — Security context scoping analysis
- `WebVella.Erp/Api/Security/QuerySecurity.cs` — Query security analysis
- `WebVella.Erp/Api/CryptoUtility.cs` — DES encryption utility analysis
- `WebVella.Erp/Api/PasswordUtil.cs` — MD5 password hashing analysis
- `WebVella.Erp.Site/Startup.cs` — CORS, authentication, authorization configuration analysis
- `WebVella.Erp.Site/Config.json` — Hardcoded secrets analysis

### 0.8.2 Explicitly Out of Scope

- **Source code modifications**: No changes to any `.cs`, `.cshtml`, or `.razor` files — remediation patterns are documented only; actual code patches are implementation-phase work guided by the documentation
- **Test file modifications**: No changes to test projects or test files
- **Feature additions or code refactoring**: No new features, API endpoints, or architectural changes
- **Deployment configuration changes**: No changes to CI/CD pipelines, cloud infrastructure, or production deployment configurations (except the new Dockerfile and docker-compose.yml for local security scanning)
- **Unrelated documentation**: No changes to the 13 existing `docs/developer/` sections other than the version reference update in `getting-started.md`
- **Plugin project documentation**: No documentation changes to any of the 6 plugin projects (`WebVella.Erp.Plugins.SDK`, `WebVella.Erp.Plugins.Next`, `WebVella.Erp.Plugins.Crm`, `WebVella.Erp.Plugins.Project`, `WebVella.Erp.Plugins.Marketplace`, `WebVella.Erp.Plugins.Duatec`)
- **Blazor WebAssembly documentation**: No documentation for the `WebVella.BlazorWasm.*` solution
- **Console application documentation**: No documentation for `WebVella.Erp.ConsoleApp`
- **Host site variant documentation**: No documentation for the 6 alternative host sites (`WebVella.Erp.Site2` through `WebVella.Erp.Site7`)
- **Planning artifact modifications**: No changes to `blitzy/` or `jira-stories/` directories
- **Production security hardening**: Actual secret rotation, key management integration, or production CORS configuration is out of scope — documentation provides guidance only
- **Automated CI/CD security pipeline**: Setting up recurring automated scans in a CI/CD pipeline is not in scope; documentation covers one-time manual execution

## 0.9 Execution Parameters

### 0.9.1 Documentation-Specific Instructions

**Docker Environment Build Command**:
```bash
docker compose up -d --build
```

**Health Check Polling Command**:
```bash
curl -sf http://localhost:5000/api/v3/en_US/meta
```

**JWT Authentication Command**:
```bash
curl -X POST http://localhost:5000/api/v3/en_US/auth/jwt/token \
  -H "Content-Type: application/json" \
  -d '{"email":"erp@webvella.com","password":"erp"}'
```

**OWASP ZAP Scan Execution Command**:
```bash
docker run --network host -v $(pwd)/zap-work:/zap/wrk \
  ghcr.io/zaproxy/zaproxy:stable zap-full-scan.py \
  -t http://localhost:5000 -J zap-report.json \
  -z "-config replacer.full_list(0).matchtype=REQ_HEADER \
      -config replacer.full_list(0).matchstr=Authorization \
      -config replacer.full_list(0).replacement='Bearer <TOKEN>'"
```

**Nuclei Scan Execution Command**:
```bash
docker run --network host projectdiscovery/nuclei:latest \
  -u http://localhost:5000 \
  -tags aspnet,api -severity critical,high \
  -H "Authorization: Bearer <TOKEN>" \
  -jsonl -o nuclei-results.jsonl
```

**Docker Image Rebuild After Remediation**:
```bash
docker compose build --no-cache web && docker compose up -d web
```

**Documentation Preview**: Not applicable — documentation uses plain Markdown files without a documentation generator. Preview via any Markdown renderer (VS Code, GitHub, or `grip` for local rendering).

**Diagram Generation**: Mermaid diagrams are embedded inline in Markdown files using fenced code blocks. No separate generation step is required — diagrams render natively in GitHub, VS Code with Mermaid extension, or any Mermaid-compatible renderer.

**Default Format**: Markdown with Mermaid diagrams, following the existing `docs/developer/` conventions (folder.json manifest, HTML-comment JSON front-matter).

**Citation Requirement**: Every technical claim in the documentation must reference its source file using the format: `Source: <file_path>:<line_range>` (e.g., `Source: WebVella.Erp.Web/Controllers/WebApiController.cs:L4270-4314`).

**Style Guide**: Follow the existing repository documentation conventions observed in `docs/developer/`:
- Plain Markdown with HTML-comment metadata headers
- `folder.json` manifests for navigation
- Code examples in fenced blocks with language identifiers
- Inline source citations rather than footnotes

**Documentation Validation**: Verify all internal links between documents resolve correctly; verify all source file citations reference valid file paths and line numbers in the repository.

## 0.10 Rules for Documentation

No explicit documentation rules were specified by the user. The following rules are inferred from the requirements and enforced for consistency:

- **Follow existing documentation style and structure**: All new documentation under `docs/security/` must follow the `docs/developer/` conventions — `folder.json` navigation manifests, HTML-comment JSON front-matter metadata, plain Markdown content
- **Include Mermaid diagrams for all workflow visualizations**: The end-to-end scan workflow, attack surface classification, authentication flow, and remediation cycle must each have corresponding Mermaid diagrams
- **Provide working command-line examples for every procedure step**: Each step in the 6-phase workflow (Docker setup, authentication, ZAP scan, Nuclei scan, finding analysis, remediation) must include exact, copy-pasteable shell commands
- **Add source code citations for all technical details**: Every reference to a source file, configuration value, API endpoint, or security behavior must include a citation in the format `Source: <file_path>:<line_range>`
- **Use CWE references for all security findings**: Every documented finding must include its Common Weakness Enumeration (CWE) identifier for standardized vulnerability classification
- **Document before/after code for every remediation**: Each remediation pattern must show the vulnerable code (before) and the fixed code (after) as paired fenced code blocks with source file citations
- **Include scanner confirmation for every remediated finding**: Each finding in the final report must include a verification step showing the scanner re-scan result confirming the finding is resolved
- **Maintain ASP.NET Core secure coding patterns**: All remediation patches must use ASP.NET Core 9 idiomatic patterns — parameterized EQL queries (not raw SQL), `[Authorize]` attributes with role specifications, `HtmlEncoder.Default.Encode()` for output encoding, and `IDataProtector` or `AesGcm` for cryptographic operations
- **Docker Compose must target exact dependency versions**: The `docker-compose.yml` must pin PostgreSQL to version 16 and .NET to version 9.0 as specified in the repository's project files
- **Parallel scan execution**: Documentation must describe ZAP and Nuclei running in parallel (not sequentially) per the user's requirement in Step 4
- **Deduplication across scanners**: The finding analysis documentation must define a cross-scanner deduplication algorithm that correlates findings from ZAP and Nuclei by CWE ID, affected URL, and parameter to avoid duplicate reporting

## 0.11 References

### 0.11.1 Repository Files and Folders Searched

**Root-Level Files**:
- `README.md` — Project overview, links to related repos, Apache 2.0 license
- `global.json` — SDK version (commented out, no pinning active)
- `docker-compose.yml` — Not found (does not exist in repository)
- `Dockerfile` — Not found (does not exist in repository root)

**Documentation Directories**:
- `docs/` — Root documentation folder (single child: `docs/developer/`)
- `docs/developer/` — 14 topical documentation sections with folder.json manifests
- `docs/developer/introduction/` — Getting started guide (references .NET Core 2.1, outdated)
- `docs/developer/web-api/` — REST API conventions (overview.md, response.md)
- `docs/developer/server-api/` — C# server-side API reference (overview.md)
- `docs/developer/users-and-roles/` — User management, role system (overview.md, users.md, roles.md)
- `docs/developer/entities/` — Entity modeling reference (overview.md, fields.md, relations.md)
- `docs/developer/hooks/` — API hooks, page hooks, render hooks (6 pages)
- `docs/developer/applications/` — Application management concepts
- `docs/developer/background-jobs/` — Job scheduling documentation
- `docs/developer/components/` — UI component reference
- `docs/developer/data-sources/` — Data source configuration
- `docs/developer/pages/` — Page system documentation
- `docs/developer/plugins/` — Plugin architecture
- `docs/developer/system-log/` — Logging documentation
- `docs/developer/tag-helpers/` — Razor tag helper reference

**Source Code — Controllers**:
- `WebVella.Erp.Web/Controllers/WebApiController.cs` (4313 lines) — Primary REST API controller with 60+ endpoints
- `WebVella.Erp.Web/Controllers/AdminController.cs` — Admin functionality
- `WebVella.Erp.Web/Controllers/ApiControllerBase.cs` — Base controller class
- `WebVella.Erp.Web/Controllers/ProjectController.cs` — Project management

**Source Code — Security**:
- `WebVella.Erp.Web/Security/AuthCache.cs` — In-process GUID cache, 5-min TTL
- `WebVella.Erp.Web/Security/AuthToken.cs` — DES-encrypted token management
- `WebVella.Erp.Web/Security/AuthorizeAttribute.cs` — ActionFilterAttribute (no role checks)
- `WebVella.Erp.Web/Security/ErpIdentity.cs` — ClaimsIdentity wrapper
- `WebVella.Erp.Web/Security/ErpPrincipal.cs` — ClaimsPrincipal wrapper
- `WebVella.Erp.Web/Security/HttpForbiddenResult.cs` — 403 result
- `WebVella.Erp.Web/Security/HttpUnauthorizedResult.cs` — 401 result
- `WebVella.Erp.Web/Security/WebSecurityUtil.cs` — Central security orchestrator
- `WebVella.Erp/Api/Security/QuerySecurity.cs` — Query-level security
- `WebVella.Erp/Api/Security/SecurityContext.cs` — AsyncLocal security scope
- `WebVella.Erp/Api/SecurityManager.cs` — User CRUD with EQL queries

**Source Code — Cryptography and Passwords**:
- `WebVella.Erp/Api/CryptoUtility.cs` — DES encryption utilities
- `WebVella.Erp/Api/PasswordUtil.cs` — MD5 password hashing (unsalted)

**Source Code — Configuration**:
- `WebVella.Erp.Site/Startup.cs` — CORS, authentication, authorization configuration
- `WebVella.Erp.Site/Config.json` — Hardcoded secrets (JWT key, encryption key, DB credentials)

**Source Code — Project Files**:
- `WebVella.Erp/WebVella.Erp.csproj` — Core library, net9.0, version 1.7.4
- `WebVella.Erp.Web/WebVella.Erp.Web.csproj` — Web layer, net9.0, version 1.7.5
- `WebVella.Erp.Site/WebVella.Erp.Site.csproj` — Host site, net9.0, InProcess hosting

**Source Code — File Upload Handlers**:
- `WebVella.Erp.Web/Controllers/WebApiController.cs` (lines 3320-3500) — UploadFile, MoveFile, DeleteFile
- `WebVella.Erp.Web/Components/PcFieldHtml/PcFieldHtml.cs` — HTML field upload
- `WebVella.Erp.Web/Components/PcFieldMultiFileUpload/PcFieldMultiFileUpload.cs` — Multi-file upload
- `WebVella.Erp.Web/Pages/ImageFinder/ImageFinder.cshtml.cs` — Image finder upload
- `WebVella.Erp/Database/DbFile.cs` — Database file storage

**Tech Spec Sections Retrieved**:
- Section 6.4 Security Architecture — Hybrid authentication, RBAC, cryptographic weaknesses, security zones
- Section 1.1 Executive Summary — WebVella ERP v1.7.4 overview, current initiative

### 0.11.2 Attachments

No attachments were provided by the user.

### 0.11.3 External References

- **WebVella ERP GitHub Repository**: `https://github.com/WebVella/WebVella-ERP` — Source repository to be cloned
- **WebVella ERP Seed Repository**: `https://github.com/WebVella/WebVella-ERP-Seed.git` — Referenced in getting-started.md for seed data
- **OWASP ZAP Documentation**: `https://www.zaproxy.org/` — ZAP 2.17.0 stable release, Docker images at `ghcr.io/zaproxy/zaproxy:stable`
- **OWASP ZAP GitHub Releases**: `https://github.com/zaproxy/zaproxy/releases` — Release history and checksums
- **Nuclei GitHub Repository**: `https://github.com/projectdiscovery/nuclei` — Nuclei v3.7.1 latest release
- **Nuclei Templates Repository**: `https://github.com/projectdiscovery/nuclei-templates` — v10.3.9, 9,821+ community templates
- **Nuclei Documentation**: `https://docs.projectdiscovery.io/tools/nuclei/install` — Installation and usage guide
- **OWASP ZAP Docker Images**: `ghcr.io/zaproxy/zaproxy:stable` — Stable Docker image for scanning
- **Nuclei Docker Images**: `projectdiscovery/nuclei:latest` — Docker image for template-based scanning
- **.NET 9.0 Docker Images**: `mcr.microsoft.com/dotnet/sdk:9.0`, `mcr.microsoft.com/dotnet/aspnet:9.0` — Microsoft official images
- **PostgreSQL Docker Images**: `postgres:16` — Official PostgreSQL 16 image

