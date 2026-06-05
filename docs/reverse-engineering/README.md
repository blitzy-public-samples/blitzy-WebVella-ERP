# WebVella ERP — Reverse-Engineering / As-Built Documentation Suite

> **Master Index** · This document is the canonical entry point and **single source of truth** for terminology, module names, and verified baseline facts across the entire suite. Every sibling document aligns to the definitions recorded here.

---

## Executive Summary

This suite is a **production-grade, reverse-engineering ("as-built") documentation set** for the **WebVella ERP** platform — an open-source, entity-centric, plugin-driven ERP built on **ASP.NET Core 9** with **PostgreSQL 16**. It was produced by reading the source tree directly and recording **what the system actually is today**, so that three audiences can act with confidence:

- **Enterprise stakeholders** can understand the current system at a glance — its modules, data model, security posture, and technical debt.
- **New developers** can onboard quickly using the module catalog, architecture diagrams, database dictionary, and the catalogued business rules — each traceable to a `path:line` citation in the codebase.
- **Modernization planners** can scope an initiative against a **verified .NET 9 baseline** rather than against assumptions, using the current-state assessment and the three-phase roadmap.

**Analysis-only mandate.** This is a documentation-and-analysis initiative, not a refactor. **No production source was modified** — no `.cs`, `.cshtml`, `.razor`, `.js`, or `.css` file was edited; no API contract, database/migration file, configuration file, or plugin implementation was changed; and **no inline comments or docstrings were added** to source. Every artifact in this suite is a brand-new external file created **only** under `docs/reverse-engineering/`. The suite documents *"what exists"* and notes technical debt **without remediating it**.

**How to read this index.** The [Documentation Suite Index](#documentation-suite-index) links all nine companion artifacts; the [Recommended Reading Order](#recommended-reading-order) sequences them for first-time readers; the [Glossary & Acronyms](#glossary--acronyms) defines the canonical vocabulary; and the [Requirement-vs-Reality Corrections](#requirement-vs-reality-corrections-c1c5) table flags five places where common assumptions diverge from the verified codebase so that readers approach the suite with an accurate mental model.

---

## Generation Metadata

| Field | Value |
|-------|-------|
| **Documentation Generated** | 2026-06-05 15:15 UTC |
| **Source commit** | `bfe15661c7f0c1dae57288d789b854186793b157` |
| **Branch** | `master` |
| **Solution** | `WebVella.ERP3.sln` (20 projects) |
| **Analysis method** | Static reverse-engineering of the source tree (no execution-time profiling required) |
| **Output location** | `docs/reverse-engineering/` (this directory only) |
| **Citation convention** | Inline `path:line` (e.g., `WebVella.Erp/Api/RecordManager.cs:120`) or `path` for whole-file references |
| **Render target** | GitHub-Flavored Markdown (GFM) + Mermaid — renders natively on GitHub with no build step |

> **Reproducibility.** The timestamp and commit above pin this suite to an exact repository state. Regenerating against the same commit yields the same facts. The optional tooling in [Regenerating & Validating the Suite](#regenerating--validating-the-suite) can re-derive metrics (LOC), lint the Markdown, or render the Mermaid diagrams to images.

---

## Verified Technology Baseline

Every value below was confirmed against the codebase at the source commit. These figures are authoritative for the whole suite; sibling documents restate them consistently.

| Aspect | Verified Finding | Primary Evidence |
|--------|------------------|------------------|
| **Runtime** | ASP.NET Core 9 / .NET 9 — **18 of 20** projects target `net9.0`; **2** target `net7.0` (the WebAssembly **Server** and **Shared** projects) | `*.csproj` `<TargetFramework>` |
| **Database** | PostgreSQL 16, accessed through a **custom `Db*` data-access layer** over **Npgsql 9.0.4** — **no Entity Framework Core** | `WebVella.Erp/Database/`, `WebVella.Erp/WebVella.Erp.csproj:61` |
| **Query engine** | Custom **Entity Query Language (EQL)**, parsed with **Irony.NetCore 1.1.11** | `WebVella.Erp/Eql/` |
| **Frontend** | Server-rendered **Razor** (`.cshtml`), **Blazor WebAssembly** (`.razor`), and **jQuery / Bootstrap 4 / StencilJs**; **no** Angular/React and **no** `package.json` | `WebVella.Erp.Web/wwwroot/`, root `README.md` |
| **Hosting** | ASP.NET Core; **IIS InProcess** via `WebVella.Erp.Site/web.config`; **tested only on Windows** | root `README.md`; `WebVella.Erp.Site/web.config` |
| **Schema evolution** | **Patch-class migrations** — exactly 25 date-versioned plugin partial classes across **four** plugins (Mail 7, Next 5, Project 8, SDK 5); there is **no EF `Migrations/` folder** | e.g. `WebVella.Erp.Plugins.Mail/MailPlugin.20190419.cs` |
| **Containerization / CI** | **Not present** — no Dockerfile, no `docker-compose`, no `.github/workflows`; packaging via `create-nuget-pkgs.bat` | `.github/FUNDING.yml` (the only file under `.github/`, no `workflows/`); `create-nuget-pkgs.bat:1` |
| **SDK pin** | `global.json` exists but its `sdk.version` entry is **commented out**, so **no SDK version is pinned** (the build resolves to the latest installed .NET 9 SDK) | `global.json` |

> **Source-tree size (primary files, excluding `bin`/`obj`/`.git`):** ~**703** `.cs`, ~**400** `.cshtml`, ~**11** `.razor`, ~**181** `.js` → **~1,295** primary source files across **20** `.csproj` modules. Full per-file detail lives in [`code-inventory.csv`](./code-inventory.csv).

---

## Documentation Suite Index

The suite is flat and self-contained — one directory of ten files (this index, **seven** Markdown documents, and **two** CSV data files). All links below are **same-directory relative links** and resolve once each sibling artifact is present in `docs/reverse-engineering/`.

| # | Document | File | Purpose |
|---|----------|------|---------|
| 1 | **Code Inventory (narrative)** | [`code-inventory.md`](./code-inventory.md) | Narrative module catalog (core, web, Blazor, console, 7 plugins, 7 Site hosts) with per-module LOC, purpose, and the NuGet dependency tree |
| 2 | **Code Inventory (data)** | [`code-inventory.csv`](./code-inventory.csv) | Per-file inventory using the exact header `Module Name,File Path,Language,Dependencies,Lines of Code,Last Modified,Primary Purpose,Complexity Score` |
| 3 | **Architecture & Data Flow** | [`architecture.md`](./architecture.md) | Layered architecture narrative plus component, record-CRUD sequence, and plugin-lifecycle Mermaid diagrams; covers the EQL read path and the meta-model |
| 4 | **Database Schema** | [`database-schema.md`](./database-schema.md) | ERD, schema-by-domain (CRM, Project, Mail, Approval), and patch-class migration history reconstructed from the date-versioned plugin partials |
| 5 | **Data Dictionary (data)** | [`data-dictionary.csv`](./data-dictionary.csv) | Per-column data dictionary using the exact header `Table Name,Column Name,Data Type,Key Type,Nullable,Default Value,Description,Constraints` |
| 6 | **Functional Overview** | [`functional-overview.md`](./functional-overview.md) | ERP module catalog, roles/permissions, key workflows, module interdependencies, and the multi-site host-shell model |
| 7 | **Business Rules Catalog** | [`business-rules.md`](./business-rules.md) | **≥50** catalogued rules (validation, process, data-integrity, calculation/derivation, authorization), each with a `path:line` citation |
| 8 | **Security & Quality** | [`security-quality.md`](./security-quality.md) | Authentication/authorization model, dependency/CVE audit of verified NuGet versions, and complexity/maintainability metrics |
| 9 | **Modernization Roadmap** | [`modernization-roadmap.md`](./modernization-roadmap.md) | Current-state assessment and an **exactly-three-phase** migration plan calibrated to the verified .NET 9 baseline |

> The two CSV deliverables preserve their **user-provided headers exactly** — they are authoritative and must not be reordered, renamed, or extended.

---

## Recommended Reading Order

For a first pass, read the documents in the sequence below. Each builds context for the next, moving from *what exists* → *how it is built* → *how it behaves* → *where it should go*.

1. **[`README.md`](./README.md)** *(you are here)* — Orient yourself: baseline facts, the canonical glossary, and the suite map.
2. **[`code-inventory.md`](./code-inventory.md)** — Learn the module taxonomy and relative sizes before diving into design; pairs with [`code-inventory.csv`](./code-inventory.csv) for per-file detail.
3. **[`architecture.md`](./architecture.md)** — Understand the layered design, the manager layer, the EQL read path, and the plugin lifecycle that the rest of the suite refers back to.
4. **[`database-schema.md`](./database-schema.md)** — See how the entity-centric meta-model maps onto physical `rec_*`/`rel_*` tables, plus the patch-class migration history.
5. **[`data-dictionary.csv`](./data-dictionary.csv)** — Drill into column-level detail for the meta-model and representative domain tables described in the schema document.
6. **[`functional-overview.md`](./functional-overview.md)** — Connect the structure and data model to user-facing modules, roles, and workflows across the seven plugins and seven Site hosts.
7. **[`business-rules.md`](./business-rules.md)** — Read the catalogued logic (validation, process, integrity, calculation, authorization) that governs the functionality just surveyed.
8. **[`security-quality.md`](./security-quality.md)** — Assess the authentication/authorization model, the dependency/CVE posture, and code-quality metrics.
9. **[`modernization-roadmap.md`](./modernization-roadmap.md)** — Finish with the synthesis: a current-state assessment and a three-phase plan that draws on every prior document.

---

## Scope, Mandate & Methodology

### Analysis-only mandate

- **Zero production-code modification.** No `.cs`, `.cshtml`, `.razor`, `.js`, or `.css` file was edited; no API contracts, database/migration files, configuration files, or plugin implementations were changed; and no inline comments or docstrings were added to source.
- **External documentation only.** Every file in this suite was created **only** within `docs/reverse-engineering/`. No other directory was written to.
- **Sibling docs preserved.** The pre-existing `docs/developer/` hub (getting-started, data-sources, web-api, tag-helpers, and related content) is **left unchanged**; this suite lives alongside it under its own folder.
- **Describe what exists, not what should exist.** Technical debt is recorded and analyzed, but **not remediated**. Recommendations are confined to [`modernization-roadmap.md`](./modernization-roadmap.md) and are advisory only.

### Methodology

- **Factual, source-anchored reporting.** Every technical claim derives from actual code analysis and carries an inline `path:line` citation (or a whole-file `path` reference), so any statement can be verified against the repository at commit `bfe15661`.
- **Cross-document consistency.** Module names, entity/table names, NuGet versions, and glossary terms are defined **once** here and reused verbatim across all sibling documents. Where this index and a sibling could differ, **this index governs**.
- **Diagrams & catalog depth.** The suite contains **≥6 Mermaid diagrams** (component, record-CRUD sequence, plugin lifecycle, ERD, authentication-flow sequence, and the roadmap phases) authored in fenced ` ```mermaid ` blocks, and **≥50 catalogued business rules**, each with a `path:line` citation.
- **Portable formats.** GitHub-Flavored Markdown for narrative, fenced `mermaid` blocks for diagrams, and UTF-8 CSV (with escaped commas/quotes and a clear header row) for the two tabular deliverables — all version-controllable and rendered natively by GitHub with no build step.

### Requirement-vs-Reality Corrections (C1–C5)

Several assumptions commonly attached to a project of this kind diverge from the **verified** WebVella codebase. Per the factual-reporting mandate, the suite documents the **actual** system and flags each divergence (rather than repeating the assumption). These corrections are the lens through which the modernization roadmap is calibrated.

| ID | Common Assumption | Verified Reality | Where It Is Addressed |
|----|-------------------|------------------|------------------------|
| **C1** | Frontend is Angular and/or React | Server-rendered **Razor** `.cshtml` + **Blazor WASM** `.razor` + **jQuery/Bootstrap 4/StencilJs**; there is **no** `package.json` | [`functional-overview.md`](./functional-overview.md), [`architecture.md`](./architecture.md); SPA migration noted as a roadmap option |
| **C2** | Target a ".NET 8" upgrade | Already targets **.NET 9** (18 of 20 projects on `net9.0`) | [`modernization-roadmap.md`](./modernization-roadmap.md) — calibrated to the .NET 9 baseline; no downgrade |
| **C3** | Uses Entity Framework Core (or another ORM) | Custom **`Db*` repository DAL** over **Npgsql 9.0.4** — **no EF Core** | [`architecture.md`](./architecture.md), [`database-schema.md`](./database-schema.md); EF/ORM adoption is a roadmap consideration |
| **C4** | Schema migrations live in a `Migrations/` folder | **No** such folder; schema evolves via **date-versioned plugin partial classes** (patch-class migrations) | [`database-schema.md`](./database-schema.md) — history reconstructed from the patch classes |
| **C5** | Docker containerization / CI pipelines exist | **None present**; **IIS InProcess** hosting + `create-nuget-pkgs.bat` packaging | [`security-quality.md`](./security-quality.md), [`modernization-roadmap.md`](./modernization-roadmap.md); containerization/CI flagged as opportunities |

---

## Module Taxonomy (Canonical)

These are the canonical module names used throughout the suite. Sibling documents reference exactly these names; the per-file mapping lives in [`code-inventory.csv`](./code-inventory.csv) and the narrative in [`code-inventory.md`](./code-inventory.md).

| Group | Project(s) | Role |
|-------|-----------|------|
| **Core platform** | `WebVella.Erp` | Entity meta-model, manager layer (`EntityManager`, `RecordManager`, `EntityRelationManager`, `DataSourceManager`, `SearchManager`, `SecurityManager`), EQL engine, hooks, jobs, recurrence, FTS, notifications |
| **Data access** | `WebVella.Erp/Database` | Custom `Db*` DAL (`DbContext`, `DbConnection`, `DbEntity`, `DbEntityRelation`, `DbRecordRepository`, `DbRepository`, `FieldTypes/`) over Npgsql |
| **Web application** | `WebVella.Erp.Web` | Controllers (versioned REST `/api/v3.0/...`), middleware, Razor Pages, components, tag helpers, and the `Security/` constructs |
| **Blazor client** | `WebVella.Erp.WebAssembly` | Blazor WebAssembly `Client` / `Server` / `Shared` projects (the two `net7.0` projects are here) |
| **Console harness** | `WebVella.Erp.ConsoleApp` | Console host for bootstrap/maintenance tasks |
| **Plugins (7)** | `WebVella.Erp.Plugins.{Approval, Crm, Mail, MicrosoftCDM, Next, Project, SDK}` | Feature module **projects**: **six** (Crm, Mail, MicrosoftCDM, Next, Project, SDK) ship a `*Plugin._.cs` bootstrap plus `Controllers`/`Services`/`Components`/`Hooks`/`Jobs` as applicable; of those six, **four** (Mail, Next, Project, SDK) also ship **dated migration partials** (**25** in total), while **Crm** and **MicrosoftCDM** have bootstraps but **no dated partials**; **Approval** is a plugin project with only dashboard code at this commit — **no `ErpPlugin` subclass, no bootstrap, no migration** |
| **Site hosts (7)** | `WebVella.Erp.Site{, .Crm, .Mail, .MicrosoftCDM, .Next, .Project, .Sdk}` | ASP.NET Core host shells: each wires DI, authentication, plugin registration, `Program.cs`, `Startup.cs`, and `Config.json` |

---

## Glossary & Acronyms

These definitions are **canonical**. Every sibling document uses these terms with the meaning given here. Citations point to the defining source so each term is verifiable.

| Term | Definition |
|------|------------|
| **EQL** | **Entity Query Language** — WebVella's **custom** query language for reading records from the entity meta-model. Its grammar is parsed with **Irony.NetCore** (`Irony.NetCore 1.1.11`) and executed on the read path via `RecordManager`/`EqlCommand`. Source: `WebVella.Erp/Eql/`. |
| **DAL** | **Data-Access Layer** — the **custom `Db*` repository layer** (`DbContext`, `DbConnection`, `DbEntity`, `DbEntityRelation`, `DbRecordRepository`, `DbRepository`, `FieldTypes/`) built directly on **Npgsql 9.0.4**. There is **no Entity Framework Core**. Source: `WebVella.Erp/Database/`. |
| **ERD** | **Entity-Relationship Diagram** — the Mermaid diagram in [`database-schema.md`](./database-schema.md) depicting the meta-model entities (entity, field, relation, record) and representative physical domain tables. |
| **CDM** | **(Microsoft) Common Data Model** — the integration plugin `WebVella.Erp.Plugins.MicrosoftCDM`, which aligns WebVella entities with Microsoft's Common Data Model schema. |
| **meta-model** | The platform's defining trait: **entities, fields, and relations are stored as data** (managed by `EntityManager`/`EntityRelationManager`) rather than as compile-time POCOs. New entities therefore create new physical tables at runtime instead of requiring code changes. |
| **plugin** | A feature module delivered as a `WebVella.Erp.Plugins.*` **project**. The suite documents **seven** plugin projects: Approval, Crm, Mail, MicrosoftCDM, Next, Project, and SDK. **Six** of them (Crm, Mail, MicrosoftCDM, Next, Project, SDK) are runtime **`ErpPlugin` subclasses** (`WebVella.Erp/ErpPlugin.cs`) with a `*Plugin._.cs` bootstrap; of these six, **four** (Mail, Next, Project, SDK) own **dated migration partials**, while **Crm** and **MicrosoftCDM** have bootstraps but **no dated partials**. **Approval** is a plugin project/module shipping **only dashboard code** at the pinned commit — it is **not** an `ErpPlugin` subclass and has **no bootstrap and no migration**. |
| **plugin bootstrap** | The plugin's primary partial-class file, conventionally named **`*Plugin._.cs`**, whose initialization logic (`ProcessPatches`) applies that plugin's dated migration partials in chronological order during startup. Present for the **six** bootstrapped plugins (Crm, Mail, MicrosoftCDM, Next, Project, SDK); of these, **four** (Mail, Next, Project, SDK) actually own dated migration partials, while **Crm** and **MicrosoftCDM** run `ProcessPatches` but ship **no** dated partials (Crm's only inline patch is commented out). The **Approval** project has **no** bootstrap. |
| **patch-class migration** | A **date-versioned plugin partial class** that evolves the schema (e.g., `WebVella.Erp.Plugins.Mail/MailPlugin.20190419.cs`). The platform has **no EF `Migrations/` folder**; exactly **25** such patch classes — distributed across **four** plugins (**Mail** 7, **Next** 5, **Project** 8, **SDK** 5) — constitute the entire schema history. |
| **Site host** | One of **seven** `WebVella.Erp.Site*` **ASP.NET Core host-shell** projects (`Site`, `Site.Crm`, `Site.Mail`, `Site.MicrosoftCDM`, `Site.Next`, `Site.Project`, `Site.Sdk`). Each wires dependency injection, authentication, plugin registration, and configuration (`Program.cs`, `Startup.cs`, `Config.json`). |
| **hook** | An extension point invoked around record/relation operations. There are **12** `IErp*Hook` interfaces in `WebVella.Erp/Hooks/`: **8 record hooks** (`IErpPre`/`PostCreateRecordHook`, `…UpdateRecordHook`, `…DeleteRecordHook`, `…SearchRecordHook`) plus **4 many-to-many relation hooks** (`IErpPre`/`PostCreateManyToManyRelationHook`, `…DeleteManyToManyRelationHook`). |
| **job** | A background unit of work derived from **`ErpJob`** (`WebVella.Erp/Jobs/ErpJob.cs`), executed on a schedule by the platform's background-services infrastructure (e.g., notification/escalation jobs). |
| **ResponseModel / QueryResponse** | The standard **API response envelope** returned by the web layer — a wrapper carrying success/error status, messages, and a typed payload. `ResponseModel` is defined at `WebVella.Erp/Api/Models/BaseModels.cs:40`; query-shaped responses use the `QueryResponse` family from the same models. |
| **`rec_*`** | Naming convention for a **per-entity physical table** — the actual PostgreSQL table generated to store records of a given entity in the meta-model. |
| **`rel_*`** | Naming convention for an **N:N join table** that materializes a many-to-many relation between two entities. |
| **manager layer** | The core service classes in `WebVella.Erp/Api/` that mediate all entity, record, relation, data-source, search, and security operations: `EntityManager`, `EntityRelationManager`, `RecordManager`, `DataSourceManager`, `SearchManager`, `SecurityManager`, and `ImportExportManager`. |
| **DataSource** | A named, reusable query/definition (managed by `DataSourceManager`) that supplies records to pages and components; data sources may be EQL-based or backed by Roslyn/CS-Script code. |
| **FTS** | **Full-Text Search** — the search subsystem under `WebVella.Erp/Fts/`, surfaced through `SearchManager`. |
| **`ErpPrincipal` / `ErpIdentity`** | **Legacy, commented-out** security types under `WebVella.Erp.Web/Security/` (alongside `AuthToken`, the custom `AuthorizeAttribute`, and `AuthCache`). These source files are **entirely commented out and do not enforce authorization** at runtime (see [`security-quality.md`](./security-quality.md) §2.1). The **active** identity model uses the framework's `ClaimsIdentity` / `ClaimsPrincipal`, populated by the host authentication schemes (`WebVella.Erp.Site/Startup.cs:88-125`); `ErpMiddleware` then opens the domain `SecurityContext` scope from `context.User` (`WebVella.Erp.Web/Middleware/ErpMiddleware.cs:32-35`), and authorization is enforced by `SecurityContext` / manager permission checks (`WebVella.Erp/Api/SecurityContext.cs:63`). |
| **GFM** | **GitHub-Flavored Markdown** — the markup dialect this suite is authored in; GitHub renders its tables and fenced `mermaid` blocks natively. |

---

## Suite Conventions

- **Headings:** a single `#` H1 per file, with properly nested `##`/`###`/`####` sections (H1–H4).
- **Diagrams:** Mermaid in fenced ` ```mermaid ` blocks; the suite carries **≥6** diagrams (see [Methodology](#methodology)).
- **Citations:** inline `path:line` (e.g., `WebVella.Erp/Api/RecordManager.cs:120`) — or `path` for whole-file references — so every claim is traceable to commit `bfe15661`.
- **Tables:** GFM pipe tables for endpoints, columns, parameters, dependencies, and rule catalogs.
- **CSV deliverables:** UTF-8 encoded, comma-separated, with escaped commas/quotes and the **exact** user-provided header rows:
  - [`code-inventory.csv`](./code-inventory.csv) → `Module Name,File Path,Language,Dependencies,Lines of Code,Last Modified,Primary Purpose,Complexity Score`
  - [`data-dictionary.csv`](./data-dictionary.csv) → `Table Name,Column Name,Data Type,Key Type,Nullable,Default Value,Description,Constraints`
- **Terminology:** aligned to the [Glossary & Acronyms](#glossary--acronyms) above; this index is the source of truth on any conflict.

---

## Regenerating & Validating the Suite

No build step is required — GitHub renders every file natively. The commands below are **optional** aids for authors who wish to render diagrams to images, lint the Markdown, or recompute metrics. Tool versions reflect current stable releases verified during research; none is a runtime dependency of WebVella ERP.

| Action | Command | Notes |
|--------|---------|-------|
| Preview locally | open any file in a Markdown viewer | Optional |
| Render Mermaid to images | `mmdc -i <file>.md -o <file>.svg` | Optional; `@mermaid-js/mermaid-cli` 11.15.0 (Node ≥18.19) |
| Lint Markdown | `markdownlint-cli2 "docs/reverse-engineering/**/*.md"` | Optional; `markdownlint-cli2` 0.22.1 |
| Recompute LOC | `cloc --by-file .` | Optional; `cloc` 2.08 — feeds the inventory metrics |
| Optional API doc site | `docfx` | Optional; DocFX 2.78.5 (Roslyn-based, supports .NET 9) |

> These tools are **not** added to the repository and **no** documentation-site generator (`mkdocs.yml`, `docusaurus.config.js`, `docfx.json`, etc.) is introduced — adopting one is noted as an optional modernization item in [`modernization-roadmap.md`](./modernization-roadmap.md), not part of this analysis-only scope.

---

## At a Glance

| Metric | Value |
|--------|-------|
| Documents in suite | **10** (this index + 7 Markdown + 2 CSV) |
| Solution projects analyzed | **20** (`WebVella.ERP3.sln`) |
| Plugins documented | **7** (Approval, Crm, Mail, MicrosoftCDM, Next, Project, SDK) |
| Site hosts documented | **7** |
| Primary source files inventoried | **~1,295** (~703 `.cs`, ~400 `.cshtml`, ~11 `.razor`, ~181 `.js`) |
| Mermaid diagrams across the suite | **≥6** |
| Catalogued business rules | **≥50** (each with a `path:line` citation) |
| Patch-class migrations reconstructed | **25** date-versioned plugin partials (across four plugins: Mail 7, Next 5, Project 8, SDK 5) |

---

*Generated 2026-06-05 15:15 UTC from source commit `bfe15661c7f0c1dae57288d789b854186793b157` (branch `master`). This is an analysis-only, reverse-engineering documentation suite — no production source, schema, configuration, build, or test file was modified, and all output is confined to `docs/reverse-engineering/`.*

