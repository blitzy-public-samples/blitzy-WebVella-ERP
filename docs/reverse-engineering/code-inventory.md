# WebVella ERP — Code Inventory (Narrative Module Catalog & Metrics)

> **Part of the [Reverse-Engineering / As-Built Documentation Suite](./README.md).** This document is the **prose companion** to the per-file [`code-inventory.csv`](./code-inventory.csv): it describes *what exists* at the module level — the solution layout, target frameworks, per-module purpose and size, the NuGet dependency tree, and the complexity-scoring convention used by the CSV. The CSV holds the authoritative per-file rows; this narrative makes them interpretable. All terminology, module names, and baseline facts align to the canonical definitions in the suite [`README.md`](./README.md).

---

## Executive Summary

**WebVella ERP** is a large, **entity-centric, plugin-driven** business-application platform built on **ASP.NET Core 9** over **PostgreSQL 16** (root `README.md:18`). Rather than modelling business objects as compile-time C# classes, the platform stores entities, fields, and relations *as data* in a meta-model and generates physical tables at runtime — so the codebase is dominated by a small set of very large "manager" and "repository" engine files plus a wide fan-out of feature plugins and host shells.

The solution `WebVella.ERP3.sln` comprises **20 `.csproj` projects** (`WebVella.ERP3.sln`), grouped into a **core** library, a **web** layer, a three-project **Blazor** client, a **console** harness, **7 feature plugins**, and **7 ASP.NET Core Site hosts**. Verified primary-source totals (excluding `bin/`, `obj/`, and `.git/`) are:

| Metric | Verified Count | How Counted |
|--------|----------------|-------------|
| C# source (`.cs`) | **703** | `find . -name '*.cs'` excluding `bin/obj/.git` |
| Razor views (`.cshtml`) | **400** | `find . -name '*.cshtml'` excluding `bin/obj/.git` |
| Blazor components (`.razor`) | **11** | `find . -name '*.razor'` excluding `bin/obj/.git` |
| JavaScript (`.js`) | **181** | `find . -name '*.js'` excluding `bin/obj/.git` |
| **Primary source files (sum)** | **~1,295** | `.cs` + `.cshtml` + `.razor` + `.js` |
| Project files (`.csproj`) | **20** | `find . -name '*.csproj'` excluding `bin/obj` |
| Markdown (`.md`) | **~143** | repository baseline, excluding this generated suite |

Two facts shape the rest of this inventory and correct common assumptions about the platform (see the suite [`README.md`](./README.md) §Requirement-vs-Reality Corrections):

- **The runtime is already .NET 9, not .NET 8.** 18 of the 20 projects target `net9.0`; only the two Blazor WebAssembly back-end projects still target `net7.0`. `global.json` exists but its `sdk.version` entry is **commented out**, so **no SDK version is pinned** (`global.json:3`). This corrects assumption **C2** — there is no ".NET 8 upgrade" to recommend.
- **The frontend is server-rendered Razor + Blazor + jQuery, not an SPA.** UI assets are host-bundled (Bootstrap 4, jQuery, Font Awesome, StencilJs, js-cookie); there is **no `package.json`** and **no** Angular/React anywhere in the tree. This corrects assumption **C1**.

The per-file inventory — module, language, dependencies, lines of code, last-modified, primary purpose, and a complexity score — lives in the companion [`code-inventory.csv`](./code-inventory.csv) under the user-provided header `Module Name,File Path,Language,Dependencies,Lines of Code,Last Modified,Primary Purpose,Complexity Score`. The [Complexity-Score Methodology](#5-complexity-score-methodology) section below documents exactly how that score is derived so the CSV is self-interpretable.

---

## Generation Metadata

| Field | Value |
|-------|-------|
| **Documentation Generated** | 2026-06-05 15:15 UTC |
| **Source commit** | `bfe15661c7f0c1dae57288d789b854186793b157` |
| **Branch** | `master` |
| **Solution analyzed** | `WebVella.ERP3.sln` (20 projects) |
| **Analysis method** | Static reverse-engineering of the source tree (file enumeration + `wc -l` line counts + `.csproj`/`.sln`/`global.json` parsing) |
| **Companion data file** | [`code-inventory.csv`](./code-inventory.csv) (per-file rows) |
| **Output location** | `docs/reverse-engineering/` (this directory only) |
| **Citation convention** | Inline `path:line` (e.g., `WebVella.Erp/Api/RecordManager.cs:120`) or whole-file `path` |

> **Reproducibility.** Line counts are physical lines (`wc -l`) measured at the pinned commit; re-running the same enumeration against commit `bfe15661` yields the same figures. Counts exclude build output (`bin/`, `obj/`) and the `.git/` directory throughout.

---

## 1. Solution and Module Taxonomy

The solution file `WebVella.ERP3.sln` declares **18 project references** plus **2 solution folders** (`Resources` and `Solution Items`) used purely for IDE organization (`WebVella.ERP3.sln`). On disk there are **20 `.csproj` files**: the difference is the Blazor client, which is physically **three** projects (`Client`, `Server`, `Shared`) while the solution references only the `Client` project as the build entry point. Counting every `.csproj` on disk yields the canonical **20 projects** below.

These are the **canonical module names** for the entire suite; [`architecture.md`](./architecture.md), [`functional-overview.md`](./functional-overview.md), and [`code-inventory.csv`](./code-inventory.csv) use exactly these names (see the suite [`README.md`](./README.md) §Module Taxonomy).

| Group | Project(s) | Count | Role |
|-------|-----------|-------|------|
| **Core platform** | `WebVella.Erp` | 1 | Entity meta-model, the manager layer (`WebVella.Erp/Api/`), the EQL engine (`WebVella.Erp/Eql/`), the custom data-access layer (`WebVella.Erp/Database/`), hooks, jobs, recurrence, FTS, notifications, diagnostics |
| **Web application** | `WebVella.Erp.Web` | 1 | Versioned REST controllers (`/api/v3.0/...`), middleware, Razor Pages, page components, tag helpers, and the `Security/` constructs |
| **Blazor client** | `WebVella.Erp.WebAssembly` (Client / Server / Shared) | 3 | Blazor WebAssembly client and its supporting server/shared projects (the two `net7.0` projects in the solution) |
| **Console harness** | `WebVella.Erp.ConsoleApp` | 1 | Console host for bootstrap/maintenance tasks |
| **Plugins** | `WebVella.Erp.Plugins.{Approval, Crm, Mail, MicrosoftCDM, Next, Project, SDK}` | 7 | Feature modules — services, hooks, jobs, components, controllers, and (for six of them) dated migration partials |
| **Site hosts** | `WebVella.Erp.Site{, .Crm, .Mail, .MicrosoftCDM, .Next, .Project, .Sdk}` | 7 | ASP.NET Core host shells, each wiring DI, authentication, plugin registration, and `Config.json` |
| | **Total** | **20** | |

> **Note on the Data-Access Layer.** The custom `Db*` DAL (`DbContext`, `DbConnection`, `DbEntity`, `DbEntityRelation`, `DbRecordRepository`, `DbRepository`, `FieldTypes/`) is **not** a separate project — it lives inside the Core platform under `WebVella.Erp/Database/`. It is documented as a distinct *sub-area* of Core in [§3.1](#31-core-platform--webvellaerp) and in [`database-schema.md`](./database-schema.md), but it counts toward the single `WebVella.Erp` project for the 20-project total.

The Core project's identity and version come from its `.csproj`: `PackageId` is `WebVella.Erp` (`WebVella.Erp/WebVella.Erp.csproj:7`) and `Version` is `1.7.4` (`WebVella.Erp/WebVella.Erp.csproj:11`); the Web project is versioned `1.7.5` (`WebVella.Erp.Web/WebVella.Erp.Web.csproj:8`).

---

## 2. Target Framework Matrix

Every `.csproj` declares a single `<TargetFramework>`. The verified breakdown is **18 of 20 targeting `net9.0`** and **2 targeting `net7.0`** — the two Blazor WebAssembly back-end projects.

| # | Project (`.csproj`) | Target Framework |
|---|---------------------|------------------|
| 1 | `WebVella.Erp/WebVella.Erp.csproj` | `net9.0` |
| 2 | `WebVella.Erp.Web/WebVella.Erp.Web.csproj` | `net9.0` |
| 3 | `WebVella.Erp.WebAssembly/Client/WebVella.Erp.WebAssembly.csproj` | `net9.0` |
| 4 | `WebVella.Erp.WebAssembly/Server/WebVella.Erp.WebAssembly.Server.csproj` | **`net7.0`** |
| 5 | `WebVella.Erp.WebAssembly/Shared/WebVella.Erp.WebAssembly.Shared.csproj` | **`net7.0`** |
| 6 | `WebVella.Erp.ConsoleApp/WebVella.Erp.ConsoleApp.csproj` | `net9.0` |
| 7 | `WebVella.Erp.Plugins.Approval/WebVella.Erp.Plugins.Approval.csproj` | `net9.0` |
| 8 | `WebVella.Erp.Plugins.Crm/WebVella.Erp.Plugins.Crm.csproj` | `net9.0` |
| 9 | `WebVella.Erp.Plugins.Mail/WebVella.Erp.Plugins.Mail.csproj` | `net9.0` |
| 10 | `WebVella.Erp.Plugins.MicrosoftCDM/WebVella.Erp.Plugins.MicrosoftCDM.csproj` | `net9.0` |
| 11 | `WebVella.Erp.Plugins.Next/WebVella.Erp.Plugins.Next.csproj` | `net9.0` |
| 12 | `WebVella.Erp.Plugins.Project/WebVella.Erp.Plugins.Project.csproj` | `net9.0` |
| 13 | `WebVella.Erp.Plugins.SDK/WebVella.Erp.Plugins.SDK.csproj` | `net9.0` |
| 14 | `WebVella.Erp.Site/WebVella.Erp.Site.csproj` | `net9.0` |
| 15 | `WebVella.Erp.Site.Crm/WebVella.Erp.Site.Crm.csproj` | `net9.0` |
| 16 | `WebVella.Erp.Site.Mail/WebVella.Erp.Site.Mail.csproj` | `net9.0` |
| 17 | `WebVella.Erp.Site.MicrosoftCDM/WebVella.Erp.Site.MicrosoftCDM.csproj` | `net9.0` |
| 18 | `WebVella.Erp.Site.Next/WebVella.Erp.Site.Next.csproj` | `net9.0` |
| 19 | `WebVella.Erp.Site.Project/WebVella.Erp.Site.Project.csproj` | `net9.0` |
| 20 | `WebVella.Erp.Site.Sdk/WebVella.Erp.Site.Sdk.csproj` | `net9.0` |
| | **`net9.0` total** | **18** |
| | **`net7.0` total** | **2** |

### 2.1 SDK pin — `global.json` (critical fact, corrects C2)

The repository contains a `global.json`, but its SDK version is **commented out**. The file in its entirety is:

```jsonc
{
  "sdk": {
    //"version": "7.0.103"
  }
}
```

The `//"version": "7.0.103"` line is a **comment** (`global.json:3`), so `global.json` pins **no active SDK version** — the build resolves to the latest installed .NET SDK. The **effective target is `net9.0`**, as declared by the 18 `net9.0` `.csproj` files above, **not** the stale `7.0.103` string in the comment. Any reader or tool must **not** report `7.0.103` as the active SDK.

This is the evidentiary basis for **correction C2**: WebVella ERP already runs on **.NET 9**, so the modernization roadmap is calibrated to a .NET 9 baseline and **never** recommends a ".NET 8 upgrade" (see [`modernization-roadmap.md`](./modernization-roadmap.md)). The two residual `net7.0` projects are flagged there as a runtime-hygiene item, since `net7.0` is out of support.

---

## 3. Per-Module Catalog

Each subsection states the module's **purpose**, its **key directories/files**, and its **size**. Line-of-code (LOC) figures are physical lines (`wc -l`) at commit `bfe15661`. The per-module `.cs` counts below sum to the solution-wide **703** `.cs`, the `.cshtml` to **400**, the `.razor` to **11**, and the `.js` to **181**, providing an internal cross-check against the [Executive Summary](#executive-summary) totals.

### 3.1 Core platform — `WebVella.Erp`

The foundational library: it defines the entity meta-model, the manager layer that mediates all entity/record/relation/security operations, the EQL query engine, the custom data-access layer, and the cross-cutting subsystems (hooks, jobs, recurrence, full-text search, notifications, diagnostics). The Core project holds **232 `.cs` files (~30,587 LOC)** and targets `net9.0` (`WebVella.Erp/WebVella.Erp.csproj:4`). Its top-level subdirectories are `Api/`, `Database/`, `Diagnostics/`, `Eql/`, `Exceptions/`, `Fts/`, `Hooks/`, `Jobs/`, `Notifications/`, `Recurrence/`, and `Utilities/`.

**Manager layer (`WebVella.Erp/Api/`).** The platform's behavioral core — and the largest concentration of hand-written control-flow logic. Verified file sizes:

| File | LOC | Primary Purpose |
|------|----:|-----------------|
| `WebVella.Erp/Api/RecordManager.cs` | 2,109 | CRUD + EQL read path for records; the busiest engine class |
| `WebVella.Erp/Api/EntityManager.cs` | 1,873 | Meta-model entity/field definition and lifecycle |
| `WebVella.Erp/Api/ImportExportManager.cs` | 1,106 | Bulk import/export across entities |
| `WebVella.Erp/Api/EntityRelationManager.cs` | 568 | Definition and maintenance of entity relations |
| `WebVella.Erp/Api/DataSourceManager.cs` | 539 | Named, reusable data-source/query definitions |
| `WebVella.Erp/Api/SecurityManager.cs` | 371 | Role/permission management |
| `WebVella.Erp/Api/SearchManager.cs` | 242 | Search orchestration over the FTS subsystem |
| `WebVella.Erp/Api/SecurityContext.cs` | 169 | Ambient security/permission scope for a request |

**Data-access layer (`WebVella.Erp/Database/`).** The custom `Db*` repository layer over **Npgsql** — there is **no Entity Framework Core** (correction C3; see [`architecture.md`](./architecture.md) and [`database-schema.md`](./database-schema.md)). The two largest DAL files are `WebVella.Erp/Database/DbRecordRepository.cs` (**2,097 LOC**) and `WebVella.Erp/Database/DbRepository.cs` (**669 LOC**); the layer also contains `DbContext`, `DbConnection`, `DbEntity`, `DbEntityRelation`, and the `FieldTypes/` converters.

**Query engine (`WebVella.Erp/Eql/`).** The custom **Entity Query Language (EQL)** grammar and parser, built on `Irony.NetCore` (`WebVella.Erp/WebVella.Erp.csproj:50`) and executed on the read path via `RecordManager`/`EqlCommand`.

### 3.2 Web application — `WebVella.Erp.Web`

The HTTP layer: versioned REST controllers, middleware, Razor Pages, page components, tag helpers, and the security constructs. It is the **largest module overall**, with **252 `.cs` files (~36,807 LOC)**, **282 `.cshtml`**, **73 `.js`**, and **2 `.razor`**, targeting `net9.0` (`WebVella.Erp.Web/WebVella.Erp.Web.csproj:4`).

The single largest source file in the entire solution lives here: `WebVella.Erp.Web/Controllers/WebApiController.cs` at **4,313 LOC**, which fronts the versioned `/api/v3.0/...` REST surface documented in [`architecture.md`](./architecture.md). Its size makes it the headline maintainability hotspot in [`security-quality.md`](./security-quality.md) (finding F7). Other key areas include `Middleware/`, `Pages/`, `Components/`, `TagHelpers/`, and `Security/`.

### 3.3 Blazor client — `WebVella.Erp.WebAssembly`

A Blazor WebAssembly client split across **three** physical projects — `Client`, `Server`, and `Shared` — holding **36 `.cs` files (~1,550 LOC)** and **9 `.razor`** components in total. The `Client` project targets `net9.0` (`WebVella.Erp.WebAssembly/Client/WebVella.Erp.WebAssembly.csproj:4`), while **both** the `Server` (`WebVella.Erp.WebAssembly/Server/WebVella.Erp.WebAssembly.Server.csproj:4`) and `Shared` (`WebVella.Erp.WebAssembly/Shared/WebVella.Erp.WebAssembly.Shared.csproj:4`) projects target `net7.0` — the only two `net7.0` projects in the solution.

### 3.4 Console harness — `WebVella.Erp.ConsoleApp`

A small console host for bootstrap and maintenance tasks: **4 `.cs` files (~318 LOC)**, targeting `net9.0` (`WebVella.Erp.ConsoleApp/WebVella.Erp.ConsoleApp.csproj`).

### 3.5 Plugins (7)

The seven feature plugins are independent `WebVella.Erp.Plugins.*` projects, all targeting `net9.0`. **Six** of them (Crm, Mail, MicrosoftCDM, Next, Project, SDK) ship a **`*Plugin._.cs` bootstrap** partial class whose initialization applies that plugin's dated migration partials during startup. **Approval is the exception**: it is a plugin *project* containing **only dashboard code** at this commit — there is **no `ErpPlugin` subclass, no bootstrap, and no migration** (see the suite [`README.md`](./README.md) glossary entry for *plugin*). Verified sizes:

| Plugin | `.cs` | `.cshtml` | `.js` | `.cs` LOC | Notes |
|--------|------:|----------:|------:|----------:|-------|
| Approval | 4 | 5 | 1 | ~917 | Dashboard-only: `Api/DashboardMetricsModel.cs`, `Components/PcApprovalDashboard/PcApprovalDashboard.cs`, `Controllers/ApprovalController.cs`, `Services/DashboardMetricsService.cs`; **no** bootstrap/migration |
| Crm | 3 | 0 | 0 | ~136 | Bootstrap (`CrmPlugin._.cs`) + `CrmPlugin.cs` + `Model/PluginSettings.cs` |
| Mail | 23 | 0 | 0 | ~8,887 | Bootstrap + **7** dated migration partials + services/hooks/jobs |
| MicrosoftCDM | 3 | 0 | 0 | ~139 | Bootstrap (`MicrosoftCDMPlugin._.cs`) + main class + settings |
| Next | 14 | 0 | 0 | ~16,446 | Bootstrap + **5** dated migration partials |
| Project | 45 | 56 | 65 | ~19,243 | **Largest plugin by web assets** (56 `.cshtml`, 65 `.js`); bootstrap + **8** dated migration partials |
| SDK | 69 | 54 | 42 | ~21,142 | **Largest plugin by C#** (69 `.cs`); bootstrap + **5** dated migration partials |

Across the four patch-owning plugins, there are **25 date-versioned migration partial classes** in total (Mail 7, Next 5, Project 8, SDK 5) — e.g., `WebVella.Erp.Plugins.Mail/MailPlugin.20190419.cs` and `WebVella.Erp.Plugins.Project/ProjectPlugin.20211012.cs`. These reconstruct the entire schema-evolution history because the platform has **no Entity Framework `Migrations/` folder** (correction C4; detailed in [`database-schema.md`](./database-schema.md)). Crm and MicrosoftCDM each carry a bootstrap but no dated partials at this commit.

### 3.6 Site hosts (7)

Seven thin ASP.NET Core host-shell projects, each wiring dependency injection, authentication, plugin registration, and a `Config.json`. All target `net9.0`. They are intentionally small — most contain just a `Program.cs`, `Startup.cs`, and configuration glue:

| Site host | `.cs` | `.cshtml` | `.cs` LOC |
|-----------|------:|----------:|----------:|
| `WebVella.Erp.Site` | 6 | 3 | ~461 |
| `WebVella.Erp.Site.Crm` | 2 | 0 | ~152 |
| `WebVella.Erp.Site.Mail` | 2 | 0 | ~152 |
| `WebVella.Erp.Site.MicrosoftCDM` | 2 | 0 | ~159 |
| `WebVella.Erp.Site.Next` | 2 | 0 | ~153 |
| `WebVella.Erp.Site.Project` | 2 | 0 | ~202 |
| `WebVella.Erp.Site.Sdk` | 2 | 0 | ~154 |

The base `WebVella.Erp.Site` is the reference host (`PackageId` `WebVella.Erp.Site`, `WebVella.Erp.Site/WebVella.Erp.Site.csproj:8`) and is the only host that also ships the IIS `web.config` used for InProcess hosting (documented, never modified — see [§7](#7-build-and-packaging)).

---

## 4. NuGet Dependency Tree Summary

The platform adds **no production NuGet dependency beyond those declared in the `.csproj` files**. The table below lists the verified production package versions, grouped by owning project, with the exact `.csproj:line` citation for each. These versions are **authoritative** and feed the dependency/CVE audit in [`security-quality.md`](./security-quality.md); the two suites must agree exactly.

### 4.1 Core — `WebVella.Erp/WebVella.Erp.csproj`

| Package | Version | Citation |
|---------|---------|----------|
| AutoMapper | `[14.0.0]` *(pinned/locked)* | `WebVella.Erp/WebVella.Erp.csproj:47` |
| CsvHelper | 33.1.0 | `WebVella.Erp/WebVella.Erp.csproj:48` |
| Ical.Net | `[4.3.1]` *(pinned/locked)* | `WebVella.Erp/WebVella.Erp.csproj:49` |
| Irony.NetCore | 1.1.11 | `WebVella.Erp/WebVella.Erp.csproj:50` |
| Microsoft.Extensions.Caching.Abstractions | 9.0.10 | `WebVella.Erp/WebVella.Erp.csproj:52` |
| Microsoft.Extensions.Caching.Memory | 9.0.10 | `WebVella.Erp/WebVella.Erp.csproj:53` |
| Microsoft.Extensions.Configuration.Json | 9.0.10 | `WebVella.Erp/WebVella.Erp.csproj:54` |
| Microsoft.Extensions.Hosting.Abstractions | 9.0.10 | `WebVella.Erp/WebVella.Erp.csproj:55` |
| Microsoft.Extensions.Logging | 9.0.10 | `WebVella.Erp/WebVella.Erp.csproj:56` |
| Microsoft.Extensions.Logging.Console | 9.0.10 | `WebVella.Erp/WebVella.Erp.csproj:57` |
| Microsoft.Extensions.Logging.Debug | 9.0.10 | `WebVella.Erp/WebVella.Erp.csproj:58` |
| MimeMapping | 3.1.0 | `WebVella.Erp/WebVella.Erp.csproj:59` |
| Newtonsoft.Json | 13.0.4 | `WebVella.Erp/WebVella.Erp.csproj:60` |
| Npgsql | 9.0.4 | `WebVella.Erp/WebVella.Erp.csproj:61` |
| Storage.Net | 9.3.0 | `WebVella.Erp/WebVella.Erp.csproj:62` |
| System.Drawing.Common | 9.0.10 | `WebVella.Erp/WebVella.Erp.csproj:63` |

> **`Npgsql 9.0.4`** is the foundation of the custom data-access layer; there is **no EF Core** package anywhere (correction C3). **`Irony.NetCore 1.1.11`** is the parser backbone for EQL. AutoMapper and Ical.Net are version-**locked** via the bracket syntax `[x.y.z]`.

### 4.2 Web — `WebVella.Erp.Web/WebVella.Erp.Web.csproj`

| Package | Version | Citation |
|---------|---------|----------|
| Microsoft.CodeAnalysis.CSharp.Scripting | 4.14.0 | `WebVella.Erp.Web/WebVella.Erp.Web.csproj:128` |
| Microsoft.CodeAnalysis.Common | 4.14.0 | `WebVella.Erp.Web/WebVella.Erp.Web.csproj:129` |
| Microsoft.CodeAnalysis.CSharp | 4.14.0 | `WebVella.Erp.Web/WebVella.Erp.Web.csproj:130` |
| Microsoft.CodeAnalysis.CSharp.Workspaces | 4.14.0 | `WebVella.Erp.Web/WebVella.Erp.Web.csproj:131` |
| CS-Script | 4.11.2 | `WebVella.Erp.Web/WebVella.Erp.Web.csproj:132` |
| HtmlAgilityPack | 1.12.4 | `WebVella.Erp.Web/WebVella.Erp.Web.csproj:133` |
| Microsoft.AspNetCore.Mvc.NewtonsoftJson | 9.0.10 | `WebVella.Erp.Web/WebVella.Erp.Web.csproj:134` |
| Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation | 9.0.10 | `WebVella.Erp.Web/WebVella.Erp.Web.csproj:135` |
| Newtonsoft.Json | 13.0.4 | `WebVella.Erp.Web/WebVella.Erp.Web.csproj:138` |
| Wangkanai.Detection | 8.20.0 | `WebVella.Erp.Web/WebVella.Erp.Web.csproj:141` |
| WebVella.TagHelpers | 1.7.2 | `WebVella.Erp.Web/WebVella.Erp.Web.csproj:142` |
| Microsoft.Extensions.FileProviders.Embedded | 9.0.10 | `WebVella.Erp.Web/WebVella.Erp.Web.csproj:143` |
| System.IdentityModel.Tokens.Jwt | 8.14.0 | `WebVella.Erp.Web/WebVella.Erp.Web.csproj:144` |

> **Commented-out references.** `SixLabors.ImageSharp` (3.1.6) and `SixLabors.ImageSharp.Drawing` (2.1.5) are present only as **commented-out** `PackageReference` entries (`WebVella.Erp.Web/WebVella.Erp.Web.csproj:139-140`) and are therefore **not** active dependencies; image handling uses `System.Drawing.Common` from Core instead. The Roslyn `Microsoft.CodeAnalysis.*` 4.14.0 packages and `CS-Script` 4.11.2 power dynamic, script-backed data sources.

### 4.3 Plugin and Blazor dependencies

| Package | Version | Owning project (citation) |
|---------|---------|---------------------------|
| MailKit | 4.14.1 | `WebVella.Erp.Plugins.Mail/WebVella.Erp.Plugins.Mail.csproj:28` |
| Microsoft.AspNetCore.Components.WebAssembly | 9.0.10 | `WebVella.Erp.WebAssembly/Client/WebVella.Erp.WebAssembly.csproj:16` |
| Microsoft.AspNetCore.Components.WebAssembly.DevServer | 9.0.10 | `WebVella.Erp.WebAssembly/Client/WebVella.Erp.WebAssembly.csproj:17` |
| Microsoft.Extensions.Http | 9.0.10 | `WebVella.Erp.WebAssembly/Client/WebVella.Erp.WebAssembly.csproj:18` |
| Microsoft.AspNetCore.Components.WebAssembly.Authentication | 9.0.10 | `WebVella.Erp.WebAssembly/Client/WebVella.Erp.WebAssembly.csproj:19` |
| Blazored.LocalStorage | 4.5.0 | `WebVella.Erp.WebAssembly/Client/WebVella.Erp.WebAssembly.csproj:20` |
| System.IdentityModel.Tokens.Jwt | 8.14.0 | `WebVella.Erp.WebAssembly/Client/WebVella.Erp.WebAssembly.csproj:21` |
| Microsoft.AspNetCore.Components.WebAssembly.Server | 7.0.13 | `WebVella.Erp.WebAssembly/Server/WebVella.Erp.WebAssembly.Server.csproj:10` |

> The Blazor **Client** components are on **9.0.10**; the Blazor **Server** component package is on **7.0.13**, matching the `net7.0` target of that project ([§2](#2-target-framework-matrix)).

### 4.4 Host (Site) dependencies — `WebVella.Erp.Site/WebVella.Erp.Site.csproj`

| Package | Version | Citation |
|---------|---------|----------|
| Microsoft.AspNetCore.Mvc.NewtonsoftJson | 9.0.10 | `WebVella.Erp.Site/WebVella.Erp.Site.csproj:49` |
| Microsoft.Web.LibraryManager.Build | 3.0.71 | `WebVella.Erp.Site/WebVella.Erp.Site.csproj:50` |
| MimeMapping | 3.1.0 | `WebVella.Erp.Site/WebVella.Erp.Site.csproj:53` |
| Newtonsoft.Json | 13.0.4 | `WebVella.Erp.Site/WebVella.Erp.Site.csproj:54` |
| morelinq | 4.4.0 | `WebVella.Erp.Site/WebVella.Erp.Site.csproj:55` |
| Microsoft.AspNetCore.Authentication.JwtBearer | 9.0.10 | `WebVella.Erp.Site/WebVella.Erp.Site.csproj:57` |

> **`Microsoft.Web.LibraryManager.Build` (libman)** restores client-side libraries from `libman.json` at build time — this is how front-end assets are managed in lieu of NPM.

### 4.5 Front-end libraries (host-bundled; corrects C1)

The browser-facing stack is **host-bundled, not NPM-managed**: there is **no `package.json`** anywhere in the repository. The libraries in use are **Bootstrap 4**, **jQuery**, **Font Awesome**, **StencilJs** (the WebVella StencilJs component set referenced from the root `README.md:27`), and **js-cookie**, served from the web project's `wwwroot/` and restored via libman ([§4.4](#44-host-site-dependencies--webvellaerpsitewebvellaerpsitecsproj)). There is **no Angular and no React** — this is the evidentiary basis for **correction C1**. A SPA migration is noted only as an *option* in [`modernization-roadmap.md`](./modernization-roadmap.md), not a present-state fact.

---

## 5. Complexity-Score Methodology

The `Complexity Score` column in [`code-inventory.csv`](./code-inventory.csv) uses a **descriptive complexity band** rather than a numeric cyclomatic-complexity measurement. Because the deliverables are static documents with no build step, the score is a **size-and-structure proxy**: it is derived primarily from **physical lines of code (LOC)** and then **adjusted for file role**. This is the *authoritative* definition for the suite — [`security-quality.md`](./security-quality.md) §4.1 refers back to this section, and its complexity discussion uses these same bands.

### 5.1 Bands

| Band | Physical LOC (baseline) | Typical files |
|------|-------------------------|---------------|
| **Low** | < 200 | Models, settings, simple components, host bootstraps, `SecurityContext.cs` (169) |
| **Moderate** | 200 – 599 | Mid-size managers/services: `SearchManager.cs` (242), `SecurityManager.cs` (371), `DataSourceManager.cs` (539), `EntityRelationManager.cs` (568) |
| **High** | 600 – 1,499 | Large engine files: `DbRepository.cs` (669), `ImportExportManager.cs` (1,106) |
| **Very High** | ≥ 1,500 | The biggest hand-written units: `EntityManager.cs` (1,873), `DbRecordRepository.cs` (2,097), `RecordManager.cs` (2,109), `WebApiController.cs` (4,313) |

### 5.2 Role adjustment

LOC alone over-states the logical complexity of **declarative** files, so the band is adjusted by file role:

- **Declarative seed/migration partials and embedded data files** (e.g., the dated `*Plugin.YYYYMMDD.cs` patch classes) are banded **one level lower** than their raw LOC implies, because they are predominantly entity/field/record *definitions* with little branching. This is why several of the largest files *by line count* (the plugin seed/migration partials) are **not** the highest-complexity files.
- **Control-flow-heavy managers, repositories, and controllers** are kept at their LOC-derived band, because their size co-occurs with dense branching and orchestration. The manager layer (`RecordManager`, `EntityManager`) and `WebApiController` are the genuine **Very High** hotspots and are flagged for decomposition in [`security-quality.md`](./security-quality.md).

The bands are **indicative**, intended to direct attention rather than to assign a precise numeric score; the per-file value in [`code-inventory.csv`](./code-inventory.csv) is the authoritative classification for each individual file.

---

## 6. Inventory Coverage Statement

The companion [`code-inventory.csv`](./code-inventory.csv) targets **≥ 95 %** coverage of the **~1,295** primary source files (703 `.cs` + 400 `.cshtml` + 11 `.razor` + 181 `.js`). Coverage is measured against that primary-source population; the following categories are **deliberately excluded** and are **not** counted as gaps:

| Excluded category | Rationale |
|-------------------|-----------|
| `bin/` and `obj/` | Build output, not source |
| `.git/` | Version-control metadata |
| Generated/restored client libraries under `wwwroot/lib/` (libman) | Third-party, not authored here |
| Binary assets (images under `doc-images/`, fonts, icons) | Not source code |
| Project/solution/config files (`.csproj`, `.sln`, `.json`, `web.config`) | Inventoried structurally in [§1](#1-solution-and-module-taxonomy)/[§2](#2-target-framework-matrix), not as per-file source rows |

Any individual primary-source file that cannot be classified from static analysis is listed explicitly in the CSV rather than omitted, so the document is honest about its own gaps. The per-module `.cs`/`.cshtml`/`.razor`/`.js` counts in [§3](#3-per-module-catalog) sum to the solution-wide totals, which is the primary completeness check for this inventory.

---

## 7. Build and Packaging

WebVella ERP has **no Docker, no `docker-compose`, and no CI workflow**. The only file under `.github/` is `.github/FUNDING.yml`; there is **no `.github/workflows/` directory**. Packaging is performed by a Windows batch script, `create-nuget-pkgs.bat`, which deletes prior packages and runs `nuget pack` against four `.nuspec` manifests:

```bat
del *.nupkg
nuget pack .\WebVella.Erp\WebVella.Erp.nuspec
```

The script packages the Core, Web, SDK-plugin, and Mail-plugin projects (`create-nuget-pkgs.bat:2-5`) and then opens the output folder (`create-nuget-pkgs.bat:6`). Runtime hosting is ASP.NET Core with **IIS InProcess** via `WebVella.Erp.Site/web.config`, and the platform is **tested only on Windows** (root `README.md:18`).

The absence of containerization and CI is recorded as **correction C5** and surfaced as a delivery-engineering opportunity in [`modernization-roadmap.md`](./modernization-roadmap.md); per the analysis-only mandate, this document **reports** the gap without remediating it.

---

## 8. Cross-Document Consistency

This inventory is the structural anchor of the suite, and its facts are reused verbatim elsewhere:

- **Module taxonomy** ([§1](#1-solution-and-module-taxonomy)) is identical to the canonical taxonomy in the suite [`README.md`](./README.md) and is the naming used by [`code-inventory.csv`](./code-inventory.csv), [`architecture.md`](./architecture.md), and [`functional-overview.md`](./functional-overview.md).
- **NuGet versions** ([§4](#4-nuget-dependency-tree-summary)) match exactly the package set audited in [`security-quality.md`](./security-quality.md).
- **The complexity bands** ([§5](#5-complexity-score-methodology)) are the definition that [`security-quality.md`](./security-quality.md) §4.1 refers back to.
- **The C1/C2/C3/C4/C5 corrections** referenced throughout are defined once in the suite [`README.md`](./README.md) and applied consistently here and in [`modernization-roadmap.md`](./modernization-roadmap.md).

Every metric in this document carries an inline `path` or `path:line` citation so it can be verified against the repository at commit `bfe15661`. For per-file detail, read this narrative alongside [`code-inventory.csv`](./code-inventory.csv); for the design that these modules implement, continue to [`architecture.md`](./architecture.md).

---

*Generated 2026-06-05 15:15 UTC from source commit `bfe15661c7f0c1dae57288d789b854186793b157` (branch `master`). This is an analysis-only, reverse-engineering code inventory — no production source, schema, configuration, build, or test file was modified, and all output is confined to `docs/reverse-engineering/`. Every metric carries an inline `path` or `path:line` citation; line counts are physical lines (`wc -l`) measured at the pinned commit.*
