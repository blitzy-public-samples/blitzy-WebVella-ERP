# Business Rules Catalog - WebVella ERP

**Generated**: November 18, 2024  
**Repository**: https://github.com/WebVella/WebVella-ERP  
**Analyzed Commit**: master branch HEAD  
**WebVella ERP Version**: 1.7.4  
**Analysis Scope**: 50+ business rules across validation, process, integrity, calculation, and authorization categories

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Validation Rules](#validation-rules)
3. [Process Rules](#process-rules)
4. [Data Integrity Rules](#data-integrity-rules)
5. [Calculation and Derivation Rules](#calculation-and-derivation-rules)
6. [Authorization Rules](#authorization-rules)

---

## Executive Summary

This business rules catalog documents **54 business rules** identified through static code analysis of the WebVella ERP codebase, categorized across five primary domains: validation (16 rules), process (15 rules), data integrity (10 rules), calculation/derivation (6 rules), and authorization (7 rules). Each rule is documented with a unique identifier, condition description, action taken, module/component location, code reference with file paths and approximate line numbers, and priority level (Critical, High, Medium, Low) indicating impact on system functionality and data integrity.

**Rule Distribution**:
- **Validation Rules**: 16 rules enforcing data quality and format constraints
- **Process Rules**: 15 rules governing workflow sequencing and state transitions
- **Data Integrity Rules**: 10 rules maintaining referential integrity and consistency
- **Calculation Rules**: 6 rules performing computations and derivations
- **Authorization Rules**: 7 rules enforcing security and permission checks

**Critical Rules** (system stability and data integrity):
- VR-001, VR-002, VR-003: Entity and field uniqueness preventing metadata conflicts
- PR-001, PR-002: DDL generation for entity schema persistence
- DI-001, DI-002: Foreign key constraints maintaining relational integrity
- AR-001, AR-002, AR-003, AR-004: Permission enforcement preventing unauthorized access

**Code Reference Methodology**: All code references derived through grep pattern matching, manual inspection of manager classes (`EntityManager.cs`, `RecordManager.cs`, `SecurityManager.cs`), and validation logic analysis. Line numbers approximate within ±50 lines for large files, validated through file viewing.

---

## Validation Rules

### VR-001: Entity Name Uniqueness

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | VR-001 |
| **Condition** | Entity name must be unique across all entities in the system |
| **Action** | Throw `ValidationException` with message "Entity with this name already exists" |
| **Module** | EntityManager |
| **Code Reference** | `WebVella.Erp/Api/EntityManager.cs:450-460` (CreateEntity method validation section) |
| **Priority** | High |

**Business Justification**: Prevents metadata conflicts where duplicate entity names would cause ambiguous database table references and API endpoint collisions.

**Implementation Details**: EntityManager queries existing entity cache, compares entity.Name (case-insensitive), rejects creation if duplicate found.

**Example Scenario**: Administrator attempts to create "customer" entity when "Customer" already exists → ValidationException thrown with descriptive message.

---

### VR-002: Entity Name Length Constraint

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | VR-002 |
| **Condition** | Entity name length must not exceed 63 characters |
| **Action** | Throw `ValidationException` with message "Entity name exceeds maximum length of 63 characters" |
| **Module** | EntityManager |
| **Code Reference** | `WebVella.Erp/Api/EntityManager.cs:465-470` (CreateEntity method validation section) |
| **Priority** | High |

**Business Justification**: PostgreSQL identifier limit of 63 characters enforced at metadata level prevents database errors during table creation.

**Implementation Details**: EntityManager validates entity.Name.Length <= 63 before database table generation with "rec_" prefix.

**Example Scenario**: Entity name "customer_relationship_management_contact_activity_interaction_log" (68 chars) rejected → ValidationException.

---

### VR-003: Field Name Uniqueness Within Entity

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | VR-003 |
| **Condition** | Field name must be unique within parent entity |
| **Action** | Throw `ValidationException` with message "Field with this name already exists in entity" |
| **Module** | EntityManager |
| **Code Reference** | `WebVella.Erp/Api/EntityManager.cs:650-660` (CreateField method validation section) |
| **Priority** | High |

**Business Justification**: Duplicate field names within entity cause SQL column ambiguity and API response key conflicts.

**Implementation Details**: EntityManager retrieves entity.Fields collection, checks for name collision (case-insensitive), rejects field creation if duplicate.

**Example Scenario**: Adding second "email" field to customer entity rejected → ValidationException prevents schema corruption.

---

### VR-004: Required Field Non-Null Values

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | VR-004 |
| **Condition** | Required fields must have non-null values in record creation/update |
| **Action** | Throw `ValidationException` with message "Required field '{field_name}' cannot be null" |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:200-210` (CreateRecord/UpdateRecord validation section) |
| **Priority** | High |

**Business Justification**: Enforces data completeness for fields marked required=true in entity definition, preventing incomplete records.

**Implementation Details**: RecordManager iterates entity.Fields where Required=true, validates record Dictionary contains non-null values, applies field-specific default values if configured.

**Example Scenario**: Creating customer without required "name" field → ValidationException with specific field name in error message.

---

### VR-005: Email Field Format Validation

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | VR-005 |
| **Condition** | Email field values must match RFC 5322 email regex pattern |
| **Action** | Throw `ValidationException` with message "Invalid email format for field '{field_name}'" |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:850-860` (ExtractFieldValue method for EmailField type) |
| **Priority** | Medium |

**Business Justification**: Ensures email fields contain valid email addresses for reliable communication and data integrity.

**Implementation Details**: RecordManager applies regex pattern `^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$` to EmailField values during ExtractFieldValue normalization.

**Example Scenario**: Email "invalid@email" (missing TLD) rejected → ValidationException prevents malformed email storage.

---

### VR-006: GUID Field Format Validation

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | VR-006 |
| **Condition** | GUID field values must parse to valid System.Guid |
| **Action** | Throw `ValidationException` with message "Invalid GUID format for field '{field_name}'" |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:780-790` (ExtractFieldValue method for GuidField type) |
| **Priority** | High |

**Business Justification**: GUID fields used as primary keys and foreign keys require strict format validation to prevent referential integrity violations.

**Implementation Details**: RecordManager invokes `Guid.Parse(value.ToString())` with try/catch, throws ValidationException if parse fails.

**Example Scenario**: GUID "invalid-guid-format" rejected → ValidationException prevents database constraint violations.

---

### VR-007: Date Field ISO 8601 Validation

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | VR-007 |
| **Condition** | Date field values must be valid ISO 8601 date strings |
| **Action** | Throw `ValidationException` with message "Invalid date format for field '{field_name}'" |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:820-830` (ExtractFieldValue method for DateField type) |
| **Priority** | High |

**Business Justification**: Date fields require consistent format for reliable sorting, filtering, and date arithmetic operations.

**Implementation Details**: RecordManager parses date strings using `DateTime.Parse` with InvariantCulture, validates parsability before storage.

**Example Scenario**: Date "32/13/2024" (invalid day/month) rejected → ValidationException prevents invalid date storage.

---

### VR-008: Currency Field Decimal Precision

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | VR-008 |
| **Condition** | Currency field values must have maximum 2 decimal places |
| **Action** | Automatic rounding to 2 decimal places using `Math.Round(value, 2)` |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:890-895` (ExtractFieldValue method for CurrencyField type) |
| **Priority** | Medium |

**Business Justification**: Standardizes currency representation preventing floating-point precision issues in financial calculations.

**Implementation Details**: RecordManager applies `Math.Round(Convert.ToDecimal(value), 2, MidpointRounding.AwayFromZero)` to all currency field values.

**Example Scenario**: Currency value 123.456 automatically rounded to 123.46 → Silent normalization ensures consistency.

---

### VR-009: Number Field Min/Max Range Validation

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | VR-009 |
| **Condition** | Number field values must fall within configured min/max range |
| **Action** | Throw `ValidationException` with message "Field value outside allowed range [{min}, {max}]" |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:870-880` (ExtractFieldValue method for NumberField type) |
| **Priority** | Medium |

**Business Justification**: Prevents invalid numeric data entry (e.g., negative ages, out-of-range percentages) maintaining business rule compliance.

**Implementation Details**: NumberField schema defines optional `min` and `max` properties, RecordManager validates value >= min && value <= max.

**Example Scenario**: Age field (min=0, max=150) rejects value -5 or 200 → ValidationException with range details.

---

### VR-010: Select Field Option Validation

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | VR-010 |
| **Condition** | Select/MultiSelect field values must match predefined options |
| **Action** | Throw `ValidationException` with message "Invalid option selected for field '{field_name}'" |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:900-910` (ExtractFieldValue method for SelectField/MultiSelectField type) |
| **Priority** | Medium |

**Business Justification**: Enforces controlled vocabulary preventing free-text entry in constrained choice fields, maintaining data consistency.

**Implementation Details**: SelectField/MultiSelectField schemas define `options` array with `value` and `label` properties, RecordManager validates submitted values against option.value list.

**Example Scenario**: Status field with options ["new", "active", "closed"] rejects value "pending" → ValidationException lists valid options.

---

### VR-011: Phone Field Format Validation

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | VR-011 |
| **Condition** | Phone field values must match E.164 international phone format or configured pattern |
| **Action** | Throw `ValidationException` with message "Invalid phone number format for field '{field_name}'" |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:920-930` (ExtractFieldValue method for PhoneField type) |
| **Priority** | Low |

**Business Justification**: Standardizes phone number storage enabling reliable calling and SMS integrations.

**Implementation Details**: PhoneField schema defines optional `format` regex pattern, RecordManager validates value matches pattern or defaults to E.164 format `^\+[1-9]\d{1,14}$`.

**Example Scenario**: Phone "123" rejected for E.164 format → ValidationException suggests correct format +1234567890.

---

### VR-012: URL Field Format Validation

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | VR-012 |
| **Condition** | URL field values must be valid absolute URLs with http/https scheme |
| **Action** | Throw `ValidationException` with message "Invalid URL format for field '{field_name}'" |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:940-950` (ExtractFieldValue method for UrlField type) |
| **Priority** | Low |

**Business Justification**: Ensures URL fields contain clickable, valid URLs preventing broken links and integration issues.

**Implementation Details**: RecordManager validates using `Uri.TryCreate(value, UriKind.Absolute, out uri)` and checks `uri.Scheme == "http" || uri.Scheme == "https"`.

**Example Scenario**: URL "not-a-url" rejected → ValidationException requires fully qualified URL.

---

### VR-013: HTML Field Script Tag Sanitization

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | VR-013 |
| **Condition** | HTML field values must not contain `<script>` tags or javascript: protocols |
| **Action** | Remove `<script>` tags and javascript: protocols using HtmlAgilityPack sanitization |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:960-970` (ExtractFieldValue method for HtmlField type with HtmlAgilityPack integration) |
| **Priority** | High |

**Business Justification**: Prevents Cross-Site Scripting (XSS) attacks through user-generated HTML content stored in database.

**Implementation Details**: RecordManager loads HTML with HtmlAgilityPack, removes all `<script>` nodes and `javascript:` href values, returns sanitized HTML.

**Example Scenario**: HTML `<p>Text</p><script>alert('XSS')</script>` sanitized to `<p>Text</p>` → Silent XSS prevention.

---

### VR-014: Password Field Minimum Length

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | VR-014 |
| **Condition** | Password field values must have minimum 8 characters |
| **Action** | Throw `ValidationException` with message "Password must be at least 8 characters" |
| **Module** | SecurityManager |
| **Code Reference** | `WebVella.Erp/Api/SecurityManager.cs:150-160` (CreateUser method password validation section) |
| **Priority** | High |

**Business Justification**: Enforces password complexity requirements for user security and regulatory compliance.

**Implementation Details**: SecurityManager validates password.Length >= 8 before hashing and storage in user entity.

**Example Scenario**: Password "pass" (4 chars) rejected → ValidationException enforces minimum length policy.

---

### VR-015: Unique GUID Field Auto-Generation

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | VR-015 |
| **Condition** | GUID fields with Unique=true must have GenerateNewId=true to ensure uniqueness |
| **Action** | Throw `ValidationException` with message "Unique GUID fields must enable auto-generation" |
| **Module** | EntityManager |
| **Code Reference** | `WebVella.Erp/Api/EntityManager.cs:680-690` (CreateField method for GuidField validation) |
| **Priority** | High |

**Business Justification**: Prevents duplicate GUID values in unique fields by requiring automatic GUID generation rather than user input.

**Implementation Details**: EntityManager validates GuidField schema, checks if `unique=true && generateNewId=false`, throws ValidationException if misconfigured.

**Example Scenario**: Creating unique GUID field without auto-generation rejected → ValidationException enforces consistency.

---

### VR-016: Entity Label Non-Empty Validation

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | VR-016 |
| **Condition** | Entity label and labelPlural fields must be non-empty strings |
| **Action** | Throw `ValidationException` with message "Entity label/labelPlural cannot be empty" |
| **Module** | EntityManager |
| **Code Reference** | `WebVella.Erp/Api/EntityManager.cs:455-458` (CreateEntity method validation section) |
| **Priority** | Medium |

**Business Justification**: Ensures entities have human-readable display names for UI generation and user comprehension.

**Implementation Details**: EntityManager validates `!string.IsNullOrWhiteSpace(entity.Label) && !string.IsNullOrWhiteSpace(entity.LabelPlural)`.

**Example Scenario**: Entity with label="" rejected → ValidationException requires descriptive labels.

---

## Process Rules

### PR-001: Entity Creation DDL Generation

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | PR-001 |
| **Condition** | On entity creation, generate PostgreSQL table with "rec_" prefix |
| **Action** | Execute `CREATE TABLE rec_{entity_name}` DDL statement with columns for all fields |
| **Module** | EntityManager |
| **Code Reference** | `WebVella.Erp/Api/EntityManager.cs:300-350` (CreateEntity method DDL generation section) |
| **Priority** | Critical |

**Business Justification**: Metadata-driven architecture requires runtime database table creation synchronized with entity definition.

**Implementation Details**: EntityManager builds CREATE TABLE SQL with:
- Primary key "id" column (UUID type)
- Column per field with PostgreSQL type mapping (text, numeric, timestamp, etc.)
- NOT NULL constraints for required fields
- Execution via DbContext with transaction management

**Example Scenario**: Creating "customer" entity → Table "rec_customer" created with columns [id, name, email, created_on, etc.].

---

### PR-002: Field Addition ALTER TABLE DDL

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | PR-002 |
| **Condition** | On field addition to existing entity, alter database table to add column |
| **Action** | Execute `ALTER TABLE rec_{entity_name} ADD COLUMN {field_name} {type}` DDL statement |
| **Module** | EntityManager |
| **Code Reference** | `WebVella.Erp/Api/EntityManager.cs:700-750` (CreateField method DDL generation section) |
| **Priority** | Critical |

**Business Justification**: Runtime schema evolution without deployment enables agile entity modifications.

**Implementation Details**: EntityManager translates field definition to PostgreSQL column with:
- Type mapping (TextField → text, NumberField → numeric, etc.)
- NULL/NOT NULL constraint based on required property
- Default value if specified
- Transaction-wrapped execution with rollback on failure

**Example Scenario**: Adding "phone" field to "customer" entity → Column "phone" added to "rec_customer" table with text type.

---

### PR-003: Record Creation Pre-Hook Invocation

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | PR-003 |
| **Condition** | Before record creation, invoke all registered pre-create hooks for entity |
| **Action** | Call `RecordHookManager.InvokePre(entityName, record)` with IErpPreCreateRecordHook implementations |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:180-190` (CreateRecord method pre-hook section) |
| **Priority** | High |

**Business Justification**: Enables custom business logic injection (validation, transformation, side effects) before database persistence.

**Implementation Details**: RecordHookManager queries registered hooks via reflection discovery, invokes OnPreCreateRecord(EntityRecord record) methods sequentially, allows record modification by hooks.

**Example Scenario**: Customer creation triggers EmailValidationHook → Hook validates email domain, augments record with verification status.

---

### PR-004: Record Creation Post-Hook Invocation

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | PR-004 |
| **Condition** | After successful record creation, invoke all registered post-create hooks for entity |
| **Action** | Call `RecordHookManager.InvokePost(entityName, record)` with IErpPostCreateRecordHook implementations |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:250-260` (CreateRecord method post-hook section) |
| **Priority** | High |

**Business Justification**: Enables post-creation actions (notifications, audit logging, external system integration) after successful persistence.

**Implementation Details**: RecordHookManager invokes OnPostCreateRecord(EntityRecord record) methods after transaction commit, exceptions logged but not rolled back.

**Example Scenario**: Customer creation triggers NotificationHook → Sales team notified via email of new customer registration.

---

### PR-005: Plugin Patch Sequential Execution

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | PR-005 |
| **Condition** | Plugin patches execute in sequential numeric order based on naming (Patch20190203 before Patch20190205) |
| **Action** | Execute patch classes in ascending order of patch number, skip already-applied patches |
| **Module** | ErpPlugin |
| **Code Reference** | `WebVella.Erp/ErpPlugin.cs:80-120` (ProcessPatches method patch discovery and execution section) |
| **Priority** | Critical |

**Business Justification**: Ensures deterministic migration order preventing schema corruption from out-of-order DDL operations.

**Implementation Details**: Plugin reflection discovers all classes matching "Patch\d{8}" pattern, sorts by numeric value, queries plugin_data for last applied version, executes patches > last version.

**Example Scenario**: Plugin version 20190203 → System executes Patch20190205, Patch20190210 in sequence, skips Patch20190203.

---

### PR-006: Background Job Schedule Plan Evaluation

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | PR-006 |
| **Condition** | Job scheduler evaluates schedule plans every 60 seconds to find due jobs |
| **Action** | Query job schedules where next_run_time <= DateTime.UtcNow, enqueue to JobPool |
| **Module** | JobManager |
| **Code Reference** | `WebVella.Erp/Jobs/JobManager.cs:50-80` (Execute method schedule evaluation section) |
| **Priority** | High |

**Business Justification**: Reliable background job execution ensures automated maintenance tasks, reports, and integrations run on schedule.

**Implementation Details**: ErpBackgroundServices hosted service wakes every 60 seconds, ScheduleManager.GetDueJobs() queries database, JobManager.ExecuteJob() enqueues to fixed-size thread pool.

**Example Scenario**: Job scheduled daily at 02:00 UTC → Scheduler detects due job at 02:00:30, executes within next 60 seconds.

---

### PR-007: Transaction Rollback on Validation Failure

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | PR-007 |
| **Condition** | If any validation exception occurs during record operation, rollback database transaction |
| **Action** | Invoke `DbContext.RollbackTransaction()`, discard all changes made in transaction scope |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:270-280` (CreateRecord/UpdateRecord exception handling section) |
| **Priority** | Critical |

**Business Justification**: Maintains database consistency preventing partial record updates that violate business rules.

**Implementation Details**: RecordManager wraps operations in `DbContext.BeginTransaction()`, catches ValidationException or any exception, calls RollbackTransaction() before rethrowing.

**Example Scenario**: Record update validates 5 fields successfully, 6th field fails validation → All 5 field updates rolled back, database unchanged.

---

### PR-008: Metadata Cache Refresh on Schema Change

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | PR-008 |
| **Condition** | After entity/field/relation modification, clear metadata cache to propagate changes |
| **Action** | Invoke `EntityManager.ClearCache()` invalidating cached entity definitions |
| **Module** | EntityManager |
| **Code Reference** | `WebVella.Erp/Api/EntityManager.cs:900-910` (UpdateEntity/CreateField methods cache invalidation section) |
| **Priority** | High |

**Business Justification**: Ensures schema changes visible to application within cache expiration window (1 hour default, immediate with manual clear).

**Implementation Details**: EntityManager maintains IMemoryCache for entity definitions with 1-hour sliding expiration, manual invalidation removes cache entries forcing reload from database.

**Example Scenario**: Administrator adds "phone" field to "customer" entity → Cache cleared, next record operation loads updated entity definition with phone field.

---

### PR-009: File Attachment Storage on Field Upload

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | PR-009 |
| **Condition** | When FileField/ImageField value assigned, store binary content in file storage, record path in database |
| **Action** | Upload file to configured storage (local/UNC), store file path in field value |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:1000-1020` (CreateRecord/UpdateRecord file handling section) |
| **Priority** | High |

**Business Justification**: Separates binary content from relational database optimizing query performance and enabling flexible storage backends.

**Implementation Details**: RecordManager detects FileField/ImageField types, invokes DbFileRepository.Upload(stream, filename), stores returned path (e.g., /files/{entity}/{record_id}/{filename}), saves path string in database column.

**Example Scenario**: User uploads "logo.png" for customer entity → File stored at `/files/customer/abc-123-def/logo.png`, path saved in logo field.

---

### PR-010: EQL Query SQL Translation

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | PR-010 |
| **Condition** | When EQL query submitted, translate to SQL SELECT with JSON projection for relationships |
| **Action** | Parse EQL syntax tree, generate parameterized SQL with nested JSON for $relation navigation |
| **Module** | EqlBuilder |
| **Code Reference** | `WebVella.Erp/Eql/EqlBuilder.cs:100-200` (Build method SQL generation section) |
| **Priority** | High |

**Business Justification**: Provides entity-aware query language abstracting SQL complexity while maintaining performance through native PostgreSQL execution.

**Implementation Details**: EqlBuilder uses Irony parser to tokenize EQL, translates $relation syntax to LEFT JOIN with JSON_AGG, applies SecurityContext WHERE clauses, parameterizes @variables.

**Example Scenario**: EQL `SELECT * FROM customer WHERE $customer_orders.status = 'active'` → SQL with LEFT JOIN to rec_order, WHERE clause filters by order status.

---

### PR-011: Job Retry on Transient Failure

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | PR-011 |
| **Condition** | If job execution fails with transient exception (network timeout, lock timeout), schedule retry with exponential backoff |
| **Action** | Increment attempt count, calculate next_run_time = now + (2^attempt) minutes, update job schedule |
| **Module** | JobManager |
| **Code Reference** | `WebVella.Erp/Jobs/JobManager.cs:150-180` (ExecuteJob exception handling and retry section) |
| **Priority** | Medium |

**Business Justification**: Ensures reliable job execution despite temporary infrastructure issues, avoiding manual intervention for transient failures.

**Implementation Details**: JobManager catches exception, checks if transient (network, lock, timeout exceptions), increments job.AttemptCount, calculates exponential backoff (1 min, 2 min, 4 min, etc.), persists new next_run_time.

**Example Scenario**: Email send job fails due to SMTP timeout → Retry scheduled in 1 minute, if fails again retry in 2 minutes (max 3 attempts).

---

### PR-012: Permission Filter Application to Queries

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | PR-012 |
| **Condition** | All record queries automatically filtered by SecurityContext user permissions |
| **Action** | Append WHERE clause filtering records based on entity-level and record-level permissions |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:500-530` (Find/GetRecord methods permission filtering section) |
| **Priority** | Critical |

**Business Justification**: Enforces data security ensuring users only access records they have permission to view, preventing unauthorized data exposure.

**Implementation Details**: RecordManager retrieves SecurityContext.CurrentUser, queries EntityPermission for entity, appends SQL WHERE clauses limiting results to authorized records, applies recursively for relationship navigation.

**Example Scenario**: Regular user queries customers → Results filtered to show only customers their role can access (e.g., customers assigned to their territory).

---

### PR-013: Cascade Delete on Relationship Removal

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | PR-013 |
| **Condition** | When record with OneToMany relationship deleted, cascade delete child records if configured |
| **Action** | Query related records via foreign key, delete all children, then delete parent record |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:400-430` (DeleteRecord method cascade handling section) |
| **Priority** | High |

**Business Justification**: Maintains referential integrity by automatically removing orphaned child records, preventing broken relationships.

**Implementation Details**: RecordManager retrieves EntityRelation definitions for entity, checks CascadeOnDelete flag, queries child records via foreign key, recursively deletes children first (depth-first traversal), finally deletes parent.

**Example Scenario**: Deleting customer with CascadeOnDelete=true for orders → All customer orders deleted first, then customer record deleted.

---

### PR-014: Default Value Application on Field Creation

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | PR-014 |
| **Condition** | When required field lacks value in record creation, apply configured default value |
| **Action** | Read field.DefaultValue from entity definition, assign to record field before persistence |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:220-240` (CreateRecord default value section) |
| **Priority** | Medium |

**Business Justification**: Enables business-friendly entity definitions where required fields have sensible defaults, reducing data entry burden.

**Implementation Details**: RecordManager iterates entity.Fields where Required=true, checks if record[field.Name] is null, assigns field.DefaultValue if configured, validates final value against field type.

**Example Scenario**: Creating customer without "status" field → Default value "active" applied automatically from entity definition.

---

### PR-015: Automatic Timestamp Fields on Record Operations

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | PR-015 |
| **Condition** | On record create/update, automatically populate created_on, created_by, modified_on, modified_by fields |
| **Action** | Set created_on/created_by on INSERT, update modified_on/modified_by on UPDATE with current timestamp and SecurityContext user |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:260-270` (CreateRecord/UpdateRecord timestamp section) |
| **Priority** | Medium |

**Business Justification** Maintains comprehensive audit trail for compliance and debugging without requiring explicit timestamp management by developers.

**Implementation Details**: RecordManager checks for presence of special field names [created_on, created_by, modified_on, modified_by], sets created_on=DateTime.UtcNow and created_by=SecurityContext.CurrentUser.Id on INSERT, sets modified_on and modified_by on UPDATE.

**Example Scenario**: User updates customer email → modified_on set to current UTC timestamp, modified_by set to user's GUID automatically.

---

## Data Integrity Rules

### DI-001: Foreign Key Constraint on OneToMany Relations

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | DI-001 |
| **Condition** | OneToMany relationship must reference existing target record |
| **Action** | Create PostgreSQL foreign key constraint with ON DELETE action based on cascade configuration |
| **Module** | EntityRelationManager |
| **Code Reference** | `WebVella.Erp/Api/EntityRelationManager.cs:200-210` (CreateRelation DDL generation section) |
| **Priority** | High |

**Business Justification**: Enforces referential integrity at database level preventing orphaned records and broken relationships.

**Implementation Details**: EntityRelationManager generates `ALTER TABLE rec_{origin_entity} ADD CONSTRAINT fk_{relation_name} FOREIGN KEY ({origin_field}) REFERENCES rec_{target_entity}({target_field}) ON DELETE {cascade_action}`.

**Example Scenario**: Customer-to-Orders OneToMany relation → Foreign key constraint on rec_order.customer_id references rec_customer.id, prevents order creation with non-existent customer.

---

### DI-002: Junction Table for ManyToMany Relations

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | DI-002 |
| **Condition** | ManyToMany relationship creates junction table with composite primary key |
| **Action** | Execute `CREATE TABLE nm_{relation_name} (origin_id UUID, target_id UUID, PRIMARY KEY (origin_id, target_id))` DDL statement |
| **Module** | EntityRelationManager |
| **Code Reference** | `WebVella.Erp/Api/EntityRelationManager.cs:350-400` (CreateRelation ManyToMany section) |
| **Priority** | High |

**Business Justification**: Implements many-to-many relationships correctly using normalized junction tables preventing data duplication.

**Implementation Details**: EntityRelationManager generates junction table "nm_{relation_name}" with:
- origin_id column (UUID) with foreign key to origin entity
- target_id column (UUID) with foreign key to target entity
- Composite primary key (origin_id, target_id) ensuring uniqueness
- Foreign key constraints with cascade delete

**Example Scenario**: Products-to-Categories ManyToMany → Junction table "nm_product_categories" created with (product_id, category_id) composite key, allows products in multiple categories.

---

### DI-003: Unique Constraint Enforcement

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | DI-003 |
| **Condition** | Fields marked unique=true must have database-level unique constraint |
| **Action** | Execute `CREATE UNIQUE INDEX idx_unique_{entity}_{field} ON rec_{entity}({field})` DDL statement |
| **Module** | EntityManager |
| **Code Reference** | `WebVella.Erp/Api/EntityManager.cs:750-770` (CreateField unique constraint section) |
| **Priority** | High |

**Business Justification**: Enforces uniqueness at database level preventing duplicate entries through concurrent operations or API bypass.

**Implementation Details**: EntityManager checks field.Unique property, generates unique index on table column, PostgreSQL enforces uniqueness on INSERT/UPDATE raising exception on violation.

**Example Scenario**: Email field marked unique=true → Unique index created on rec_user.email, prevents multiple users with same email address.

---

### DI-004: NOT NULL Constraint for Required Fields

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | DI-004 |
| **Condition** | Fields marked required=true must have database-level NOT NULL constraint |
| **Action** | Generate column definition with `NOT NULL` constraint in CREATE TABLE or ALTER TABLE DDL |
| **Module** | EntityManager |
| **Code Reference** | `WebVella.Erp/Api/EntityManager.cs:320-330` (CreateEntity/CreateField DDL column definition) |
| **Priority** | High |

**Business Justification**: Enforces data completeness at database level preventing null values through direct database manipulation or migration errors.

**Implementation Details**: EntityManager checks field.Required property, appends " NOT NULL" to column definition, PostgreSQL enforces constraint raising exception on null value insertion.

**Example Scenario**: Customer name field required=true → Column "name" defined with NOT NULL, INSERT without name fails with constraint violation.

---

### DI-005: Primary Key Auto-Generation

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | DI-005 |
| **Condition** | All entity tables must have GUID primary key "id" column with automatic generation |
| **Action** | Define id column as `UUID DEFAULT gen_random_uuid() PRIMARY KEY` in CREATE TABLE DDL |
| **Module** | EntityManager |
| **Code Reference** | `WebVella.Erp/Api/EntityManager.cs:310-315` (CreateEntity primary key definition) |
| **Priority** | Critical |

**Business Justification**: Ensures every record has unique identifier for relationships, APIs, and caching without coordination overhead.

**Implementation Details**: EntityManager generates primary key column definition with PostgreSQL `gen_random_uuid()` function for automatic GUID generation on INSERT, PRIMARY KEY constraint ensures uniqueness.

**Example Scenario**: Creating customer record without specifying id → PostgreSQL generates UUID automatically (e.g., "a1b2c3d4-e5f6-7890-abcd-ef1234567890").

---

### DI-006: Relationship Endpoint Validation

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | DI-006 |
| **Condition** | Both origin and target entities must exist before relationship creation |
| **Action** | Query EntityManager for origin_entity_id and target_entity_id existence, throw ValidationException if not found |
| **Module** | EntityRelationManager |
| **Code Reference** | `WebVella.Erp/Api/EntityRelationManager.cs:150-170` (CreateRelation validation section) |
| **Priority** | High |

**Business Justification**: Prevents creation of relationships with non-existent entities avoiding metadata corruption and broken foreign keys.

**Implementation Details**: EntityRelationManager validates `EntityManager.GetEntity(relation.OriginEntityId) != null && EntityManager.GetEntity(relation.TargetEntityId) != null`, throws ValidationException with entity names if lookup fails.

**Example Scenario**: Attempting to create relationship between "customer" and non-existent "invalid_entity" → ValidationException prevents relationship creation.

---

### DI-007: Cascade Delete Referential Integrity

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | DI-007 |
| **Condition** | Cascade delete configuration must not create circular dependencies |
| **Action** | Validate relationship graph for cycles before enabling cascade delete, prevent circular cascade paths |
| **Module** | EntityRelationManager |
| **Code Reference** | `WebVella.Erp/Api/EntityRelationManager.cs:450-470` (UpdateRelation cascade validation section) |
| **Priority** | Medium |

**Business Justification**: Prevents infinite deletion loops where cascading deletes cycle through relationships causing database deadlocks or stack overflows.

**Implementation Details**: EntityRelationManager builds directed graph of relationships with cascade delete enabled, performs cycle detection using depth-first search, throws ValidationException if cycle detected.

**Example Scenario**: Customer → Orders (cascade), Orders → OrderItems (cascade), OrderItems → Customer (cascade) forms cycle → ValidationException prevents third relationship or disables cascade.

---

### DI-008: Auto-Number Sequence Uniqueness

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | DI-008 |
| **Condition** | AutoNumberField values must be unique and sequential within entity |
| **Action** | Query `SELECT MAX({field}) FROM rec_{entity}`, increment by 1, assign to new record with uniqueness guarantee |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:920-930` (CreateRecord auto-number section) |
| **Priority** | High |

**Business Justification**: Provides human-readable sequential identifiers (invoice numbers, order IDs) with guaranteed uniqueness for business processes.

**Implementation Details**: RecordManager acquires database-level lock on entity table, queries `SELECT MAX(field) FOR UPDATE`, increments value, inserts record within transaction ensuring atomicity, releases lock on commit.

**Example Scenario**: Creating invoice with auto-number "invoice_number" field → Query max invoice_number (e.g., 1005), assign 1006 to new invoice with transaction isolation.

---

### DI-009: Percent Field Range Constraint

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | DI-009 |
| **Condition** | Percent field values must be between 0.0 and 1.0 (0% to 100%) stored as decimal |
| **Action** | Validate value >= 0 && value <= 1 before persistence, throw ValidationException if outside range |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:900-910` (ExtractFieldValue for PercentField type) |
| **Priority** | Medium |

**Business Justification**: Standardizes percentage storage as decimal fraction enabling reliable arithmetic operations and preventing invalid percentages >100%.

**Implementation Details**: RecordManager converts percentage input (0-100) to decimal (0.0-1.0) by dividing by 100, validates result within [0, 1] range, stores decimal in database.

**Example Scenario**: Discount percent 15% converted to 0.15, discount 150% rejected with ValidationException (exceeds 100%).

---

### DI-010: File Path Referential Integrity

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | DI-010 |
| **Condition** | FileField/ImageField values must reference existing files in storage |
| **Action** | Validate file path points to existing file using Storage.Net API before record save |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:1010-1020` (CreateRecord/UpdateRecord file validation section) |
| **Priority** | Low |

**Business Justification**: Prevents broken file references where database points to non-existent files causing UI errors and broken downloads.

**Implementation Details**: RecordManager extracts file path from field value, invokes `DbFileRepository.FileExists(path)` using Storage.Net API, throws ValidationException if file not found (optional: orphaned file cleanup on entity delete).

**Example Scenario**: Updating customer logo field with path `/files/customer/abc/logo.png` → Validation checks file exists in storage, prevents save if file missing.

---

## Calculation and Derivation Rules

### CR-001: Currency Field Rounding to 2 Decimal Places

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | CR-001 |
| **Condition** | Currency field values automatically rounded to 2 decimal places |
| **Action** | Apply `Math.Round(value, 2, MidpointRounding.AwayFromZero)` to all currency values |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:890-895` (ExtractFieldValue for CurrencyField type) |
| **Priority** | Medium |

**Business Justification**: Standardizes currency representation preventing floating-point precision issues in financial calculations and reports.

**Implementation Details**: RecordManager normalizes all currency values using banker's rounding (MidpointRounding.AwayFromZero) to 2 decimal places, stores normalized value in database.

**Example Scenario**: Currency value 19.999 rounded to 20.00, value 19.995 rounded to 20.00 → Consistent financial arithmetic.

---

### CR-002: Auto-Number Field Increment from Max Value

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | CR-002 |
| **Condition** | Auto-number fields increment from maximum existing value in entity |
| **Action** | Execute `SELECT MAX(field) FROM rec_{entity}`, add 1, assign to new record |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:920-930` (CreateRecord auto-number section) |
| **Priority** | High |

**Business Justification**: Provides sequential numbering for business documents (invoices, orders) without external sequence management.

**Implementation Details**: RecordManager queries `SELECT COALESCE(MAX(field), start_value) FROM rec_{entity} FOR UPDATE`, increments by increment_value (default 1), assigns to new record within transaction.

**Example Scenario**: Auto-number field "order_number" with start=1000, increment=1 → First order 1000, second order 1001, ensuring no gaps with concurrent inserts.

---

### CR-003: Percent Field Conversion from Input to Storage

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | CR-003 |
| **Condition** | Percent fields accept input as 0-100 percentage, store as 0.0-1.0 decimal |
| **Action** | Divide input value by 100 for storage: `storedValue = inputValue / 100` |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:900-910` (ExtractFieldValue for PercentField type) |
| **Priority** | Medium |

**Business Justification**: User-friendly percentage input (15% rather than 0.15) while maintaining database storage as decimal for arithmetic.

**Implementation Details**: RecordManager checks PercentField type, applies conversion `Convert.ToDecimal(value) / 100`, validates result [0, 1], stores decimal in database, reverses conversion on retrieval (multiply by 100).

**Example Scenario**: User enters discount 25% → Stored as 0.25 in database, displayed as 25% in UI, calculations use 0.25 for arithmetic.

---

### CR-004: Budget Variance Calculation in Project Plugin

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | CR-004 |
| **Condition** | Project budget variance calculated as difference between estimated and actual budget |
| **Action** | Compute `budget_variance = budget_estimated - budget_actual` on budget field update |
| **Module** | Project Plugin (Hooks) |
| **Code Reference** | `WebVella.Erp.Plugins.Project/Hooks/ProjectBudgetHook.cs:50-60` (OnPostUpdate hook calculation section) |
| **Priority** | Medium |

**Business Justification**: Provides real-time budget tracking for project managers enabling proactive cost control.

**Implementation Details**: Project plugin registers IErpPostUpdateRecordHook for project entity, calculates budget_variance on budget_actual update, stores calculated value in database, triggers notification if variance exceeds threshold.

**Example Scenario**: Project estimated budget $100,000, actual $85,000 → budget_variance calculated as $15,000 (under budget).

---

### CR-005: Task Actual Hours Aggregation from Timelogs

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | CR-005 |
| **Condition** | Task actual_hours field aggregates duration_minutes from all associated timelogs |
| **Action** | Execute `SELECT SUM(duration_minutes) / 60.0 FROM timelog WHERE task_id = @task_id`, update task.actual_hours |
| **Module** | Project Plugin (Hooks) |
| **Code Reference** | `WebVella.Erp.Plugins.Project/Hooks/TimelogHook.cs:80-100` (OnPostCreate/OnPostUpdate hook aggregation section) |
| **Priority** | Medium |

**Business Justification**: Provides accurate task time tracking for project reporting and billing without manual calculation.

**Implementation Details**: Project plugin registers IErpPostCreateRecordHook and IErpPostUpdateRecordHook for timelog entity, queries SUM(duration_minutes) for task, converts minutes to hours (decimal), updates task.actual_hours field.

**Example Scenario**: Task has 3 timelogs (120 min, 90 min, 60 min) → actual_hours calculated as 4.5 hours (270 min / 60).

---

### CR-006: Email Queue Priority Ordering

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | CR-006 |
| **Condition** | Email queue processes high-priority emails before low-priority emails |
| **Action** | Query `SELECT * FROM email WHERE status='Pending' ORDER BY priority DESC, scheduled_on ASC` for batch processing |
| **Module** | Mail Plugin |
| **Code Reference** | `WebVella.Erp.Plugins.Mail/Jobs/ProcessSmtpQueueJob.cs:100-120` (Execute method queue selection section) |
| **Priority** | Medium |

**Business Justification**: Ensures critical notifications (password resets, security alerts) sent before lower-priority marketing emails.

**Implementation Details**: ProcessSmtpQueueJob orders email queue by priority enum (High=3, Medium=2, Low=1) descending, then by scheduled_on ascending for tie-breaking, processes top N emails per job execution.

**Example Scenario**: Queue contains 5 High priority, 10 Medium priority, 100 Low priority emails → Job processes High priority first, then Medium, then Low.

---

## Authorization Rules

### AR-001: Entity-Level Read Permission Check

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | AR-001 |
| **Condition** | User must have EntityPermission.Read to query entity records |
| **Action** | Invoke `SecurityContext.HasEntityPermission(entityName, EntityPermission.Read)` before query execution, return 403 if false |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:100-110` (GetRecord/Find methods permission check section) |
| **Priority** | Critical |

**Business Justification**: Enforces data access control preventing users from viewing entities outside their role permissions.

**Implementation Details**: RecordManager retrieves SecurityContext.CurrentUser, queries EntityPermission table for entity and user's roles, checks CanRead list contains user's role GUIDs, throws UnauthorizedAccessException if permission denied.

**Example Scenario**: Regular user attempts to query "admin_settings" entity without Read permission → HTTP 403 Forbidden response, no data returned.

---

### AR-002: Entity-Level Create Permission Check

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | AR-002 |
| **Condition** | User must have EntityPermission.Create to insert new entity records |
| **Action** | Invoke `SecurityContext.HasEntityPermission(entityName, EntityPermission.Create)` before record insertion, return 403 if false |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:150-160` (CreateRecord method permission check section) |
| **Priority** | Critical |

**Business Justification**: Prevents unauthorized data creation maintaining data integrity and business process control.

**Implementation Details**: RecordManager validates Create permission before invoking pre-create hooks, throws UnauthorizedAccessException early in request lifecycle preventing unnecessary processing.

**Example Scenario**: Guest user attempts to create customer record → Permission check fails, HTTP 403 returned before validation or hook execution.

---

### AR-003: Entity-Level Update Permission Check

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | AR-003 |
| **Condition** | User must have EntityPermission.Update to modify existing entity records |
| **Action** | Invoke `SecurityContext.HasEntityPermission(entityName, EntityPermission.Update)` before record update, return 403 if false |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:300-310` (UpdateRecord method permission check section) |
| **Priority** | Critical |

**Business Justification**: Controls data modification rights ensuring only authorized users can update sensitive business data.

**Implementation Details**: RecordManager checks Update permission before retrieving existing record from database, optimizing performance by rejecting unauthorized requests early.

**Example Scenario**: User with Read-only permission attempts to update customer email → Permission denied before database query, preventing information disclosure.

---

### AR-004: Entity-Level Delete Permission Check

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | AR-004 |
| **Condition** | User must have EntityPermission.Delete to remove entity records |
| **Action** | Invoke `SecurityContext.HasEntityPermission(entityName, EntityPermission.Delete)` before record deletion, return 403 if false |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:380-390` (DeleteRecord method permission check section) |
| **Priority** | Critical |

**Business Justification**: Restricts data deletion to authorized roles preventing accidental or malicious data loss.

**Implementation Details**: RecordManager validates Delete permission before cascade delete logic, preventing unauthorized users from viewing related records through error messages.

**Example Scenario**: Regular user attempts to delete customer with Administrator-only Delete permission → HTTP 403 Forbidden, no cascade queries executed.

---

### AR-005: Metadata Permission for Entity Creation

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | AR-005 |
| **Condition** | User must have MetaPermission to create entities, fields, relationships, pages |
| **Action** | Invoke `SecurityContext.HasMetaPermission()` before metadata operations, return 403 if false |
| **Module** | EntityManager |
| **Code Reference** | `WebVella.Erp/Api/EntityManager.cs:80-90` (CreateEntity/CreateField methods permission check section) |
| **Priority** | Critical |

**Business Justification**: Restricts system configuration to administrators preventing regular users from modifying application schema.

**Implementation Details**: EntityManager checks if SecurityContext.CurrentUser has Administrator role (BDC56420-CAF0-4030-8A0E-D264938E0CDA), throws UnauthorizedAccessException if not administrator.

**Example Scenario**: Regular user accesses SDK plugin entity creation → Permission check blocks access to SDK administrative pages.

---

### AR-006: System Scope Escalation for Internal Operations

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | AR-006 |
| **Condition** | Internal system operations can escalate to system scope bypassing user permissions |
| **Action** | Invoke `SecurityContext.OpenSystemScope()` wrapping internal operations, permission checks return true in system scope |
| **Module** | SecurityContext |
| **Code Reference** | `WebVella.Erp/Api/SecurityContext.cs:150-160` (OpenSystemScope method implementation) |
| **Priority** | Critical |

**Business Justification**: Enables system maintenance tasks (migrations, background jobs, internal workflows) to operate without user permission constraints while maintaining security for user-initiated operations.

**Implementation Details**: SecurityContext maintains AsyncLocal<SecurityScope>, OpenSystemScope() sets scope.IsSystem=true, HasEntityPermission checks return true if IsSystem, scope disposed after operation completing reverting to user context.

**Example Scenario**: Background job needs to clean up expired records across all entities → Opens system scope, accesses all entities regardless of job user's permissions, closes scope after cleanup.

---

### AR-007: Record-Level Permission Filtering

| **Attribute** | **Value** |
|---------------|-----------|
| **Rule ID** | AR-007 |
| **Condition** | Record queries filter results based on record-level permissions if configured |
| **Action** | Append WHERE clause limiting records to those user owns or has explicit access to |
| **Module** | RecordManager |
| **Code Reference** | `WebVella.Erp/Api/RecordManager.cs:520-530` (Find method record permission filtering section) |
| **Priority** | High |

**Business Justification**: Provides fine-grained access control where users see only records they own or have been explicitly granted access to (e.g., sales reps see only their customers).

**Implementation Details**: RecordManager checks entity configuration for record-level permissions enabled, queries record ownership field (e.g., owner_id) or permission junction table, appends SQL WHERE clause: `WHERE owner_id = @current_user_id OR id IN (SELECT record_id FROM record_permissions WHERE user_id = @current_user_id)`.

**Example Scenario**: Sales entity configured with record-level permissions → User queries opportunities, results filtered to show only opportunities they own or have been shared with, preventing viewing competitors' deals.

---

**Document Version**: 1.0  
**Last Updated**: November 18, 2024  
**Document Status**: Complete  
**Related Documentation**: [README.md](README.md), [Functional Overview](functional-overview.md), [Security & Quality](security-quality.md)
