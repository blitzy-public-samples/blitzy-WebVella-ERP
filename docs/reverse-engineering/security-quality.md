# Security & Quality Assessment — WebVella ERP

> **Deliverable 6 of 7** · Reverse-Engineering Documentation Suite
> **Generated (UTC):** 2026-06-05 23:35 UTC
> **Analysis mode:** Read-only static inspection of the `WebVella.ERP3.sln` solution. **No production code, configuration, or schema artifact was modified.** Any static measurement performed was transient and left the source tree unchanged.
> **Companion deliverables:** [`code-inventory.md`](./code-inventory.md) · [`architecture.md`](./architecture.md) · [`database-schema.md`](./database-schema.md) · [`functional-overview.md`](./functional-overview.md) · [`business-rules.md`](./business-rules.md) · [`modernization-roadmap.md`](./modernization-roadmap.md)
> **Suite index:** `README.md` _(forthcoming)_

---

## Executive Summary

This document is a **factual security-posture and code-quality assessment** of WebVella ERP, an **open-source, metadata-driven ERP platform** built on **ASP.NET Core 9** over **PostgreSQL 16** (core library `WebVella.Erp` v1.7.4, Apache-2.0). It reports the system **as built** — every finding cites a real file, and where the agent task and prior deliverables fix a line number, that line was re-verified by reading the file. Forward-looking remediation belongs to `modernization-roadmap.md`; this document confines itself to *what exists*.

The headline observations are:

- **One Critical surface.** A runtime code-compilation endpoint — `POST api/v3.0/datasource/code-compile` in `WebVella.Erp.Web/Controllers/WebApiController.cs` (line 494) — compiles and loads **arbitrary user-supplied C#** through **CS-Script** + **Roslyn** (`WebVella.Erp.Web/Services/CodeEvalService.cs`). It is gated by a class-level `[Authorize]` (line 36), so it is authenticated-only, but it remains a remote-code-execution (RCE) class surface that warrants the strictest authorization.
- **Two High-severity classes.** (1) **Insecure-deserialization** configuration — Newtonsoft `TypeNameHandling.All` / `.Auto` is used in background-job and relation serialization (`WebVella.Erp/Jobs/JobDataService.cs`, `WebVella.Erp/Database/DbRelationRepository.cs`). (2) **Plaintext secrets** — every host site's `Config.json` stores a database connection string, an encryption key, and (in two sites) a weak hardcoded JWT signing key in cleartext.
- **A genuine data-layer strength, with two residuals.** The custom Npgsql data layer **parameterizes ordinary values** with `NpgsqlParameter` throughout `WebVella.Erp/Database/**` (50 parameter constructions), which is the correct defense against SQL injection. Two residuals remain: **SQL identifiers** (table/column names sourced from entity metadata) are composed via string interpolation, and the **full-text-search language literal** — `query.FtsLanguage`, an externally-bindable query-model property — is concatenated **unparameterized** into the SQL at `WebVella.Erp/Database/DbRecordRepository.cs:1503,1511`.
- **A pervasive code-hygiene signal.** The entire `WebVella.Erp.Web/Security/**` folder consists almost entirely of **commented-out** legacy authentication/authorization code (for example, `WebSecurityUtil.cs` is **~193 of 232** lines commented).
- **Dependency hygiene is mostly current, with two real findings.** Active NuGet packages are pinned to current versions, but **two projects target the out-of-support `net7.0` runtime**, and the active imaging dependency (`System.Drawing.Common`) is **Windows-only since .NET 6**. Several legacy package references (the ASP.NET Core **2.2.0** packages and the **SixLabors.ImageSharp** packages) are **commented out**, i.e. *not* active dependencies — reported here as a code-hygiene observation rather than as live end-of-life dependencies.
- **Concentrated complexity and no automated tests.** Complexity is concentrated in a handful of very large files, anchored by the **4,313-line** monolithic `WebApiController.cs`. The solution contains **no automated test project** of any kind, which is itself a quality risk.

The findings below feed directly into the phased plan in `modernization-roadmap.md`.

---

## Table of Contents

1. [Methodology, Scope & Severity Legend](#1-methodology-scope--severity-legend)
2. [Findings at a Glance](#2-findings-at-a-glance)
3. [Vulnerability Findings](#3-vulnerability-findings)
4. [Dependency & CVE Audit](#4-dependency--cve-audit)
5. [Code Quality Metrics](#5-code-quality-metrics)
6. [Compliance Posture](#6-compliance-posture)
7. [Four Corrections Honored](#7-four-corrections-honored)
8. [Cross-Document Consistency](#8-cross-document-consistency)
9. [Source Citation Index](#9-source-citation-index)

---

## 1. Methodology, Scope & Severity Legend

### 1.1 How findings were derived

All findings in this report were produced by **read-only static inspection** of the repository source tree (C#, Razor, JavaScript, JSON configuration, and `.csproj` manifests), cross-referenced against the shared module taxonomy and file paths established in [`code-inventory.md`](./code-inventory.md). No code was executed against a live database, no production file was modified, and no dependency was added, removed, or upgraded. Where complexity is quantified, it is derived from a **deterministic heuristic** (physical lines of code plus decision-point counting), described in [§5.1](#51-metric-method); no analyzer package was permanently added to the repository.

### 1.2 Code-metric thresholds

For the quality assessment in [§5](#5-code-quality-metrics), the following industry-standard thresholds are applied qualitatively (no per-method tool report is committed):

| Metric | Band | Interpretation |
|--------|------|----------------|
| **Cyclomatic Complexity (CC)** | ≤ 10 | Acceptable |
| | > 10 | **Watch** — review for simplification |
| | > 15 | **High** — hard to maintain/test |
| | > 30 | **Split** — strong candidate for decomposition |
| **Maintainability Index (MI)** | 20–100 | Good |
| | 10–19 | Moderate |
| | 0–9 | Low |

The Maintainability Index is the conventional composite of Halstead Volume, Cyclomatic Complexity, and lines of code (the model surfaced by the .NET / Roslyn code-quality analyzers and equivalent third-party tooling). CC bands below are reported as **bands**, not as precise tool output, and are labelled as heuristic estimates throughout.

### 1.3 Severity legend (qualitative)

Severities are **qualitative** risk ratings only; they carry no time or effort estimate.

| Severity | Meaning |
|----------|---------|
| 🟥 **Critical** | Could lead to remote code execution or full compromise; warrants the strictest controls. |
| 🟧 **High** | A serious weakness (e.g., insecure deserialization, cleartext secrets) that should be addressed deliberately. |
| 🟨 **Medium** | A misconfiguration or weakening of defense-in-depth. |
| 🟦 **Low / Info** | A hygiene or hardening observation with limited direct exploitability. |
| 🟩 **Strength** | A positive control worth recording so it is preserved during modernization. |

### 1.4 CVE-audit method & disclaimer

The dependency audit in [§4](#4-dependency--cve-audit) enumerates the **exact pinned versions** read from the `.csproj` manifests and discusses class-of-risk for security-relevant packages. **No live vulnerability-database lookup (NVD / GitHub Advisory) was performed** in this read-only environment. Consequently, this report **does not assert specific CVE identifiers**; it states the audit method and flags the packages whose *class* or *support status* warrants a live `dotnet list package --vulnerable` / advisory cross-check during modernization. This is an explicit, deliberate limitation rather than an omission.

---

## 2. Findings at a Glance

| ID | Severity | Finding | Primary location |
|----|----------|---------|------------------|
| `SEC-001` | 🟥 Critical | Runtime compilation of user-supplied C# (RCE-class surface) | `WebVella.Erp.Web/Controllers/WebApiController.cs:494`; `WebVella.Erp.Web/Services/CodeEvalService.cs:45,57` |
| `SEC-002` | 🟧 High | Insecure deserialization via Newtonsoft `TypeNameHandling.All`/`.Auto` | `WebVella.Erp/Jobs/JobDataService.cs:27,96,297,346`; `WebVella.Erp/Database/DbRelationRepository.cs:47,128,173` |
| `SEC-003` | 🟧 High | Plaintext secrets in `Config.json` (conn string, encryption key, JWT key) | `WebVella.Erp.Site/Config.json:3,4,24` (pattern across all 7 sites) |
| `SEC-004` | 🟨 Medium | Overly permissive default CORS policy (`AllowAnyOrigin`) | `WebVella.Erp.Site/Startup.cs:61–63` |
| `SEC-005` | 🟦 Low/Info | Global Npgsql legacy-timestamp behavior switch enabled | `WebVella.Erp.Site/Startup.cs:40` (+ 6 other sites) |
| `SEC-006` | 🟩 Strength (+ residual) | Parameterized SQL is the norm; identifiers and the FTS-language literal (`query.FtsLanguage`) are string-composed | `WebVella.Erp/Database/**` (50 `NpgsqlParameter`); `DbRecordRepository.cs:1503,1511` |
| `SEC-007` | 🟦 Low/Info | Substantial commented-out legacy security code | `WebVella.Erp.Web/Security/**` (8 files) |
| `DEP-001` | 🟧 High | Two projects target out-of-support `net7.0` | `WebVella.Erp.WebAssembly/Server`, `/Shared` |
| `DEP-002` | 🟦 Low/Info | Commented-out legacy package references (2.2.0, SixLabors) | `*.csproj` (verified comment lines) |
| `DEP-003` | 🟨 Medium | `System.Drawing.Common` is Windows-only since .NET 6 (portability) | `WebVella.Erp/WebVella.Erp.csproj:63` |
| `QA-001` | 🟧 High | No automated tests anywhere in the solution | solution-wide |
| `QA-002` | 🟨 Medium | Extreme-size files concentrate complexity | `WebApiController.cs` (4,313 LOC) + hotspots |

> The `SEC-`, `DEP-`, and `QA-` identifiers are local to this document and are referenced by `modernization-roadmap.md`.

---

## 3. Vulnerability Findings

### 3.1 `SEC-001` — Runtime compilation of user-supplied C# 🟥 Critical

**Location:** `WebVella.Erp.Web/Controllers/WebApiController.cs:494` → `WebVella.Erp.Web/Services/CodeEvalService.cs:45,57`

The Web API exposes an endpoint that compiles and loads **arbitrary C# source supplied in the request body**:

```text
WebVella.Erp.Web/Controllers/WebApiController.cs
494:  [Route("api/v3.0/datasource/code-compile")]
495:  [HttpPost]
496:  public ActionResult DataSourceAction([FromBody] DataSourceCodeTestModel model)
        ...
500:      CodeEvalService.Compile(model.CsCode);
```

The `CsCode` string flows into `CodeEvalService`, which uses **CS-Script** (`using CSScriptLib;`, line 1) backed by **Roslyn** to evaluate it at runtime:

```text
WebVella.Erp.Web/Services/CodeEvalService.cs
44:   CSScript.EvaluatorConfig.ReferenceDomainAssemblies = true;
45:   ICodeVariable scriptObject = CSScript.Evaluator.LoadCode<ICodeVariable>(sourceCode);
        ...
57:   internal static void Compile(string sourceCode)
```

`LoadCode<ICodeVariable>` compiles the supplied text into a loadable assembly with **domain assemblies referenced**, and `Evaluate(...)` (line 51) executes it. Compiling and loading caller-controlled code is, by definition, a **remote-code-execution-class** capability: a successful caller can run arbitrary code inside the application's process and trust boundary.

**Mitigating control (verified, factual).** The controller carries a **class-level `[Authorize]` attribute** at `WebVella.Erp.Web/Controllers/WebApiController.cs:36`, so the endpoint is **authenticated-only** — it is not anonymously reachable through this controller. The control is *authentication*, not fine-grained authorization: the attribute as written does not, on its own, restrict the endpoint to a narrow administrative/developer role.

**Why it still rates Critical.** Authenticated RCE remains RCE. The blast radius is the whole host process; the feature exists to let the data-source designer test code, but the compile path itself imposes no language/assembly allow-listing visible at this layer. The roadmap should treat this endpoint as requiring the **strictest** authorization (dedicated developer/admin policy), and ideally sandboxing or removal in production builds.

> **Backed by:** `Microsoft.CodeAnalysis.CSharp` 4.14.0 and `CS-Script` 4.11.2, both declared in `WebVella.Erp.Web/WebVella.Erp.Web.csproj` (lines 130 and 132).

---

### 3.2 `SEC-002` — Insecure deserialization via Newtonsoft `TypeNameHandling` 🟧 High

**Location:** `WebVella.Erp/Jobs/JobDataService.cs:27,96,297,346`; `WebVella.Erp/Database/DbRelationRepository.cs:47,128,173`; `WebVella.Erp.Plugins.SDK/Services/CodeGenService.cs:958,990`

Background-job payload (de)serialization configures Newtonsoft.Json with **`TypeNameHandling.All`**, which embeds and honors **CLR type names** in the JSON:

```text
WebVella.Erp/Jobs/JobDataService.cs
27:   JsonSerializerSettings settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
96:   JsonSerializerSettings settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
297:  JsonSerializerSettings settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
346:  JsonSerializerSettings settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
```

The relation repository uses the slightly narrower **`TypeNameHandling.Auto`** at three sites (the agent task identified two; a **third** at line 173 was found during verification and is reported here for accuracy):

```text
WebVella.Erp/Database/DbRelationRepository.cs
47:   ... new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
128:  ... new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
173:  ... new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
```

The SDK code generator also sets `TypeNameHandling.Auto` at `WebVella.Erp.Plugins.SDK/Services/CodeGenService.cs:958,990` (and emits `TypeNameHandling.All` inside generated sample-code string templates at lines 9197 and 9220). Solution-wide, `TypeNameHandling` appears in **20** places.

**The risk.** `TypeNameHandling.All`/`.Auto` is the canonical Newtonsoft **gadget-chain / polymorphic-deserialization** weakness: when untrusted JSON can specify the `$type` to instantiate, an attacker may coerce the deserializer into constructing dangerous types, enabling property-setter-driven side effects up to code execution. The job payloads here originate inside the application (job scheduling), which lowers exposure, but the pattern is a recognized High-severity sink and should be constrained with a strict `SerializationBinder` / type allow-list wherever the input is not fully trusted.

---

### 3.3 `SEC-003` — Plaintext secrets in host configuration 🟧 High

**Location:** `WebVella.Erp.Site/Config.json:3,4,24` — pattern repeats across the host sites (with the nuance below)

Each runnable host site ships a `Config.json` containing secrets in **cleartext**:

```text
WebVella.Erp.Site/Config.json
3:   "ConnectionString": "Server=…;Port=…;User Id=…;Password=…;Database=erp3;…"   (host, port, and credentials redacted here)
4:   "EncryptionKey": "BC93…7658"   (a hardcoded 64-character hex literal — value redacted here)
24:  "Key": "…"   (a low-entropy JWT signing key — a short phrase repeated three times; value redacted here)
```

- **Line 3 — database connection string** with an inline `User Id` / `Password` (low-entropy demo credentials, **redacted here**) and a hardcoded LAN host/port (**redacted here**).
- **Line 4 — `EncryptionKey`**, a hardcoded 64-character hexadecimal literal used by the platform's encryption helper. (The full value is intentionally **redacted** in this report; reproducing it would re-expose it. It is present verbatim in the file.)
- **Line 24 — `Jwt:Key`**, a **weak, low-entropy** signing key consisting of a short phrase repeated three times.

**Scope (verified, factual correction).** The **connection string (line 3)** and the **`EncryptionKey` (line 4)** are present in **all seven** host sites' `Config.json`, and the `EncryptionKey` hex value is **identical** across them. However, a **`Jwt` block is present in only two sites** — `WebVella.Erp.Site/Config.json:24` and `WebVella.Erp.Site.Project/Config.json:20`; the other five sites (`Crm`, `Mail`, `MicrosoftCDM`, `Next`, `Sdk`) contain **no `Jwt:Key`**. This report deliberately states the narrower, verified scope rather than asserting the JWT key repeats across all seven.

**The risk.** Cleartext secrets in a source-controlled file are exposed to anyone with repository or deployment-artifact access; a reused/identical encryption key and a guessable JWT signing key undermine token integrity and at-rest encryption. The `Settings.DevelopmentMode` flag is `"true"` and these are evidently sample/development values, but the **pattern** (secrets in `Config.json`) is what production deployments must not inherit. Remediation — externalized secret management (environment variables, a secrets vault, user-secrets in development) — belongs to `modernization-roadmap.md`.

---

### 3.4 `SEC-004` — Overly permissive default CORS policy 🟨 Medium

**Location:** `WebVella.Erp.Site/Startup.cs:61–63` (within the `AddCors` block, lines 59–64)

The reference host registers a **default CORS policy that allows any origin, method, and header**:

```text
WebVella.Erp.Site/Startup.cs
59:   services.AddCors(options =>
60:   {
61:       options.AddDefaultPolicy(policy =>
62:           policy.AllowAnyOrigin()
63:               .AllowAnyMethod()
64:               .AllowAnyHeader());
```

Notably, a **more restrictive** named policy — `WithOrigins("http://localhost:3333", …).AllowAnyMethod().AllowCredentials()` — exists in the file but is **commented out** at lines 53–57, indicating the permissive default replaced an origin-scoped policy. `AllowAnyOrigin()` cannot be combined with credentials, which limits one attack class, but a wildcard CORS policy still weakens cross-origin defense-in-depth and should be scoped to known front-end origins in production.

---

### 3.5 `SEC-005` — Global Npgsql legacy-timestamp behavior switch 🟦 Low / Info

**Location:** `WebVella.Erp.Site/Startup.cs:40` and the analogous line in every other site

Every host enables a legacy Npgsql behavior switch at process start:

```text
WebVella.Erp.Site/Startup.cs
39:   //legacy until we fix system tables
40:   AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
```

The in-code comment on line 39 — *"legacy until we fix system tables"* — documents this as a deliberate, acknowledged stopgap. The same switch is set in all seven sites, at the lines below (each verified):

| Site | `Startup.cs` line |
|------|------------------:|
| `WebVella.Erp.Site` | 40 |
| `WebVella.Erp.Site.Crm` | 27 |
| `WebVella.Erp.Site.Mail` | 27 |
| `WebVella.Erp.Site.MicrosoftCDM` | 29 |
| `WebVella.Erp.Site.Next` | 30 |
| `WebVella.Erp.Site.Project` | 34 |
| `WebVella.Erp.Site.Sdk` | 27 |

This is **not a security vulnerability**; it is a **technical-debt / correctness** signal (timestamp semantics tied to an older Npgsql behavior) recorded here because it is a self-identified legacy workaround that the roadmap should retire alongside the system-table schema cleanup.

---

### 3.6 `SEC-006` — SQL-injection posture: parameterized values (strength) with identifier and FTS-language-literal interpolation (residual) 🟩 Strength / residual

**Location:** `WebVella.Erp/Database/**`

The custom Npgsql data layer **parameterizes user values** with `NpgsqlParameter`, which is the correct and effective defense against SQL injection. Parameter constructions are distributed across the data layer:

| File | `new NpgsqlParameter(...)` count |
|------|---------------------------------:|
| `WebVella.Erp/Database/DbDataSourceRepository.cs` | 23 |
| `WebVella.Erp/Database/DbFileRepository.cs` | 18 |
| `WebVella.Erp/Database/DbRelationRepository.cs` | 6 |
| `WebVella.Erp/Database/DbRecordRepository.cs` | 2 |
| `WebVella.Erp/Database/DbConnection.cs` | 1 |
| **Total (Database/)** | **50** |

Value binding consistently uses `@`-prefixed placeholders (e.g., `WebVella.Erp/Database/DbRecordRepository.cs:215` — `con.CreateCommand("SELECT * FROM {table} WHERE id=@id;")` binds `@id` as a parameter). **This is a real strength and should be preserved.**

**Residual areas to review (factual).** While ordinary *values* are parameterized, two categories of SQL text are still composed by string interpolation/concatenation. **(a) SQL identifiers** (table and column names) are composed from entity metadata, for example:

```text
WebVella.Erp/Database/DbRecordRepository.cs
215:  ... con.CreateCommand($"SELECT * FROM {tableName} WHERE id=@id;")
276:  string sql = $"SELECT COUNT( {tableName}.id ) FROM {tableName} ";
289–292:  sql = sql + " WHERE " + whereSql;     // WHERE fragment concatenated
664–679:  sql.AppendLine("SELECT " + columnNames + " FROM " + tableName);
```

These identifiers derive from the **dynamic entity meta-model** (entity/field definitions), **not** directly from end-user request bodies, so the practical injection exposure for identifiers is low. It is nonetheless flagged as a **residual review item**: any path where an identifier could be influenced by external input should validate it against the known-entity catalog (allow-list) rather than rely on interpolation.

**(b) Full-text-search (FTS) language literal — a residual *value* concatenation.** Correcting an earlier, narrower framing: one **value-like SQL literal *is* concatenated unparameterized**. In the record query builder's full-text-search branch, `query.FtsLanguage` is interpolated directly into the SQL as a quoted text-search-configuration literal:

```text
WebVella.Erp/Database/DbRecordRepository.cs
1503:  sql = sql + " to_tsvector( '" + query.FtsLanguage + "' , " + completeFieldName + ") @@ to_tsquery( '" + query.FtsLanguage + "' ," + paramName + ") ";
1511:  sql = sql + " to_tsvector( '" + query.FtsLanguage + "' , " + completeFieldName + ") @@ plainto_tsquery( '" + query.FtsLanguage + "' ," + paramName + ") ";
```

Unlike the identifiers above, **`FtsLanguage` is a public, externally-bindable query-model property** — `QueryObject.FtsLanguage` at `WebVella.Erp/Api/Models/QueryObject.cs:23` (JSON-bound as `ftsLanguage`). The adjacent guards at `DbRecordRepository.cs:1500,1508` only test for null/whitespace (falling back to the safe `'simple'` configuration); they do **not** validate the supplied value, so a caller that can set `ftsLanguage` controls a string embedded verbatim in the executed SQL — a single quote in that value would break out of the literal. The `@`-prefixed *search term* on the same lines remains correctly parameterized, but the language literal is not; this is the one identified path where an unparameterized **value** reaches the SQL text. **Recommendation:** validate `FtsLanguage` against an **allow-list** of known PostgreSQL text-search configurations, or pass it as a bound parameter, rather than interpolating it.

---

### 3.7 `SEC-007` — Substantial commented-out legacy security code 🟦 Low / Info

**Location:** `WebVella.Erp.Web/Security/**` (8 files)

The web security folder is **overwhelmingly commented out** — it contains a previous, hand-rolled authentication/authorization implementation that has been disabled (the live system instead uses the standard ASP.NET Core `[Authorize]` pipeline with the `JWT_OR_COOKIE` scheme; see [`architecture.md`](./architecture.md) §4). Measured comment density per file:

| File | Commented lines / total | Disabled construct |
|------|------------------------:|--------------------|
| `WebVella.Erp.Web/Security/WebSecurityUtil.cs` | ~193 / 232 | Whole `WebSecurityUtil` class: `Login`, `LoginWithToken`, `CreateIdentity`, token encryption, cookie handling |
| `WebVella.Erp.Web/Security/AuthorizeAttribute.cs` | ~120 / 146 | Whole custom `AuthorizeAttribute : ActionFilterAttribute` (`OnActionExecuting`, `IsAuthenticated`) |
| `WebVella.Erp.Web/Security/AuthToken.cs` | ~116 / 146 | Token create/encrypt/decrypt helpers |
| `WebVella.Erp.Web/Security/AuthCache.cs` | ~54 / 61 | Auth result caching |
| `WebVella.Erp.Web/Security/ErpIdentity.cs` | ~23 / 28 | Custom identity type |
| `WebVella.Erp.Web/Security/HttpForbiddenResult.cs` | ~16 / 19 | 403 result helper |
| `WebVella.Erp.Web/Security/HttpUnauthorizedResult.cs` | ~15 / 19 | 401 result helper |
| `WebVella.Erp.Web/Security/ErpPrincipal.cs` | ~10 / 12 | Custom principal type |

> **Counting basis (approximate).** Counts are lines whose trimmed text begins with `//` against total lines, and are marked approximate (`~`) because exact totals vary by ±1 depending on whether a file ends with a trailing newline — three of these files (`AuthorizeAttribute.cs`, `AuthToken.cs`, `AuthCache.cs`) have no final newline, so editor-style line counts read one higher than `wc -l`.

This is reported as a **code-hygiene** finding, not an exploitable vulnerability: dead security code increases cognitive load, can mislead future maintainers about which controls are actually in force, and risks accidental reactivation. The roadmap should remove or revive these files deliberately.

---

## 4. Dependency & CVE Audit

### 4.1 Active third-party packages (pinned versions, read from `.csproj`)

The table below lists the principal **active** (non-commented) third-party dependencies and the **exact** versions pinned in the manifests. These versions are **inputs** to this audit — none was changed by this task. Security-relevant notes are called out in the right-hand column.

| Package | Version | Notes (security / quality relevance) |
|---------|---------|--------------------------------------|
| Npgsql | 9.0.4 | PostgreSQL ADO.NET driver underpinning the custom data layer. |
| Newtonsoft.Json | 13.0.4 | JSON serialization; see `SEC-002` re: `TypeNameHandling`. |
| AutoMapper | 14.0.0 | DTO ↔ entity mapping. |
| Irony.NetCore | 1.1.11 | EQL grammar/parser. |
| Ical.Net | 4.3.1 | Recurrence/calendar. |
| CsvHelper | 33.1.0 | CSV read/write. |
| Storage.Net | 9.3.0 | Blob/file storage abstraction. |
| MimeMapping | 3.1.0 | MIME-type lookup. |
| **System.Drawing.Common** | **9.0.10** | **Windows-only since .NET 6** — see `DEP-003`. |
| Microsoft.Extensions.* | 9.0.10 | Caching, configuration, hosting, logging, DI (9 references). |
| **Microsoft.CodeAnalysis.CSharp** (+ Scripting/Workspaces/Common) | **4.14.0** | Roslyn — **runtime code compilation**; see `SEC-001`. |
| **CS-Script** | **4.11.2** | Runtime C# scripting; see `SEC-001`. |
| HtmlAgilityPack | 1.12.4 | HTML parsing. |
| Microsoft.AspNetCore.Mvc.NewtonsoftJson | 9.0.10 | JSON.NET MVC formatter. |
| Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation | 9.0.10 | Runtime Razor view compilation. |
| Wangkanai.Detection | 8.20.0 | Device/browser detection. |
| WebVella.TagHelpers | 1.7.2 | Proprietary ERP UI tag-helper library (ships Bootstrap/jQuery assets). |
| System.IdentityModel.Tokens.Jwt | 8.14.0 | JWT token handling. |
| Microsoft.AspNetCore.Authentication.JwtBearer | 9.0.10 | JWT bearer authentication. |
| MailKit | 4.14.1 | Email (Mail plugin). |
| Microsoft.AspNetCore.Components.WebAssembly | 9.0.10 | Blazor WebAssembly runtime (Client project, `net9.0`). |
| Blazored.LocalStorage | 4.5.0 | Blazor local-storage helper. |
| Microsoft.Web.LibraryManager.Build | 3.0.71 | LibMan client-library restore. |

> **Runtime baseline.** 18 of the 20 projects target `net9.0`; the `global.json` SDK version is **commented out** (`//"version": "7.0.103"` at `global.json:3`), so the build uses the latest installed SDK. (.NET 9 is itself a **Standard-Term-Support** release rather than an LTS — a roadmap cadence consideration, not a vulnerability.)

### 4.2 `DEP-001` — Two projects target the out-of-support `net7.0` runtime 🟧 High

Two Blazor WebAssembly projects target **`net7.0`**, which reached **end of support in May 2024**:

```text
WebVella.Erp.WebAssembly/Server/WebVella.Erp.WebAssembly.Server.csproj
4:    <TargetFramework>net7.0</TargetFramework>
10:   <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.Server" Version="7.0.13" />

WebVella.Erp.WebAssembly/Shared/WebVella.Erp.WebAssembly.Shared.csproj
4:    <TargetFramework>net7.0</TargetFramework>
```

Running on an out-of-support runtime means **no further security patches** for the framework. Per the setup analysis these two projects are **orphaned** (not registered in `WebVella.ERP3.sln`, which builds the 18 `net9.0` projects), so they do not ship in the main solution build — but they remain in the repository on an unsupported target and are a genuine dependency-hygiene finding. The `Microsoft.AspNetCore.Components.WebAssembly.Server` 7.0.13 reference is likewise on the 7.x line.

### 4.3 `DEP-002` — Commented-out legacy package references 🟦 Low / Info (fidelity)

> **Critical fidelity nuance — verified.** Certain legacy package references that might at first glance look like active end-of-life dependencies are in fact **commented out** in the `.csproj` files and are therefore **not active dependencies**. They are reported here strictly as a **code-hygiene observation**.

**ASP.NET Core 2.2.0 references — all commented out.** Every occurrence of `Version="2.2.0"` in the solution is inside an XML comment. A re-grep excluding comment lines returns **zero active** 2.2.0 references. The four occurrences are:

```text
WebVella.Erp/WebVella.Erp.csproj
51:   <!--<PackageReference Include="Microsoft.AspNetCore.Http.Abstractions" Version="2.2.0" />-->

WebVella.Erp.Web/WebVella.Erp.Web.csproj
136:  <!--<PackageReference Include="Microsoft.AspNetCore.Mvc.ViewFeatures" Version="2.2.0" />-->
137:  <!--<PackageReference Include="Microsoft.AspNetCore.StaticFiles" Version="2.2.0" />-->

WebVella.Erp.Site/WebVella.Erp.Site.csproj
56:   <!--<PackageReference Include="Microsoft.AspNetCore.ResponseCompression" Version="2.2.0" />-->
```

Because they are commented out, **ASP.NET Core 2.2 is not an active dependency** of this solution; the functionality is instead provided by the shared framework (`Microsoft.AspNetCore.App` 9.0.x). Reporting these as live EOL dependencies would be inaccurate.

**SixLabors imaging references — also commented out.** The cross-platform imaging packages are present but commented out as a single block:

```text
WebVella.Erp.Web/WebVella.Erp.Web.csproj
139:  <!--<PackageReference Include="SixLabors.ImageSharp" Version="3.1.6" />
140:        <PackageReference Include="SixLabors.ImageSharp.Drawing" Version="2.1.5" />-->
```

The `<!--` opens on line 139 and the `-->` closes on line 140, so **both** `SixLabors.ImageSharp` and `SixLabors.ImageSharp.Drawing` are inactive. The practical consequence is recorded under `DEP-003`: the **active** imaging dependency is the Windows-only `System.Drawing.Common`, while the cross-platform replacement sits commented out.

### 4.4 `DEP-003` — `System.Drawing.Common` portability caveat 🟨 Medium

`System.Drawing.Common` 9.0.10 is an **active** dependency (`WebVella.Erp/WebVella.Erp.csproj:63`). Since **.NET 6**, `System.Drawing.Common` is **supported only on Windows**; calling its APIs on Linux/macOS throws `PlatformNotSupportedException`. Combined with the IIS-in-process deployment model documented in [`architecture.md`](./architecture.md), this is consistent with a **Windows-bound** runtime. It is a **portability** constraint (and an obstacle to the containerization/Linux-hosting options the roadmap will explore) rather than a direct vulnerability; the commented-out SixLabors packages (`DEP-002`) are the cross-platform path that has not been adopted.

### 4.5 CVE-audit method (explicit limitation)

As stated in [§1.4](#14-cve-audit-method--disclaimer), **no live advisory lookup was performed** in this environment, so **no specific CVE identifiers are asserted**. The recommended live audit during modernization is `dotnet list package --vulnerable --include-transitive`, cross-referenced against the GitHub Advisory Database / NVD. The highest-priority targets for that live audit, based on this static inspection, are: the **`net7.0` projects** (`DEP-001`, unsupported framework), **Newtonsoft.Json** usage with `TypeNameHandling` (`SEC-002`), and the **Roslyn / CS-Script** runtime-compilation stack (`SEC-001`). Active packages are otherwise pinned to current major versions, which is favorable.

---

## 5. Code Quality Metrics

### 5.1 Metric method

This section uses a **deterministic, read-only heuristic** that does not modify or instrument the build:

- **LOC** is the physical line count per file (`wc -l`-equivalent), measured over the in-scope tree excluding `bin/`, `obj/`, `.git/`, and `node_modules/`. The solution totals **~137,605 `.cs` lines** and **~17,929 `.cshtml` lines**, consistent with the baseline recorded in [`code-inventory.md`](./code-inventory.md).
- **Cyclomatic Complexity (CC)** is estimated by **decision-point counting** — summing branch/loop/case/catch keywords and short-circuit operators (`if`, `for`, `foreach`, `while`, `case`, `catch`, `&&`, `||`, `?:`) — and reported as a **band** against the thresholds in [§1.2](#12-code-metric-thresholds). These are **estimates**, not the output of a committed analyzer run; a precise measurement would use the .NET / Roslyn code-quality analyzers (CA1502 cyclomatic complexity, CA1505 maintainability index, CA1501 inheritance depth, CA1506 class coupling) or an equivalent CLI such as NDepend.

### 5.2 Complexity hotspots

Complexity is **highly concentrated** in a small number of very large files. The largest source files in the solution are:

| File | Module | ~LOC | CC band (heuristic) | MI band (heuristic) | Note |
|------|--------|-----:|---------------------|---------------------|------|
| `WebVella.Erp.Plugins.Next/NextPlugin.20190203.cs` | Next plugin | 11,502 | n/a (declarative) | n/a | Dated **patch/seed** file — largely declarative entity/field/data definitions; size ≫ branching. |
| `WebVella.Erp.Plugins.Project/ProjectPlugin.20190203.cs` | Project plugin | 11,035 | n/a (declarative) | n/a | Dated patch/seed file (declarative). |
| `WebVella.Erp.Plugins.SDK/Services/CodeGenService.cs` | SDK plugin | 9,321 | **> 30 (Split)** | Low | Code generator with large string-templating + branching. |
| `WebVella.Erp.Plugins.Mail/MailPlugin.20190215.cs` | Mail plugin | 5,499 | n/a (declarative) | n/a | Dated patch/seed file (declarative). |
| **`WebVella.Erp.Web/Controllers/WebApiController.cs`** | **Web** | **4,313** | **> 30 (Split)** | **Low** | **Monolithic API controller — anchor finding (see §5.3).** |
| `WebVella.Erp.Plugins.Next/NextPlugin.20190204.cs` | Next plugin | 2,663 | n/a (declarative) | n/a | Dated patch/seed file. |
| `WebVella.Erp/Utilities/Helpers.cs` | Core | 2,642 | > 30 (Split) | Low–Moderate | Broad utility grab-bag. |
| `WebVella.Erp.Web/Utils/PageUtils.cs` | Web | 2,317 | > 30 (Split) | Low–Moderate | Page-builder utilities. |
| `WebVella.Erp/Api/RecordManager.cs` | Core | 2,109 | > 30 (Split) | Low | Central record CRUD + hook pipeline. |
| `WebVella.Erp/Database/DbRecordRepository.cs` | Core | 2,097 | > 30 (Split) | Low | Dynamic SQL builder for records. |
| `WebVella.Erp.Web/Services/PageService.cs` | Web | 1,962 | > 15 (High) | Low–Moderate | Page composition service. |
| `WebVella.Erp/Api/EntityManager.cs` | Core | 1,873 | > 30 (Split) | Low | Entity/field meta-model management. |
| `WebVella.Erp/ERPService.cs` | Core | 1,472 | > 15 (High) | Low–Moderate | Bootstrap + embedded system DDL. |

> **Important nuance (factual).** The very largest files are the **date-versioned plugin patch files** (`*.YYYYMMDD.cs`), which are **mostly declarative seed/patch data** (entity, field, page, and sitemap definitions expressed as object initializers). Their high LOC reflects **volume, not branching complexity**, so a raw cyclomatic count over-states their maintainability risk; they are large but low-decision. By contrast, `WebApiController.cs`, `CodeGenService.cs`, `RecordManager.cs`, `EntityManager.cs`, and `DbRecordRepository.cs` are **genuinely complex procedural** units where the high CC band is meaningful.

### 5.3 Anchor: `WebApiController.cs` (4,313 LOC)

The single web API controller is the clearest maintainability hotspot. A heuristic decision-point scan of `WebVella.Erp.Web/Controllers/WebApiController.cs` finds approximately:

| Signal | Count |
|--------|------:|
| Public action/methods | ~70 |
| `if` statements | 365 |
| Loops (`for`/`foreach`/`while`) | 60 |
| `case` labels | 68 |
| `catch` blocks | 58 |
| Ternary (`?`) operators | 34 |
| Short-circuit operators (`&&` / `\|\|`) | 75 |

That is on the order of **600+ aggregate decision points** in one file — far above the **> 30 "split"** threshold at the file level, and a textbook **God class**: a single `[Authorize]`-gated class concentrating the entire HTTP API surface (records, relations, files, data sources, the `SEC-001` code-compile endpoint, and more) rather than per-resource controllers. Maintainability Index for a unit of this size and branching density falls into the **low** band. This file is the canonical input to the **modular-decomposition** recommendation in `modernization-roadmap.md`. (For architectural context on the monolithic API, see [`architecture.md`](./architecture.md).)

### 5.4 `QA-001` — No automated tests 🟧 High

The solution contains **no automated test project of any kind** — no xUnit, NUnit, MSTest, or `Microsoft.NET.Test.Sdk` reference exists in any `.csproj`, and there are no test directories. The setup analysis reached the same conclusion ("none exist in the repo … nothing to run"). For a codebase of ~155k combined `.cs`/`.cshtml` lines with the complexity hotspots above and an RCE-class endpoint, the **absence of any regression safety net** is a material quality risk: refactoring (including the decomposition this assessment recommends) cannot be undertaken safely without first establishing characterization tests. This is recorded as a High-severity **quality** finding.

---

## 6. Compliance Posture

This section summarizes the posture **qualitatively** against the dimensions common to baselines such as the **OWASP Application Security Verification Standard (ASVS)**. Each item is framed factually as either a control that exists or a **gap**; remediation sequencing belongs to `modernization-roadmap.md`.

### 6.1 Authentication & authorization

- **Exists:** A hybrid **`JWT_OR_COOKIE`** authentication scheme (`WebVella.Erp.Site/Startup.cs:90`) routes bearer-token requests to JWT validation and others to cookie auth (`AddJwtBearer` at line 102; `AddCookie` at line 93). The auth cookie sets **`HttpOnly = true`** (`WebVella.Erp.Site/Startup.cs:95`, cookie name `erp_auth_base` at line 96) — a positive control against script access to the cookie.
- **Exists:** Authorization is applied via the standard ASP.NET Core `[Authorize]` attribute, used in **37** places solution-wide, including the class-level gate on `WebApiController` (`SEC-001`).
- **Gap:** The JWT signing key is **weak and hardcoded** where present (`SEC-003`), undermining token integrity. Authorization on the high-risk code-compile endpoint is **coarse** (authentication-only), not scoped to a dedicated privileged role.

### 6.2 Secret management

- **Gap:** Secrets (DB credentials, encryption key, JWT key) are stored in **cleartext** in source-controlled `Config.json` (`SEC-003`), with an **identical** encryption key reused across sites. ASVS-style verification expects externalized secret storage and per-environment key separation — not present today.

### 6.3 Transport security

- **Gap:** The reference host (`WebVella.Erp.Site/Startup.cs`) configures **no `UseHttpsRedirection`, `UseHsts`, or `RequireHttps`** in its pipeline. Transport hardening, if any, is delegated to the hosting layer (IIS / reverse proxy) rather than enforced by the application. Explicit HTTPS redirection and HSTS are expected for an ASVS baseline.

### 6.4 Input handling & data access

- **Exists (strength):** Database **values are parameterized** end-to-end via `NpgsqlParameter` (`SEC-006`), satisfying the core injection-defense expectation for the data layer.
- **Gap / review:** SQL **identifiers** are interpolated from metadata, and the **full-text-search language literal** (`query.FtsLanguage`) is concatenated **unparameterized** into SQL at `DbRecordRepository.cs:1503,1511` (`SEC-006` residual); and the **deserialization** configuration (`TypeNameHandling.All/.Auto`, `SEC-002`) does not constrain types via a `SerializationBinder`. The **runtime code-compilation** endpoint (`SEC-001`) is the most significant input-handling concern.

### 6.5 Summary of gaps vs. an ASVS-style baseline

| Dimension | Status | Reference |
|-----------|--------|-----------|
| Authentication mechanism present | ✅ Exists (hybrid JWT/cookie, `HttpOnly`) | `SEC-003`, §6.1 |
| Strong, externalized signing/encryption keys | ❌ Gap (weak/hardcoded, cleartext) | `SEC-003` |
| Externalized secret management | ❌ Gap (secrets in `Config.json`) | `SEC-003` |
| Enforced transport security (HTTPS/HSTS) | ❌ Gap (not in app pipeline) | §6.3 |
| Injection defense — values | ✅ Strength (parameterized) | `SEC-006` |
| Injection defense — identifiers | ⚠️ Review (interpolated from metadata) | `SEC-006` |
| Injection defense — FTS language literal | ⚠️ Review (`query.FtsLanguage` interpolated, externally bindable) | `SEC-006` |
| Safe deserialization | ❌ Gap (`TypeNameHandling` unconstrained) | `SEC-002` |
| Dangerous-capability control (runtime compile) | ⚠️ Review (authn-only gate on RCE surface) | `SEC-001` |
| Restrictive CORS | ⚠️ Review (`AllowAnyOrigin` default) | `SEC-004` |
| Supported runtime/dependencies | ⚠️ Review (`net7.0` projects) | `DEP-001` |
| Automated test coverage / regression safety | ❌ Gap (no tests) | `QA-001` |

> These rows are a **factual gap inventory**, not a formal certification result. They are the direct inputs to the security-hardening track of `modernization-roadmap.md`.

---

## 7. Four Corrections Honored

This assessment honors the four system-reality corrections that govern the entire suite, so its findings stay accurate:

1. **Custom Npgsql data layer, not Entity Framework Core.** The SQL-injection posture (`SEC-006`) is assessed against hand-written, parameterized Npgsql commands in `WebVella.Erp/Database/**` — there is no EF Core query provider to evaluate.
2. **Razor / Blazor / plain JS front end, not Angular/React/TypeScript.** No `.ts`/SPA-framework supply chain is audited; the front-end-adjacent dependency of note is `WebVella.TagHelpers` 1.7.2, and the Blazor concern is the `net7.0` WebAssembly projects (`DEP-001`).
3. **Code-embedded DDL + dated patch methods, not EF Migrations.** The legacy-timestamp switch (`SEC-005`) and the "fix system tables" comment are evaluated in that context; there is no migrations pipeline to assess.
4. **No Docker present.** Containerization is **not** an existing control; the `System.Drawing.Common` Windows-only constraint (`DEP-003`) is reported as a portability fact, and containerization appears only as a forward-looking option in `modernization-roadmap.md`.

---

## 8. Cross-Document Consistency

- **Shared taxonomy & paths.** Module names (Core `WebVella.Erp`, Web `WebVella.Erp.Web`, WebAssembly, ConsoleApp, the 7 plugins, the 7 sites) and every file path cited here are used **verbatim** from [`code-inventory.md`](./code-inventory.md).
- **Architecture alignment.** The authentication model (`JWT_OR_COOKIE`), the monolithic `WebApiController`, and the custom data layer referenced in this report match the descriptions in [`architecture.md`](./architecture.md).
- **Business-rule alignment.** The authorization observations here (the class-level `[Authorize]`, role-gated endpoints) reconcile with the `AUTHZ-` rules catalogued in [`business-rules.md`](./business-rules.md).
- **Roadmap hand-off.** Every `SEC-`, `DEP-`, and `QA-` finding in this document is an explicit input to the phased plan in [`modernization-roadmap.md`](./modernization-roadmap.md): `SEC-001`/`SEC-002`/`SEC-003` and `QA-001` drive the early hardening + test-harness phase; `QA-002`/`WebApiController` decomposition and `DEP-001` drive the modularization phase; `DEP-003` and transport/secret externalization inform the platform-modernization phase.

---

## 9. Source Citation Index

Every finding above resolves to a real file. The table consolidates the citations for quick verification (paths relative to repository root).

| Finding | File | Line(s) |
|---------|------|---------|
| `SEC-001` RCE | `WebVella.Erp.Web/Controllers/WebApiController.cs` | 36 (`[Authorize]`), 494 (route), 500 (`Compile` call) |
| `SEC-001` RCE | `WebVella.Erp.Web/Services/CodeEvalService.cs` | 1 (`using CSScriptLib`), 45 (`LoadCode`), 57 (`Compile`) |
| `SEC-001` backing deps | `WebVella.Erp.Web/WebVella.Erp.Web.csproj` | 130 (CodeAnalysis), 132 (CS-Script) |
| `SEC-002` deserialization | `WebVella.Erp/Jobs/JobDataService.cs` | 27, 96, 297, 346 |
| `SEC-002` deserialization | `WebVella.Erp/Database/DbRelationRepository.cs` | 47, 128, 173 |
| `SEC-002` deserialization | `WebVella.Erp.Plugins.SDK/Services/CodeGenService.cs` | 958, 990 |
| `SEC-003` secrets | `WebVella.Erp.Site/Config.json` | 3 (conn string), 4 (`EncryptionKey`), 24 (`Jwt:Key`) |
| `SEC-003` JWT scope | `WebVella.Erp.Site.Project/Config.json` | 20 (`Jwt:Key`); other 5 sites have no `Jwt` block |
| `SEC-004` CORS | `WebVella.Erp.Site/Startup.cs` | 61–63 (active), 53–57 (commented restrictive policy) |
| `SEC-005` Npgsql switch | `WebVella.Erp.Site/Startup.cs` | 39 (comment), 40 (switch) |
| `SEC-005` other sites | `…Site.Crm/Mail/Sdk/Startup.cs` :27 · `…MicrosoftCDM` :29 · `…Next` :30 · `…Project` :34 | — |
| `SEC-006` parameterized values (strength) | `WebVella.Erp/Database/DbRecordRepository.cs` | 215, 276, 289–292, 664–679 |
| `SEC-006` FTS-language literal (residual) | `WebVella.Erp/Database/DbRecordRepository.cs` · `WebVella.Erp/Api/Models/QueryObject.cs` | 1503, 1511 (`query.FtsLanguage` concatenation); 1500, 1508 (guards); `QueryObject.cs`:23 (`FtsLanguage` property) |
| `SEC-007` commented security | `WebVella.Erp.Web/Security/WebSecurityUtil.cs` | 1–232 (193 commented) |
| `SEC-007` commented security | `WebVella.Erp.Web/Security/AuthorizeAttribute.cs` | 1–146 (120 commented) |
| `DEP-001` net7.0 | `WebVella.Erp.WebAssembly/Server/WebVella.Erp.WebAssembly.Server.csproj` | 4 (`net7.0`), 10 (`…WebAssembly.Server` 7.0.13) |
| `DEP-001` net7.0 | `WebVella.Erp.WebAssembly/Shared/WebVella.Erp.WebAssembly.Shared.csproj` | 4 (`net7.0`) |
| `DEP-002` 2.2.0 (commented) | `WebVella.Erp/WebVella.Erp.csproj` | 51 |
| `DEP-002` 2.2.0 (commented) | `WebVella.Erp.Web/WebVella.Erp.Web.csproj` | 136, 137 |
| `DEP-002` 2.2.0 (commented) | `WebVella.Erp.Site/WebVella.Erp.Site.csproj` | 56 |
| `DEP-002` SixLabors (commented) | `WebVella.Erp.Web/WebVella.Erp.Web.csproj` | 139–140 |
| `DEP-003` portability | `WebVella.Erp/WebVella.Erp.csproj` | 63 (`System.Drawing.Common` 9.0.10) |
| SDK pin (commented) | `global.json` | 3 (`//"version": "7.0.103"`) |
| `QA-002` anchor | `WebVella.Erp.Web/Controllers/WebApiController.cs` | 4,313 lines total |

---

*End of Deliverable 6 — Security & Quality Assessment. Generated 2026-06-05 23:35 UTC by read-only static analysis of `WebVella.ERP3.sln`. No production code, configuration, or schema artifact was modified in the production of this report.*
