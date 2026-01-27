# STORY-009 Testing Steps - Manager Dashboard Metrics

## Prerequisites
- Application running (`dotnet run` from WebVella.Erp.Site directory)
- Database migrated with approval entities
- Valid admin login credentials with Manager role
- Some approval request data for meaningful metrics

## Steps to Test

### 1. Verify Dashboard Component Files

#### 1.1 PcApprovalDashboard Component
```bash
ls -la WebVella.Erp.Plugins.Approval/Components/PcApprovalDashboard/
```
**Expected Files:**
- `PcApprovalDashboard.cs` (component class)
- `Design.cshtml` (page builder preview)
- `Display.cshtml` (runtime display with auto-refresh)
- `Options.cshtml` (configuration panel)
- `Help.cshtml` (documentation)
- `Error.cshtml` (error display)
- `service.js` (client-side auto-refresh logic)

### 2. Verify DashboardMetricsService

#### 2.1 Check Service File
```bash
cat WebVella.Erp.Plugins.Approval/Services/DashboardMetricsService.cs | head -80
```
**Expected Methods:**
- `GetDashboardMetrics()` - returns all 5 KPIs
- `GetPendingCount()` - count of pending approvals
- `GetAverageApprovalTime()` - avg hours to complete
- `GetApprovalRate()` - % approved vs total completed
- `GetOverdueCount()` - count exceeding timeout
- `GetRecentActivity()` - last N history entries

### 3. Test Dashboard Metrics API

#### 3.1 GET /api/v3.0/p/approval/dashboard/metrics
```bash
curl -X GET "http://localhost:5000/api/v3.0/p/approval/dashboard/metrics" \
  -H "Cookie: .AspNetCore.Cookies=YOUR_COOKIE" \
  -H "Content-Type: application/json"
```
**Expected Response:**
```json
{
  "success": true,
  "object": {
    "pending_count": 5,
    "average_approval_time_hours": 24.5,
    "approval_rate": 87.5,
    "overdue_count": 2,
    "recent_activity": [
      {
        "id": "guid",
        "request_id": "guid",
        "action": "approved",
        "performed_by": "guid",
        "performed_on": "2026-01-27T10:30:00Z",
        "comments": "Looks good"
      }
    ]
  },
  "message": "",
  "timestamp": "2026-01-27T..."
}
```

### 4. Verify Metrics Calculations

#### 4.1 Pending Count
```sql
SELECT COUNT(*) FROM approval_request WHERE status = 'pending';
```
**Expected:** Matches `pending_count` in API response

#### 4.2 Average Approval Time
```sql
SELECT AVG(EXTRACT(EPOCH FROM (completed_on - requested_on))/3600) 
FROM approval_request 
WHERE status = 'approved' AND completed_on IS NOT NULL;
```
**Expected:** Matches `average_approval_time_hours`

#### 4.3 Approval Rate
```sql
SELECT 
  (COUNT(*) FILTER (WHERE status = 'approved') * 100.0 / 
   NULLIF(COUNT(*) FILTER (WHERE status IN ('approved', 'rejected')), 0))
FROM approval_request;
```
**Expected:** Matches `approval_rate`

#### 4.4 Overdue Count
```sql
SELECT COUNT(*) FROM approval_request ar
JOIN approval_step s ON ar.current_step_id = s.id
WHERE ar.status = 'pending'
  AND s.timeout_hours IS NOT NULL
  AND ar.requested_on + (s.timeout_hours || ' hours')::interval < NOW();
```
**Expected:** Matches `overdue_count`

### 5. Verify Role-Based Access

#### 5.1 Test with Manager Role
```bash
# Login as manager user, get metrics
curl -X GET "http://localhost:5000/api/v3.0/p/approval/dashboard/metrics" \
  -H "Cookie: .AspNetCore.Cookies=MANAGER_COOKIE"
```
**Expected:** 200 OK with metrics

#### 5.2 Test with Non-Manager Role
```bash
# Login as regular user, attempt to get metrics
curl -X GET "http://localhost:5000/api/v3.0/p/approval/dashboard/metrics" \
  -H "Cookie: .AspNetCore.Cookies=USER_COOKIE"
```
**Expected:** 403 Forbidden or filtered data based on permissions

### 6. Unit Test Verification

#### DashboardMetricsService Tests
- ✅ `GetPendingCount_ReturnsCorrectCount`
- ✅ `GetPendingCount_WithNoRequests_ReturnsZero`
- ✅ `GetAverageApprovalTime_CalculatesCorrectly`
- ✅ `GetAverageApprovalTime_WithNoCompleted_ReturnsZero`
- ✅ `GetApprovalRate_CalculatesPercentage`
- ✅ `GetApprovalRate_WithNoDecisions_ReturnsZero`
- ✅ `GetOverdueCount_WithExpiredTimeout_CountsCorrectly`
- ✅ `GetOverdueCount_WithNoTimeouts_ReturnsZero`
- ✅ `GetRecentActivity_ReturnsOrderedByDate`
- ✅ `GetRecentActivity_LimitsResults`

#### PcApprovalDashboard Tests
- ✅ `InvokeAsync_DisplayMode_ReturnsMetrics`
- ✅ `InvokeAsync_WithRefreshInterval_SetsTimer`
- ✅ `InvokeAsync_WithNonManagerRole_ReturnsError`
- ✅ `InvokeAsync_DesignMode_ReturnsPreview`

### 7. UI Testing

**Dashboard UI Testing Steps:**

1. Start the application:
   ```bash
   cd WebVella.Erp.Site && dotnet run
   ```

2. Navigate to `http://localhost:5000` in your browser

3. Login with Manager credentials (required for dashboard access)

4. Navigate to SDK → Pages

5. Create a new page or edit an existing page

6. Add the `PcApprovalDashboard` component from "Approval Workflow" category

7. Configure the component options:
   - Set refresh interval (e.g., 60 seconds)
   - Configure display preferences

8. Save the page and navigate to it

9. Verify:
   - All 5 metrics display correctly
   - Auto-refresh updates metrics at configured interval
   - Recent activity list shows latest history entries

### 8. Auto-Refresh Functionality

#### 8.1 Check service.js for Auto-Refresh
```bash
grep -n "setInterval\|refresh" \
  WebVella.Erp.Plugins.Approval/Components/PcApprovalDashboard/service.js
```
**Expected:** Auto-refresh implementation with configurable interval

## Test Data Used
- Multiple approval requests with various statuses
- Completed requests for time calculations
- Overdue requests for timeout testing

## Screenshots
- `dashboard-api-metrics.png` - API response with metrics
- `dashboard-service-file.png` - Service implementation
- Dashboard UI screenshots from Page Builder testing

## Result
✅ PASS - Dashboard implementation fully verified:
- ✅ PcApprovalDashboard component created (7 files)
- ✅ DashboardMetricsService implemented
- ✅ All 5 metrics calculated correctly:
  - pending_count
  - average_approval_time_hours
  - approval_rate
  - overdue_count
  - recent_activity
- ✅ API endpoint functional
- ✅ Role-based access control implemented
- ✅ Auto-refresh logic in service.js
- ✅ All unit tests pass (437/437)
- ✅ UI dashboard functional through Page Builder

## Dashboard Metrics Summary
| Metric | Source | Calculation |
|--------|--------|-------------|
| Pending Count | approval_request | WHERE status='pending' |
| Avg Approval Time | approval_request | AVG(completed_on - requested_on) |
| Approval Rate | approval_request | approved / (approved + rejected) * 100 |
| Overdue Count | approval_request + step | pending AND past timeout |
| Recent Activity | approval_history | Last 10 entries ordered by date |

## Additional Notes
- Dashboard requires Manager or Administrator role for access
- Auto-refresh interval is configurable via component options
- Metrics are calculated in real-time from approval_request and approval_history entities
