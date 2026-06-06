# WebVella ERP — Reverse-Engineering Documentation Suite

_Generated: 2026-06-06T03:04:44Z (UTC)_

**WebVella ERP** is a free and open-source, plugin-driven business-application platform targeting **ASP.NET Core 9** with **PostgreSQL 16** as its database of choice. This suite is a read-only, reverse-engineered reference for the platform's core library `WebVella.Erp` **v1.7.4** (Apache-2.0), produced to support modernization planning, developer onboarding, and architectural decision-making.

> **Scope note:** Every artifact in this folder was derived **exclusively from read-only analysis** of the existing solution (`WebVella.ERP3.sln`). **No production code, configuration, or schema was modified.** The documents describe the system **as it actually exists today**; forward-looking statements appear only in the modernization roadmap and are clearly framed as recommendations.

## Executive Summary

WebVella ERP is a classic **layered architecture** (Sites → Web → Core) wrapped in a **plugin-extensibility model**, spanning roughly **1,315 primary source files** across 20 projects (18 target `net9.0`; the two Blazor WebAssembly Server/Shared projects target the out-of-support `net7.0`). Data access uses a **custom Npgsql data layer** — raw, parameterized SQL with a dynamic entity/record model serialized as JSON — **not Entity Framework Core**. The presentation tier is built from **Razor Pages + ERP TagHelpers, Blazor WebAssembly, and plain JavaScript** page-builder components — **not** Angular, React, or TypeScript (the repository contains zero `.ts` files). The database schema is created from **code-embedded PostgreSQL DDL** plus **date-versioned plugin patch methods** (for example, `Patch20190123`) — there is **no EF Migrations folder and no `.sql` files anywhere**. The platform is deployed as plain ASP.NET Core host sites on **IIS (InProcess)**; **no Docker artifacts exist** in the repository today (containerization is offered only as a roadmap recommendation).

## Documents in this suite

The suite comprises **seven narrative documents**, **two CSV data exports**, and this master index. Begin with [`code-inventory.md`](code-inventory.md) — it is the foundational coverage map whose module names and file paths the other documents reuse verbatim.

- **[`code-inventory.md`](code-inventory.md)** — Narrative inventory of the codebase: per-module file/LOC and dependency tables, functional grouping into the shared module taxonomy (Core / Web / WebAssembly / ConsoleApp / 7 Plugins / 7 Sites), and a project-reference dependency tree built from the `.csproj` manifests. This is the **foundational coverage map** for the entire suite.
- **[`code-inventory.csv`](code-inventory.csv)** — Per-file inventory with columns `Module, File Path, Language, Dependencies, LOC, Last Modified, Primary Purpose, Complexity Score`. It catalogs **≥95%** of the ~1,315 in-scope primary files (703 `.cs`, 400 `.cshtml`, 11 `.razor`, 181 `.js`, 20 `.csproj`), one row per file.
- **[`architecture.md`](architecture.md)** — The as-built architecture: the layered + plugin-extensibility model and `AddErp`/`UseErp` composition root, the EQL → SQL data path (Irony parser → parameterized Npgsql → JSON record materialization), the JWT-or-Cookie hybrid authentication flow, and the page-builder render lifecycle. Includes **six Mermaid diagrams** (component, data-flow, two sequences, middleware pipeline, deployment topology).
- **[`database-schema.md`](database-schema.md)** — The schema reconstructed from code, since no SQL or migration files exist: **17 fixed system tables** created via embedded DDL, plus the **dynamic entity meta-model** in which user- and plugin-defined entities and fields are stored as JSON records. Includes a Mermaid ERD and the chronological plugin patch/version history.
- **[`data-dictionary.csv`](data-dictionary.csv)** — Per-column data dictionary with columns `Table, Column, Data Type, Key Type, Nullable, Default, Description, Constraints`, covering all 17 fixed system tables. Its table and column names align exactly with the ERD in `database-schema.md`.
- **[`functional-overview.md`](functional-overview.md)** — The ERP module catalog for the **seven plugins** (CRM, Project, Mail, Next, MicrosoftCDM, SDK, Approval), the platform capabilities of Core + Web, functional workflows derived from the service classes, and the user-role and security model seeded by the system. Module names match the architecture and inventory documents.
- **[`business-rules.md`](business-rules.md)** — A catalog of **more than 50 business rules** (76 in total) across five categories — Validation, Process/Workflow, Data Integrity, Calculation/KPI, and Authorization. Each rule is cited to a real file, class, and method in the source tree.
- **[`security-quality.md`](security-quality.md)** — The security and quality assessment, covering vulnerability findings such as runtime C# compilation as an RCE surface, insecure deserialization via Newtonsoft `TypeNameHandling`, plaintext secrets in host config, and overly permissive CORS. It also includes a dependency/CVE audit, code-quality and complexity metrics, and a compliance posture measured against an ASVS-style baseline.
- **[`modernization-roadmap.md`](modernization-roadmap.md)** — A factual current-state assessment (strengths, technical debt, and a risk matrix) followed by a target-state vision and a **three-phase modernization roadmap** — Stabilize & De-risk, Decompose & Harden, then Modernize & Operationalize — informed by industry practice (Strangler Fig, modular monolith / Clean Architecture / DDD, .NET LTS cadence, containerization, and security hardening). All recommendations are framed as future options, not existing state.

## Scope & Method

- **Read-only analysis.** Every document derives from inspection of `WebVella.ERP3.sln` and its source tree. No `.cs`, `.cshtml`, `.razor`, `.js`, `.csproj`, `.sln`, or `.json` file was edited, moved, or renamed; the existing `docs/`, `blitzy/`, `jira-stories/`, root `README.md`, and `doc-images/` are untouched.
- **Factual reporting.** The suite describes the system **as built**. Aspirational content is confined to `modernization-roadmap.md` and is explicitly labeled as recommendation.
- **Citation discipline.** Every claim about the existing system resolves to a real file, class, or method, so the documents double as a navigation aid into the source.
- **Schema from code.** Because the repository has no migration files, the database schema is extracted from the embedded `CREATE TABLE` DDL in `WebVella.Erp/ERPService.cs`, the `WebVella.Erp/Database/**` builders, and the plugin `ProcessPatches()` patch methods.

## How to navigate

- **Start with [`code-inventory.md`](code-inventory.md)** for the big-picture map of modules and files; its taxonomy anchors every other document.
- **Architecture and data flow:** [`architecture.md`](architecture.md), then [`database-schema.md`](database-schema.md) and [`data-dictionary.csv`](data-dictionary.csv) for the persisted model.
- **Behavior and features:** [`functional-overview.md`](functional-overview.md) and [`business-rules.md`](business-rules.md).
- **Risk and forward planning:** [`security-quality.md`](security-quality.md) feeds directly into [`modernization-roadmap.md`](modernization-roadmap.md).

## Shared module taxonomy

The following names are used identically across every document in this suite:

| Group | Projects |
|-------|----------|
| Core | `WebVella.Erp` |
| Web | `WebVella.Erp.Web` |
| WebAssembly | `WebVella.Erp.WebAssembly` |
| ConsoleApp | `WebVella.Erp.ConsoleApp` |
| Plugins (7) | Approval, Crm, Mail, MicrosoftCDM, Next, Project, SDK (`WebVella.Erp.Plugins.*`) |
| Sites (7) | `WebVella.Erp.Site`, `.Site.Sdk`, `.Site.Project`, `.Site.Next`, `.Site.Mail`, `.Site.Crm`, `.Site.MicrosoftCDM` |

## Four accuracy corrections honored throughout

The original assignment carried four technology assumptions that do not match the actual codebase. Every document in this suite honors the verified reality:

1. **Custom ORM / data layer** — raw, parameterized Npgsql SQL with a JSON record model — **not** Entity Framework Core.
2. **Razor Pages + ERP TagHelpers + Blazor WebAssembly + plain JavaScript** — **not** Angular, React, or TypeScript (0 `.ts` files).
3. **Code-embedded PostgreSQL DDL + dated plugin patch methods** — **not** an EF Migrations folder (none exists).
4. **No Docker present** — containerization appears only as a modernization recommendation.

---

_License: the core `WebVella.Erp` library is distributed under **Apache-2.0**. See the repository [`LICENSE.txt`](../../LICENSE.txt) and [`LIBRARIES.md`](../../LIBRARIES.md) for third-party attributions. Project homepage: [webvella.com](https://webvella.com)._
