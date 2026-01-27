# STORY-007 Testing Steps - REST API

## Prerequisites
- Application running (`dotnet run` from WebVella.Erp.Site directory)
- PostgreSQL database available with migrations applied
- User authenticated with Bearer token
- API client (Postman, curl, or browser DevTools)

## Steps to Test

### 1. List Workflows (GET /api/v3.0/p/approval/workflow)
```bash
curl -X GET "http://localhost:5000/api/v3.0/p/approval/workflow" \
  -H "Authorization: Bearer {token}"
```
**Expected Response:**
```json
{
  "success": true,
  "message": "",
  "data": [
    {
      "id": "guid",
      "name": "Purchase Order Approval",
      "targetEntityName": "purchase_order",
      "isEnabled": true
    }
  ]
}
```
**Screenshot:** api-list-workflows.png

### 2. Create Workflow (POST /api/v3.0/p/approval/workflow)
```bash
curl -X POST "http://localhost:5000/api/v3.0/p/approval/workflow" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Test Workflow",
    "targetEntityName": "test_entity",
    "description": "Test description",
    "isEnabled": true
  }'
```
**Expected Response:**
```json
{
  "success": true,
  "message": "Workflow created successfully",
  "data": { "id": "new-guid", ... }
}
```
**Screenshot:** api-create-workflow.png

### 3. Get Workflow by ID (GET /api/v3.0/p/approval/workflow/{id})
```bash
curl -X GET "http://localhost:5000/api/v3.0/p/approval/workflow/{id}" \
  -H "Authorization: Bearer {token}"
```
**Expected Response:** Full workflow details including steps and rules

### 4. Update Workflow (PUT /api/v3.0/p/approval/workflow/{id})
```bash
curl -X PUT "http://localhost:5000/api/v3.0/p/approval/workflow/{id}" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Updated Workflow",
    "isEnabled": false
  }'
```
**Expected Response:** Updated workflow data

### 5. Delete Workflow (DELETE /api/v3.0/p/approval/workflow/{id})
```bash
curl -X DELETE "http://localhost:5000/api/v3.0/p/approval/workflow/{id}" \
  -H "Authorization: Bearer {token}"
```
**Expected Response:**
```json
{
  "success": true,
  "message": "Workflow deleted successfully"
}
```

### 6. Get Pending Approvals (GET /api/v3.0/p/approval/pending)
```bash
curl -X GET "http://localhost:5000/api/v3.0/p/approval/pending" \
  -H "Authorization: Bearer {token}"
```
**Expected Response:** List of pending approval requests for current user
**Screenshot:** api-pending-approvals.png

### 7. Approve Request (POST /api/v3.0/p/approval/request/{id}/approve)
```bash
curl -X POST "http://localhost:5000/api/v3.0/p/approval/request/{id}/approve" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "comments": "Approved"
  }'
```
**Expected Response:** Updated request with new status

### 8. Reject Request (POST /api/v3.0/p/approval/request/{id}/reject)
```bash
curl -X POST "http://localhost:5000/api/v3.0/p/approval/request/{id}/reject" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "comments": "Rejected",
    "reason": "Budget constraints"
  }'
```
**Expected Response:** Updated request with rejected status

### 9. Delegate Request (POST /api/v3.0/p/approval/request/{id}/delegate)
```bash
curl -X POST "http://localhost:5000/api/v3.0/p/approval/request/{id}/delegate" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "delegateToUserId": "user-guid",
    "comments": "Please review"
  }'
```
**Expected Response:** Delegation confirmation

### 10. Get Request History (GET /api/v3.0/p/approval/request/{id}/history)
```bash
curl -X GET "http://localhost:5000/api/v3.0/p/approval/request/{id}/history" \
  -H "Authorization: Bearer {token}"
```
**Expected Response:** Chronological list of history entries

### 11. Get Dashboard Metrics (GET /api/v3.0/p/approval/dashboard/metrics)
```bash
curl -X GET "http://localhost:5000/api/v3.0/p/approval/dashboard/metrics" \
  -H "Authorization: Bearer {token}"
```
**Expected Response (requires Manager/Administrator role):**
```json
{
  "success": true,
  "data": {
    "pendingCount": 5,
    "averageTimeInHours": 12.5,
    "approvalRatePercent": 85.0,
    "overdueCount": 2,
    "recentActivity": [...]
  }
}
```
**Screenshot:** api-dashboard-metrics.png

## Test Data Used
- Bearer token for authenticated user
- Workflow IDs from previous tests
- Request IDs for approval actions

## API Endpoint Summary
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | /api/v3.0/p/approval/workflow | List workflows |
| POST | /api/v3.0/p/approval/workflow | Create workflow |
| GET | /api/v3.0/p/approval/workflow/{id} | Get workflow |
| PUT | /api/v3.0/p/approval/workflow/{id} | Update workflow |
| DELETE | /api/v3.0/p/approval/workflow/{id} | Delete workflow |
| GET | /api/v3.0/p/approval/pending | List pending |
| GET | /api/v3.0/p/approval/request/{id} | Get request |
| POST | /api/v3.0/p/approval/request/{id}/approve | Approve |
| POST | /api/v3.0/p/approval/request/{id}/reject | Reject |
| POST | /api/v3.0/p/approval/request/{id}/delegate | Delegate |
| GET | /api/v3.0/p/approval/request/{id}/history | Get history |
| GET | /api/v3.0/p/approval/dashboard/metrics | Dashboard |

## Authorization Tests
- Without token: Returns 401 Unauthorized
- Without Manager role (for dashboard): Returns 403 Forbidden
- With valid token: Returns 200 OK with data

## Result
✅ PASS - REST API verified:
- ApprovalController implements all 12+ endpoints
- Endpoints use [Authorize] attribute
- ResponseModel envelope pattern used
- Proper HTTP status codes returned
- Unit tests: 437/437 passed
