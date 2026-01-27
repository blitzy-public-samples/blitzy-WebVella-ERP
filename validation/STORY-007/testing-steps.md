# STORY-007 Testing Steps - REST API

## Prerequisites
- Application running (`dotnet run` from WebVella.Erp.Site directory)
- Database migrated with approval entities
- Valid admin login credentials
- API client (curl, Postman, or browser dev tools)

## Steps to Test

### 1. Obtain Authentication Credentials

#### 1.1 Login and Get Session Cookie
1. Navigate to http://localhost:5000/login
2. Login with admin credentials
3. Open browser DevTools → Application → Cookies
4. Copy `.AspNetCore.Cookies` value

#### 1.2 Get CSRF Token
1. On any authenticated page, open DevTools → Console
2. Run: `document.querySelector('input[name="__RequestVerificationToken"]')?.value`
3. Or from page source: `<input name="__RequestVerificationToken" ...>`

### 2. Test Workflow Management Endpoints

#### 2.1 GET /api/v3.0/p/approval/workflow - List All Workflows
```bash
curl -X GET "http://localhost:5000/api/v3.0/p/approval/workflow" \
  -H "Cookie: .AspNetCore.Cookies=YOUR_COOKIE" \
  -H "Content-Type: application/json"
```
**Expected Response:**
```json
{
  "success": true,
  "object": [],
  "message": "",
  "timestamp": "2026-01-27T..."
}
```

#### 2.2 POST /api/v3.0/p/approval/workflow - Create Workflow
```bash
curl -X POST "http://localhost:5000/api/v3.0/p/approval/workflow" \
  -H "Cookie: .AspNetCore.Cookies=YOUR_COOKIE" \
  -H "RequestVerificationToken: YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "PO Approval Workflow",
    "target_entity_name": "purchase_order",
    "description": "For purchase orders over $5000",
    "is_enabled": true
  }'
```
**Expected Response:**
```json
{
  "success": true,
  "object": {
    "id": "guid...",
    "name": "PO Approval Workflow",
    "target_entity_name": "purchase_order",
    "description": "For purchase orders over $5000",
    "is_enabled": true,
    "created_on": "2026-01-27T...",
    "created_by": "user-guid"
  },
  "message": "",
  "timestamp": "..."
}
```

#### 2.3 GET /api/v3.0/p/approval/workflow/{id} - Get Single Workflow
```bash
curl -X GET "http://localhost:5000/api/v3.0/p/approval/workflow/{workflow-id}" \
  -H "Cookie: .AspNetCore.Cookies=YOUR_COOKIE"
```

#### 2.4 PUT /api/v3.0/p/approval/workflow/{id} - Update Workflow
```bash
curl -X PUT "http://localhost:5000/api/v3.0/p/approval/workflow/{workflow-id}" \
  -H "Cookie: .AspNetCore.Cookies=YOUR_COOKIE" \
  -H "RequestVerificationToken: YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Updated PO Approval",
    "is_enabled": false
  }'
```

#### 2.5 DELETE /api/v3.0/p/approval/workflow/{id} - Delete Workflow
```bash
curl -X DELETE "http://localhost:5000/api/v3.0/p/approval/workflow/{workflow-id}" \
  -H "Cookie: .AspNetCore.Cookies=YOUR_COOKIE" \
  -H "RequestVerificationToken: YOUR_TOKEN"
```

### 3. Test Approval Action Endpoints

#### 3.1 GET /api/v3.0/p/approval/pending - Get Pending Approvals
```bash
curl -X GET "http://localhost:5000/api/v3.0/p/approval/pending" \
  -H "Cookie: .AspNetCore.Cookies=YOUR_COOKIE"
```
**Expected:** List of requests pending current user's approval

#### 3.2 GET /api/v3.0/p/approval/request/{id} - Get Request Details
```bash
curl -X GET "http://localhost:5000/api/v3.0/p/approval/request/{request-id}" \
  -H "Cookie: .AspNetCore.Cookies=YOUR_COOKIE"
```

#### 3.3 POST /api/v3.0/p/approval/request/{id}/approve - Approve Request
```bash
curl -X POST "http://localhost:5000/api/v3.0/p/approval/request/{request-id}/approve" \
  -H "Cookie: .AspNetCore.Cookies=YOUR_COOKIE" \
  -H "RequestVerificationToken: YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"comments": "Approved - meets requirements"}'
```

#### 3.4 POST /api/v3.0/p/approval/request/{id}/reject - Reject Request
```bash
curl -X POST "http://localhost:5000/api/v3.0/p/approval/request/{request-id}/reject" \
  -H "Cookie: .AspNetCore.Cookies=YOUR_COOKIE" \
  -H "RequestVerificationToken: YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "comments": "Rejected",
    "reason": "Budget exceeded"
  }'
```

#### 3.5 POST /api/v3.0/p/approval/request/{id}/delegate - Delegate Request
```bash
curl -X POST "http://localhost:5000/api/v3.0/p/approval/request/{request-id}/delegate" \
  -H "Cookie: .AspNetCore.Cookies=YOUR_COOKIE" \
  -H "RequestVerificationToken: YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "delegateToUserId": "delegate-user-guid",
    "comments": "Please review"
  }'
```

#### 3.6 GET /api/v3.0/p/approval/request/{id}/history - Get Request History
```bash
curl -X GET "http://localhost:5000/api/v3.0/p/approval/request/{request-id}/history" \
  -H "Cookie: .AspNetCore.Cookies=YOUR_COOKIE"
```

### 4. Test Dashboard Metrics Endpoint

#### 4.1 GET /api/v3.0/p/approval/dashboard/metrics - Get Dashboard Metrics
```bash
curl -X GET "http://localhost:5000/api/v3.0/p/approval/dashboard/metrics" \
  -H "Cookie: .AspNetCore.Cookies=YOUR_COOKIE"
```
**Expected Response:**
```json
{
  "success": true,
  "object": {
    "pending_count": 5,
    "average_approval_time_hours": 12.5,
    "approval_rate": 85.0,
    "overdue_count": 1,
    "recent_activity": [...]
  },
  "message": "",
  "timestamp": "..."
}
```

### 5. Test Error Handling

#### 5.1 Unauthorized Request (No Auth)
```bash
curl -X GET "http://localhost:5000/api/v3.0/p/approval/workflow"
```
**Expected:** 401 Unauthorized or redirect to login

#### 5.2 Not Found
```bash
curl -X GET "http://localhost:5000/api/v3.0/p/approval/workflow/non-existent-guid" \
  -H "Cookie: .AspNetCore.Cookies=YOUR_COOKIE"
```
**Expected:** 404 or `{"success": false, "message": "Workflow not found"}`

#### 5.3 Validation Error
```bash
curl -X POST "http://localhost:5000/api/v3.0/p/approval/workflow" \
  -H "Cookie: .AspNetCore.Cookies=YOUR_COOKIE" \
  -H "RequestVerificationToken: YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name": ""}'  # Empty name
```
**Expected:** 400 Bad Request with validation errors

### 6. Verify Controller Implementation

#### 6.1 Check Controller File
```bash
cat WebVella.Erp.Plugins.Approval/Controllers/ApprovalController.cs | head -50
```
**Expected Attributes:**
- `[Authorize]`
- `[Route("api/v3.0/p/approval")]`
- Correct HTTP method attributes on actions

### 7. API Bug Fix Applied

**Issue Found:** EQL relation names were incorrect
**File:** `WebVella.Erp.Plugins.Approval/Services/WorkflowConfigService.cs`
**Fix:** Changed `$approval_step_step` to `$approval_workflow_1n_step`

## Test Data Used
- Test workflows created via API
- Test approval requests
- Admin user credentials

## Screenshots
- `api-workflow-list.png` - GET /workflow response
- `api-workflow-create.png` - POST /workflow response
- `api-pending-approvals.png` - GET /pending response
- `api-approve-request.png` - POST /approve response
- `api-dashboard-metrics.png` - GET /dashboard/metrics response

## Result
✅ PASS - REST API verified:
- ✅ All 12+ endpoints implemented
- ✅ Proper authorization (`[Authorize]` attribute)
- ✅ Correct route patterns (`/api/v3.0/p/approval/...`)
- ✅ ResponseModel envelope used consistently
- ✅ Error handling with appropriate status codes
- ✅ CSRF protection via RequestVerificationToken
- ✅ EQL bug fixed in WorkflowConfigService
- ✅ All unit tests pass

## API Endpoint Summary
| Method | Endpoint | Purpose |
|--------|----------|---------|
| GET | /workflow | List all workflows |
| POST | /workflow | Create workflow |
| GET | /workflow/{id} | Get workflow details |
| PUT | /workflow/{id} | Update workflow |
| DELETE | /workflow/{id} | Delete workflow |
| GET | /pending | Get pending approvals |
| GET | /request/{id} | Get request details |
| POST | /request/{id}/approve | Approve request |
| POST | /request/{id}/reject | Reject request |
| POST | /request/{id}/delegate | Delegate request |
| GET | /request/{id}/history | Get request history |
| GET | /dashboard/metrics | Get dashboard metrics |
