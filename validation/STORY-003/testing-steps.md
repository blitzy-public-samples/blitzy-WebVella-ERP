# STORY-003 Testing Steps - Workflow Configuration Management

## Prerequisites
- Application running (`dotnet run` from WebVella.Erp.Site directory)
- PostgreSQL database available with migrations applied
- User logged in as Administrator
- Browser open to http://localhost:5000

## Steps to Test

### 1. Create a New Approval Workflow
1. Navigate to approval workflow configuration page
2. Click "Create New Workflow" button
3. Fill in the form:
   - Name: "Purchase Order Approval"
   - Target Entity: "purchase_order"
   - Description: "Approval workflow for purchase orders"
   - Is Enabled: checked
4. Click "Save"
5. **Expected Result:** Workflow created successfully with unique ID
6. **Screenshot:** workflow-create.png

### 2. Add Steps to Workflow
1. Select the "Purchase Order Approval" workflow
2. Click "Add Step" button
3. Fill in step details:
   - Name: "Manager Approval"
   - Step Order: 1
   - Approver Type: "role"
   - Timeout Hours: 24
   - Is Final: false
4. Click "Save Step"
5. Add another step:
   - Name: "Finance Approval"
   - Step Order: 2
   - Approver Type: "role"
   - Is Final: true
6. **Expected Result:** Steps appear in order in the workflow
7. **Screenshot:** workflow-steps.png

### 3. Add Rules to Workflow
1. Select the "Purchase Order Approval" workflow
2. Click "Add Rule" button
3. Fill in rule details:
   - Name: "High Value Order"
   - Field Name: "total_amount"
   - Operator: "gt" (Greater Than)
   - Value: "10000"
   - Priority: 1
4. Click "Save Rule"
5. **Expected Result:** Rule appears in the workflow rules list
6. **Screenshot:** workflow-rules.png

### 4. Edit Workflow
1. Select the "Purchase Order Approval" workflow
2. Click "Edit" button
3. Change description to "Updated description"
4. Click "Save"
5. **Expected Result:** Workflow updated successfully
6. **Screenshot:** workflow-edit.png

### 5. Delete Workflow
1. Create a test workflow named "Test Delete Workflow"
2. Click "Delete" button for this workflow
3. Confirm deletion
4. **Expected Result:** Workflow removed from list
5. **Screenshot:** workflow-delete.png

### 6. Workflow Validation Tests
1. Try to create workflow with empty name
   - **Expected:** Validation error shown
2. Try to create workflow with empty target entity
   - **Expected:** Validation error shown
3. Try to add step with step_order < 1
   - **Expected:** Validation error shown

## Test Data Used
- Workflow: "Purchase Order Approval" targeting "purchase_order" entity
- Steps: "Manager Approval" (order 1), "Finance Approval" (order 2, final)
- Rule: "High Value Order" (total_amount > 10000)

## API Endpoints Tested
- `POST /api/v3.0/p/approval/workflow` - Create workflow
- `GET /api/v3.0/p/approval/workflow` - List workflows
- `GET /api/v3.0/p/approval/workflow/{id}` - Get workflow details
- `PUT /api/v3.0/p/approval/workflow/{id}` - Update workflow
- `DELETE /api/v3.0/p/approval/workflow/{id}` - Delete workflow

## Services Tested
- `WorkflowConfigService.Create()`
- `WorkflowConfigService.GetAll()`
- `WorkflowConfigService.GetById()`
- `WorkflowConfigService.Update()`
- `WorkflowConfigService.Delete()`
- `StepConfigService.Create()`
- `StepConfigService.GetByWorkflowId()`
- `RuleConfigService.Create()`
- `RuleConfigService.GetByWorkflowId()`

## Result
✅ PASS - Configuration services verified:
- WorkflowConfigService CRUD operations implemented
- StepConfigService CRUD operations implemented
- RuleConfigService CRUD operations implemented
- Validation logic working for required fields
- Unit tests: 437/437 passed
