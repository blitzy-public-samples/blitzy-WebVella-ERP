# WebVella ERP — Business Rules Catalog

> Part of the [Reverse-Engineering / As-Built Documentation Suite](./README.md). This document catalogs the **business rules** embedded in the WebVella ERP codebase — the validation, process, data-integrity, calculation/derivation, and authorization logic that governs how the platform behaves. Every rule carries an inline **`path:line` citation** so it can be verified against the source.

---

## Executive Summary

WebVella ERP is an **entity-centric, plugin-driven** platform: entities, fields, and relations are stored as data in a meta-model rather than as compile-time classes, and physical PostgreSQL tables (`rec_*`, `rel_*`) are generated at runtime. Because of this design, the platform's business rules are **not** scattered across hand-written domain classes — they are concentrated in a small number of **core managers** (`EntityManager`, `EntityRelationManager`, `RecordManager`, `SecurityManager`), a **custom data-access layer** (the `Db*` repositories and `FieldTypes/`), the **hook / job** infrastructure, and the **plugin** feature modules. This document reverse-engineers those rules and presents them as a navigable catalog.

This catalog contains **74 catalogued rules** organized into the five required categories. Every rule has a **stable Rule ID** (so it can be referenced from other documents and from code reviews), a concise **statement**, an inline **`path:line` citation** to the governing source, and a short **rationale / notes** field.

| Category | Prefix | Count | What it covers |
|----------|--------|-------|----------------|
| **Validation** | `VAL-*` | 24 | Field- and entity-level input validation enforced by the manager layer before persistence |
| **Process** | `PROC-*` | 14 | Lifecycle and workflow rules — plugin patching, record hooks, scheduled jobs, approval state machine |
| **Data integrity** | `INTEG-*` | 13 | Keys, constraints, uniqueness, and table lifecycle enforced by the DDL layer |
| **Calculation / derivation** | `CALC-*` | 10 | Server-computed and server-derived values (auto-numbers, GUIDs, metrics, hashes) |
| **Authorization** | `AUTHZ-*` | 13 | Access control — role-gated CRUD, security scopes, and the authorization enforcement model |
| **Total** | | **74** | |

Two findings deserve emphasis because they describe **what the system does *not* do**, which is as important as what it does:

- **The MVC `AuthorizeAttribute` is dead code.** The entire class in `WebVella.Erp.Web/Security/AuthorizeAttribute.cs:1-147` is commented out. Authorization is **not** enforced by this attribute; it is enforced by `SecurityContext`/`RecordManager` permission checks plus the host's authentication scheme. This is catalogued as `AUTHZ-010`.
- **Database-layer formula fields are not implemented.** `WebVella.Erp/Database/FieldTypes/DbFormulaField.cs:5-13` is entirely commented out with the note *"Not supported at the moment."* This is catalogued as `CALC-010`.

> **Analysis-only mandate.** This document was produced by reading the source tree at the commit below. **No production source was modified** — no `.cs`, `.cshtml`, `.razor`, or `.js` file was edited, and **no comments or docstrings were added** to code. The catalog describes *what exists*; it does not prescribe changes. Remediation of any debt noted here lives in [`modernization-roadmap.md`](./modernization-roadmap.md).

---

## Generation Metadata

| Field | Value |
|-------|-------|
| **Documentation Generated** | 2026-06-05 15:15 UTC |
| **Source commit** | `bfe15661c7f0c1dae57288d789b854186793b157` |
| **Branch** | `master` |
| **Catalogued rules** | **74** (≥ 50 required) |
| **Citation convention** | Inline `path:line` (e.g., `WebVella.Erp/Api/EntityManager.cs:45`) or `path:start-end` for ranges |
| **Render target** | GitHub-Flavored Markdown (GFM) — renders natively on GitHub |

---

## How to Read This Catalog

- **Rule ID** — a stable identifier (`VAL-001`, `PROC-001`, …). IDs are never reused; if a rule is retired in a future revision its ID is retained as a tombstone rather than recycled.
- **Citation** — the `path:line` location of the **governing** source. Where a rule is enforced at several lines, the citation points to the canonical/first occurrence and the notes mention siblings.
- **Source type** — most rules cite **implemented code**. Rules for the **Approval** domain that are specified by Jira stories but whose implementation is design-stage are explicitly labeled **(Story-specified)** and cite the relevant `jira-stories/STORY-00X-*.md` file. These are clearly distinguished from implemented rules so readers never mistake a requirement for shipped behavior.
- **Terminology** — entity names, table conventions (`rec_*`, `rel_*`), and platform terms (meta-model, hook, plugin bootstrap, patch-class migration, manager layer, EQL, DAL) follow the canonical [Glossary in `README.md`](./README.md#glossary--acronyms) and are used consistently with [`functional-overview.md`](./functional-overview.md), [`architecture.md`](./architecture.md), and [`database-schema.md`](./database-schema.md).

### Method of discovery (how these rules were found)

1. **Validation** rules were mined from the `errorList.Add(new ErrorModel(...))` / `Errors.Add(...)` pattern in `EntityManager.cs` and `EntityRelationManager.cs`, plus the shared `ValidationUtility` helper. Each distinct validation message is a rule.
2. **Process** rules were enumerated from the plugin `ProcessPatches()` mechanism, the **12** `IErp*Hook` interfaces in `WebVella.Erp/Hooks/`, and the background-service / job-scheduling infrastructure in `WebVella.Erp/Jobs/`.
3. **Data-integrity** rules were extracted from the DDL helpers in `DbRepository.cs` and the table-lifecycle logic in `DbRelationRepository.cs` / `DbEntityRepository.cs`.
4. **Calculation / derivation** rules were taken from the `FieldTypes/`, the record write-path in `RecordManager.cs`, and plugin services that compute metrics.
5. **Authorization** rules were taken from `SecurityContext`, `RecordManager`'s permission gates, `DbEntity`'s `RecordPermissions`, and the host authentication model.

---

## 1. Validation Rules (`VAL-*`)

Validation rules govern what constitutes a well-formed entity, field, or relation **definition**, and a well-formed **record id**, *before* anything is written to the database. They are enforced almost entirely by the meta-model managers (`EntityManager`, `EntityRelationManager`) using the `ErrorModel` accumulation pattern — each `errorList.Add(new ErrorModel(...))` is a distinct, user-visible validation rule. The shared `ValidationUtility` helper (`WebVella.Erp/Api/Models/ValidationUtility.cs`) provides the reusable name/label checks that the managers call into.

| Rule ID | Rule Statement | Citation (`path:line`) | Rationale / Notes |
|---------|----------------|--------------------------|-------------------|
| **VAL-001** | An entity update must supply a non-empty `Id`; a missing id is rejected with *"Id is required!"* | `WebVella.Erp/Api/EntityManager.cs:45` | Guards the update path — the meta-model cannot resolve which `entities` row to mutate without an id. |
| **VAL-002** | An entity update must reference an `Id` that already exists, else *"Entity with such Id does not exist!"* | `WebVella.Erp/Api/EntityManager.cs:55` | Prevents "update" from silently behaving as an insert of an orphan definition. |
| **VAL-003** | Entity `Name` length must be ≤ 63 characters | `WebVella.Erp/Api/EntityManager.cs:70` | Hard PostgreSQL identifier limit — the name becomes part of the generated `rec_*` table/identifier. |
| **VAL-004** | Entity `Name` must be unique across the meta-model, else *"Entity with such Name exists already!"* | `WebVella.Erp/Api/EntityManager.cs:75` | Two entities cannot map to the same physical `rec_*` table. |
| **VAL-005** | Entity/field `Name` must match the pattern `^[a-z](?!.*__)[a-z0-9_]*[a-z0-9]$` (lowercase, begins with a letter, no spaces, no trailing/double underscores) | `WebVella.Erp/Api/Models/ValidationUtility.cs:42` | Pattern constant defined at `WebVella.Erp/Api/Models/ValidationUtility.cs:9`; invoked for entities at `WebVella.Erp/Api/EntityManager.cs:64` and for fields at `WebVella.Erp/Api/EntityManager.cs:157`. Names are emitted verbatim as SQL identifiers. |
| **VAL-006** | An entity must declare at least one field, else *"There should be at least one field!"* | `WebVella.Erp/Api/EntityManager.cs:111` | A physical table with zero columns is meaningless; the loop short-circuits on empty field sets. |
| **VAL-007** | An entity must have exactly one unique-identifier (primary GUID) field — neither zero (*"Must have one unique identifier field!"*) nor more than one (*"Too many primary fields…"*) | `WebVella.Erp/Api/EntityManager.cs:132` | Sibling check for the "too many" case at `WebVella.Erp/Api/EntityManager.cs:135`; the primary GUID field becomes the table's primary key. |
| **VAL-008** | Field `Name` length must be ≤ 63 characters | `WebVella.Erp/Api/EntityManager.cs:128` | PostgreSQL column-name limit — field names become physical column names. |
| **VAL-009** | Every field must supply a non-empty `Id`, else *"Id is required!"* | `WebVella.Erp/Api/EntityManager.cs:145` | Field identity is required to address the column in subsequent meta operations. |
| **VAL-010** | Field `Id` must be unique within its entity, else *"There is already a field with such Id!"* | `WebVella.Erp/Api/EntityManager.cs:150` | Prevents duplicate field definitions colliding on id. |
| **VAL-011** | Field `Name` must be unique within its entity, else *"There is already a field with such Name!"* | `WebVella.Erp/Api/EntityManager.cs:155` | Two columns cannot share a name in the generated table. |
| **VAL-012** | A name is required and must satisfy min/max length (2–63 by default), else *"Name is required!"* / length messages | `WebVella.Erp/Api/Models/ValidationUtility.cs:30` | Min-length check at `WebVella.Erp/Api/Models/ValidationUtility.cs:35`, max-length at `:38`; shared by entity, field, and relation name validation. |
| **VAL-013** | A field/entity `Label` is required, else *"Label is required!"* | `WebVella.Erp/Api/Models/ValidationUtility.cs:92` | Labels drive UI rendering; a blank label yields an unusable form control. |
| **VAL-014** | An entity `LabelPlural` is required, else *"Plural label is required!"* | `WebVella.Erp/Api/Models/ValidationUtility.cs:119` | Plural labels are used in list views and navigation. |
| **VAL-015** | A **Date** field must define a display `format`, else *"Date format is required!"* | `WebVella.Erp/Api/EntityManager.cs:201` | The format string drives parsing/rendering of the date column. |
| **VAL-016** | A **DateTime** field must define a display `format`, else *"Datetime format is required!"* | `WebVella.Erp/Api/EntityManager.cs:217` | Same as VAL-015 for datetime-typed fields. |
| **VAL-017** | A required Date/DateTime field that does not auto-generate its value must supply a default, else *"Default Value is required when the field is marked as required and generate new id option is not selected!"* | `WebVella.Erp/Api/EntityManager.cs:209` | Also enforced for the DateTime branch at `:225` and the GUID branch at `:268`. |
| **VAL-018** | A unique GUID field must enable "generate new id", else *"Generate New Id is required when the field is marked as unique!"* | `WebVella.Erp/Api/EntityManager.cs:263` | A unique key that is neither supplied nor generated would violate the uniqueness constraint on insert. |
| **VAL-019** | Required fields of many scalar types (number, text, currency, percent, phone, etc.) must supply a non-null default, else *"Default Value is required!"* | `WebVella.Erp/Api/EntityManager.cs:164` | The same rule recurs per field type at `:182`, `:231`, `:239`, `:256`, `:273`, `:278` and others — each field-type branch repeats the default-value guard. |
| **VAL-020** | **Select / MultiSelect** fields must define options that are non-empty and free of duplicates, else *"Options must contains at least one item!"* | `WebVella.Erp/Api/EntityManager.cs:303` | Duplicate-value guard at `:311`, required-options guard at `:319`; the MultiSelect branch repeats these at `:392`, `:400`, `:408`. |
| **VAL-021** | Relation `Name` length must be ≤ 63 characters | `WebVella.Erp/Api/EntityRelationManager.cs:46` | Relation names become `rel_*` join-table identifiers, bound by the same PostgreSQL limit. |
| **VAL-022** | A relation's origin and target **entities must exist** and the chosen origin/target **fields must be unique-identifier (GUID) fields** | `WebVella.Erp/Api/EntityRelationManager.cs:117` | Companion checks: origin entity `:110`, target entity `:121`, target field GUID `:128`. Relations are keyed on GUID identity columns. |
| **VAL-023** | Once created, a relation's **type and endpoints are immutable** — changing relation type, origin/target entity, or origin/target field is rejected as *"…is readonly and cannot be changed."* | `WebVella.Erp/Api/EntityRelationManager.cs:141` | Endpoint-immutability checks continue through `:145`–`:158`; altering a live relation would orphan existing join rows. |
| **VAL-024** | Relation-type field constraints: **1:1** and **N:N** require *both* origin and target fields to be Required **and** Unique; **1:N** requires the origin field to be Required and Unique | `WebVella.Erp/Api/EntityRelationManager.cs:184` | The semantic contract is documented in the `EntityRelationType` enum XML comments at `WebVella.Erp/Api/Models/EntityRelation.cs:9-31`; the 1:N origin checks are at `:200`–`:203`. |

---

## 2. Process Rules (`PROC-*`)

Process rules govern the **lifecycle and workflow** of the platform: how schema patches are applied at startup, how record operations are extended through hooks, and how background work is scheduled and executed. The Approval plugin contributes a domain workflow (the approval state machine and its supporting jobs); because its implementation is design-stage, those rows are explicitly marked **(Story-specified)** and cite the originating Jira story.

| Rule ID | Rule Statement | Citation (`path:line`) | Rationale / Notes |
|---------|----------------|--------------------------|-------------------|
| **PROC-001** | A plugin's schema patches are applied inside a **single database transaction** opened at the start of `ProcessPatches()` | `WebVella.Erp.Plugins.SDK/SdkPlugin._.cs:35` | `connection.BeginTransaction()` wraps the patch run so a failed patch rolls the plugin's schema changes back atomically. `ProcessPatches()` itself begins at `:19`. |
| **PROC-002** | Patch application is **version-gated** by the persisted ERP/plugin version (`system_settings` / `PluginSettings.Version`) | `WebVella.Erp.Plugins.SDK/SdkPlugin._.cs:44` | `if (systemSettings.Version > 0)` lets a plugin react to the installed database version before patching. |
| **PROC-003** | Patches are applied **in chronological order**, each guarded by `if (currentPluginSettings.Version < <date>)`, and the stored version is **bumped** after each patch | `WebVella.Erp.Plugins.SDK/SdkPlugin._.cs:79` | The pattern repeats per dated patch (`:94`, `:107`, `:121`, `:134`, …), implementing the platform's **patch-class migration** model in lieu of EF migrations. |
| **PROC-004** | Record create/update/delete/search operations fire **Pre** and **Post** extension hooks | `WebVella.Erp/Hooks/IErpPreCreateRecordHook.cs:6-9` | Eight record-hook interfaces under `WebVella.Erp/Hooks/`, each declaring a single hook method: `IErpPreCreateRecordHook.cs:6-9` / `IErpPostCreateRecordHook.cs:5-8`, `IErpPreUpdateRecordHook.cs:6-9` / `IErpPostUpdateRecordHook.cs:5-8`, `IErpPreDeleteRecordHook.cs:6-9` / `IErpPostDeleteRecordHook.cs:5-8`, `IErpPreSearchRecordHook.cs:7-10` / `IErpPostSearchRecordHook.cs:6-9`. Pre-hooks receive a `List<ErrorModel>` and can add errors to veto the operation (`IErpPreCreateRecordHook.cs:9`). |
| **PROC-005** | Many-to-many relation changes fire **Pre** and **Post** hooks | `WebVella.Erp/Hooks/IErpPreCreateManyToManyRelationHook.cs:7-10` | Four relation-hook interfaces under `WebVella.Erp/Hooks/`: `IErpPreCreateManyToManyRelationHook.cs:7-10` / `IErpPostCreateManyToManyRelationHook.cs:5-8`, `IErpPreDeleteManyToManyRelationHook.cs:7-10` / `IErpPostDeleteManyToManyRelationHook.cs:5-8`. Together with PROC-004 these are the **12** `IErp*Hook` extension points defined in the README glossary. |
| **PROC-006** | A hosted **schedule** background service continuously processes due schedule plans | `WebVella.Erp/Jobs/ErpBackgroundServices.cs:7` | `ErpJobScheduleService : BackgroundService` waits for `ScheduleManager.Current` then calls `ProcessSchedulesAsync(stoppingToken)`. |
| **PROC-007** | A hosted **job-processing** background service continuously executes queued jobs | `WebVella.Erp/Jobs/ErpBackgroundServices.cs:24` | `ErpJobProcessService : BackgroundService` waits for `JobManager.Current` then calls `ProcessJobsAsync(stoppingToken)`. |
| **PROC-008** | Only schedule plans whose next trigger time has arrived are selected for execution | `WebVella.Erp/Jobs/JobDataService.cs:450` | `GetReadyForExecutionScheduledPlans()` is the gate that turns scheduled plans into runnable jobs. |
| **PROC-009** | Background jobs run under an **elevated system security scope** | `WebVella.Erp.Plugins.Mail/Jobs/ProcessSmtpQueueJob.cs:12` | The SMTP-queue job wraps its work in `using (SecurityContext.OpenSystemScope())` so server-side processing is not blocked by per-user permissions; the job is registered via `[Job(...)]` at `:7` and derives from `ErpJob` at `:8`. |
| **PROC-010** | The Project task **open queue** excludes tasks whose status is flagged closed (`is_closed`) | `WebVella.Erp.Plugins.Project/Services/TaskService.cs:160` | `GetTaskQueue` builds a `status_id <> @…` filter from the set of closed statuses; results are then ordered by due date and priority at `:188`. |
| **PROC-011** | *(Story-specified)* An approval request follows the state machine **Pending → Approved / Rejected / Escalated**, with delegation and cancellation as additional transitions | `jira-stories/STORY-004-approval-service-layer.md:11` | Design-stage workflow owned by `ApprovalRequestService`; **not** yet implemented as shipped code. Mirrors the post-update hook that detects status transitions (STORY-005). |
| **PROC-012** | *(Story-specified)* Approval workflows are triggered and advanced by record **hooks**: a pre-create hook on `approval_request` initializes routing, a post-update hook reacts to status changes, and a pre-create hook on target entities (`purchase_order`, `expense_request`) auto-initiates approvals when thresholds are met | `jira-stories/STORY-005-approval-hooks-integration.md:9` | Update-hook behavior at `:11`, target-entity auto-initiation at `:13`. Implements PROC-004 as the integration mechanism for the approval domain. |
| **PROC-013** | *(Story-specified)* Approval support jobs run on schedules — **notifications every 5 minutes**, **escalations every 30 minutes** (driven by each step's `timeout_hours`), and **cleanup/archival daily** | `jira-stories/STORY-006-notification-escalation-jobs.md:9` | Escalation cadence at `:11`, cleanup at `:13` (the story specifies *"daily"* — it does **not** specify a 2 AM time). Plans are registered in `ApprovalPlugin.SetSchedulePlans()` per `:20`. |
| **PROC-014** | *(Story-specified)* Hooks are **stateless**, decorated with `[HookAttachment("entity_name")]`, support an optional **priority** for ordering, and are **auto-discovered and registered** by `HookManager` at startup (then invoked by `RecordHookManager` during CRUD) | `jira-stories/STORY-005-approval-hooks-integration.md:22` | Attribute/priority conventions at `:16` and `:20`. Describes the platform's existing hook-registration mechanism as relied upon by the approval design. |

---

## 3. Data-Integrity Rules (`INTEG-*`)

Data-integrity rules are the **physical-schema invariants** the platform applies when it generates and mutates PostgreSQL tables on behalf of the meta-model. They live in the DDL helpers of `DbRepository.cs` and in the table-lifecycle logic of `DbRelationRepository.cs` and `DbEntityRepository.cs`. These rules consistently use the `rec_*` (per-entity) and `rel_*` (N:N join) table conventions defined in [`database-schema.md`](./database-schema.md).

| Rule ID | Rule Statement | Citation (`path:line`) | Rationale / Notes |
|---------|----------------|--------------------------|-------------------|
| **INTEG-001** | Every generated table is given an explicit **primary key** | `WebVella.Erp/Database/DbRepository.cs:288` | `SetPrimaryKey(table, columns)` emits `ALTER TABLE … ADD PRIMARY KEY (…)` at `:302`; called for both `rec_*` and `rel_*` tables. |
| **INTEG-002** | The primary GUID column defaults to a server-generated UUID (`uuid_generate_v1()`) | `WebVella.Erp/Database/DbRepository.cs:233` | Inside `CreateColumn`; guarantees a unique key even when the client supplies none (see CALC-003). |
| **INTEG-003** | Fields marked unique are backed by a database **UNIQUE constraint** | `WebVella.Erp/Database/DbRepository.cs:310` | `CreateUniqueConstraint` emits `ALTER TABLE … ADD CONSTRAINT … UNIQUE (…)` at `:328`; the constraint is what makes VAL-018's "generate new id" mandatory. |
| **INTEG-004** | Columns are **NOT NULL** unless explicitly nullable, and a primary key is always NOT NULL | `WebVella.Erp/Database/DbRepository.cs:224` | `canBeNull = isNullable && !isPrimaryKey ? "NULL" : "NOT NULL"`. Nullability is later togglable via `SetColumnNullable` at `:344` (`ALTER COLUMN … SET/DROP NOT NULL` at `:351`). |
| **INTEG-005** | A many-to-many association row requires **both** `origin_id` and `target_id` | `WebVella.Erp/Database/DbRelationRepository.cs:265` | `CreateManyToManyRecord` inserts `(@origin_id, @target_id)` into `rel_{name}`; a half-populated link is impossible. |
| **INTEG-006** | An N:N join (`rel_*`) table uses a **composite primary key** of `(origin_id, target_id)` | `WebVella.Erp/Database/DbRepository.cs:417` | `CreateNtoNRelation` creates the `origin_id`/`target_id` columns (`:414`–`:415`) then sets the composite PK — preventing duplicate links between the same two records. |
| **INTEG-007** | Deleting a many-to-many link requires at least one of `origin_id` / `target_id`; supplying neither is rejected | `WebVella.Erp/Database/DbRelationRepository.cs:275` | Guard throws *"Both origin id and target id cannot be null when delete many to many relation!"*, preventing an unscoped mass-delete of links. |
| **INTEG-008** | Deleting an entity definition also **drops its `rec_*` physical table** (within a transaction, after its relations are removed) | `WebVella.Erp/Database/DbEntityRepository.cs:275` | Single command `DELETE FROM entities WHERE id=@id; DROP TABLE rec_<name>` keeps the meta-model and physical schema consistent. |
| **INTEG-009** | Per-entity tables are named with the **`rec_`** prefix (and N:N join tables with **`rel_`**) | `WebVella.Erp/Database/DbEntityRepository.cs:17` | `RECORD_COLLECTION_PREFIX = "rec_"`; `CreateTable` is invoked on entity creation at `:66`. The `rel_` convention is applied in `DbRelationRepository.cs` (e.g., `:261`). |
| **INTEG-010** | Implicit `text`/`varchar` → `uuid` casts are installed so GUID columns interoperate with string inputs | `WebVella.Erp/Database/DbRepository.cs:17` | `CreatePostgresqlCasts()` creates the implicit casts at bootstrap, preventing type-mismatch failures on GUID parameters. |
| **INTEG-011** | The `uuid-ossp` PostgreSQL extension is ensured to exist | `WebVella.Erp/Database/DbRepository.cs:30` | `CreatePostgresqlExtensions()` runs `CREATE EXTENSION IF NOT EXISTS "uuid-ossp"`, which underpins INTEG-002's UUID generation. |
| **INTEG-012** | Relation key columns are **indexed** for join performance | `WebVella.Erp/Database/DbRepository.cs:461` | `CreateIndex` (with an optional partial `WHERE … IS NOT NULL` at `:480`); N:N `origin_id`/`target_id` indexes are created at `:422`–`:423`. |
| **INTEG-013** | A record id of `Guid.Empty` is rejected on write | `WebVella.Erp/Api/RecordManager.cs:333` | Throws *"Guid.Empty value cannot be used as valid value for record id."* (and *"Invalid record id"* at `:331`), protecting key integrity before the row reaches the database. |

---

## 4. Calculation / Derivation Rules (`CALC-*`)

Calculation and derivation rules cover values the platform **computes or generates itself** rather than accepting from the caller — auto-numbers, generated keys, dashboard metrics, derived task state, and credential hashing. `CALC-010` is included because it documents a derivation capability that is *declared but not implemented*, which is a material as-built fact.

| Rule ID | Rule Statement | Citation (`path:line`) | Rationale / Notes |
|---------|----------------|--------------------------|-------------------|
| **CALC-001** | **AutoNumber** field values are always server-generated; any client-supplied value is ignored | `WebVella.Erp/Api/RecordManager.cs:684` | The write loop `continue`s on `AutoNumberField` (*"Autonumber Value is always autogenerated, this ignored if provided"*); the same exclusion recurs at `:1367`, and `:1867` coerces stored values to `decimal`. |
| **CALC-002** | An AutoNumber field is parameterized by a **starting number** and an optional **display format** | `WebVella.Erp/Api/Models/FieldTypes/AutoNumberField.cs:19` | `StartingNumber` at `:19`, `DisplayFormat` at `:16`; mirrored on the DAL model `WebVella.Erp/Database/FieldTypes/DbAutoNumberField.cs:15`. Defines the sequence's origin and rendering. |
| **CALC-003** | A new record's primary GUID is **auto-generated** when the caller does not supply an `id` | `WebVella.Erp/Api/RecordManager.cs:322` | `if (!record.Properties.ContainsKey("id")) recordId = Guid.NewGuid();`. A supplied id is parsed/validated instead (and `Guid.Empty` is rejected — see INTEG-013). |
| **CALC-004** | **Average approval time** (hours) = Σ(completed − created) ÷ count, rounded to **2 decimal places**; zero when no completed requests | `WebVella.Erp.Plugins.Approval/Services/DashboardMetricsService.cs:189` | `return count > 0 ? Math.Round(totalHours / count, 2) : 0;` inside `GetAverageApprovalTime` (declared at `:146`); only `approved`/`rejected` requests in range are counted. |
| **CALC-005** | **Approval rate** (%) = approved ÷ total × 100, rounded to **1 decimal place**; zero when no completed requests | `WebVella.Erp.Plugins.Approval/Services/DashboardMetricsService.cs:236` | `Math.Round((decimal)approvedCount / totalCount * 100, 1)` inside `GetApprovalRate` (declared at `:205`); the enclosing `return` begins at `:235`. |
| **CALC-006** | User passwords are hashed/compared as **MD5** | `WebVella.Erp/Utilities/PasswordUtil.cs:11` | `GetMd5Hash` (using `MD5.Create()` at `:9`); login compares the hash at `WebVella.Erp/Api/SecurityManager.cs:84`. **Weak by modern standards** — flagged for the security assessment, not changed here. |
| **CALC-007** | A task is **overdue** when its `end_time` is earlier than the start of the current day | `WebVella.Erp.Plugins.Project/Services/TaskService.cs:128` | `EndTimeOverdue` filter `end_time < @currentDateStart`; "due today" and "due" variants are derived at the adjacent lines using `DateTime.Now.Date` boundaries. |
| **CALC-008** | A task's display fields (subject, project, project owner) are **derived from related records** rather than stored redundantly | `WebVella.Erp.Plugins.Project/Services/TaskService.cs:26` | `SetCalculationFields` resolves values by expanding `$task_status_1n_task`, `$project_nn_task` relations via EQL. |
| **CALC-009** | Time logs are recorded in **minutes** and default to **billable**; the service can **filter and retrieve** time logs by project, user, and period (date range) | `WebVella.Erp.Plugins.Project/Services/TimeLogService.cs:20,45-46` | `Create(… int minutes = 0, bool isBillable = true …)` (`:20`) persists `record["minutes"]` (`:45`) and `record["is_billable"]` (`:46`). `GetTimelogsForPeriod` (`:85-105`) builds an EQL `logged_on` date-range query and **optionally filters** by project (`l_related_records CONTAINS @projectId`, `:93`) and user (`created_by = @userId`, `:98`), returning the matching records (`:105`) — it does **not** sum, group, or aggregate. |
| **CALC-010** | The **database-layer Formula field is not implemented** — its class is entirely commented out and marked *"Not supported at the moment."* | `WebVella.Erp/Database/FieldTypes/DbFormulaField.cs:5` | The `DbFormulaField` class body (`:5`–`:13`) is dead/commented code; there is no computed-column derivation at the DAL. An as-built gap, recorded for the roadmap. |

---

## 5. Authorization Rules (`AUTHZ-*`)

Authorization rules define **who may do what**. The platform's authoritative access-control point is `SecurityContext`, consulted by `RecordManager` (for record CRUD) and `EntityManager` (for meta operations). Per-entity permissions are stored as role-GUID lists on each entity definition. The story-specified rows describe the Approval domain's planned authorization. `AUTHZ-010` is the single most important authorization finding in this catalog and is also surfaced in [`security-quality.md`](./security-quality.md).

| Rule ID | Rule Statement | Citation (`path:line`) | Rationale / Notes |
|---------|----------------|--------------------------|-------------------|
| **AUTHZ-001** | Record-level CRUD is gated by `HasEntityPermission`, which checks the current user's roles against the entity's per-operation permission lists | `WebVella.Erp/Api/SecurityContext.cs:63` | `Read`/`Create`/`Update`/`Delete` map to `RecordPermissions.CanRead`/`CanCreate`/`CanUpdate`/`CanDelete` at `:79`–`:86`. The central record-authorization predicate. |
| **AUTHZ-002** | `RecordManager` enforces the matching permission **before** each create/read/update/delete, returning *"Access denied."* on failure | `WebVella.Erp/Api/RecordManager.cs:282` | Create gate at `:282`; Update at `:982`; Delete at `:1645`; Read at `:1759` — each calls `SecurityContext.HasEntityPermission` for the corresponding `EntityPermission`. |
| **AUTHZ-003** | A `RecordManager` constructed with `ignoreSecurity = true` **bypasses** all record permission checks | `WebVella.Erp/Api/RecordManager.cs:40` | Backing field at `:24`; every gate is wrapped in `if (!ignoreSecurity)`. Intended for trusted server-side/system operations; misuse would silently disable authorization. |
| **AUTHZ-004** | The built-in **system user has unlimited permissions**, and `OpenSystemScope()` elevates the current execution to that system identity | `WebVella.Erp/Api/SecurityContext.cs:74` | System-user short-circuit `if (user.Id == SystemIds.SystemUserId) return true;`. `OpenSystemScope()` (at `:134`) pushes the system user onto the scope stack — used by background jobs (see PROC-009). |
| **AUTHZ-005** | When there is **no authenticated user**, permissions are evaluated against the **Guest role** | `WebVella.Erp/Api/SecurityContext.cs:96` | The `else` branch (begins `:91`) checks each `CanRead`/`CanCreate`/`CanUpdate`/`CanDelete` list for `SystemIds.GuestRoleId`, governing anonymous access. |
| **AUTHZ-006** | **Meta operations** (creating/altering entities, fields, relations) require the **Administrator** role | `WebVella.Erp/Api/SecurityContext.cs:117` | `HasMetaPermission` returns true only if the user holds `SystemIds.AdministratorRoleId`. `EntityManager` enforces it before each meta change (e.g., `:452`, `:545`, `:626`), returning *"Access denied."* |
| **AUTHZ-007** | Each entity carries four **role-GUID permission lists** — `CanRead`, `CanCreate`, `CanUpdate`, `CanDelete` | `WebVella.Erp/Database/DbEntity.cs:37` | The `DbRecordPermissions` class (declared at `:37`, with `CanRead` at `:40`) is held by the entity's `RecordPermissions` property at `:28`; these `List<Guid>` role lists are exactly what AUTHZ-001 evaluates. |
| **AUTHZ-008** | Role membership is tested via `IsUserInRole`, accepting either role objects or role GUIDs | `WebVella.Erp/Api/SecurityContext.cs:54` | GUID overload at `:54`, `ErpRole[]` overload at `:45`; the building block for role-based checks throughout the platform. |
| **AUTHZ-009** | A user account must have a **password** and be **enabled** to authenticate | `WebVella.Erp/Api/SecurityManager.cs:280`; `WebVella.Erp.Web/Services/AuthService.cs:32,86` | `SaveUser` enforces *"Password is required."* at `SecurityManager.cs:280` (the `enabled` flag is round-tripped at `:137`). The **enabled-account** gate is enforced at login: cookie sign-in checks `user != null && user.Enabled` in `Authenticate` (`AuthService.cs:32`), and JWT issuance checks the same in `GetTokenAsync` (`AuthService.cs:86`). Disabled or password-less accounts cannot log in. |
| **AUTHZ-010** | **CRITICAL — the MVC `AuthorizeAttribute` is dead code.** The entire class is commented out, so authorization is **not** enforced by this attribute; it is enforced by `SecurityContext` / `RecordManager` permission checks plus the host's authentication scheme | `WebVella.Erp.Web/Security/AuthorizeAttribute.cs:1-147` | Every line of the file is commented out — the line-numbered source ends with a commented closing brace at line 147 (`//}`). Any assumption that controllers are protected by this attribute is incorrect — a notable authorization gap recorded for the security assessment. |
| **AUTHZ-011** | *(Story-specified)* Approval service methods validate `SecurityContext.CurrentUser` before acting and throw `UnauthorizedAccessException` (with a descriptive message) on permission failure | `jira-stories/STORY-004-approval-service-layer.md:53` | Acceptance criterion AC15; an illustrative throw appears in the story at `:186` (*"Authentication required"*). Design-stage; centralizes authorization so it cannot be bypassed via alternate entry points. |
| **AUTHZ-012** | *(Story-specified)* The Approval REST controller is guarded by `[Authorize]`, requiring authentication for every endpoint, and uses `SecurityContext.CurrentUser` for context | `jira-stories/STORY-007-approval-rest-api.md:15` | Authentication-context usage at `:18`; route family `/api/v3.0/p/approval/{resource}/{action}` at `:19`. Note: per AUTHZ-010 the platform's own `AuthorizeAttribute` is dead code, so this refers to the framework `[Authorize]` filter. |
| **AUTHZ-013** | **(Implemented)** The Approval **manager-dashboard** metrics endpoint is restricted to the roles **`manager`, `administrator`, and `admin`**, returning HTTP **403** to other authenticated users | `WebVella.Erp.Plugins.Approval/Controllers/ApprovalController.cs:31-36,131-136` | `AuthorizedDashboardRoles = { "manager", "administrator", "admin" }` (`:31-36`); `IsManagerRole()` (`:92-96`) tests `CurrentUserRoles` against that list, and the dashboard action returns `StatusCode(403, …)` when the check fails (`:131-136`). The controller is also `[Authorize]` (`:20`). **Story-vs-implemented note:** the originating story (`jira-stories/STORY-009-manager-dashboard-metrics.md:32`) specified only the **Manager** role; the shipped controller **broadens** access to the three roles above. |

---

## Implemented vs. Story-Specified Rules

To keep the catalog honest about *what exists* versus *what is specified*, every rule is one of two kinds:

- **Implemented** — the rule is enforced by code that exists in the repository at commit `bfe15661`. These rows cite a `.cs` source file and line. The overwhelming majority of the catalog (all `VAL-*`, all `INTEG-*`, all `CALC-*`, `PROC-001`–`PROC-010`, `AUTHZ-001`–`AUTHZ-010`, and `AUTHZ-013`) is implemented.
- **Story-specified (design-stage)** — the rule is a requirement defined in a Jira story for the **Approval** domain, whose service/controller/job implementation is design-stage. These rows are explicitly labeled *(Story-specified)* and cite a `jira-stories/STORY-00X-*.md` file. They are **requirements, not shipped behavior**, and must not be read as a description of running code.

| Story-specified rule | Source story |
|----------------------|--------------|
| `PROC-011` — approval state machine | `jira-stories/STORY-004-approval-service-layer.md:11` |
| `PROC-012` — approval-triggering hooks | `jira-stories/STORY-005-approval-hooks-integration.md:9` |
| `PROC-013` — notification / escalation / cleanup schedules | `jira-stories/STORY-006-notification-escalation-jobs.md:9` |
| `PROC-014` — hook registration conventions | `jira-stories/STORY-005-approval-hooks-integration.md:22` |
| `AUTHZ-011` — `UnauthorizedAccessException` on permission failure | `jira-stories/STORY-004-approval-service-layer.md:53` |
| `AUTHZ-012` — `[Authorize]` on `ApprovalController` | `jira-stories/STORY-007-approval-rest-api.md:15` |

> Two **implemented** rows document *absent* behavior and are therefore as important as any positive rule: `AUTHZ-010` (the `AuthorizeAttribute` is commented-out dead code) and `CALC-010` (the DAL `DbFormulaField` is commented out, "Not supported"). Both are real, verifiable facts about the current codebase.

---

## Cross-Document Consistency

This catalog is one node in the suite and shares vocabulary and references with its siblings:

- **Terminology** follows the canonical [Glossary in `README.md`](./README.md#glossary--acronyms): *meta-model*, *manager layer*, *hook* (the 12 `IErp*Hook` interfaces), *plugin bootstrap* / *patch-class migration*, *EQL*, *DAL*, and the `rec_*` / `rel_*` table conventions are all used here with their glossary meanings.
- **Architecture** — the manager-layer enforcement points (`EntityManager`, `RecordManager`, `SecurityContext`) and the hook/job lifecycle cited above correspond to the components and sequence diagrams in [`architecture.md`](./architecture.md).
- **Database schema** — the `rec_*`/`rel_*` table conventions, the composite N:N key, and the `entities`/`entity_relations`/`system_settings` meta-tables referenced by `INTEG-*` align with [`database-schema.md`](./database-schema.md).
- **Functional overview** — the Approval, Project, and Mail behaviors cited in `PROC-*`/`CALC-*`/`AUTHZ-*` correspond to the module descriptions and workflows in [`functional-overview.md`](./functional-overview.md).
- **Security & quality** — the MD5 hashing (`CALC-006`) and the dead `AuthorizeAttribute` (`AUTHZ-010`) are analyzed further in [`security-quality.md`](./security-quality.md); remediation options appear in [`modernization-roadmap.md`](./modernization-roadmap.md).

Every `path:line` citation in this document was verified to resolve against the source tree at the commit recorded in the [Generation Metadata](#generation-metadata).

---

*Generated 2026-06-05 15:15 UTC from source commit `bfe15661c7f0c1dae57288d789b854186793b157` (branch `master`). This is an analysis-only, reverse-engineering artifact — no production source, schema, configuration, build, or test file was modified, and all output is confined to `docs/reverse-engineering/`. **74** business rules catalogued across 5 categories, each with an inline `path:line` (or `jira-stories/STORY-00X`) citation.*
