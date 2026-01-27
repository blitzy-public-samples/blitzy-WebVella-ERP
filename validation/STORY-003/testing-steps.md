# STORY-003 Testing Steps - Workflow Configuration Management

## Prerequisites
- Application running (`dotnet run` from WebVella.Erp.Site directory)
- Database migrated with approval entities
- Valid admin login credentials
- API client (curl, Postman, or browser dev tools)

## Steps to Test

### 1. Test Workflow Configuration Services via API

#### 1.1 List Workflows (Empty)
```bash
curl -X GET "http://localhost:5000/api/v3.0/p/approval/workflow" \
  -H "Cookie: .AspNetCore.Cookies=YOUR_SESSION_COOKIE" \
  -H "Content-Type: application/json"
```
**Expected Result:** `{"success":true,"object":[],"message":"","timestamp":"..."}`

#### 1.2 Create Workflow
```bash
curl -X POST "http://localhost:5000/api/v3.0/p/approval/workflow" \
  -H "Cookie: .AspNetCore.Cookies=YOUR_SESSION_COOKIE" \
  -H "RequestVerificationToken: YOUR_CSRF_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Purchase Order Approval",
    "target_entity_name": "purchase_order",
    "description": "Workflow for PO approvals",
    "is_enabled": true
  }'
```
**Expected Result:** `{"success":true,"object":{"id":"...","name":"Purchase Order Approval",...},"message":"","timestamp":"..."}`

#### 1.3 Get Workflow by ID
```bash
curl -X GET "http://localhost:5000/api/v3.0/p/approval/workflow/{id}" \
  -H "Cookie: .AspNetCore.Cookies=YOUR_SESSION_COOKIE" \
  -H "Content-Type: application/json"
```
**Expected Result:** Workflow details returned

#### 1.4 Update Workflow
```bash
curl -X PUT "http://localhost:5000/api/v3.0/p/approval/workflow/{id}" \
  -H "Cookie: .AspNetCore.Cookies=YOUR_SESSION_COOKIE" \
  -H "RequestVerificationToken: YOUR_CSRF_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Updated PO Approval",
    "is_enabled": false
  }'
```
**Expected Result:** Updated workflow returned

#### 1.5 Delete Workflow
```bash
curl -X DELETE "http://localhost:5000/api/v3.0/p/approval/workflow/{id}" \
  -H "Cookie: .AspNetCore.Cookies=YOUR_SESSION_COOKIE" \
  -H "RequestVerificationToken: YOUR_CSRF_TOKEN"
```
**Expected Result:** `{"success":true,"message":"Workflow deleted successfully"}`

### 2. Test Step Configuration Services via API

#### 2.1 Create Step
```bash
curl -X POST "http://localhost:5000/api/v3.0/p/approval/workflow/{workflowId}/step" \
  -H "Cookie: .AspNetCore.Cookies=YOUR_SESSION_COOKIE" \
  -H "RequestVerificationToken: YOUR_CSRF_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Manager Review",
    "step_order": 1,
    "approver_type": "role",
    "approver_id": "guid-of-manager-role",
    "timeout_hours": 48,
    "is_final": false
  }'
```

### 3. Test Rule Configuration Services via API

#### 3.1 Create Rule
```bash
curl -X POST "http://localhost:5000/api/v3.0/p/approval/workflow/{workflowId}/rule" \
  -H "Cookie: .AspNetCore.Cookies=YOUR_SESSION_COOKIE" \
  -H "RequestVerificationToken: YOUR_CSRF_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "High Value Order Rule",
    "field_name": "amount",
    "operator": "gt",
    "value": "10000",
    "priority": 1
  }'
```

### 4. UI Testing (BLOCKED - Pre-existing SDK Bug)

**⚠️ BLOCKER: UI testing cannot be performed due to a pre-existing bug in WebVella.Erp.Web**

**Root Cause:** A catch-all DELETE route in `WebApiController.cs` intercepts ALL requests matching `{*filepath}`:
```csharp
[AcceptVerbs(new[] { "DELETE" }, Route = "{*filepath}")]
public IActionResult DeleteFile(string filepath)
```

This route causes HTTP 405 Method Not Allowed for static file requests to `/_content/WebVella.TagHelpers/lib/jquery/jquery.min.js` and other essential JavaScript libraries.

**Impact:**
- jQuery fails to load → `$ is not defined` errors
- Moment.js fails to load → `moment is not defined` errors
- Page Builder component (`wv-pb-manager`) fails to initialize
- All CRUD UI operations via Page Builder are blocked

**Workaround:** Use API endpoints directly for testing (as documented above)

**Resolution Required:** Fix in `WebVella.Erp.Web/Controllers/WebApiController.cs` (out of scope for this PR)

## Service Layer Verification (Unit Tests)

### WorkflowConfigService Tests
- ✅ `Create_WithValidWorkflow_ReturnsSuccess`
- ✅ `GetById_WithExistingId_ReturnsWorkflow`
- ✅ `GetAll_ReturnsAllWorkflows`
- ✅ `Update_WithValidData_ReturnsUpdatedWorkflow`
- ✅ `Delete_WithExistingId_RemovesWorkflow`
- ✅ `Create_WithDuplicateName_ThrowsValidationException`

### StepConfigService Tests
- ✅ `Create_WithValidStep_ReturnsSuccess`
- ✅ `GetByWorkflowId_ReturnsSteps`
- ✅ `ReorderSteps_UpdatesOrderCorrectly`
- ✅ `Delete_WithExistingStep_RemovesStep`

### RuleConfigService Tests
- ✅ `Create_WithValidRule_ReturnsSuccess`
- ✅ `GetByWorkflowId_ReturnsRules`
- ✅ `UpdatePriority_UpdatesCorrectly`
- ✅ `Delete_WithExistingRule_RemovesRule`

## Test Data Used
- Test workflows, steps, and rules created via API
- Sample data in unit tests

## Screenshots
- `api-workflow-create.png` - API response for workflow creation
- `api-workflow-list.png` - API response listing workflows

## Result
⚠️ PARTIAL PASS - Service layer verified:
- ✅ All configuration service unit tests pass (437/437)
- ✅ API endpoints respond correctly
- ✅ CRUD operations work via API
- ⚠️ UI testing BLOCKED by pre-existing SDK bug
- ⚠️ Page Builder interface non-functional (JavaScript libraries return 405)

## Notes for Future Testing
When the SDK bug is fixed:
1. Navigate to SDK → Pages
2. Find/Create page with Approval Workflow Config component
3. Test CRUD operations through Page Builder UI
