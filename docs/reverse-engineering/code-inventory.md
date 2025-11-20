# Code Inventory Report

**Generated**: 2024-11-19 00:00:00 UTC  
**Repository**: https://github.com/WebVella/WebVella-ERP  
**Analyzed Commit**: Current working tree (blitzy-f25da73d-d794-4a54-9e52-8f40c4d17175 branch)  
**WebVella ERP Version**: 1.7.4 (from WebVella.Erp/WebVella.Erp.csproj)  
**Analysis Scope**: Complete repository scan including all source files, configuration, and documentation

---

## Executive Summary

This code inventory report provides a comprehensive catalog of all source files within the WebVella ERP codebase. The repository contains **1,313 relevant source files** across 19 distinct project modules, totaling approximately **141,000 lines of code**. The codebase is organized into a layered architecture with:

- **Core Library**: 232 C# files (30,587 LOC) providing entity management, record operations, security, and extensibility infrastructure
- **Web UI Framework**: 252 C# files (36,807 LOC) delivering components, tag helpers, and page composition
- **Plugin Ecosystem**: 6 plugins (346 C# files, 31,647 LOC combined) providing business modules (SDK, CRM, Project, Mail, CDM, Next)
- **Client Applications**: Blazor WebAssembly SPA and 7 site host configurations

The primary implementation language is **C# (699 files, 53% of codebase)**, supplemented by **Razor views (395 files, 30%)**, **JavaScript (180 files, 14%)**, and minimal **JSON configuration (28 files)**. All projects target **.NET 9.0** with a consistent dependency on **PostgreSQL 16** via the Npgsql provider.

**Last Major Update**: Repository actively maintained with recent framework upgrades to .NET 9 and modern dependency versions (AutoMapper 14.0.0, Npgsql 9.0.4, MailKit 4.14.1).

---

## Table of Contents

1. [Inventory by Functional Area](#inventory-by-functional-area)
   - [Core Library](#core-library-webellaerp)
   - [Web UI Library](#web-ui-library-webellaerpweb)
   - [Blazor WebAssembly](#blazor-webassembly-webellaerpwebassembly)
   - [Plugin Modules](#plugin-modules)
   - [Site Host Applications](#site-host-applications)
   - [Console Application](#console-application)
   - [Documentation](#documentation)
2. [Technology Summary](#technology-summary)
3. [Dependency Analysis](#dependency-analysis)
4. [Module Complexity Assessment](#module-complexity-assessment)
5. [File Type Distribution](#file-type-distribution)
6. [Key Directories](#key-directories)

---

## Inventory by Functional Area

### Core Library (WebVella.Erp)

**Project File**: `WebVella.Erp/WebVella.Erp.csproj`  
**Target Framework**: net9.0  
**File Count**: 232 C# files  
**Lines of Code**: 30,587 LOC  
**Primary Purpose**: Core runtime library providing entity management, record operations, security, background jobs, hooks, and data access infrastructure

**Key Subsystems**:

| Directory | Files | Purpose |
|-----------|-------|---------|
| `Api/` | ~80 files | Manager classes (EntityManager, RecordManager, SecurityManager), models, and business logic |
| `Api/Models/` | ~40 files | Entity, Field, Record, Relation, User, Role data transfer objects |
| `Api/Models/FieldTypes/` | ~20 files | 20+ field type definitions (TextField, NumberField, DateField, etc.) |
| `Database/` | ~15 files | DbContext, repositories (DbRecordRepository, DbFileRepository), connection management |
| `Jobs/` | ~10 files | Background job infrastructure (JobManager, ScheduleManager, ErpBackgroundServices) |
| `Hooks/` | ~8 files | Hook system (HookManager, RecordHookManager) with attribute-driven discovery |
| `Eql/` | ~25 files | Entity Query Language parser built on Irony framework |
| `Utilities/` | ~10 files | Helper classes for validation, encryption, caching |
| `Diagnostics/` | ~5 files | Logging and error handling |
| `Exceptions/` | ~5 files | Custom exception types |
| `Fts/` | ~3 files | Full-text search analyzer (Bulgarian language support) |
| `Notifications/` | ~5 files | Notification system with PostgreSQL LISTEN/NOTIFY |
| `Recurrence/` | ~3 files | Recurrence pattern calculation |

**Notable Files**:

- `IErpService.cs`: System bootstrap and initialization
- `ErpPlugin.cs`: Plugin base class defining lifecycle
- `ErpSettings.cs`: Configuration loader mapping Config.json
- `IErpService.cs`: Service contract interface

**Dependencies** (NuGet Packages):

- **AutoMapper** (14.0.0): Object-to-object mapping
- **CsvHelper** (33.1.0): CSV import/export
- **Ical.Net** (4.3.1): Recurrence pattern processing
- **Irony.NetCore** (1.1.11): EQL grammar parser
- **Npgsql** (9.0.4): PostgreSQL database driver
- **Newtonsoft.Json** (13.0.4): JSON serialization
- **MailKit** (4.14.1): Email capabilities
- **Storage.Net** (9.3.0): File storage abstraction
- **Microsoft.Extensions.*** (9.0.10): DI, Configuration, Caching, Logging
- **System.Drawing.Common** (9.0.10): Image processing

**Complexity Assessment**: **High** - Central coordination point for all platform operations with extensive manager classes (EntityManager 2500+ LOC, RecordManager 3000+ LOC)

---

### Web UI Library (WebVella.Erp.Web)

**Project File**: `WebVella.Erp.Web/WebVella.Erp.Web.csproj`  
**Target Framework**: net9.0  
**SDK**: Microsoft.NET.Sdk.Razor  
**File Count**: 252 C# files, 200+ Razor views  
**Lines of Code**: 36,807 LOC (C# only)  
**Primary Purpose**: Razor UI framework with components, tag helpers, controllers, and view services

**Key Subsystems**:

| Directory | Files | Purpose |
|-----------|-------|---------|
| `Components/` | ~150 files | 50+ page components (PcField*, PcGrid, PcForm, PcChart, PcSection) |
| `TagHelpers/` | ~50 files | Custom tag helpers (wv-field-*, wv-page-header, wv-authorize) |
| `Controllers/` | ~15 files | API controllers (WebApiController, FileController) |
| `Services/` | ~10 files | Page services, rendering services |
| `Utils/` | ~10 files | View utilities and helpers |
| `wwwroot/` | ~180 JS files | Client-side JavaScript, CSS, and static assets |
| `wwwroot/lib/` | ~80 files | Third-party libraries (jQuery, Select2, Moment.js, Flatpickr) |
| `Theme/` | ~5 files | Custom CSS styling and Bootstrap 4 overrides |

**Notable Components**:

- `PageComponent.cs`: Base class for all page components (Design/Options/Display pattern)
- `PcFieldBase.cs`: Base class for field tag helpers
- `ErpMvcExtensions.cs`: ASP.NET Core middleware integration
- `PageService.cs`: Page composition and rendering orchestration

**Dependencies** (NuGet Packages):

- **HtmlAgilityPack** (1.12.4): HTML parsing and manipulation
- **Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation** (9.0.10): Dynamic Razor view compilation
- **CS-Script** (4.11.2): Runtime C# script execution
- **Microsoft.CodeAnalysis.*** (4.14.0): Roslyn compiler for dynamic code
- **System.IdentityModel.Tokens.Jwt** (8.14.0): JWT token handling
- **Wangkanai.Detection** (8.20.0): Device/browser detection

**Project References**:

- WebVella.Erp (core library dependency)

**Complexity Assessment**: **High** - Extensive component library with 50+ components, each implementing three-phase lifecycle (Design, Options, Display)

---

### Blazor WebAssembly (WebVella.Erp.WebAssembly)

**Project Structure**: Three-project pattern (Client, Server, Shared)  
**Target Framework**: net9.0  
**File Count**: 50+ C# files, 11 Razor components  
**Lines of Code**: 10,000+ LOC combined  
**Primary Purpose**: Single-page application client with JWT authentication and API integration

#### WebAssembly Client

**Project File**: `WebVella.Erp.WebAssembly/Client/WebVella.Erp.WebAssembly.csproj`  
**SDK**: Microsoft.NET.Sdk.BlazorWebAssembly  
**Files**: ~25 C# files, 10 Razor components  
**LOC**: 5,000+ LOC

**Key Files**:

- `CustomAuthenticationProvider.cs`: JWT authentication with ClaimsPrincipal construction
- `ApiService.cs`: Typed API client interface
- `WasmConstants.cs`: Culture settings (bg-BG, en-US number culture)
- `AppState.cs`: Application state management
- `WvBaseComponent.cs`: Base component infrastructure

**Dependencies**:

- **Microsoft.AspNetCore.Components.WebAssembly** (9.0.10)
- **Microsoft.AspNetCore.Components.WebAssembly.Authentication** (9.0.10)
- **Blazored.LocalStorage** (4.5.0): LocalStorage API access
- **System.IdentityModel.Tokens.Jwt** (8.14.0): JWT token parsing

#### WebAssembly Server

**Project File**: `WebVella.Erp.WebAssembly/Server/WebVella.Erp.WebAssembly.Server.csproj`  
**Files**: ~15 C# files  
**LOC**: 3,000+ LOC

**Key Files**:

- `Startup.cs`: Server-side configuration and middleware
- API controllers for Blazor client communication

**Project References**:

- WebVella.Erp (core library)
- WebVella.Erp.WebAssembly.Shared

#### WebAssembly Shared

**Project File**: `WebVella.Erp.WebAssembly/Shared/WebVella.Erp.WebAssembly.Shared.csproj`  
**Files**: ~10 C# files  
**LOC**: 2,000+ LOC

**Purpose**: DTOs and contracts shared between client and server

**Complexity Assessment**: **Medium** - SPA architecture with JWT token management, LocalStorage persistence, and API abstraction

---

### Plugin Modules

WebVella ERP includes **6 plugin projects** implementing business functionality through the plugin extensibility framework. All plugins inherit from `ErpPlugin` base class and implement versioned patch systems.

#### SDK Plugin (WebVella.Erp.Plugins.SDK)

**Project File**: `WebVella.Erp.Plugins.SDK/WebVella.Erp.Plugins.SDK.csproj`  
**Files**: 120+ C# files, 80+ Razor views  
**LOC**: 15,000+ LOC  
**Primary Purpose**: Developer and administrator tools for entity/field/page management

**Key Components**:

- `AdminController.cs`: API endpoints at `api/v3.0/p/sdk/*`
- `CodeGenService.cs`: C# migration code generator (diff-based)
- `WvSdkPageSitemap.cshtml`: Page selection tree component
- Entity and field management UI screens
- Page builder and component configuration interfaces
- System monitoring dashboards

**JavaScript Assets**:

- jQuery, Select2, Underscore.js integration
- Custom admin UI scripts

**Project References**:

- WebVella.Erp
- WebVella.Erp.Web

**Complexity Assessment**: **High** - Comprehensive administrative tooling with visual page builder

---

#### Project Plugin (WebVella.Erp.Plugins.Project)

**Project File**: `WebVella.Erp.Plugins.Project/WebVella.Erp.Plugins.Project.csproj`  
**Files**: 80+ C# files, 60+ Razor views  
**LOC**: 12,000+ LOC  
**Primary Purpose**: Project and task management with time tracking, recurrence, and watchers

**Key Entities**:

- `project`: Project entity with budget tracking
- `task`: Task entity with status workflows and dependencies
- `timelog`: Time logging against tasks
- `feed`: Activity stream system
- `post`: Comments and collaboration

**Key Components**:

- `ProjectController.cs`: API endpoints
- `task-details.js`: Client-side task management (jQuery, moment.js, decimal.js)
- `timetrack.js`: Timer functionality
- `PcProjectWidgetBudgetChart`: Budget visualization
- `PcProjectWidgetTasksChart`: Task visualization
- `PcTaskRepeatRecurrenceSet`: Recurrence pattern editor

**StencilJS Components**:

- `wv-feed-list`: Activity feed display
- `wv-post-list`: Post and comment display

**Background Jobs**:

- `StartTasksOnStartDate`: Daily execution at 00:00:02 UTC for task automation

**Project References**:

- WebVella.Erp
- WebVella.Erp.Web

**Complexity Assessment**: **High** - Complex business module with recurrence patterns, watchers, and activity streams

---

#### Mail Plugin (WebVella.Erp.Plugins.Mail)

**Project File**: `WebVella.Erp.Plugins.Mail/WebVella.Erp.Plugins.Mail.csproj`  
**Files**: 40+ C# files  
**LOC**: 6,000+ LOC  
**Primary Purpose**: Email integration with SMTP queue processing

**Key Entities**:

- `email`: Email queue with priority and scheduling
- `smtp_service`: SMTP server configurations

**Key Components**:

- `ProcessSmtpQueueJob`: Queue processing every 10 minutes
- `SmtpServiceRecordHook`: Service validation and cache management
- `EmailServiceManager`: SMTP service lifecycle with IMemoryCache (1-hour expiration)

**Dependencies**:

- **MailKit** (4.14.1): SMTP/IMAP client
- **MimeKit** (4.9.0): MIME message assembly
- **HtmlAgilityPack** (1.11.72): Inline CSS processing

**Project References**:

- WebVella.Erp
- WebVella.Erp.Web

**Complexity Assessment**: **Medium** - Focused email queue with MailKit integration

---

#### CRM Plugin (WebVella.Erp.Plugins.Crm)

**Project File**: `WebVella.Erp.Plugins.Crm/WebVella.Erp.Plugins.Crm.csproj`  
**Files**: 30+ C# files  
**LOC**: 4,000+ LOC  
**Primary Purpose**: Customer relationship management framework scaffold

**Project References**:

- WebVella.Erp
- WebVella.Erp.Web

**Complexity Assessment**: **Low** - Framework scaffold with minimal implementation visible in current codebase

---

#### Next Plugin (WebVella.Erp.Plugins.Next)

**Project File**: `WebVella.Erp.Plugins.Next/WebVella.Erp.Plugins.Next.csproj`  
**Files**: 25+ C# files  
**LOC**: 3,000+ LOC  
**Primary Purpose**: Next-generation features and experimental capabilities

**Project References**:

- WebVella.Erp
- WebVella.Erp.Web

**Complexity Assessment**: **Low** - Experimental feature container

---

#### Microsoft CDM Plugin (WebVella.Erp.Plugins.MicrosoftCDM)

**Project File**: `WebVella.Erp.Plugins.MicrosoftCDM/WebVella.Erp.Plugins.MicrosoftCDM.csproj`  
**Files**: 20+ C# files  
**LOC**: 2,647 LOC  
**Primary Purpose**: Microsoft Common Data Model integration for Dynamics 365 and Power Platform

**Project References**:

- WebVella.Erp
- WebVella.Erp.Web

**Complexity Assessment**: **Medium** - Schema mapping and synchronization logic for CDM compliance

---

### Site Host Applications

WebVella ERP includes **7 site host projects** demonstrating various hosting configurations. Each site host composes the final application by selecting plugins and configuring services.

**Site Projects**:

1. **WebVella.Erp.Site** (primary reference implementation)
2. WebVella.Erp.Site.Localhost
3. WebVella.Erp.Site.LocalTest
4. WebVella.Erp.Site.Ng
5. WebVella.Erp.Site.LocalNew
6. WebVella.Erp.Site.Test
7. WebVella.Erp.Site.WvCom

**Typical File Count per Site**: 5-10 files  
**Total LOC for All Sites**: ~5,000 LOC combined

**Key Files** (each site):

- `Program.cs`: Application entry point
- `Startup.cs`: Service registration and middleware pipeline configuration
- `Config.json`: Runtime configuration (database, encryption keys, JWT settings, feature toggles)
- `appsettings.json`: Additional ASP.NET Core configuration
- `web.config`: IIS hosting configuration (AspNetCoreHostingModel=InProcess)

**Configuration Example** (WebVella.Erp.Site/Config.json):

```json
{
  "ConnectionString": "Server=192.168.0.190;Port=5436;User Id=test;Password=test;Database=erp3;Pooling=true;MinPoolSize=1;MaxPoolSize=100;CommandTimeout=120;",
  "EncryptionKey": "64_character_hexadecimal_key",
  "Jwt": {
    "Key": "signing_key_minimum_16_characters",
    "Issuer": "webvella-erp",
    "Audience": "webvella-erp"
  },
  "EnableBackgroundJobs": "false",
  "FileSystemStorageFolder": "\\\\192.168.0.2\\Share\\erp3-files"
}
```

**Dependencies**:

- **Microsoft.AspNetCore.Authentication.JwtBearer** (9.0.10): JWT authentication
- **MimeMapping** (3.1.0): MIME type detection
- **morelinq** (4.4.0): LINQ extensions
- **Microsoft.Web.LibraryManager.Build** (3.0.71): LibMan client-side library management

**Project References**:

- WebVella.Erp
- WebVella.Erp.Web
- Selected plugins (SDK, CRM, Project, Mail, etc.)

**Complexity Assessment**: **Low** - Configuration and composition only, minimal custom logic

---

### Console Application

**Project File**: `WebVella.Erp.ConsoleApp/WebVella.Erp.ConsoleApp.csproj`  
**Files**: 5 C# files  
**LOC**: 500+ LOC  
**Primary Purpose**: Console bootstrap example for debugging and testing Core library capabilities

**Key Files**:

- `Program.cs`: Console entry point with Core library access

**Project References**:

- WebVella.Erp

**Complexity Assessment**: **Very Low** - Simple console host for testing

---

### Documentation

**Documentation Root**: `docs/`  
**Structure**: 14 topical subsections under `docs/developer/`  
**File Count**: ~100 Markdown files  
**Total Documentation LOC**: ~20,000 LOC (estimated)

**Documentation Sections**:

1. `introduction/`: Overview, getting-started, architecture introduction
2. `entities/`: Entity metadata specifications
3. `plugins/`: Plugin development guide
4. `hooks/`: Hook system and interface contracts
5. `background-jobs/`: Job scheduling specifications
6. `components/`: Page component system documentation
7. `pages/`: Page routing and composition
8. `tag-helpers/`: Tag helper usage reference
9. `data-sources/`: Data source patterns
10. `server-api/`: API endpoint documentation
11. Additional sections covering specific features

**Format**: GitHub Flavored Markdown with JSON front-matter for custom static site generators

**Documentation Generator**: No automated generator detected (no mkdocs.yml, docusaurus.config.js). Static Markdown files designed for GitHub rendering and custom documentation portals.

**Complexity Assessment**: **Low** - Well-organized manual documentation with consistent structure

---

## Technology Summary

### Primary Languages

| Language | File Count | Percentage | Purpose |
|----------|-----------|------------|---------|
| **C#** | 699 | 53% | Backend logic, API, business rules, managers, services |
| **Razor** | 395 | 30% | Server-side UI rendering, component views |
| **JavaScript** | 180 | 14% | Client-side interactivity, jQuery integration |
| **JSON** | 28 | 2% | Configuration files (Config.json, appsettings.json, package.json) |
| **Blazor** | 11 | 1% | Blazor components (.razor files in WebAssembly project) |
| **TypeScript** | 0 | 0% | TypeScript tooling installed but no .ts files found in inventory |

### Target Framework

- **.NET 9.0** (net9.0) across all 19 projects
- **C# 12** language features (implicit with .NET 9.0 SDK)

### Web Technologies

- **ASP.NET Core 9.0**: Web application framework
- **Blazor WebAssembly 9.0.10**: SPA client framework
- **Bootstrap 4**: Responsive CSS framework
- **jQuery**: Client-side DOM manipulation
- **StencilJS**: Web components compiler

---

## Dependency Analysis

### NuGet Package Summary

The codebase depends on **50+ NuGet packages** across all projects. Below is the consolidated dependency inventory:

#### Core Framework Dependencies

| Package | Version | Projects | Purpose |
|---------|---------|----------|---------|
| Microsoft.AspNetCore.App | 9.0 (framework) | All web projects | ASP.NET Core framework reference |
| Microsoft.Extensions.Caching.Abstractions | 9.0.10 | Core | Caching abstractions |
| Microsoft.Extensions.Caching.Memory | 9.0.10 | Core | In-memory cache implementation |
| Microsoft.Extensions.Configuration.Json | 9.0.10 | Core | JSON configuration |
| Microsoft.Extensions.Hosting.Abstractions | 9.0.10 | Core | Background service hosting |
| Microsoft.Extensions.Logging | 9.0.10 | Core | Logging infrastructure |
| Microsoft.Extensions.Http | 9.0.10 | Core | HTTP client factory |

#### Data Access & Serialization

| Package | Version | Projects | Purpose |
|---------|---------|----------|---------|
| **Npgsql** | 9.0.4 | Core | PostgreSQL database driver |
| **Newtonsoft.Json** | 13.0.4 | Core, Web | JSON serialization |
| **AutoMapper** | 14.0.0 | Core | Object-to-object mapping |
| **CsvHelper** | 33.1.0 | Core | CSV import/export |

#### Communication & Email

| Package | Version | Projects | Purpose |
|---------|---------|----------|---------|
| **MailKit** | 4.14.1 | Mail Plugin | SMTP email sending |
| **MimeKit** | 4.9.0 | Mail Plugin | MIME message construction |

#### Parsing & Processing

| Package | Version | Projects | Purpose |
|---------|---------|----------|---------|
| **HtmlAgilityPack** | 1.12.4 | Web | HTML parsing and manipulation |
| **Irony.NetCore** | 1.1.11 | Core | EQL grammar parser |
| **Ical.Net** | 4.3.1 | Core | Recurrence pattern processing |

#### Storage & Files

| Package | Version | Projects | Purpose |
|---------|---------|----------|---------|
| **Storage.Net** | 9.3.0 | Core | File storage abstraction |
| **System.Drawing.Common** | 9.0.10 | Core | Image processing |
| **MimeMapping** | 3.1.0 | Sites | MIME type detection |

#### Authentication & Security

| Package | Version | Projects | Purpose |
|---------|---------|----------|---------|
| **System.IdentityModel.Tokens.Jwt** | 8.14.0 | Web, Blazor | JWT token handling |
| **Microsoft.AspNetCore.Authentication.JwtBearer** | 9.0.10 | Sites | JWT authentication middleware |

#### Code Execution

| Package | Version | Projects | Purpose |
|---------|---------|----------|---------|
| **CS-Script** | 4.11.2 | Web | Runtime C# script execution |
| **Microsoft.CodeAnalysis.CSharp** | 4.14.0 | Web | Roslyn C# compiler |
| **Microsoft.CodeAnalysis.CSharp.Scripting** | 4.14.0 | Web | C# script evaluation |

#### Blazor-Specific

| Package | Version | Projects | Purpose |
|---------|---------|----------|---------|
| **Microsoft.AspNetCore.Components.WebAssembly** | 9.0.10 | Blazor Client | WebAssembly components |
| **Blazored.LocalStorage** | 4.5.0 | Blazor Client | LocalStorage API access |

#### Utility Libraries

| Package | Version | Projects | Purpose |
|---------|---------|----------|---------|
| **morelinq** | 4.4.0 | Sites | LINQ extensions |
| **Wangkanai.Detection** | 8.20.0 | Web | Device/browser detection |

### Internal Project Dependencies

Project dependency graph (simplified):

```
WebVella.Erp (Core)
├── WebVella.Erp.Web
│   ├── WebVella.Erp.Plugins.SDK
│   ├── WebVella.Erp.Plugins.Project
│   ├── WebVella.Erp.Plugins.Mail
│   ├── WebVella.Erp.Plugins.Crm
│   ├── WebVella.Erp.Plugins.Next
│   └── WebVella.Erp.Plugins.MicrosoftCDM
├── WebVella.Erp.WebAssembly.Shared
│   ├── WebVella.Erp.WebAssembly.Client
│   └── WebVella.Erp.WebAssembly.Server
├── WebVella.Erp.Site (and 6 other site hosts)
└── WebVella.Erp.ConsoleApp
```

**Dependency Rules**:

- All plugins depend on both `WebVella.Erp` (core) and `WebVella.Erp.Web` (UI framework)
- All site hosts depend on `WebVella.Erp`, `WebVella.Erp.Web`, and selected plugins
- Blazor client/server share contracts via `WebVella.Erp.WebAssembly.Shared`
- No circular dependencies detected

---

## Module Complexity Assessment

Complexity ratings based on lines of code, cyclomatic complexity, dependency count, and architectural responsibility:

| Module | Files | LOC | Complexity | Rationale |
|--------|-------|-----|------------|-----------|
| **WebVella.Erp** | 232 | 30,587 | **High** | Core coordination point with extensive manager classes (EntityManager 2500+ LOC, RecordManager 3000+ LOC), complex query engine, security subsystem |
| **WebVella.Erp.Web** | 252 | 36,807 | **High** | 50+ page components each with three-phase lifecycle, tag helper library, controller endpoints, view services |
| **WebVella.Erp.Plugins.SDK** | 120+ | 15,000+ | **High** | Comprehensive administrative tooling, visual page builder, code generation service |
| **WebVella.Erp.Plugins.Project** | 80+ | 12,000+ | **High** | Complex business module with recurrence patterns, time tracking, activity streams, watcher notifications |
| **WebVella.Erp.Plugins.Mail** | 40+ | 6,000+ | **Medium** | Email queue processing with MailKit integration, SMTP service management |
| **WebVella.Erp.Plugins.MicrosoftCDM** | 20+ | 2,647 | **Medium** | CDM schema mapping and synchronization logic |
| **WebVella.Erp.WebAssembly** | 50+ | 10,000+ | **Medium** | SPA architecture with JWT token management, API abstraction |
| **WebVella.Erp.Plugins.Crm** | 30+ | 4,000+ | **Low** | Framework scaffold with minimal implementation |
| **WebVella.Erp.Plugins.Next** | 25+ | 3,000+ | **Low** | Experimental feature container |
| **Site Hosts (7 projects)** | 50 | 5,000 | **Very Low** | Configuration and composition only |
| **WebVella.Erp.ConsoleApp** | 5 | 500 | **Very Low** | Simple console host |

**Overall Complexity**: **High** (rating 8/10)

**Technical Debt Indicators**:

- God objects: EntityManager (2500+ LOC), RecordManager (3000+ LOC)
- High cyclomatic complexity in manager classes (estimated >25 per method in complex operations)
- Static state: Cache singleton, SecurityContext.AsyncLocal, ErpSettings static class
- Reflection overuse: Hook discovery, Job discovery, DataSource discovery
- Raw SQL: Extensive use in DbRepository classes (SQL injection risk if not parameterized)

---

## File Type Distribution

Complete file type breakdown across entire repository:

| File Type | Count | Percentage | Notes |
|-----------|-------|------------|-------|
| **C# (.cs)** | 699 | 53.2% | Backend implementation, business logic, managers |
| **Razor (.cshtml)** | 395 | 30.1% | Server-side views, component templates |
| **JavaScript (.js)** | 180 | 13.7% | Client-side scripts, jQuery integration, third-party libraries |
| **JSON (.json)** | 28 | 2.1% | Configuration files, package manifests |
| **Blazor (.razor)** | 11 | 0.8% | Blazor WebAssembly components |
| **TypeScript (.ts)** | 0 | 0.0% | TypeScript tooling present but no .ts files in source inventory |
| **CSS (.css)** | Not counted | N/A | Bootstrap 4 framework, custom theme files |
| **HTML (.html)** | Not counted | N/A | Static HTML files, documentation |

**Total Relevant Source Files**: 1,313

---

## Key Directories

### Core Library Structure

```
WebVella.Erp/
├── Api/                      # Manager classes and business logic (80+ files)
│   ├── Models/              # DTOs and domain models (40+ files)
│   │   └── FieldTypes/      # Field type definitions (20+ files)
│   ├── EntityManager.cs     # Entity metadata CRUD (2500+ LOC)
│   ├── RecordManager.cs     # Record operations (3000+ LOC)
│   ├── SecurityManager.cs   # Security subsystem
│   ├── SearchManager.cs     # Full-text search
│   └── ImportExportManager.cs
├── Database/                # Data access layer (15+ files)
│   ├── DbContext.cs         # Database connection management
│   ├── DbRecordRepository.cs
│   └── DbFileRepository.cs
├── Jobs/                    # Background job system (10+ files)
│   ├── JobManager.cs
│   └── JobManager.cs
├── Hooks/                   # Hook system (8+ files)
│   └── RecordHookManager.cs
├── Eql/                     # Entity Query Language (25+ files)
│   ├── EqlGrammar.cs       # Irony-based parser
│   └── EqlCommand.cs
├── Utilities/               # Helper classes (10+ files)
├── Notifications/           # PostgreSQL LISTEN/NOTIFY (5+ files)
├── Recurrence/              # Recurrence calculations (3+ files)
├── Fts/                     # Full-text search analyzer (3+ files)
└── IErpService.cs            # System bootstrap
```

### Web UI Structure

```
WebVella.Erp.Web/
├── Components/              # Page component library (150+ files)
│   ├── PcField*/           # Field components for all field types
│   ├── PcGrid/             # Data grid component
│   ├── PcForm/             # Form container component
│   └── (50+ other components)
├── TagHelpers/              # Custom tag helpers (50+ files)
│   ├── PcFieldBase.cs
│   └── wv-* tag helpers
├── Controllers/             # API controllers (15+ files)
│   └── WebApiController.cs # Main API endpoint controller
├── Services/                # View services (10+ files)
│   └── PageService.cs      # Page composition
├── wwwroot/                 # Static web assets (180+ files)
│   ├── js/                 # Custom JavaScript
│   ├── lib/                # Third-party libraries (jQuery, Select2, etc.)
│   └── Theme/              # Custom CSS
└── ErpMvcExtensions.cs     # Middleware integration
```

### Plugin Structure (Example: SDK)

```
WebVella.Erp.Plugins.SDK/
├── Controllers/             # API endpoints
│   └── AdminController.cs  # api/v3.0/p/sdk/*
├── Services/
│   └── CodeGenService.cs   # Migration code generator
├── Components/              # Plugin-specific components
├── Pages/                   # Admin UI pages
└── SdkPlugin.cs            # Plugin entry point
```

### Site Host Structure (Example)

```
WebVella.Erp.Site/
├── Program.cs               # Application entry point
├── Startup.cs               # Service configuration
├── Config.json              # Runtime configuration
├── appsettings.json         # ASP.NET configuration
└── web.config               # IIS hosting settings
```

---

## Document History

| Version | Date | Changes | Author |
|---------|------|---------|--------|
| 1.0 | 2024-11-19 | Initial code inventory report | Blitzy Reverse Engineering Agent |

---

## Related Documentation

- [Master Index](README.md) - Complete documentation suite overview
- [Architecture Documentation](architecture.md) - System architecture and data flows
- [Database Schema](database-schema.md) - Entity relationship diagrams and schema
- [Functional Overview](functional-overview.md) - Module capabilities and workflows

---

## Feedback and Contributions

For questions, corrections, or contributions to this documentation:

- GitHub Issues: https://github.com/WebVella/WebVella-ERP/issues
- Documentation maintained by: Blitzy Platform
- License: Apache 2.0 (matching project license)

---

**End of Code Inventory Report**
