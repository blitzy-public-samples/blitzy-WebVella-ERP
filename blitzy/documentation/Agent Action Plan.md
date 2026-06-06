# Technical Specification

# 0. Agent Action Plan

## 0.1 Intent Clarification

### 0.1.1 Core Documentation Objective

Based on the provided requirements, the Blitzy platform understands that the documentation objective is to **produce a complete, production-grade reverse-engineering documentation suite that describes the as-built state of the WebVella ERP system**, emitted exclusively as new external artifacts under `docs/reverse-engineering/`, so that enterprise stakeholders can understand the current system, onboard developers, and plan a modernization initiative — all while making **zero modifications** to any production source file.

- **Request category:** Create new documentation — specifically *reverse-engineering / as-built* documentation. This is not an update of existing docs, because the repository's `docs/` tree is an empty placeholder containing only `docs/developer/test.txt`.
- **Documentation types to be produced:** Architecture documentation, REST API documentation, database schema & data dictionary, functional/module overview, business-rules catalog, security & code-quality assessment, and a modernization roadmap — plus a master index.

The requirement decomposes into eight discrete documentation deliverables, restated below with technical precision:

| # | Requirement (Deliverable) | Output File(s) | Enhanced Interpretation |
|---|---------------------------|----------------|--------------------------|
| 1 | Code Inventory Report | `code-inventory.md` + `code-inventory.csv` | Catalogue every source file with module, language, dependencies, LOC, last-modified, purpose, and a complexity score; summarize the NuGet dependency tree. |
| 2 | System Architecture & Data Flow | `architecture.md` | Mermaid component diagram, data-flow diagrams (entity CRUD, API processing, plugin lifecycle), and integration architecture for the entity-centric, plugin-driven platform. |
| 3 | Database Schema & Data Dictionary | `database-schema.md` + `data-dictionary.csv` | Mermaid ERD plus a column-level dictionary covering the meta-model tables and the per-entity physical tables; summarize schema evolution. |
| 4 | Functional Overview | `functional-overview.md` | ERP module catalog (CRM, Project, Mail, Approval, Next, Microsoft CDM, SDK), roles/permissions, key workflows, and module interdependencies. |
| 5 | Business Rules Catalog | `business-rules.md` | ≥50 inferred rules across validation, process, data-integrity, calculation/derivation, and authorization categories, each with a code reference. |
| 6 | Security & Quality Assessment | `security-quality.md` | Vulnerability analysis, authentication/authorization review, data protection, dependency CVE audit, complexity/maintainability metrics, code smells, and compliance notes. |
| 7 | Modernization Roadmap | `modernization-roadmap.md` | Current-state assessment, target architecture, technology upgrades, a three-phase migration plan, risk mitigation, and success metrics. |
| 8 | Master Index | `README.md` | Suite landing page linking all deliverables, with a generation timestamp, reading order, and glossary. |

### 0.1.2 Special Instructions and Constraints

The following directives are explicit in the requirements and govern every downstream action. They are preserved here so that no generation agent can misinterpret them.

- **CRITICAL — Zero production code modification:** No edits to any `.cs`, `.cshtml`, `.razor`, or `.js` file; no changes to API contracts, database/migration files, configuration files, or plugin implementations; **no inline comments or docstrings added** to source. The initiative is analysis-only.
- **External-documentation only:** New files may be created **only** within `docs/reverse-engineering/`. No other directory is written to.
- **Analysis-only mindset:** Document "what exists," not "what should exist." Note technical debt without remediating it.
- **Factual reporting:** Every statement must derive from actual code analysis, not assumptions, and carry a source citation (file path + line number).
- **Output formats:** GitHub Flavored Markdown (GFM); Mermaid diagrams in fenced `mermaid` blocks; CSV exports that are UTF-8 encoded with escaped commas/quotes and a clear header row. Heading hierarchy is H1–H4. Each major document opens with an executive summary and carries a "Documentation Generated" timestamp.
- **Cross-document consistency:** Module names, entity names, and terminology must align across all seven documents.

The requirements also embed two artifacts that are treated as **user-provided templates and preserved EXACTLY** during generation (full reproduction in §0.4 and §0.5):

- USER PROVIDED TEMPLATE — Code Inventory CSV header: `Module Name,File Path,Language,Dependencies,Lines of Code,Last Modified,Primary Purpose,Complexity Score`
- USER PROVIDED TEMPLATE — Data Dictionary CSV header: `Table Name,Column Name,Data Type,Key Type,Nullable,Default Value,Description,Constraints`

A web-search requirement is also present ("validate documentation best practices / tooling"); this has been satisfied and the validated tool versions are recorded in §0.6.

### 0.1.3 Technical Interpretation

These documentation requirements translate to the following technical documentation strategy, expressed as requirement-to-action mappings:

- To **inventory the codebase**, we will create `code-inventory.md` + `code-inventory.csv` by scanning every `.cs`, `.cshtml`, `.razor`, and `.js` file across the solution `WebVella.ERP3.sln` and parsing all 20 `.csproj` files for dependency metadata.
- To **document architecture**, we will create `architecture.md` from the core managers in `WebVella.Erp/Api/` (e.g., `EntityManager.cs`, `RecordManager.cs`, `SecurityManager.cs`) [WebVella.Erp/Api/RecordManager.cs], the custom query engine in `WebVella.Erp/Eql/`, and the web pipeline in `WebVella.Erp.Web/Middleware/`, rendering component and data-flow diagrams in Mermaid.
- To **document the database**, we will create `database-schema.md` + `data-dictionary.csv` from the custom data-access layer in `WebVella.Erp/Database/` (`DbEntity.cs`, `DbEntityRelation.cs`, `FieldTypes/`) [WebVella.Erp/Database/DbEntity.cs] and the date-versioned plugin migration partials.
- To **document functionality**, we will create `functional-overview.md` by analyzing the seven plugin projects and seven site hosts, supplemented by the `jira-stories/STORY-*.md` requirements for the Approval domain.
- To **catalog business rules**, we will create `business-rules.md` by extracting validation/process/authorization logic from plugin services, core managers, and `WebVella.Erp.Web/Security/AuthorizeAttribute.cs` [WebVella.Erp.Web/Security/AuthorizeAttribute.cs].
- To **assess security and quality**, we will create `security-quality.md` from `WebVella.Erp.Web/Security/` constructs and a dependency CVE audit of the verified NuGet versions.
- To **plan modernization**, we will create `modernization-roadmap.md` synthesizing the prior documents against the verified current baseline (ASP.NET Core 9, PostgreSQL 16).

### 0.1.4 Inferred Documentation Needs

Beyond the explicit deliverables, repository analysis surfaces the following implicit documentation needs that the suite must address:

- **Custom migration model:** WebVella has **no Entity Framework `Migrations/` folder**; schema evolution is implemented as date-versioned plugin partial classes (e.g., `WebVella.Erp.Plugins.Mail/MailPlugin.20190419.cs`). The database and business-rules documents must reverse-engineer schema history from these patch classes rather than from EF migrations.
- **Entity-centric meta-model:** Entities and fields are stored as data (managed by `EntityManager`/`EntityRelationManager`) rather than as compile-time POCOs, so the data dictionary must distinguish meta-model tables from the per-entity physical tables they generate.
- **Custom query language (EQL):** The platform ships its own Entity Query Language parsed by Irony.NetCore; the architecture document needs a dedicated treatment of EQL and the `RecordManager`/`EqlCommand` read path.
- **Plugin execution lifecycle:** Discovery, registration, patch application, hooks, and background jobs form a lifecycle that must be diagrammed.
- **Multi-site host-shell pattern:** Seven `WebVella.Erp.Site.*` projects each wire dependency injection, authentication, and plugin registration; the functional and architecture docs must explain this hosting model.
- **Suite navigation and glossary:** A master `README.md` index and a shared glossary/acronym list are required for cross-document coherence.
- **Reality-calibrated roadmap inputs:** Several requirement assumptions diverge from the actual codebase and must be flagged for the roadmap (detailed in §0.3.3): the system is already on .NET 9 (not a ".NET 8 upgrade" target), the frontend is server-rendered Razor + Blazor + jQuery (not Angular/React), the data layer is a custom DAL over Npgsql (not EF Core), and there is no Docker/CI configuration present.


## 0.2 Documentation Discovery and Analysis

Repository analysis reveals a large, multi-project .NET 9 / ASP.NET Core solution (`WebVella.ERP3.sln`) with an **essentially empty documentation surface**, confirming that this initiative is a green-field documentation build rather than an update.

### 0.2.1 Existing Documentation Infrastructure Assessment

- **Documentation framework:** None. There is no documentation-site generator configured — no `mkdocs.yml`, `docusaurus.config.js`, Sphinx `conf.py`, or `docfx.json` exists anywhere in the repository.
- **Existing docs tree:** `docs/` contains only a placeholder `docs/developer/test.txt`; there is no authored developer documentation [docs/developer/test.txt].
- **API documentation tooling in use:** None detected (no DocFX/Swagger generation configuration). API behavior must be reverse-engineered from controller routing attributes.
- **Diagram tooling:** None committed; Mermaid will be authored inline (GitHub renders `mermaid` fenced blocks natively).
- **Hosting/deployment of docs:** None.
- **Reusable assets discovered:** `doc-images/` holds existing product screenshots (e.g., `sdk-entity-create.png`, `sdk-datasource-list.png`, `sdk-application-sitemap.png`) that can be linked from the functional overview [doc-images/sdk-entity-create.png]; `blitzy/documentation/Technical Specifications.md` and `blitzy/documentation/Project Guide.md` provide an in-repo Markdown + Mermaid + table style to mirror [blitzy/documentation/Technical Specifications.md]; `jira-stories/STORY-001..009.md` plus `stories-export.csv`/`stories-export.json` document the Approval domain requirements.
- **Target directory status:** `docs/reverse-engineering/` does **not** exist yet — every deliverable is a CREATE.

The current state and the verified technology stack are summarized below:

| Aspect | Finding | Evidence |
|--------|---------|----------|
| Runtime | ASP.NET Core 9 (18 of 20 projects target `net9.0`; 2 target `net7.0`) | `*.csproj` `<TargetFramework>` |
| Database | PostgreSQL 16, accessed via **Npgsql 9.0.4** (no EF Core) | `WebVella.Erp.csproj` Npgsql 9.0.4 |
| Query engine | Custom Entity Query Language (EQL), Irony.NetCore 1.1.11 parser | `WebVella.Erp/Eql/` |
| Frontend | Server-rendered Razor (`.cshtml`), Blazor WebAssembly (`.razor`), jQuery/Bootstrap 4/StencilJs; **no** Angular/React, **no** `package.json` | `WebVella.Erp.Web/wwwroot/`, README |
| Hosting | ASP.NET Core; IIS InProcess via `WebVella.Erp.Site/web.config`; tested on Windows | README.md; `WebVella.Erp.Site/web.config` |
| Containerization / CI | **Not present** — no Dockerfile, no docker-compose, no `.github/workflows` | repository scan |

### 0.2.2 Repository Code Analysis for Documentation

The codebase is organized into clearly separable functional areas, each of which becomes a documentation grouping. Verified file counts (excluding `bin`/`obj`/`.git`): ~703 `.cs`, ~400 `.cshtml`, ~181 `.js`, ~11 `.razor`, ~20 `.csproj`, ~143 `.md`.

| Functional Area | Key Directories / Files | Documentation Relevance |
|-----------------|-------------------------|--------------------------|
| Core platform (`WebVella.Erp`) | `Api/` (`EntityManager.cs`, `EntityRelationManager.cs`, `RecordManager.cs`, `DataSourceManager.cs`, `SearchManager.cs`, `SecurityManager.cs`, `SecurityContext.cs`), `Eql/`, `Fts/`, `Hooks/`, `Jobs/`, `Recurrence/`, `Notifications/`, `Diagnostics/` | Architecture, business rules, functional overview |
| Data access (`WebVella.Erp/Database`) | `DbContext.cs`, `DbConnection.cs`, `DbEntity.cs`, `DbEntityRelation.cs`, `DbRecordRepository.cs`, `DbRepository.cs`, `FieldTypes/`, `DBTypeConverter.cs` | Database schema, data dictionary |
| Web application (`WebVella.Erp.Web`) | `Controllers/` (`WebApiController.cs`, `ApiControllerBase.cs`), `Middleware/`, `Pages/`, `Components/`, `TagHelpers/`, `Security/`, `Datasource/`, `Repositories/`, `wwwroot/` | Architecture, API, security |
| Blazor client (`WebVella.Erp.WebAssembly`) | `Client/`, `Server/`, `Shared/` | Architecture, functional overview |
| Plugins (7) | `WebVella.Erp.Plugins.{Approval,Crm,Mail,MicrosoftCDM,Next,Project,SDK}` — each with `*._.cs` bootstrap + dated migration partials + `Controllers/`/`Services/`/`Components/`/`Hooks/`/`Jobs/` | Functional overview, business rules, schema |
| Site hosts (7) | `WebVella.Erp.Site{,.Crm,.Mail,.MicrosoftCDM,.Next,.Project,.Sdk}` — each `Program.cs`, `Startup.cs`, `Config.json`, `.csproj` | Architecture, deployment |
| Console harness | `WebVella.Erp.ConsoleApp` (`Program.cs`, `Config.json`) | Functional overview |

- **Public API surface:** A versioned REST surface under `/api/v3.0/...` is exposed by `WebVella.Erp.Web/Controllers/WebApiController.cs` and plugin controllers (`ApprovalController.cs`, `ProjectController.cs`, `SDK/AdminController.cs`); endpoints include datasource operations, page-builder node operations (`/api/v3.0/page/{pageId}/node/...`), and plugin endpoints (`/api/v3.0/p/{plugin}/...`) [WebVella.Erp.Web/Controllers/WebApiController.cs].
- **Security constructs:** `WebVella.Erp.Web/Security/` provides `ErpIdentity.cs`, `ErpPrincipal.cs`, `AuthToken.cs`, a custom `AuthorizeAttribute.cs`, and `AuthCache.cs` [WebVella.Erp.Web/Security/AuthorizeAttribute.cs].
- **Configuration files (read-only):** Per-site `Config.json`, `WebVella.Erp.Site/web.config`, and `appsettings.json` in `Site.MicrosoftCDM` and `WebAssembly/Server` — these are documented but never modified.
- **Schema evolution:** Implemented through ~25 date-versioned plugin partial classes (e.g., `WebVella.Erp.Plugins.Project/ProjectPlugin.20211012.cs`) rather than EF migrations.

### 0.2.3 Web Search Research Conducted

Targeted web research validated current, stable documentation-tooling versions and confirmed best practices for a .NET reverse-engineering documentation suite (full version table in §0.6):

- **Diagram rendering:** `@mermaid-js/mermaid-cli` (`mmdc`) current stable **11.15.0**; used optionally to pre-render or validate inline Mermaid; requires Node.js ≥18.19.
- **.NET API reference:** **DocFX 2.78.5** (a .NET global tool that extracts C# API docs via Roslyn and supports .NET 9) — recommended optionally for an auto-generated API reference, but the suite is primarily hand-authored Markdown.
- **Markdown linting:** `markdownlint-cli2` **0.22.1** for GFM style validation.
- **Line counting:** `cloc` **2.08** for physical-LOC metrics feeding the Code Inventory CSV.
- **Best-practice confirmation:** Markdown + Mermaid + CSV is the recommended portable, version-controllable documentation format for legacy reverse-engineering, and is natively rendered by GitHub with no build step.


## 0.3 Documentation Scope Analysis

Given the requirements and the repository analysis, the documentation scope maps the platform's modules to specific deliverables, identifies the gaps each deliverable fills, and reconciles requirement assumptions against the verified codebase.

### 0.3.1 Code-to-Documentation Mapping

- **Module:** `WebVella.Erp/Api/` (core managers)
  - Public surface: `EntityManager`, `EntityRelationManager`, `RecordManager`, `DataSourceManager`, `SearchManager`, `SecurityManager`, `SecurityContext`, `ImportExportManager` [WebVella.Erp/Api/EntityManager.cs].
  - Current documentation: missing.
  - Documentation needed: architecture (component + data-flow), API reference for the manager layer, business rules (validation/derivation), and the EQL read path.
- **Module:** `WebVella.Erp/Database/`
  - Public surface: `DbContext`, `DbEntity`, `DbEntityRelation`, `DbRecordRepository`, `DbRepository`, `FieldTypes/` [WebVella.Erp/Database/DbEntity.cs].
  - Current documentation: missing.
  - Documentation needed: database schema (meta-model + physical tables), ERD, data dictionary, data-integrity rules.
- **Module:** `WebVella.Erp.Web/Controllers/` and plugin controllers
  - Endpoints: versioned REST under `/api/v3.0/...` including `/api/v3.0/p/{plugin}/...`, `/api/v3.0/page/{pageId}/node/...`, datasource test/compile, and `/fs/...` file serving [WebVella.Erp.Web/Controllers/WebApiController.cs].
  - Current documentation: missing.
  - Documentation needed: API specification with HTTP methods/paths and request/response envelope (`ResponseModel`).
- **Module:** `WebVella.Erp.Web/Security/`
  - Public surface: `ErpIdentity`, `ErpPrincipal`, `AuthToken`, `AuthorizeAttribute`, `AuthCache`, `WebSecurityUtil` [WebVella.Erp.Web/Security/AuthorizeAttribute.cs].
  - Current documentation: missing.
  - Documentation needed: authentication/authorization architecture, authorization rules, and security assessment.
- **Module:** Plugins `Crm`, `Project`, `Mail`, `Approval`, `Next`, `MicrosoftCDM`, `SDK`
  - Public surface: each plugin's `Services/`, `Hooks/`, `Jobs/`, `Components/`, and dated migration partials.
  - Current documentation: partial — only the Approval domain has `jira-stories/STORY-*.md`.
  - Documentation needed: per-module functional overview, workflows, entities, and business rules.
- **Configuration:** per-site `Config.json`, `web.config`, `appsettings.json`
  - Options documented: 0 of N currently.
  - Documentation needed: configuration reference table (documented, never modified) [WebVella.Erp.Site/Config.json].

### 0.3.2 Documentation Gap Analysis

Given the requirements and repository analysis, documentation gaps include the following — every item below is currently undocumented and constitutes the work to be created:

- **Undocumented public APIs:** the entire core manager layer (`EntityManager`, `RecordManager`, `EntityRelationManager`, `DataSourceManager`, `SearchManager`, `SecurityManager`) and the versioned REST endpoints.
- **Missing architecture documentation:** no component diagram, no data-flow diagrams (entity CRUD, API processing, plugin lifecycle), and no description of the EQL engine or the meta-model.
- **Missing database documentation:** no ERD, no data dictionary, and no consolidated record of the patch-class schema evolution.
- **Missing functional documentation:** no module catalog, no role/permission map, and no workflow descriptions for CRM, Project, Mail, Approval, Next, or Microsoft CDM.
- **Missing business-rules catalog:** validation, process, integrity, calculation, and authorization rules are embedded in code but not catalogued.
- **Missing security & quality assessment:** no vulnerability analysis, no dependency CVE audit, and no complexity/maintainability metrics.
- **Missing modernization roadmap:** no current-state assessment or migration plan.
- **No master index or glossary:** nothing ties the suite together for stakeholders.

### 0.3.3 Requirement-Assumption Reconciliation

The requirements contain several technology assumptions that the verified codebase contradicts. Per the analysis-only, factual-reporting mandate, the documentation will describe the **actual** system and flag each divergence for the modernization roadmap. No assumption forces a code change.

| ID | Requirement Assumption | Verified Reality | Resolution in the Suite |
|----|------------------------|------------------|--------------------------|
| C1 | Frontend is "Angular and/or React" | Server-rendered Razor `.cshtml` + Blazor WASM `.razor` + jQuery/Bootstrap 4/StencilJs; no `package.json` | Document the actual server-rendered/Blazor frontend; note SPA migration as a roadmap option |
| C2 | Recommend upgrade to ".NET 8" | Already targets .NET 9 (18/20 projects) | Calibrate roadmap to the .NET 9 baseline; no downgrade |
| C3 | "Entity Framework Core or custom ORM" | Custom `Db*` repository DAL over Npgsql 9.0.4 (no EF Core) | Document the custom DAL/EQL; treat EF/ORM adoption as a roadmap consideration |
| C4 | Migrations in `/WebVella.Erp.Web/Migrations/` | No such folder; schema evolves via date-versioned plugin partial classes | Reverse-engineer schema history from patch classes; document the patch model |
| C5 | "Docker containerization / deployment scripts / CI" | No Dockerfile/compose/CI; IIS InProcess + `create-nuget-pkgs.bat` | Document actual hosting; flag containerization/CI as modernization opportunities |


## 0.4 Documentation Implementation Design

These documentation requirements translate into a single, self-contained suite rooted at `docs/reverse-engineering/`, generated by reading the source tree and rendering Markdown, CSV, and Mermaid artifacts. No documentation generator is mandated; the deliverables are plain files that render natively on GitHub.

### 0.4.1 Documentation Structure Planning

The suite is flat and self-contained — one directory, ten files (seven Markdown documents, two CSV data files, and one README index):

```
docs/
└── reverse-engineering/
    ├── README.md                  (master index, reading order, glossary, generation timestamp)
    ├── code-inventory.md           (narrative module catalog + metrics)
    ├── code-inventory.csv           (per-file inventory; user-specified columns)
    ├── architecture.md             (component, data-flow, integration Mermaid diagrams)
    ├── database-schema.md          (ERD, schema-by-domain, patch-class migration history)
    ├── data-dictionary.csv          (per-column data dictionary; user-specified columns)
    ├── functional-overview.md      (modules, roles, workflows, screen references)
    ├── business-rules.md           (50+ catalogued rules with file:line citations)
    ├── security-quality.md         (auth model, dependency/CVE audit, quality metrics)
    └── modernization-roadmap.md    (current-state assessment + 3-phase plan)
```

### 0.4.2 Content Generation Strategy

- **Information extraction approach:**
  - Extract module/project structure from `WebVella.ERP3.sln` and the 20 `.csproj` files [WebVella.ERP3.sln].
  - Extract manager and repository signatures from `WebVella.Erp/Api/` and `WebVella.Erp/Database/` for API and architecture docs.
  - Extract endpoint routes from controller route attributes under `WebVella.Erp.Web/Controllers/` and plugin controllers.
  - Derive schema and migration history from `WebVella.Erp/Database/` plus the date-versioned plugin partial classes (e.g., `MailPlugin.20190419.cs`).
  - Mine business rules from plugin `Services/`/`Hooks/`/`Jobs/`, core manager validation, `FieldTypes/`, and `AuthorizeAttribute`/`SecurityManager`.
  - Source functional content from the 7 plugins, the Site hosts, `jira-stories/STORY-*.md`, and `doc-images/*.png`.
- **Documentation standards:**
  - GitHub-Flavored Markdown with a single `#` H1 per file and properly nested `##`/`###` sections.
  - Mermaid diagrams in fenced `mermaid` blocks (GitHub renders these natively).
  - Code examples in fenced language blocks limited to short, illustrative excerpts.
  - Source citations inline as `path:line` (e.g., `WebVella.Erp/Api/RecordManager.cs:120`) so every technical claim is traceable.
  - Tables for parameters, columns, endpoints, and mappings; consistent terminology aligned to the glossary in the README index.

### 0.4.3 User-Provided CSV Templates

The two CSV deliverables MUST use the column headers below, reproduced EXACTLY as provided. These are authoritative and must not be reordered, renamed, or extended.

USER PROVIDED TEMPLATE (code-inventory.csv header row):

```
Module Name,File Path,Language,Dependencies,Lines of Code,Last Modified,Primary Purpose,Complexity Score
```

USER PROVIDED TEMPLATE (data-dictionary.csv header row):

```
Table Name,Column Name,Data Type,Key Type,Nullable,Default Value,Description,Constraints
```

### 0.4.4 Diagram and Visual Strategy

Mermaid diagrams are included by default; at least six are planned across the suite:

- **Component diagram** (`architecture.md`): core, web, Blazor, plugins, and Site hosts with their dependency edges.
- **Data-flow / sequence diagram** (`architecture.md`): a record CRUD request through controller → manager → `Db*` repository → PostgreSQL, including the `ResponseModel` envelope.
- **Plugin-lifecycle diagram** (`architecture.md`): startup registration and application of dated migration partials.
- **Entity-relationship diagram** (`database-schema.md`): the meta-model entities (entity, field, relation, record) and representative domain tables.
- **Authentication-flow sequence diagram** (`security-quality.md`): the hybrid JWT-or-cookie authorization path.
- **Roadmap diagram** (`modernization-roadmap.md`): the three-phase modernization sequence.

Existing screenshots in `doc-images/*.png` will be referenced where they clarify UI workflows; no new screenshots are produced by this analysis-only task.


## 0.5 Documentation File Transformation Mapping

Every documentation file is listed below with its transformation mode and source. All ten artifacts are net-new CREATE operations — the target directory `docs/reverse-engineering/` does not exist today, so there are no UPDATE or DELETE operations on documentation. Nothing is left "pending" or "to be discovered."

### 0.5.1 File-by-File Documentation Plan

| Target Documentation File | Transformation | Source Code/Docs | Content/Changes |
|---------------------------|----------------|------------------|-----------------|
| docs/reverse-engineering/README.md | CREATE | Entire suite (synthesis) | Master index, recommended reading order, glossary, acronyms, and generation timestamp linking all nine artifacts |
| docs/reverse-engineering/code-inventory.md | CREATE | WebVella.ERP3.sln, all 20 .csproj, global.json, full source tree | Narrative module catalog (core, web, Blazor, console, 7 plugins, 7 Site hosts) with per-module LOC and purpose |
| docs/reverse-engineering/code-inventory.csv | CREATE | All ~1,295 source files (.cs/.cshtml/.razor/.js) | Per-file rows under the exact header `Module Name,File Path,Language,Dependencies,Lines of Code,Last Modified,Primary Purpose,Complexity Score` |
| docs/reverse-engineering/architecture.md | CREATE | WebVella.Erp/Api, Eql/, Hooks/, Jobs/, Recurrence/, Fts/; WebVella.Erp.Web Middleware/Controllers/Pages; Site Startup.cs; plugin ._.cs | Layered architecture narrative + component, data-flow, and plugin-lifecycle Mermaid diagrams |
| docs/reverse-engineering/database-schema.md | CREATE | WebVella.Erp/Database, plugin dated partials, EntityManager | ERD, schema-by-domain, patch-class migration history; distinguishes meta-model from physical tables |
| docs/reverse-engineering/data-dictionary.csv | CREATE | WebVella.Erp/Database, FieldTypes/, EntityManager, plugin entity definitions | Per-column rows under the exact header `Table Name,Column Name,Data Type,Key Type,Nullable,Default Value,Description,Constraints` |
| docs/reverse-engineering/functional-overview.md | CREATE | 7 plugins, Site hosts, jira-stories/STORY-*.md, doc-images/*.png | Module catalog, role/permission map, business workflows, and screen references |
| docs/reverse-engineering/business-rules.md | CREATE | Plugin Services/Hooks/Jobs, core manager validation, AuthorizeAttribute/SecurityManager, FieldTypes/ | 50+ catalogued rules (validation, process, integrity, calculation, authorization) each with a file:line citation |
| docs/reverse-engineering/security-quality.md | CREATE | WebVella.Erp.Web/Security, SecurityManager, all .csproj (dependency/CVE audit) | Auth/authz model, dependency vulnerability audit, and complexity/maintainability metrics |
| docs/reverse-engineering/modernization-roadmap.md | CREATE | Synthesis of all artifacts + infrastructure reality | Current-state assessment and an exactly-three-phase modernization plan calibrated to the .NET 9 baseline |

### 0.5.2 New Documentation Files Detail

Representative detail for the most structurally complex deliverables:

```
File: docs/reverse-engineering/architecture.md
Type: Architecture documentation
Source Code: WebVella.Erp/Api/, WebVella.Erp.Web/ (Middleware, Controllers, Pages), Site/Startup.cs
Sections:
    - Overview (entity-centric, plugin-driven platform on ASP.NET Core 9)
    - Layered architecture (Site host -> Web -> Core -> Database -> PostgreSQL)
    - Manager layer (EntityManager, RecordManager, SecurityManager, ...)
    - EQL read path (RecordManager + EqlCommand, Irony.NetCore parser)
    - Plugin model and dated migration partials
Diagrams:
    - Component diagram (modules + dependency edges)
    - Sequence diagram (record CRUD through the stack)
    - Plugin-lifecycle diagram (startup + patch application)
Key Citations: WebVella.Erp/Api/EntityManager.cs, WebVella.Erp/Api/RecordManager.cs
```

```
File: docs/reverse-engineering/database-schema.md
Type: Database schema documentation
Source Code: WebVella.Erp/Database/, plugin dated partials, EntityManager
Sections:
    - Meta-model (entity, field, relation, record) vs physical tables
    - Schema by domain (CRM, Project, Mail, Approval)
    - Migration history reconstructed from date-versioned partial classes
Diagrams:
    - Entity-relationship diagram
Key Citations: WebVella.Erp/Database/DbEntity.cs, WebVella.Erp/Database/DbEntityRelation.cs
```

### 0.5.3 Documentation Configuration Updates

No documentation-tooling configuration is created or modified. The repository has no `mkdocs.yml`, `docusaurus.config.js`, `.readthedocs.yml`, or DocFX configuration, and none is required because the deliverables are plain Markdown/CSV/Mermaid that render natively on GitHub. Adopting a documentation site generator is noted as an optional modernization item, not part of this scope.

### 0.5.4 Cross-Documentation Dependencies

- The README index links to all nine other artifacts and hosts the shared glossary and acronym list.
- `code-inventory.md` and `code-inventory.csv` share module names that must match the module taxonomy used in `architecture.md` and `functional-overview.md`.
- `database-schema.md` (table/column names) must stay consistent with `data-dictionary.csv`.
- `business-rules.md` references entities and endpoints described in `functional-overview.md` and `architecture.md`.
- `modernization-roadmap.md` synthesizes findings from `security-quality.md`, `architecture.md`, and `code-inventory.md`; terminology must be consistent across all files.


## 0.6 Dependency Inventory

This task adds no production dependencies. Two dependency views are relevant: the optional tooling that can render or lint the deliverables, and the verified subject-system packages that the suite will document (especially in the security/quality audit).

### 0.6.1 Documentation Tooling Dependencies

All tools below are OPTIONAL — the deliverables are plain Markdown/CSV/Mermaid that render natively on GitHub with zero mandatory build step. They are listed for authors who wish to render diagrams to images, lint Markdown, or recompute LOC.

| Registry | Package Name | Version | Purpose |
|----------|--------------|---------|---------|
| npm | @mermaid-js/mermaid-cli | 11.15.0 | Render Mermaid diagrams to SVG/PNG (optional; requires Node ^18.19 \|\| >=20) |
| NuGet (dotnet tool) | docfx | 2.78.5 | Optional Roslyn-based C# API extraction / static doc site for .NET 9 |
| npm | markdownlint-cli2 | 0.22.1 | Optional Markdown linting of the suite |
| npm / perl | cloc | 2.08 | Optional line-of-code counting for inventory metrics |

### 0.6.2 Subject-System Dependencies to be Documented

These are the verified production package versions (from the dependency manifests) that the suite reports on; none are changed by this task. This is the authoritative input to the dependency/CVE audit in `security-quality.md`.

| Registry | Package Name | Version | Purpose |
|----------|--------------|---------|---------|
| NuGet | Npgsql | 9.0.4 | PostgreSQL ADO.NET data provider — the DAL foundation (no EF Core) |
| NuGet | AutoMapper | 14.0.0 (pinned) | Object-to-object mapping in the data layer |
| NuGet | Irony.NetCore | 1.1.11 | Grammar/parser backbone for EQL (Entity Query Language) |
| NuGet | CsvHelper | 33.1.0 | CSV import/export |
| NuGet | Ical.Net | 4.3.1 (pinned) | Calendar/recurrence support |
| NuGet | Newtonsoft.Json | 13.0.4 | JSON serialization across core, web, and host |
| NuGet | Storage.Net | 9.3.0 | Storage abstraction |
| NuGet | System.Drawing.Common | 9.0.10 | Image handling |
| NuGet | Microsoft.Extensions.* | 9.0.10 | Caching, configuration, hosting, logging abstractions |
| NuGet | Microsoft.CodeAnalysis.CSharp.Scripting | 4.14.0 | Roslyn scripting for dynamic data sources |
| NuGet | CS-Script | 4.11.2 | Dynamic C# script execution |
| NuGet | HtmlAgilityPack | 1.12.4 | HTML parsing |
| NuGet | Microsoft.AspNetCore.Mvc.NewtonsoftJson | 9.0.10 | MVC JSON formatter |
| NuGet | Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation | 9.0.10 | Runtime Razor compilation |
| NuGet | Wangkanai.Detection | 8.20.0 | Device/browser detection |
| NuGet | WebVella.TagHelpers | 1.7.2 | Platform UI tag helpers |
| NuGet | System.IdentityModel.Tokens.Jwt | 8.14.0 | JWT token handling |
| NuGet | Microsoft.AspNetCore.Authentication.JwtBearer | 9.0.10 | JWT bearer authentication (host) |
| NuGet | Microsoft.Web.LibraryManager.Build | 3.0.71 | Client-library management via libman.json |
| NuGet | morelinq | 4.4.0 | LINQ extensions (host) |

Front-end libraries are host-bundled rather than NPM-managed: Bootstrap 4, jQuery, Font Awesome, StencilJs (under `wwwroot/js/wv-lazyload/`), and js-cookie. There is no `package.json` and no Angular/React.

### 0.6.3 Documentation Reference Updates

Not applicable. Because no documentation exists under the target directory today, there are no pre-existing internal links to rewrite. All cross-links are authored fresh and resolve within `docs/reverse-engineering/`.


## 0.7 Coverage and Quality Targets

Coverage and quality are measured against the verified codebase so the suite can be objectively judged complete.

### 0.7.1 Documentation Coverage Metrics

- **Inventory coverage:** ≥95% of the ~1,295 primary source files (703 `.cs`, 400 `.cshtml`, 11 `.razor`, 181 `.js`) appear as rows in `code-inventory.csv`. Every one of the 20 `.csproj` modules and the 7 plugins is represented in `code-inventory.md`.
- **Architecture coverage:** every top-level project (core, web, Blazor, console, 7 plugins, 7 Site hosts) is placed in the component diagram and described in `architecture.md`.
- **Database coverage:** the meta-model entities and each domain's representative tables are captured in the ERD and `data-dictionary.csv`; the migration history covers all 25 date-versioned plugin partial classes.
- **Business-rule coverage:** ≥50 rules catalogued, each traceable to a `path:line` citation.
- **Functional coverage:** 100% of the 7 plugins documented with purpose, entities, and workflows.
- **Target:** these targets follow the requirement to comprehensively reverse-engineer the system; gaps that cannot be resolved from source are explicitly listed rather than omitted.

### 0.7.2 Documentation Quality Criteria

- **Completeness:** each API/module entry has a purpose, key types/members, and relationships; each guide has overview, detail, and (where relevant) workflow; each architecture topic has a diagram and rationale.
- **Accuracy:** every technical claim resolves to source — file paths, type names, route strings, and versions are quoted from the codebase, not assumed. Assumptions contradicted by the code (C1–C5) are corrected, not propagated.
- **Clarity:** technically precise but accessible prose, progressive disclosure (overview → detail), and consistent terminology aligned to the README glossary.
- **Maintainability:** inline `path:line` citations provide traceability; a generation timestamp and the source commit context are recorded in the README so the suite can be regenerated.

### 0.7.3 Example and Diagram Requirements

- **Diagrams:** at least six Mermaid diagrams (component, record-CRUD sequence, plugin lifecycle, ERD, authentication-flow sequence, roadmap), all in fenced `mermaid` blocks.
- **Examples:** short, illustrative code excerpts (2–3 lines) drawn directly from the source, used to clarify the manager API, the EQL read path, and the `ResponseModel` envelope.
- **Tables:** endpoints, columns, parameters, dependencies, and rule catalogs are presented as tables for scanability.
- **Freshness:** screenshots are referenced from the existing `doc-images/*.png`; no UI is re-captured because this is an analysis-only task.


## 0.8 Scope Boundaries

This is an analysis-and-documentation task. The only files created live under `docs/reverse-engineering/`; the rest of the repository is read-only input.

### 0.8.1 Exhaustively In Scope

- **New documentation files (created):**
  - `docs/reverse-engineering/README.md`
  - `docs/reverse-engineering/code-inventory.md`
  - `docs/reverse-engineering/code-inventory.csv`
  - `docs/reverse-engineering/architecture.md`
  - `docs/reverse-engineering/database-schema.md`
  - `docs/reverse-engineering/data-dictionary.csv`
  - `docs/reverse-engineering/functional-overview.md`
  - `docs/reverse-engineering/business-rules.md`
  - `docs/reverse-engineering/security-quality.md`
  - `docs/reverse-engineering/modernization-roadmap.md`
  - `docs/reverse-engineering/**/*` (any supporting Mermaid/image assets generated under the suite directory)
- **Read-only analysis inputs (examined, never modified):**
  - `WebVella.ERP3.sln`, all `*.csproj`, `global.json`
  - `WebVella.Erp/**`, `WebVella.Erp.Web/**`, `WebVella.Erp.WebAssembly/**`, `WebVella.Erp.ConsoleApp/**`
  - `WebVella.Erp.Plugins.*/**` (Approval, Crm, Mail, MicrosoftCDM, Next, Project, SDK)
  - `WebVella.Erp.Site*/**` (all 7 Site hosts), `web.config`, `Config.json`, `appsettings*.json`
  - `jira-stories/STORY-*.md`, `doc-images/*.png`, `README.md`, `LICENSE.txt`
  - `blitzy/documentation/**` (style reference only)

### 0.8.2 Explicitly Out of Scope

- **All source-code modifications.** No `.cs`, `.cshtml`, `.razor`, `.js`, or `.css` file is edited; no docstrings or comments are added to code (the requirement is analysis-only).
- **All schema and migration changes.** The date-versioned plugin partial classes are read, never altered; no tables are created or migrated.
- **All configuration and build changes.** `Config.json`, `web.config`, `appsettings*.json`, `.csproj`, `global.json`, and `create-nuget-pkgs.bat` are inputs only.
- **Test files.** No test is added or modified.
- **Feature work and refactoring.** No behavior change of any kind; the C1–C5 modernization items are documented as recommendations, not implemented.
- **Infrastructure.** No Dockerfile, CI workflow, or deployment script is introduced (their absence is documented as a roadmap opportunity).
- **Documentation outside the target directory.** The existing root `README.md`, `blitzy/**`, and `docs/developer/**` are not edited; the suite gets its own README under `docs/reverse-engineering/`.


## 0.9 Execution Parameters

Because the deliverables are plain Markdown/CSV/Mermaid, there is no mandatory build. The commands below are optional aids for rendering and validating the suite locally.

### 0.9.1 Build, Preview, and Validation Commands

| Action | Command | Notes |
|--------|---------|-------|
| Build (default) | _none required_ | GitHub renders Markdown, tables, and Mermaid natively |
| Preview locally | `npx markdown-preview docs/reverse-engineering/` or open in any Markdown viewer | Optional |
| Render diagrams to images | `mmdc -i <file>.md -o <file>.svg` | Optional; requires `@mermaid-js/mermaid-cli` 11.15.0 and Node ≥18.19 |
| Lint Markdown | `markdownlint-cli2 "docs/reverse-engineering/**/*.md"` | Optional; markdownlint-cli2 0.22.1 |
| Recompute LOC metrics | `cloc --by-file .` | Optional; cloc 2.08 |
| Optional API doc site | `docfx` | Optional; DocFX 2.78.5 supports .NET 9 |

### 0.9.2 Authoring Conventions

- **Default format:** GitHub-Flavored Markdown with Mermaid diagrams; CSV for the two tabular deliverables using the exact user-specified headers.
- **Citation requirement:** every technical claim cites its source as `path:line` (or `path` for whole-file references) so the documentation is independently verifiable against the codebase.
- **Style guide:** follow the structural and tabular conventions already present in `blitzy/documentation/Technical Specifications.md` and `Project Guide.md` (used as REFERENCE only), and keep terminology consistent with the README glossary.
- **Validation:** confirm all internal links resolve within the suite, all Mermaid blocks parse, both CSVs keep their exact headers, and ≥50 business rules and ≥6 diagrams are present before completion.
- **Determinism:** record a generation timestamp and source context in the README so the suite is reproducible.


## 0.10 Rules for Documentation

No separate user-specified implementation rules were provided (the rules input was empty). The governing constraints therefore come directly from the task requirements and are restated here as binding directives for the documentation work:

- **Analysis-only — zero code modification.** Read and document the system; never alter source, schema, configuration, build, or test files.
- **Write only to the target directory.** All output lands under `docs/reverse-engineering/`; no other path is created or edited.
- **Document the system as it actually is.** Report verified facts; correct the contradicted assumptions (Razor/Blazor not Angular/React; .NET 9 not .NET 8; custom `Db*` DAL not EF Core; patch-class schema not EF Migrations; IIS/no-CI not Docker) instead of repeating them.
- **Cite every technical claim** with an inline `path:line` reference for traceability.
- **Include Mermaid diagrams by default** for architecture, data flow, plugin lifecycle, ERD, authentication, and the roadmap.
- **Preserve the user-provided CSV headers exactly** for `code-inventory.csv` and `data-dictionary.csv`.
- **Catalog ≥50 business rules** and provide narrative executive summaries suitable for stakeholders.
- **Produce exactly three modernization phases**, calibrated to the verified .NET 9 baseline.
- **Follow existing documentation style** from `blitzy/documentation/**` as a REFERENCE without modifying it.


## 0.11 Attachments

No attachments were provided with this task.

- **File attachments:** none. The `review_attachments` input returned "No attachments found for this project."
- **Figma designs:** none provided; the Figma analysis and Design System Alignment protocols are not triggered for this task.
- **External URLs supplied by the user:** none.

All source material for the documentation suite is the repository itself. In-repo assets used as analysis inputs or style references (not user attachments) include `blitzy/documentation/Technical Specifications.md`, `blitzy/documentation/Project Guide.md`, `jira-stories/STORY-001.md` through `STORY-009.md`, and the screenshots under `doc-images/*.png`.


