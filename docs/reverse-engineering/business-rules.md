# Business Rules Catalog — WebVella ERP

*Generated 2026-06-05 18:51 UTC by read-only static analysis of `WebVella.ERP3.sln`. No production code, configuration, or schema artifact was modified in the production of this report.*

> **Deliverable 5 of the WebVella ERP reverse-engineering suite.** This catalog documents the business rules **as they are actually implemented** in the codebase. Every rule resolves to a real file, class, and method (line numbers included wherever practical). It is a factual "what-exists" record, not a specification of desired behavior; recommendations belong only to `modernization-roadmap.md` (a forthcoming deliverable).

---

## Executive Summary

WebVella ERP is a **metadata-driven ERP platform** built on **ASP.NET Core 9** over **PostgreSQL 16**, using a **custom Npgsql data layer** rather than an off-the-shelf ORM. Because there is no Entity Framework model and no declarative validation framework, the platform's business rules live in three concrete places:

1. **C# manager / service / validator classes** — `EntityManager`, `RecordManager`, `EntityRelationManager`, the `ValidationUtility` helper, plugin services, and API controllers enforce field/format/uniqueness/relationship rules in code.
2. **Embedded PostgreSQL DDL** — primary-key, unique, foreign-key and not-null constraints are created from `CREATE TABLE`/`ALTER TABLE` statements emitted by `WebVella.Erp/ERPService.cs`, plus per-field constraints generated for the dynamic `rec_` record tables.
3. **Plugin patch pipelines** — dated, version-gated `Patch20YYMMDD` methods applied in ascending order by each plugin's `ProcessPatches()` method govern schema/seed evolution.

This catalog enumerates **76 distinct business rules** across the five required categories — comfortably above the ≥50 success criterion — each cited to its source location:

| Category | Prefix | Count | Primary sources |
|----------|--------|-------|-----------------|
| Validation | `VAL-` | 28 | `ValidationUtility`, `EntityManager`, `RecordManager`, `EqlBuilder`, `ERPService` seed |
| Process / Workflow | `PROC-` | 12 | plugin `ProcessPatches()` + `Patch20YYMMDD` files, `RecordManager` hook pipeline, `ApprovalController` |
| Data Integrity | `INTEG-` | 16 | embedded DDL in `ERPService`, `DbRecordRepository`, `DbRepository`, `EntityRelationManager` |
| Calculation / KPI | `CALC-` | 6 | `DashboardMetricsService`, `DashboardMetricsModel` |
| Authorization | `AUTHZ-` | 14 | `SecurityContext`, `WebApiController`, `ApprovalController`, `ApiControllerBase`, `Startup`, role seed |
| **Total** | | **76** | |

The codebase contains a large surplus of rule-bearing constructs (see [§7 Coverage & Inference Signals](#7-coverage--inference-signals)), so this catalog is a representative — not exhaustive — selection chosen to cover all five categories and the most consequential rules.

---

## How to read this catalog

- Rules are grouped into five tables, one per category, each with a stable ID (`VAL-001`, `PROC-001`, …).
- The **Source** column uses the form `path:Class.Method:line` (or `path:line` for DDL / attribute sites). All paths are relative to the repository root and use the [canonical module taxonomy](./code-inventory.md) shared across the suite.
- Where a rule is enforced at several adjacent lines, a representative line or short range is cited.
- Data-integrity table and column names are written exactly as they appear in [`database-schema.md`](./database-schema.md) and [`data-dictionary.csv`](./data-dictionary.csv).

### Rule ID legend

| Prefix | Category | Definition |
|--------|----------|------------|
| `VAL-` | **Validation** | Required fields, type/format constraints, length limits, uniqueness checks enforced before persistence. |
| `PROC-` | **Process / Workflow** | State transitions, ordering guarantees, transactional lifecycle, plugin patch sequencing. |
| `INTEG-` | **Data Integrity** | Database-level primary-key, unique, foreign-key, and not-null constraints, and the relationship model. |
| `CALC-` | **Calculation / KPI** | Numeric aggregation and KPI formulas computed in code. |
| `AUTHZ-` | **Authorization** | Role allow-lists, `[Authorize]` scopes, permission evaluation, and the seeded security model. |

---

## Fidelity corrections honored throughout

This catalog reflects the system **as built**. Four common assumptions are corrected here because they change how the rules are sourced:

1. **Custom Npgsql data layer, not Entity Framework Core.** Validation and integrity are enforced by C# manager classes **plus** PostgreSQL constraints emitted from embedded DDL — there are no EF data annotations or `DbContext` validation conventions.
2. **Razor Pages + ERP TagHelpers + Blazor WebAssembly + plain JavaScript** front end (no Angular/React/TypeScript). UI-side authorization rules are therefore found in Razor/Blazor page-builder components such as `PcApprovalDashboard`, not in a SPA framework.
3. **Code-embedded DDL and dated patch methods, not a migrations folder.** The schema-evolution process rules come from each plugin's `ProcessPatches()` and the `Patch20YYMMDD` partial-class files, not from an EF `Migrations/` directory (which does not exist).
4. **No Docker / containerization** exists in the repository; nothing in this catalog assumes a container runtime.

---

## 1. Validation Rules (`VAL-`)

Validation rules govern what constitutes acceptable input before a record, entity, field, or relation is persisted. The platform centralizes name/label checks in the internal `ValidationUtility` helper (`WebVella.Erp/Api/Models/ValidationUtility.cs`) and layers entity-, field-, record-, and query-level checks on top. Errors are accumulated into a `ValidationException` whose `CheckAndThrow()` raises only when at least one error is present.

| Rule ID | Rule (what it enforces) | Source (`file:Class.Method:line`) |
|---------|-------------------------|-----------------------------------|
| VAL-001 | A `ValidationException` accumulates field-level errors and is thrown **only if at least one error exists** (`CheckAndThrow()` is a no-op on an empty error list), so validation failures surface as a single aggregated exception. | `WebVella.Erp/Exceptions/ValidationException.cs:ValidationException.CheckAndThrow:34` |
| VAL-002 | `AddError(fieldName, message, index)` appends a `ValidationError` and promotes the first message to the exception's top-level `Message` when none is set yet. | `WebVella.Erp/Exceptions/ValidationException.cs:ValidationException.AddError:26` |
| VAL-003 | A `ValidationError` **must carry a non-blank message** — the constructor throws `ArgumentException` for a null/whitespace message (and for a negative index). | `WebVella.Erp/Exceptions/ValidationError.cs:ValidationError..ctor:41` |
| VAL-004 | A validation error's `PropertyName` is normalized to lower-case invariant, keeping field references case-insensitive across the API envelope. | `WebVella.Erp/Exceptions/ValidationError.cs:ValidationError..ctor:44` |
| VAL-005 | Entity and field **names are required** — a blank name fails validation with "Name is required!". | `WebVella.Erp/Api/Models/ValidationUtility.cs:ValidationUtility.ValidateName:28-30` |
| VAL-006 | Entity/field names must be **at least 2 characters** long (default `minLen`). | `WebVella.Erp/Api/Models/ValidationUtility.cs:ValidationUtility.ValidateName:34-35` |
| VAL-007 | Entity/field names may be **at most 63 characters** — the PostgreSQL identifier limit — and the validator itself refuses any `maxLen` above 63. | `WebVella.Erp/Api/Models/ValidationUtility.cs:ValidationUtility.ValidateName:15-16,37-38` |
| VAL-008 | Names must match the pattern `^[a-z](?!.*__)[a-z0-9_]*[a-z0-9]$`: begin with a lower-case letter, contain only lower-case alphanumerics and underscores, never contain two consecutive underscores, and never end with an underscore. | `WebVella.Erp/Api/Models/ValidationUtility.cs:NAME_VALIDATION_PATTERN:9` and `ValidateName:40-42` |
| VAL-009 | **View names** use a relaxed pattern that additionally permits the tilde (`~`) and allows up to 200 characters. | `WebVella.Erp/Api/Models/ValidationUtility.cs:VIEW_NAME_VALIDATION_PATTERN:10` and `ValidateViewName:47-73` |
| VAL-010 | **Labels are required** and must be 1–200 characters ("Label is required!"). | `WebVella.Erp/Api/Models/ValidationUtility.cs:ValidationUtility.ValidateLabel:90-100` |
| VAL-011 | An entity's **plural label is required** ("Plural label is required!"). | `WebVella.Erp/Api/Models/ValidationUtility.cs:ValidationUtility.ValidateLabelPlural:117-119` |
| VAL-012 | An entity **Id is required** (must be a non-empty GUID) before create/update. | `WebVella.Erp/Api/EntityManager.cs:EntityManager.ValidateEntity:44-45` |
| VAL-013 | On update, the **entity must already exist** ("Entity with such Id does not exist!"). | `WebVella.Erp/Api/EntityManager.cs:EntityManager.ValidateEntity:52-55` |
| VAL-014 | **Entity names are unique** — creating/renaming to a name already held by a different entity fails with "Entity with such Name exists already!". | `WebVella.Erp/Api/EntityManager.cs:EntityManager.ValidateEntity:72-75` |
| VAL-015 | An entity **must define at least one field** ("There should be at least one field!"). | `WebVella.Erp/Api/EntityManager.cs:EntityManager.ValidateFields:109-111` |
| VAL-016 | An entity **must have exactly one unique-identifier (GUID primary) field** — zero triggers "Must have one unique identifier field!"; more than one triggers "Too many primary fields…". | `WebVella.Erp/Api/EntityManager.cs:EntityManager.ValidateFields:131-135` |
| VAL-017 | A field **Id is required**. | `WebVella.Erp/Api/EntityManager.cs:EntityManager.ValidateField:144-145` |
| VAL-018 | **Field Ids are unique within an entity** ("There is already a field with such Id!"). | `WebVella.Erp/Api/EntityManager.cs:EntityManager.ValidateField:147-150` |
| VAL-019 | **Field names are unique within an entity** ("There is already a field with such Name!"). | `WebVella.Erp/Api/EntityManager.cs:EntityManager.ValidateField:152-155` |
| VAL-020 | Field definitions enforce **type-specific required attributes** — e.g. Date fields require a format ("Date format is required!"), DateTime fields require a format, and Select/Multiselect fields require options ("Options is required!"). | `WebVella.Erp/Api/EntityManager.cs:EntityManager.ValidateField:201,217,319` |
| VAL-021 | The seeded **`user` entity enforces a required, unique `username`** field. | `WebVella.Erp/ERPService.cs:165-166` |
| VAL-022 | The seeded **`user` entity enforces a required, unique `email`** field (max length 255). | `WebVella.Erp/ERPService.cs:188-194` |
| VAL-023 | A record **Id must be a valid, non-empty GUID** — `Guid.Empty` is rejected ("Guid.Empty value cannot be used as valid value for record id."). | `WebVella.Erp/Api/RecordManager.cs:RecordManager.CreateRecord:331-334` |
| VAL-024 | When a **required field arrives null or is omitted**, the record manager substitutes the field's configured default value rather than persisting null. | `WebVella.Erp/Api/RecordManager.cs:RecordManager.CreateRecord:696` and `SetRecordRequiredFieldsDefaultData:2087-2099` |
| VAL-025 | An **EQL query string must not be empty** — parsing a null/whitespace source throws `EqlException("Source is empty.")`. | `WebVella.Erp/Eql/EqlBuilder.cs:EqlBuilder.Parse:118-119` |
| VAL-026 | Only **`SELECT` statements are accepted** by the EQL abstract-tree builder; any other root operator throws "Not supported operator in abstract tree building.". | `WebVella.Erp/Eql/EqlBuilder.cs:EqlBuilder.BuildAbstractTree:154` |
| VAL-027 | An EQL **`PAGE` clause parameter must be supplied** — a referenced parameter that is absent throws "PAGE: Parameter '…' not found.". | `WebVella.Erp/Eql/EqlBuilder.cs:EqlBuilder.BuildSelectTree:201-202` |
| VAL-028 | An EQL **`PAGE` value must be a valid 32-bit integer** — a non-numeric value throws "PAGE: Invalid parameter '…' value '…'." (the same rule applies to `PAGESIZE`). | `WebVella.Erp/Eql/EqlBuilder.cs:EqlBuilder.BuildSelectTree:205-206` |

> Beyond these representative entries, validation is pervasive: **129 files reference `ValidationException`** and there are **145 `AddError`/`AddValidationError` call sites** across the solution (see [§7](#7-coverage--inference-signals)).

---

## 2. Process / Workflow Rules (`PROC-`)

Process rules govern ordering, state, and transactional lifecycle. The most consequential is **plugin patch sequencing**: each plugin owns a `ProcessPatches()` method that applies dated `Patch20YYMMDD` methods in ascending date order, gated on a persisted version number. (Note: `WebVella.Erp.Plugins.Crm/CrmPlugin._.cs` contains only a *commented-out* `Patch20190123` call; the live, executing examples are cited from the SDK, Project, and Mail plugins.)

| Rule ID | Rule (what it enforces) | Source (`file:Class.Method:line`) |
|---------|-------------------------|-----------------------------------|
| PROC-001 | Plugin schema/seed patches are applied **in ascending date order, each gated by `if (currentPluginSettings.Version < <patchDate>)`**, so a patch runs at most once and only when newer than the stored version. | `WebVella.Erp.Plugins.SDK/SdkPlugin._.cs:SdkPlugin.ProcessPatches:79-145` |
| PROC-002 | Patch application is **idempotent**: the stored version is advanced to the patch date *before* the patch body runs, so re-running `ProcessPatches()` skips already-applied patches. | `WebVella.Erp.Plugins.SDK/SdkPlugin._.cs:SdkPlugin.ProcessPatches:83-84` |
| PROC-003 | `ProcessPatches()` executes **inside the system security scope** (`SecurityContext.OpenSystemScope()`), granting the patch pipeline unrestricted, system-level rights for the duration. | `WebVella.Erp.Plugins.SDK/SdkPlugin._.cs:SdkPlugin.ProcessPatches:21` |
| PROC-004 | The entire patch run is **atomic** — wrapped in a single DB transaction that commits on success and rolls back on any exception. | `WebVella.Erp.Plugins.SDK/SdkPlugin._.cs:SdkPlugin.ProcessPatches:35,153,158` |
| PROC-005 | Each plugin's **installed version is persisted as serialized JSON in the `plugin_data` table**, which is how the patch gate (PROC-001) reads prior state. | `WebVella.Erp.Plugins.SDK/SdkPlugin._.cs:SdkPlugin.ProcessPatches:151` and `WebVella.Erp.Plugins.Crm/CrmPlugin._.cs:CrmPlugin.ProcessPatches:84` |
| PROC-006 | Every patch method follows the **fixed signature `private static void Patch20YYMMDD(EntityManager, EntityRelationManager, RecordManager)`**, the contract `ProcessPatches()` invokes them by. | `WebVella.Erp.Plugins.SDK/SdkPlugin.20181215.cs:SdkPlugin.Patch20181215:12`; `WebVella.Erp.Plugins.Project/ProjectPlugin.20190222.cs:Patch20190222:14`; `WebVella.Erp.Plugins.Mail/MailPlugin.20190419.cs:Patch20190419:16` |
| PROC-007 | Each plugin declares an **initial-version baseline constant** used as the starting version when no `plugin_data` record exists yet. | `WebVella.Erp.Plugins.Crm/CrmPlugin._.cs:WEBVELLA_CRM_INIT_VERSION:13`; `WebVella.Erp.Plugins.SDK/SdkPlugin._.cs:WEBVELLA_SDK_INIT_VERSION:12` |
| PROC-008 | System-table DDL is **idempotent**: each `CREATE TABLE` is guarded by an `information_schema.tables` existence check, so initialization is safe to re-run. | `WebVella.Erp/ERPService.cs:1190-1198` |
| PROC-009 | Record create/update/delete runs **pre- and post-hooks inside a transaction**; if a pre-hook reports errors the transaction is rolled back and the operation aborts before any write. | `WebVella.Erp/Api/RecordManager.cs:RecordManager.CreateRecord:295-318` |
| PROC-010 | When a record is created **without an explicit `id`, a new GUID is generated** server-side. | `WebVella.Erp/Api/RecordManager.cs:RecordManager.CreateRecord:320-330` |
| PROC-011 | A relation's **type, origin, and target are immutable after creation** — attempts to change them on update fail with "…is readonly and cannot be changed.". | `WebVella.Erp/Api/EntityRelationManager.cs:140-156` |
| PROC-012 | The Approval dashboard **defaults its metrics window to the last 30 days** when the caller supplies no date range (`to = UtcNow`, `from = to − 30 days`). | `WebVella.Erp.Plugins.Approval/Controllers/ApprovalController.cs:ApprovalController.GetDashboardMetrics:140-141` |

---

## 3. Data Integrity Rules (`INTEG-`)

Data-integrity rules are enforced at the **database** level by constraints in the embedded DDL and by per-field constraints generated for the dynamic `rec_` record tables. Table and column names below match [`database-schema.md`](./database-schema.md) and [`data-dictionary.csv`](./data-dictionary.csv) exactly.

| Rule ID | Rule (what it enforces) | Source (`file:line`) |
|---------|-------------------------|----------------------|
| INTEG-001 | The `entities` table has primary key `entities_pkey (id)` and stores its definition in a `NOT NULL` `json` column. | `WebVella.Erp/ERPService.cs:937` |
| INTEG-002 | The `entity_relations` table has primary key `entity_relations_pkey (id)` and a `NOT NULL` `json` column — this table is the backbone of the relationship model (see INTEG-015/016). | `WebVella.Erp/ERPService.cs:952` |
| INTEG-003 | The `system_settings` table has primary key `system_settings_pkey (id)` and a `NOT NULL` integer `version` column. | `WebVella.Erp/ERPService.cs:968` |
| INTEG-004 | The `files` table enforces primary key `files_pkey (id)` plus **two unique constraints** — `udx_filepath (filepath)` and `udx_object_id (object_id)`. | `WebVella.Erp/ERPService.cs:1027-1032` |
| INTEG-005 | The `plugin_data` table enforces primary key `plugin_data_pkey (id)`, a **`UNIQUE(name)` constraint `idx_u_plugin_data_name`**, and a `NOT NULL` `name` column — guaranteeing one settings row per plugin. | `WebVella.Erp/ERPService.cs:1201-1206` |
| INTEG-006 | The `app` table enforces primary key `app_pkey (id)`, a **`UNIQUE(name)` constraint `ux_app_name`**, and a `NOT NULL` `label` column. | `WebVella.Erp/ERPService.cs:1225-1228,1377` |
| INTEG-007 | The `data_source` table enforces primary key `data_source_pkey (id)` and a **`UNIQUE(name)` constraint `ux_data_source_name`**. | `WebVella.Erp/ERPService.cs:1393-1394` |
| INTEG-008 | The `app_page_data_source` table enforces primary key `app_page_data_source_pkey (id)` and a **composite unique constraint `app_page_data_uxc_name_page_id (name, page_id)`**. | `WebVella.Erp/ERPService.cs:1405-1406` |
| INTEG-009 | `app_page_body_node` rows are referentially bound by foreign keys to their parent node (`parent_id → app_page_body_node(id)`) and owning page (`page_id → app_page(id)`). | `WebVella.Erp/ERPService.cs:1345-1350` |
| INTEG-010 | Sitemap structures are referentially bound: `app_id → app(id)`, `area_id → app_sitemap_area(id)`, and `node_id → app_sitemap_area_node(id)` foreign keys tie areas, groups, and nodes to their owners. | `WebVella.Erp/ERPService.cs:1353-1374` |
| INTEG-011 | Sitemap area nodes support **self-referencing hierarchy** via `fkey_app_sitemap_area_node_parent_id (parent_id → app_sitemap_area_node(id))`. | `WebVella.Erp/ERPService.cs:1464-1465` |
| INTEG-012 | A field flagged **`Unique` generates a database `UNIQUE` constraint `idx_u_<entity>_<field>`** on the dynamic `rec_<entity_name>` record table. | `WebVella.Erp/Database/DbRecordRepository.cs:DbRecordRepository.CreateRecordField:309-310` |
| INTEG-013 | A field flagged **`Required` is enforced `NOT NULL` at the column level** on the `rec_<entity_name>` table (nullable is set to `!field.Required`). | `WebVella.Erp/Database/DbRecordRepository.cs:DbRecordRepository.UpdateRecordField:326` |
| INTEG-014 | Unique indexes are created via `CREATE UNIQUE INDEX IF NOT EXISTS`, and **geography fields receive a GIST spatial index** rather than a btree index. | `WebVella.Erp/Database/DbRepository.cs:DbRepository.CreateIndex:469-472` |
| INTEG-015 | A relation's **origin and target field must each be a Unique Identifier (GUID) field** — relations may only be anchored on primary-key columns. | `WebVella.Erp/Api/EntityRelationManager.cs:117,128` |
| INTEG-016 | A relation's **origin/target entity and field must exist** before the relation is accepted ("The origin entity do not exist." / "The target field do not exist."). | `WebVella.Erp/Api/EntityRelationManager.cs:110-126` |

---

## 4. Calculation / KPI Rules (`CALC-`)

Calculation rules are the numeric formulas that drive the Approval manager dashboard. They are implemented in `DashboardMetricsService` and surfaced through the `DashboardMetricsModel` DTO. Each metric reads `approval_request` / `approval_history` data via EQL and computes its value in C#.

| Rule ID | Rule (what it enforces) | Source (`file:Class.Method:line`) |
|---------|-------------------------|-----------------------------------|
| CALC-001 | **Pending approvals count** = the number of `approval_request` rows whose `status = 'pending'`. | `WebVella.Erp.Plugins.Approval/Services/DashboardMetricsService.cs:DashboardMetricsService.GetPendingApprovalsCount:58-75` |
| CALC-002 | **Overdue requests count** = pending requests where `now > created_on + 24h`; the timeout threshold defaults to **24 hours**. | `WebVella.Erp.Plugins.Approval/Services/DashboardMetricsService.cs:DashboardMetricsService.GetOverdueRequestsCount:112-126` |
| CALC-003 | **Average approval time (hours)** = `round( Σ (completed_on − created_on).TotalHours / N , 2 )` over requests `approved`/`rejected` within the date range; returns 0 when N = 0. | `WebVella.Erp.Plugins.Approval/Services/DashboardMetricsService.cs:DashboardMetricsService.GetAverageApprovalTime:183-189` |
| CALC-004 | **Approval rate (%)** = `round( approvedCount / totalProcessed × 100 , 1 )` over requests completed within the date range; returns 0 when none were processed. | `WebVella.Erp.Plugins.Approval/Services/DashboardMetricsService.cs:DashboardMetricsService.GetApprovalRate:230-236` |
| CALC-005 | **Recent activity** = `approval_history` rows ordered by `performed_on DESC` limited to the requested count; the dashboard requests the **last 5** items. | `WebVella.Erp.Plugins.Approval/Services/DashboardMetricsService.cs:DashboardMetricsService.GetRecentActivity:257-268` and `GetDashboardMetrics:46` |
| CALC-006 | Every metrics response is **stamped `MetricsAsOf = DateTime.UtcNow`** and carries the date-range start/end it was computed over. | `WebVella.Erp.Plugins.Approval/Services/DashboardMetricsService.cs:DashboardMetricsService.GetDashboardMetrics:39-41` |

---

## 5. Authorization Rules (`AUTHZ-`)

Authorization is enforced at three layers: **transport** (the hybrid JWT-or-Cookie scheme), **endpoint** (`[Authorize]` attributes and role allow-lists), and **domain** (`SecurityContext` permission evaluation against the seeded role model). The platform defines three roles — **administrator**, **regular**, and **guest** — seeded at initialization.

| Rule ID | Rule (what it enforces) | Source (`file:Class.Method:line`) |
|---------|-------------------------|-----------------------------------|
| AUTHZ-001 | **Every Web API endpoint requires authentication** — the base controller is annotated `[Authorize]`, so all derived API controllers inherit the requirement. | `WebVella.Erp.Web/Controllers/ApiControllerBase.cs:9` |
| AUTHZ-002 | The central `WebApiController` is itself `[Authorize]`-scoped (in addition to inheriting from `ApiControllerBase`). | `WebVella.Erp.Web/Controllers/WebApiController.cs:36` |
| AUTHZ-003 | **All metadata (schema) mutations require the `administrator` role** — entity, field, and relation create/update/patch/delete endpoints are each `[Authorize(Roles = "administrator")]`. | `WebVella.Erp.Web/Controllers/WebApiController.cs:WebApiController.CreateEntity:1476`; `CreateField:1595`; `CreateEntityRelation:2038` |
| AUTHZ-004 | **Server-administration endpoints require the `administrator` role** — plugin listing, job listing, and schedule-plan management are each `[Authorize(Roles = "administrator")]`. | `WebVella.Erp.Web/Controllers/WebApiController.cs:WebApiController.GetPlugins:3405`; `GetJobs:3422`; `UpdateSchedulePlan:3451` |
| AUTHZ-005 | The Approval API controller requires authentication for all actions (class-level `[Authorize]`) and restricts the dashboard to the role allow-list **{`manager`, `administrator`, `admin`}**. | `WebVella.Erp.Plugins.Approval/Controllers/ApprovalController.cs:20,31-36` |
| AUTHZ-006 | The dashboard-metrics action **rejects unauthenticated callers with 401** and **callers lacking a manager-equivalent role with 403**. | `WebVella.Erp.Plugins.Approval/Controllers/ApprovalController.cs:ApprovalController.GetDashboardMetrics:124-137` |
| AUTHZ-007 | The Approval **health endpoint is explicitly anonymous** (`[AllowAnonymous]`), overriding the class-level `[Authorize]`. | `WebVella.Erp.Plugins.Approval/Controllers/ApprovalController.cs:ApprovalController.GetDashboardHealth:189` |
| AUTHZ-008 | The Blazor/Razor **`PcApprovalDashboard` component re-enforces the same manager allow-list** in the UI tier, denying render with "Access denied. You must have a Manager role to view this dashboard.". | `WebVella.Erp.Plugins.Approval/Components/PcApprovalDashboard/PcApprovalDashboard.cs:41-46,186-196` |
| AUTHZ-009 | **Metadata operations are restricted to administrators at the domain layer** — `HasMetaPermission` returns true only for users holding `AdministratorRoleId`. | `WebVella.Erp/Api/SecurityContext.cs:SecurityContext.HasMetaPermission:117` |
| AUTHZ-010 | **Record-level CRUD is permission-checked by role** — `HasEntityPermission` grants Read/Create/Update/Delete only when one of the user's roles is in the entity's corresponding `RecordPermissions` allow-list. | `WebVella.Erp/Api/SecurityContext.cs:SecurityContext.HasEntityPermission:79-86` |
| AUTHZ-011 | The **system user has unlimited permissions** — any check for `SystemUserId` short-circuits to allow. | `WebVella.Erp/Api/SecurityContext.cs:SecurityContext.HasEntityPermission:74-75` |
| AUTHZ-012 | **Anonymous (no current user) requests are evaluated against the `guest` role's permissions**, so public access is governed by what the Guest role is granted. | `WebVella.Erp/Api/SecurityContext.cs:SecurityContext.HasEntityPermission:95-102` |
| AUTHZ-013 | The security model **seeds exactly three roles — `administrator`, `regular`, and `guest`** — and provisions the first user as `administrator` (also granted `regular`). | `WebVella.Erp/ERPService.cs:464-525` |
| AUTHZ-014 | Transport authentication uses a **hybrid "JWT_OR_COOKIE" scheme**: a request bearing an `Authorization: Bearer …` header is validated as a JWT (issuer, audience, lifetime, and signing key all verified); otherwise the HTTP-only auth cookie is used. | `WebVella.Erp.Site/Startup.cs:90-125` |

> Authorization is broadly applied: there are **37 `[Authorize]` attribute sites** across the solution (see [§7](#7-coverage--inference-signals)).

---

## 6. Cross-Document Consistency

This catalog is one node in a cross-referential suite; the following contracts hold:

- **Module taxonomy** — module and file references use the canonical taxonomy defined in [`code-inventory.md`](./code-inventory.md) (Core, Web, WebAssembly, ConsoleApp, the 7 Plugins, the 7 Sites).
- **Schema names** — every table and constraint named in the Data Integrity table (e.g. `plugin_data`, `idx_u_plugin_data_name`, `entity_relations`, `rec_<entity_name>`) matches [`database-schema.md`](./database-schema.md) and [`data-dictionary.csv`](./data-dictionary.csv) exactly.
- **Citations** — every `Source` path resolves to a real file catalogued in [`code-inventory.csv`](./code-inventory.csv).
- **Findings flow** — the authorization and integrity findings here will feed the assessment in `security-quality.md` and the phased plan in `modernization-roadmap.md` (both forthcoming deliverables).

---

## 7. Coverage & Inference Signals

The ≥50-rule target is comfortably met (this catalog documents **76 rules**). The selection is representative; the codebase contains a far larger population of rule-bearing constructs, quantified below from a solution-wide scan (excluding `bin`/`obj`):

| Signal | Count | Meaning |
|--------|-------|---------|
| Files referencing `ValidationException` | **129** | Breadth of code paths that raise validation failures. |
| `AddError` / `AddValidationError` call sites | **145** | Discrete validation error conditions. |
| `throw new …Exception` sites | **1,326** | Total guard/precondition checks across the solution. |
| `[Authorize]` attribute sites | **37** | Endpoint-level authorization scopes. |
| `Required`-related occurrences | **700+** | Required-field handling across managers, field types, and seeds. |
| Dated `Patch20YYMMDD` definition files | **25** | Plugin process-rule (patch-ordering) population across SDK, Project, Next, and Mail plugins. |

Because each category is sourced from a surplus of real sites, additional rules can be catalogued by extending the same tables; the entries above were chosen for coverage across all five categories and for the consequence of the rule.

---

## 8. Source File Index

The rules in this catalog were derived by reading the following files (all analyzed read-only):

| Area | Files |
|------|-------|
| Validation infrastructure | `WebVella.Erp/Exceptions/ValidationException.cs`, `WebVella.Erp/Exceptions/ValidationError.cs`, `WebVella.Erp/Api/Models/ValidationUtility.cs` |
| Metadata & record managers | `WebVella.Erp/Api/EntityManager.cs`, `WebVella.Erp/Api/EntityRelationManager.cs`, `WebVella.Erp/Api/RecordManager.cs` |
| Data layer / DDL | `WebVella.Erp/ERPService.cs`, `WebVella.Erp/Database/DbRecordRepository.cs`, `WebVella.Erp/Database/DbRepository.cs` |
| Query language | `WebVella.Erp/Eql/EqlBuilder.cs` |
| Security model | `WebVella.Erp/Api/SecurityContext.cs`, `WebVella.Erp/Api/Definitions.cs`, `WebVella.Erp/Api/Models/ErpUser.cs`, `WebVella.Erp.Site/Startup.cs` |
| API controllers | `WebVella.Erp.Web/Controllers/WebApiController.cs`, `WebVella.Erp.Web/Controllers/ApiControllerBase.cs`, `WebVella.Erp.Plugins.Approval/Controllers/ApprovalController.cs`, `WebVella.Erp.Plugins.Project/Controllers/ProjectController.cs` |
| Approval KPIs | `WebVella.Erp.Plugins.Approval/Services/DashboardMetricsService.cs`, `WebVella.Erp.Plugins.Approval/Api/DashboardMetricsModel.cs`, `WebVella.Erp.Plugins.Approval/Components/PcApprovalDashboard/PcApprovalDashboard.cs` |
| Plugin patch pipeline | `WebVella.Erp.Plugins.SDK/SdkPlugin._.cs`, `WebVella.Erp.Plugins.SDK/SdkPlugin.20181215.cs`, `WebVella.Erp.Plugins.Crm/CrmPlugin._.cs`, `WebVella.Erp.Plugins.Project/ProjectPlugin.20190222.cs`, `WebVella.Erp.Plugins.Mail/MailPlugin.20190419.cs` |

*End of Business Rules Catalog.*
