# STORY-002 Testing Steps - Entity Schema

## Prerequisites
- Application running (`dotnet run` from WebVella.Erp.Site directory)
- PostgreSQL database available and configured
- Database migration completed on first startup
- Browser open to http://localhost:5000

## Steps to Test

### 1. Verify Entity Creation in Entity Manager
1. Navigate to http://localhost:5000/sdk/objects/entity
2. Search for "approval" in the entity list
3. **Expected Result:** Five entities should be visible:
   - `approval_workflow`
   - `approval_step`
   - `approval_rule`
   - `approval_request`
   - `approval_history`
4. **Screenshot:** entity-manager-view.png

### 2. Verify approval_workflow Entity Fields
1. Click on `approval_workflow` entity
2. Navigate to Fields tab
3. **Expected Fields:**
   - `id` (Guid, Primary Key)
   - `name` (Text, Required, Unique, Max 256)
   - `target_entity_name` (Text, Required, Max 128)
   - `description` (Multiline Text, Optional)
   - `is_enabled` (Checkbox, Default: true)
   - `created_on` (DateTime, Auto)
   - `created_by` (Guid, FK to user)

### 3. Verify approval_step Entity Fields
1. Click on `approval_step` entity
2. Navigate to Fields tab
3. **Expected Fields:**
   - `id` (Guid, Primary Key)
   - `workflow_id` (Guid, FK to approval_workflow)
   - `step_order` (Number, Required, Default: 1)
   - `name` (Text, Required)
   - `approver_type` (Select: role/user/department_head)
   - `approver_id` (Guid, Optional)
   - `timeout_hours` (Number, Optional)
   - `is_final` (Checkbox, Default: false)

### 4. Verify approval_rule Entity Fields
1. Click on `approval_rule` entity
2. Navigate to Fields tab
3. **Expected Fields:**
   - `id` (Guid, Primary Key)
   - `workflow_id` (Guid, FK to approval_workflow)
   - `name` (Text, Required)
   - `field_name` (Text, Required)
   - `operator` (Select: eq/neq/gt/gte/lt/lte/contains)
   - `value` (Text, Required)
   - `priority` (Number, Default: 0)

### 5. Verify approval_request Entity Fields
1. Click on `approval_request` entity
2. Navigate to Fields tab
3. **Expected Fields:**
   - `id` (Guid, Primary Key)
   - `workflow_id` (Guid, FK to approval_workflow)
   - `current_step_id` (Guid, FK to approval_step)
   - `title` (Text, Optional)
   - `source_entity_name` (Text, Required)
   - `source_record_id` (Guid, Required)
   - `status` (Select: pending/approved/rejected/escalated/expired)
   - `requested_by` (Guid, FK to user)
   - `requested_on` (DateTime, Auto)
   - `completed_on` (DateTime, Optional)

### 6. Verify approval_history Entity Fields
1. Click on `approval_history` entity
2. Navigate to Fields tab
3. **Expected Fields:**
   - `id` (Guid, Primary Key)
   - `request_id` (Guid, FK to approval_request)
   - `step_id` (Guid, FK to approval_step)
   - `action` (Select: submitted/approved/rejected/delegated/escalated)
   - `performed_by` (Guid, FK to user)
   - `performed_on` (DateTime, Auto)
   - `comments` (Multiline Text, Optional)

### 7. Verify Entity Relationships
1. Navigate to http://localhost:5000/sdk/objects/entity_relation
2. Search for "approval" in relations
3. **Expected Relations:**
   - `approval_workflow_1n_step` (workflow → steps)
   - `approval_workflow_1n_rule` (workflow → rules)
   - `approval_workflow_1n_request` (workflow → requests)
   - `approval_step_1n_request` (step → requests)
   - `approval_request_1n_history` (request → history)

## Test Data Used
- No manual test data required - entities are created by migration

## Database Verification
```sql
-- Connect to PostgreSQL and verify tables
SELECT table_name FROM information_schema.tables 
WHERE table_schema = 'public' 
AND table_name LIKE 'approval%';

-- Expected output:
-- approval_workflow
-- approval_step
-- approval_rule
-- approval_request
-- approval_history
```

## Result
✅ PASS - Entity schema verified:
- All 5 entities defined in migration patch
- Fields correctly configured with types and constraints
- Relationships established between entities
- Default values fixed for required text fields
- Unit tests: 437/437 passed
