# Critical Decision Review: PcGrid Bulk Archive and Bulk Delete

The WebVella ERP team added bulk archive and bulk delete to the PcGrid list view. The feature lets a user select several records on the rendered page and act on the whole selection in one request, with a reversible Archive and a permanent Delete. The document below records the five highest-risk decisions behind that feature, ordered from highest risk to lowest. Each entry states the decision, the rationale, the risk level, and the reviewer persona who should sign off. Three concerns drove the ordering: irreversible data loss, authorization, and the assumptions that resolved gaps the prompt left open.

The application code diff stays inside six files: `WebVella.Erp.Web/Components/PcGrid/PcGrid.cs`, `WebVella.Erp.Web/Components/PcGrid/Display.cshtml`, `WebVella.Erp.Web/Components/PcGrid/service.js`, `WebVella.Erp.Web/Components/PcGrid/Options.cshtml`, `WebVella.Erp.Web/Controllers/WebApiController.cs`, and the new `WebVella.Erp.Web/Models/BulkRecordActionModel.cs`. Every new option defaults to off, so grids that do not opt in render exactly as they do today.

## Decision 1: Permanent bulk delete removes records with no recovery path

- **Decision:** Bulk delete calls `RecordManager.DeleteRecord` once per selected record (`WebVella.Erp/Api/RecordManager.cs:1627`), which permanently removes each record. The feature builds no trash bin and no restore path. The new `BulkDeleteRecords` action in `WebVella.Erp.Web/Controllers/WebApiController.cs` mirrors the single-record delete transaction template (`WebVella.Erp.Web/Controllers/WebApiController.cs:1408-1438`).
- **Rationale:** The prompt asks for a true delete alongside a reversible archive, so the delete must stay permanent. To protect the user, the client fires a count-aware, permanence-explicit native `confirm()` dialog before the request runs. The exact wording states "Permanently delete N record(s)? The records cannot be recovered." The confirmation follows the repository destructive-action convention that pairs a native `confirm()` with a jQuery ajax call (`WebVella.Erp.Web/TagHelpers/WvFieldUserFileMultiple/form.js:19-59`). The wording carries the permanence signal that a styled modal would otherwise supply, because the repository ships no modal or toast library for confirmations.
- **Risk level:** Critical. A wrong click destroys data for every selected record at once, and no undo exists.
- **Reviewer persona:** Data-integrity or security reviewer.

## Decision 2: Authorization runs per record and never relaxes

- **Decision:** The bulk path constructs the default `RecordManager`, which keeps `ignoreSecurity` false and `executeHooks` true (`WebVella.Erp/Api/RecordManager.cs:40`). `DeleteRecord` checks `EntityPermission.Delete` for each record (`WebVella.Erp/Api/RecordManager.cs:1647`), and `UpdateRecord` checks `EntityPermission.Update` for each record (`WebVella.Erp/Api/RecordManager.cs:984`). The controller reuses the existing `recMan` field and adds no authorization code of its own.
- **Rationale:** Routing every record through the same data-layer calls that the single-record actions use means the bulk operations inherit the exact permission checks and lifecycle hooks that already guard single-record work. A per-record check, rather than one check for the batch, stops a user from deleting or archiving any record they lack rights to, even when other records in the same selection are allowed. The code never passes `ignoreSecurity: true` and never bypasses a check.
- **Risk level:** High. A weaker model would let a bulk request escalate beyond a user's single-record rights.
- **Reviewer persona:** Security reviewer.

## Decision 3: Live client logic lives in the Display view, not service.js

- **Decision:** All live selection, confirmation, ajax, and refresh logic lives in an inline `<script>` block inside `WebVella.Erp.Web/Components/PcGrid/Display.cshtml`. The component `service.js` gains only Page Builder wiring for the new admin toggles.
- **Rationale:** A component `service.js` reaches the browser only through the Page Builder editor, served at `/api/v3.0/pc/{name}/resource/service.js` (`WebVella.Erp.Web/Services/PageComponentLibraryService.cs:52`), so runtime page logic cannot live there. The repository already runs live Display-mode behavior from an inline script inside the Display view, as `PcApplications` and `PcJavaScriptBlock` do (`WebVella.Erp.Web/Components/PcJavaScriptBlock/Display.cshtml:9-11`). The inline script wraps its body in an IIFE and scopes every selector by the component node id, so several PcGrids on one page never collide.
- **Risk level:** Medium. A wrong placement would ship code that never runs on live pages and would break the feature silently.
- **Reviewer persona:** Frontend or architecture reviewer.

## Decision 4: PcGrid supplies the entity name because the record models omit it (ambiguity resolved)

- **Decision:** The bulk endpoint needs the target entity name, yet neither `EntityRecord` nor `EntityRecordList` carries one. `EntityRecordList` declares only `total_count` (`WebVella.Erp/Api/Models/EntityRecordList.cs:6-9`). PcGrid resolves the gap with an administrator-configured `entity_name` option, which the Display script sends to the endpoint in the request body.
- **Rationale:** The rendered records expose no entity name, so the client cannot infer the route target from the data alone. An explicit admin option removes the guesswork and keeps the request body self-describing. The request model `BulkRecordActionModel` carries `EntityName` and the selected record ids, and binds through `[FromBody]`, matching how the controller already receives single-record payloads.
- **Risk level:** Medium. A blank or wrong entity name would send the batch to the wrong target, though the per-record permission checks still guard the outcome.
- **Reviewer persona:** Backend or API reviewer.

## Decision 5: Best-effort partial failure and the pre-existing is_archived field (ambiguity resolved)

- **Decision:** The server processes every selected record inside its own `DbContext` transaction, commits the records that succeed, rolls back only the records that fail, continues past a failure, and returns a per-record result list. Each result is a `BulkRecordActionResultItem` that reports the record id, a success flag, and a message, wrapped in the existing `ResponseModel` envelope. Archive writes the pre-existing boolean field `is_archived` through `UpdateRecord` (`WebVella.Erp/Api/RecordManager.cs:952`) and adds no schema and no migration. Archive disables itself on any grid whose rendered records lack that field.
- **Rationale:** A single record's failure must not abort the batch, so per-record isolation lets the good records proceed while the server reports which records failed. The single-record actions already wrap each operation in a `DbContext` transaction with commit and rollback and log errors through `LogService` (`WebVella.Erp.Web/Controllers/WebApiController.cs:1538-1585`), so the bulk loop applies that same wrapper once per record. The `is_archived` flag already serves as a soft-delete convention elsewhere in the repository, where a job archives records by setting `is_archived` to true (`jira-stories/STORY-006-notification-escalation-jobs.md`, AC13; `jira-stories/stories-export.json`), so Archive reuses the field rather than defining new schema. The archive availability check reads the rendered records and hides Archive when the field is absent, which keeps the action honest.
- **Risk level:** Low. Archive reverses easily, per-record isolation contains any single failure, and the field-absence guard prevents a silent no-op.
- **Reviewer persona:** Backend or reliability reviewer.

## Explicit callouts

- **Irreversible operation:** Decision 1 covers the permanent bulk delete. The count-aware, permanence-explicit `confirm()` dialog stands as the only guard before the records leave for good.
- **Authorization decisions:** Decision 2 covers the per-record `EntityPermission.Delete` and `EntityPermission.Update` checks that run through the default `RecordManager` and never relax.
- **Ambiguity-resolving assumptions:** Decision 4 resolves the missing entity name with an administrator-configured `entity_name` option. Decision 5 treats `is_archived` as a pre-existing field, adds no schema or migration, and disables Archive when a grid's records lack the field.
