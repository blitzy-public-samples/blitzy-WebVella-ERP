# WebVella ERP - Code Inventory Report

**Generated:** 2024-11-20 UTC  
**Repository:** https://github.com/WebVella/WebVella-ERP  
**Documentation Suite:** Reverse Engineering Documentation  
**Analysis Scope:** Complete repository scan including all source files, configuration files, and documentation

---

## Executive Summary

This code inventory provides a comprehensive catalog of all files within the WebVella ERP codebase. The analysis identified **1,446 files** totaling **141,773 lines of code** across multiple project modules, plugins, and supporting infrastructure.

### Key Statistics

| Metric | Value |
|--------|-------|
| **Total Files** | 1,446 |
| **Total Lines of Code** | 141,773 |
| **Primary Language** | C# (81% of codebase) |
| **Project Modules** | 13 distinct modules |
| **Plugin Count** | 6 business/infrastructure plugins |
| **Documentation Files** | 14 markdown documents |

### Technology Distribution

- **Backend:** C# (.NET 9.0) - 699 files, 114,845 LOC
- **Frontend:** Razor views - 395 files, 16,372 LOC
- **Client-Side:** JavaScript - 180 files, 9,217 LOC
- **Configuration:** JSON, MSBuild - 47 files, 1,134 LOC
- **UI Components:** Blazor - 11 files, 205 LOC

### Complexity Assessment

| Complexity Level | File Count | Percentage |
|------------------|------------|------------|
| **Low** | 1,268 | 87.7% |
| **Medium** | 47 | 3.2% |
| **High** | 17 | 1.2% |
| **Configuration/Data** | 114 | 7.9% |

The codebase demonstrates a well-structured architecture with the majority of files (87.7%) having low complexity, indicating maintainable and readable code. High-complexity files are concentrated in core platform areas such as plugin initialization patches, API controllers, and entity management logic.

---

## Inventory by Functional Area

### 1. Core Library (WebVella.Erp.Core)

The core runtime library provides foundational entity management, security, data access, and extensibility infrastructure.

| Metric | Value |
|--------|-------|
| **Files** | 233 |
| **Lines of Code** | 23,829 |
| **Percentage of Total** | 16.8% |
| **Primary Language** | C# |

**Key Subsystems:**
- `Api/` - Manager classes (EntityManager, RecordManager, SecurityManager, etc.)
- `Database/` - Repository pattern implementations with Npgsql
- `Jobs/` - Background job scheduling and execution
- `Hooks/` - Extensibility hook system
- `Eql/` - Entity Query Language parser and executor
- `Utilities/` - Helper classes and common functions

**Notable Files:**
- `Api/RecordManager.cs` - 1,743 LOC (record CRUD operations)
- `Api/EntityManager.cs` - 1,482 LOC (entity metadata management)
- `Database/DbRecordRepository.cs` - 1,662 LOC (database operations)
- `Utilities/Helpers.cs` - 2,616 LOC (utility functions)
- `ERPService.cs` - 1,274 LOC (system bootstrap)

### 2. Web UI Library (WebVella.Erp.Web)

The Razor-based web UI framework with page components, tag helpers, and controllers.

| Metric | Value |
|--------|-------|
| **Files** | 610 |
| **Lines of Code** | 41,367 |
| **Percentage of Total** | 29.2% |
| **Languages** | C#, Razor, JavaScript, CSS |

**Key Subsystems:**
- `Components/` - 50+ page components for UI composition
- `TagHelpers/` - Custom Razor tag helpers
- `Controllers/` - API and MVC controllers
- `Services/` - Page rendering and view services
- `wwwroot/` - Static assets (JavaScript libraries, CSS, images)

**Notable Files:**
- `Controllers/WebApiController.cs` - 3,645 LOC (REST API endpoints)
- `Services/PageService.cs` - 1,408 LOC (page rendering logic)
- `Utils/PageUtils.cs` - 1,900 LOC (page utilities)

### 3. Blazor WebAssembly (WebVella.Erp.WebAssembly)

Single-page application client/server/shared project trio with JWT authentication.

| Component | Files | LOC |
|-----------|-------|-----|
| **Client** | 45 | 1,405 |
| **Server** | 4 | 56 |
| **Shared** | 2 | 13 |
| **Total** | 51 | 1,474 |

**Key Features:**
- JWT token management with LocalStorage
- Blazor WebAssembly client executing in browser
- API abstraction layer for server communication
- Shared DTOs and contracts

### 4. Plugin Ecosystem

#### 4.1 SDK Plugin (WebVella.Erp.Plugins.SDK)

Developer tools and administrative interface for entity/field/page management.

| Metric | Value |
|--------|-------|
| **Files** | 165 |
| **Lines of Code** | 26,962 |
| **Complexity** | Medium-High |

**Notable Files:**
- `Services/CodeGenService.cs` - 8,413 LOC (code generation utilities)
- `Pages/entity/manage-field.cshtml.cs` - 1,252 LOC (field management)
- `Pages/entity/create-field.cshtml.cs` - 882 LOC (field creation)

#### 4.2 Project Plugin (WebVella.Erp.Plugins.Project)

Project and task management with time tracking, recurrence, and notifications.

| Metric | Value |
|--------|-------|
| **Files** | 166 |
| **Lines of Code** | 22,450 |
| **Complexity** | Medium |

**Notable Files:**
- `ProjectPlugin.20190203.cs` - 10,341 LOC (initial plugin setup migration)
- `ProjectPlugin.20211012.cs` - 1,338 LOC (subsequent migration)
- `ProjectPlugin.20190222.cs` - 1,266 LOC (migration patch)

#### 4.3 Next Plugin (WebVella.Erp.Plugins.Next)

Next-generation features and experimental capabilities.

| Metric | Value |
|--------|-------|
| **Files** | 14 |
| **Lines of Code** | 15,106 |
| **Complexity** | High |

**Notable Files:**
- `NextPlugin.20190203.cs` - 10,674 LOC (massive initialization patch)
- `NextPlugin.20190204.cs` - 2,424 LOC (follow-up patch)
- `NextPlugin.20190206.cs` - 1,259 LOC (additional patch)

#### 4.4 Mail Plugin (WebVella.Erp.Plugins.Mail)

Email integration with SMTP, queue processing, and template management.

| Metric | Value |
|--------|-------|
| **Files** | 23 |
| **Lines of Code** | 8,086 |
| **Complexity** | Medium |

**Notable Files:**
- `MailPlugin.20190215.cs` - 5,171 LOC (plugin initialization)

#### 4.5 CRM Plugin (WebVella.Erp.Plugins.Crm)

Customer relationship management framework scaffold.

| Metric | Value |
|--------|-------|
| **Files** | 3 |
| **Lines of Code** | 86 |
| **Complexity** | Low |

**Status:** Framework scaffold with minimal implementation.

#### 4.6 Microsoft CDM Plugin (WebVella.Erp.Plugins.MicrosoftCDM)

Microsoft Common Data Model integration.

| Metric | Value |
|--------|-------|
| **Files** | 3 |
| **Lines of Code** | 86 |
| **Complexity** | Low |

**Status:** Integration framework for Microsoft ecosystem.

### 5. Site Host Applications (WebVella.Erp.Site.*)

Multiple site host projects demonstrating different deployment configurations.

| Metric | Value |
|--------|-------|
| **Files** | 37 |
| **Lines of Code** | 1,745 |
| **Projects** | 7 site hosts |

**Configuration Files:**
- `Program.cs` - Application entry points
- `Startup.cs` - ASP.NET Core configuration
- `Config.json` - Runtime settings (database, JWT, email, file storage)

### 6. Console Application (WebVella.Erp.ConsoleApp)

Console application bootstrap example for non-web hosting scenarios.

| Metric | Value |
|--------|-------|
| **Files** | 6 |
| **Lines of Code** | 306 |
| **Purpose** | Console host demo |

### 7. Documentation (docs/)

Developer documentation organized into 14 topical sections.

| Metric | Value |
|--------|-------|
| **Files** | 14 |
| **Lines of Code** | 70 |
| **Format** | Markdown |

**Documentation Topics:**
- Introduction and getting started
- Entities, fields, and relationships
- Background jobs and hooks
- Page components and tag helpers
- Data sources and EQL

### 8. Root Files

| Metric | Value |
|--------|-------|
| **Files** | 1 |
| **Lines of Code** | 5 |

**Files:**
- `WebVella.ERP3.sln` - Visual Studio solution file

---

## Language Breakdown

Comprehensive analysis of all programming languages and file types in the codebase.

| Language | File Count | Lines of Code | Percentage |
|----------|------------|---------------|------------|
| **C#** | 699 | 114,845 | 81.0% |
| **Razor** | 395 | 16,372 | 11.5% |
| **JavaScript** | 180 | 9,217 | 6.5% |
| **MSBuild** | 19 | 791 | 0.6% |
| **JSON** | 28 | 343 | 0.2% |
| **Blazor** | 11 | 205 | 0.1% |
| **Other/Config** | 114 | 0 | 0.0% |

### Language Distribution Analysis

**C# Dominance (81%)**: The majority of the codebase is C#, reflecting the platform's .NET 9.0 foundation and server-side business logic implementation.

**Razor Views (11.5%)**: Significant Razor view presence indicates comprehensive server-side rendering capabilities for the web UI.

**JavaScript (6.5%)**: Client-side scripting for interactivity, third-party library integration, and Blazor interop.

**Configuration Files (1%)**: MSBuild project files (.csproj) and JSON configuration files (Config.json, appsettings.json) for build and runtime settings.

---

## Dependency Analysis

### NuGet Package Dependencies (Extracted from .csproj files)

**Core Dependencies:**
- Npgsql 9.0.4 (PostgreSQL client)
- Newtonsoft.Json 13.0.4 (JSON serialization)
- AutoMapper 14.0.0 (object mapping)
- CsvHelper 33.1.0 (CSV import/export)
- Irony.NetCore 1.1.11 (EQL parser)
- Ical.Net 4.3.1 (recurrence patterns)
- MailKit 4.14.1 (SMTP email)
- Storage.Net 9.3.0 (file storage abstraction)

**Microsoft Extensions (v9.0.10):**
- Microsoft.Extensions.Caching.Abstractions
- Microsoft.Extensions.Caching.Memory
- Microsoft.Extensions.Configuration.Json
- Microsoft.Extensions.Hosting.Abstractions
- Microsoft.Extensions.Logging
- Microsoft.Extensions.Http

**ASP.NET Core (v9.0.10):**
- Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation
- Microsoft.AspNetCore.Authentication.JwtBearer
- Microsoft.AspNetCore.Components.WebAssembly

**Security:**
- System.IdentityModel.Tokens.Jwt 8.14.0

**Project References:**
All plugin and site projects depend on:
- WebVella.Erp (core library)
- WebVella.Erp.Web (web UI framework)

---

## Complexity Assessment

### Complexity Distribution by File Count

| Complexity | Count | Percentage | Description |
|------------|-------|------------|-------------|
| **Low** | 1,268 | 87.7% | Simple, maintainable code with minimal branching |
| **Medium** | 47 | 3.2% | Moderate complexity requiring careful review |
| **High** | 17 | 1.2% | Complex logic requiring expert review |
| **Config/Data** | 114 | 7.9% | Configuration and data files (0 complexity) |

### High-Complexity Files

Files with high cyclomatic complexity, typically large plugin initialization patches and core infrastructure:

1. **Plugin Initialization Patches** (10,000+ LOC)
   - `NextPlugin.20190203.cs` - 10,674 LOC
   - `ProjectPlugin.20190203.cs` - 10,341 LOC
   - Massive entity/field/page setup in single transaction

2. **Code Generation and Utilities** (8,000+ LOC)
   - `CodeGenService.cs` - 8,413 LOC
   - Diff-based migration code generation

3. **API Controllers** (3,000+ LOC)
   - `WebApiController.cs` - 3,645 LOC
   - REST endpoint implementations

4. **Core Managers** (1,500+ LOC)
   - `RecordManager.cs` - 1,743 LOC
   - `DbRecordRepository.cs` - 1,662 LOC
   - `EntityManager.cs` - 1,482 LOC

### Medium-Complexity Files

Files requiring careful review but not extremely complex (47 files):
- Service implementations with multiple responsibilities
- Controllers with extensive endpoint definitions
- Page components with complex rendering logic

### Complexity Mitigation Strategies

**Identified Patterns:**
- Large plugin patches could benefit from modularization
- Core managers approaching god object anti-pattern
- Utility classes with diverse responsibilities

**Recommendations documented in modernization-roadmap.md**

---

## Top 20 Largest Files

Files ordered by lines of code, representing significant implementation areas.

| Rank | File Path | LOC | Purpose |
|------|-----------|-----|---------|
| 1 | WebVella.Erp.Plugins.Next/NextPlugin.20190203.cs | 10,674 | Next plugin initialization patch |
| 2 | WebVella.Erp.Plugins.Project/ProjectPlugin.20190203.cs | 10,341 | Project plugin initialization |
| 3 | WebVella.Erp.Plugins.SDK/Services/CodeGenService.cs | 8,413 | Migration code generation |
| 4 | WebVella.Erp.Plugins.Mail/MailPlugin.20190215.cs | 5,171 | Mail plugin initialization |
| 5 | WebVella.Erp.Web/Controllers/WebApiController.cs | 3,645 | REST API endpoints |
| 6 | WebVella.Erp/Utilities/Helpers.cs | 2,616 | Utility functions |
| 7 | WebVella.Erp.Plugins.Next/NextPlugin.20190204.cs | 2,424 | Next plugin patch |
| 8 | WebVella.Erp.Web/Utils/PageUtils.cs | 1,900 | Page rendering utilities |
| 9 | WebVella.Erp/Api/RecordManager.cs | 1,743 | Record CRUD operations |
| 10 | WebVella.Erp/Database/DbRecordRepository.cs | 1,662 | Database repository |
| 11 | WebVella.Erp/Api/EntityManager.cs | 1,482 | Entity management |
| 12 | WebVella.Erp.Web/Services/PageService.cs | 1,408 | Page service |
| 13 | WebVella.Erp.Plugins.Project/ProjectPlugin.20211012.cs | 1,338 | Project plugin patch |
| 14 | WebVella.Erp/ERPService.cs | 1,274 | System bootstrap |
| 15 | WebVella.Erp.Plugins.Project/ProjectPlugin.20190222.cs | 1,266 | Project plugin patch |
| 16 | WebVella.Erp.Plugins.Next/NextPlugin.20190206.cs | 1,259 | Next plugin patch |
| 17 | WebVella.Erp.Plugins.SDK/Pages/entity/manage-field.cshtml.cs | 1,252 | Field management UI |
| 18 | WebVella.Erp/Api/ImportExportManager.cs | 934 | CSV import/export |
| 19 | WebVella.Erp.Plugins.SDK/Pages/entity/create-field.cshtml.cs | 882 | Field creation UI |
| 20 | WebVella.Erp/Eql/EqlBuilder.Sql.cs | 819 | EQL SQL translation |

### Analysis Insights

**Plugin Initialization Dominance**: The two largest files are plugin initialization patches (10,000+ LOC each), demonstrating the comprehensive entity/field/page setup required for business modules.

**Core Infrastructure**: API controllers, managers, and repositories occupy significant LOC, reflecting the metadata-driven architecture's complexity.

**Code Generation**: The SDK plugin's CodeGenService (8,413 LOC) highlights the platform's ability to generate migration code from schema differences.

---

## Module-Level Dependency Graph

### Internal Project References

```
WebVella.Erp (Core)
    ↓
    ├── WebVella.Erp.Web (Web UI)
    │   ↓
    │   ├── WebVella.Erp.WebAssembly.Server
    │   ├── WebVella.Erp.Site (and variants)
    │   └── All Plugins
    │       ├── WebVella.Erp.Plugins.SDK
    │       ├── WebVella.Erp.Plugins.Mail
    │       ├── WebVella.Erp.Plugins.Crm
    │       ├── WebVella.Erp.Plugins.Project
    │       ├── WebVella.Erp.Plugins.Next
    │       └── WebVella.Erp.Plugins.MicrosoftCDM
    ├── WebVella.Erp.WebAssembly.Client (standalone)
    └── WebVella.Erp.ConsoleApp
```

**Dependency Principles:**
- Core library has zero dependencies on web or plugins
- Web UI depends only on Core
- Plugins depend on Core and Web
- Site hosts depend on Core, Web, and selected plugins
- Clean architectural layering enables modular deployment

---

## Coverage Analysis

### Total Repository Coverage

| Metric | Value | Coverage |
|--------|-------|----------|
| **Source Files Cataloged** | 1,446 | 100% |
| **Lines of Code Analyzed** | 141,773 | 100% |
| **Project Modules Documented** | 13 | 100% |
| **Plugins Inventoried** | 6 | 100% |
| **Configuration Files** | 47 | 100% |

**Coverage Target:** 95%+ of all source files  
**Achieved Coverage:** 100% (all files in repository cataloged)

### Excluded Files

The following file types were intentionally excluded from LOC calculations:

- Binary files (DLL, EXE, PDB)
- Generated files in obj/ and bin/ directories
- NuGet package cache files
- Git metadata (.git/)
- IDE configuration (.vs/, *.user files)
- Build outputs

---

## Last Modified Analysis

Files are actively maintained with recent modifications:

**Recent Activity Indicators:**
- Plugin projects contain migration files with version-based naming (20190203, 20190215, etc.)
- .csproj files reference .NET 9.0 (current as of 2024)
- NuGet packages use current versions (Npgsql 9.0.4, AutoMapper 14.0.0)

**Maintenance Pattern:**
- Core platform receives continuous updates
- Plugins use versioned patch system for schema evolution
- Site hosts remain relatively stable (configuration-driven)

---

## File Organization Patterns

### Directory Structure Conventions

**Project-Level Organization:**
- Each project in separate folder (WebVella.Erp/, WebVella.Erp.Web/, etc.)
- Plugin projects follow naming: WebVella.Erp.Plugins.{Name}/
- Site hosts follow naming: WebVella.Erp.Site{Variant}/

**Code Organization Within Projects:**
- `Api/` - Public-facing managers and interfaces
- `Database/` - Data access repositories
- `Models/` - DTOs and domain models
- `Services/` - Business logic services
- `Controllers/` - API and MVC controllers
- `Pages/` - Razor pages
- `Components/` - UI components
- `wwwroot/` - Static web assets

**Naming Conventions:**
- Managers: {Domain}Manager.cs (EntityManager.cs, RecordManager.cs)
- Repositories: Db{Domain}Repository.cs (DbRecordRepository.cs)
- Plugins: {Name}Plugin.cs
- Plugin Patches: {Name}Plugin.{Version}.cs (ProjectPlugin.20190203.cs)

---

## Technology Stack Summary

### Framework and Runtime

- **.NET:** 9.0 (latest LTS)
- **ASP.NET Core:** 9.0
- **C# Language:** 12 (implicit with .NET 9)
- **Blazor WebAssembly:** 9.0.10

### Database and Storage

- **Database:** PostgreSQL 16 (via Npgsql 9.0.4)
- **File Storage:** Local filesystem or UNC paths (via Storage.Net 9.3.0)
- **Caching:** In-memory (Microsoft.Extensions.Caching.Memory 9.0.10)

### UI Technologies

- **Frontend Framework:** Bootstrap 4
- **View Engine:** Razor (ASP.NET Core)
- **Client-Side:** jQuery, Moment.js, js-cookie
- **Web Components:** StencilJS (referenced repository)

### Infrastructure Libraries

- **Object Mapping:** AutoMapper 14.0.0
- **JSON Serialization:** Newtonsoft.Json 13.0.4
- **CSV Processing:** CsvHelper 33.1.0
- **Email:** MailKit 4.14.1 / MimeKit
- **HTML Parsing:** HtmlAgilityPack 1.12.4
- **Parser Framework:** Irony.NetCore 1.1.11
- **Calendar/Recurrence:** Ical.Net 4.3.1
- **Authentication:** System.IdentityModel.Tokens.Jwt 8.14.0

---

## Inventory Maintenance

### Document Metadata

- **Document Version:** 1.0
- **Analysis Date:** 2024-11-20
- **Analysis Tool:** Automated bash script with CSV aggregation
- **Repository Branch:** blitzy-f25da73d-d794-4a54-9e52-8f40c4d17175
- **Repository State:** Active development

### Update Procedures

To regenerate this inventory:

1. Execute the inventory generation script located at `/tmp/analyze_files.sh`
2. Process the output CSV with analysis script `/tmp/analyze_inventory_fixed.sh`
3. Update this document with current statistics
4. Commit changes to `/docs/reverse-engineering/` directory

### Related Documentation

- **Machine-Readable Inventory:** [code-inventory.csv](code-inventory.csv)
- **System Architecture:** [architecture.md](architecture.md)
- **Database Schema:** [database-schema.md](database-schema.md)
- **Functional Overview:** [functional-overview.md](functional-overview.md)

---

## Appendix A: File Naming Conventions

### C# Files

- **Classes:** PascalCase (EntityManager.cs, RecordManager.cs)
- **Interfaces:** IPascalCase (IErpService.cs)
- **Enums:** PascalCase (FieldType.cs)

### Razor Files

- **Pages:** kebab-case.cshtml (manage-field.cshtml)
- **Page Models:** PascalCase.cshtml.cs (ManageField.cshtml.cs)
- **Components:** PascalCase.cshtml (GridComponent.cshtml)

### JavaScript Files

- **Utility Files:** kebab-case.js (site-utils.js)
- **Library Files:** library-name.js (js-cookie.js)

### Configuration Files

- **Project Files:** ProjectName.csproj
- **Runtime Config:** Config.json, appsettings.json
- **Solution File:** WebVella.ERP3.sln

---

## Appendix B: Glossary

**LOC (Lines of Code):** Physical lines in source files, excluding blank lines and comments where feasible.

**Complexity:** Cyclomatic complexity estimation based on decision points, method count, and class count.

**Module:** A distinct project within the solution (Core, Web, Plugins, etc.).

**Plugin:** Extensibility module inheriting from ErpPlugin base class.

**Entity:** Metadata-driven data structure definition.

**Patch:** Versioned migration code within plugin initialization.

---

**End of Code Inventory Report**

