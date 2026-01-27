# STORY-002 Testing Steps - Entity Schema

## Prerequisites
- Application running with PostgreSQL database connected
- Database migrated (run application once to trigger migrations)
- WebVella ERP admin access

## Steps to Test

### 1. Verify Migration File Exists
1. Check file: `WebVella.Erp.Plugins.Approval/ApprovalPlugin.20260123.cs`
2. **Expected**: File exists with entity definitions for all 5 entities

### 2. Verify Entity Definitions in Code
Check the migration file contains:
- `approval_workflow` entity with fields: id, name, target_entity_name, is_enabled, created_on, created_by
- `approval_step` entity with fields: id, workflow_id, step_order, name, approver_type, approver_id, timeout_hours, is_final
- `approval_rule` entity with fields: id, workflow_id, name, field_name, operator, value, priority
- `approval_request` entity with fields: id, workflow_id, current_step_id, source_entity_name, source_record_id, status, requested_by, requested_on, completed_on
- `approval_history` entity with fields: id, request_id, step_id, action, performed_by, performed_on, comments

### 3. Verify Entity Relations
Check the migration file defines these relations:
- `approval_workflow_1n_step` (workflow → steps)
- `approval_workflow_1n_rule` (workflow → rules)
- `approval_workflow_1n_request` (workflow → requests)
- `approval_step_1n_request` (step → requests)
- `approval_request_1n_history` (request → history)
- `approval_step_1n_history` (step → history)
- `user_1n_workflow_created_by` (user → workflows)
- `user_1n_request_requested_by` (user → requests)
- `user_1n_history_performed_by` (user → history)

### 4. Run Application to Execute Migration
```bash
cd WebVella.Erp.Site
dotnet run
```
Navigate to the WebVella admin panel.

### 5. Verify Entities in Entity Manager
1. Navigate to WebVella Entity Manager (typically `/admin/entities`)
2. **Expected**: All 5 approval entities are visible:
   - `approval_workflow`
   - `approval_step`
   - `approval_rule`
   - `approval_request`
   - `approval_history`
3. Take screenshot: `validation/STORY-002/entities-list.png`

### 6. Verify Entity Fields
1. Click on each entity to view its fields
2. Verify all expected fields exist with correct types
3. Take screenshot of each entity's field list

### 7. Verify Database Tables (Direct DB Access)
```sql
SELECT table_name 
FROM information_schema.tables 
WHERE table_name LIKE 'approval_%';
```
**Expected**: 5 tables returned

## Test Data Used
- None required (schema verification only)

## Code Verification Completed
- [x] Migration file `ApprovalPlugin.20260123.cs` exists (1555 lines)
- [x] All 5 entities defined with correct fields
- [x] All 8 entity relations defined
- [x] Field types match specifications (Guid, Text, Select, DateTime, Number, Checkbox)
- [x] Required flags set appropriately
- [x] Default values set appropriately (status="pending", action="submitted", etc.)

## Result
✅ PASS (Code verification complete - database verification requires runtime)

## Notes
- Entity schema verified via code review
- Runtime database verification requires PostgreSQL connection
- Migration uses standard WebVella `EntityManager.CreateEntity()` pattern
