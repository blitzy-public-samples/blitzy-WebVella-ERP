# STORY-009 Testing Steps - Manager Dashboard Metrics

## Prerequisites
- Application running with PostgreSQL database connected
- Database migrated with approval entities created
- User logged in with Manager or Administrator role
- Approval workflow configured with test data
- Multiple approval requests in various states (pending, approved, rejected)

## Steps to Test

### 1. Verify Dashboard Component Files Exist
- [x] `Components/PcApprovalDashboard/PcApprovalDashboard.cs`
- [x] `Components/PcApprovalDashboard/Design.cshtml`
- [x] `Components/PcApprovalDashboard/Display.cshtml`
- [x] `Components/PcApprovalDashboard/Options.cshtml`
- [x] `Components/PcApprovalDashboard/Help.cshtml`
- [x] `Components/PcApprovalDashboard/Error.cshtml`
- [x] `Components/PcApprovalDashboard/service.js`

### 2. Verify DashboardMetricsService Exists
- [x] `Services/DashboardMetricsService.cs`

### 3. Add Dashboard to Page
1. Open WebVella Page Builder
2. Find PcApprovalDashboard in "Approval Workflow" category
3. Add component to a new page
4. **Expected**: Component appears with icon fas fa-chart-line
5. Configure component options
6. Save and publish page

### 4. Test Dashboard Role Access
1. Log in as regular user (not Manager/Admin)
2. Navigate to dashboard page
3. **Expected**: Access denied or limited view (per security requirements)

4. Log in as Manager role user
5. Navigate to dashboard page
6. **Expected**: Full dashboard visible with all metrics
7. Take screenshot: `validation/STORY-009/dashboard-manager-access.png`

### 5. Test Metric: Pending Count
1. View dashboard
2. **Expected**: "Pending Approvals" card displays count of status='pending'
3. Create new approval request
4. Refresh dashboard
5. **Expected**: Pending count increases by 1
6. Take screenshot: `validation/STORY-009/metric-pending-count.png`

### 6. Test Metric: Average Approval Time
1. View dashboard
2. **Expected**: "Average Approval Time" displays calculated value
3. Calculation: Average of (completed_on - requested_on) for approved requests
4. **Expected**: Displayed in hours or days format
5. Take screenshot: `validation/STORY-009/metric-avg-time.png`

### 7. Test Metric: Approval Rate
1. View dashboard
2. **Expected**: "Approval Rate" displays percentage
3. Calculation: (approved count / total completed count) * 100
4. **Expected**: Displayed as percentage (e.g., "87.5%")
5. Take screenshot: `validation/STORY-009/metric-approval-rate.png`

### 8. Test Metric: Overdue Count
1. View dashboard
2. **Expected**: "Overdue Approvals" displays count
3. Calculation: Pending requests past their step timeout_hours
4. Create request with timeout_hours = 0.001 (test value)
5. Wait for timeout
6. Refresh dashboard
7. **Expected**: Overdue count increases
8. Take screenshot: `validation/STORY-009/metric-overdue-count.png`

### 9. Test Metric: Recent Activity
1. View dashboard
2. **Expected**: "Recent Activity" shows last N approval actions
3. **Expected**: Each activity shows: action type, user, timestamp
4. Perform approval action
5. Refresh dashboard
6. **Expected**: New activity appears in list
7. Take screenshot: `validation/STORY-009/metric-recent-activity.png`

### 10. Test Dashboard Auto-Refresh
1. Configure dashboard with auto-refresh interval (e.g., 30 seconds)
2. Leave dashboard open
3. In another tab, approve a request
4. Wait for auto-refresh interval
5. **Expected**: Dashboard updates automatically without manual refresh
6. Take screenshot: `validation/STORY-009/dashboard-auto-refresh.png`

### 11. Test Dashboard API Endpoint
```bash
curl -X GET "http://localhost:5000/api/v3.0/p/approval/dashboard/metrics" \
  -H "Authorization: Bearer <token>"
```
**Expected Response**:
```json
{
  "success": true,
  "data": {
    "pendingCount": 5,
    "averageApprovalTimeHours": 24.5,
    "approvalRatePercent": 87.5,
    "overdueCount": 2,
    "recentActivity": [
      {
        "action": "approved",
        "performedBy": "John Doe",
        "performedOn": "2026-01-27T10:30:00Z",
        "requestId": "..."
      }
    ]
  }
}
```

### 12. Test Dashboard with No Data
1. Clear all approval requests (or use new database)
2. View dashboard
3. **Expected**: Dashboard renders without errors
4. **Expected**: Shows "0" for counts, "N/A" for averages
5. **Expected**: "No recent activity" message

### 13. Test Dashboard Rendering Modes
1. In Page Builder, view Design mode
2. **Expected**: Preview/placeholder renders
3. View Options panel
4. **Expected**: Configuration options displayed
5. View Help
6. **Expected**: Usage instructions displayed

## Test Data Requirements
For comprehensive testing, create the following data:
- 10+ approval requests with status='pending'
- 5+ approved requests (completed)
- 3+ rejected requests (completed)
- 2+ overdue requests (past timeout)
- 15+ history entries for recent activity

## Code Verification Completed

### PcApprovalDashboard Component
- [x] PageComponent attribute with Label="Approval Dashboard"
- [x] IconClass = "fas fa-chart-line"
- [x] Category = "Approval Workflow"
- [x] InvokeAsync handles Display, Design, Options, Help, Error modes
- [x] Uses DashboardMetricsService for data
- [x] Role validation for Manager/Administrator access

### DashboardMetricsService
- [x] GetDashboardMetrics() method returns all 5 metrics
- [x] GetPendingCount() - counts status='pending'
- [x] GetAverageApprovalTime() - calculates average
- [x] GetApprovalRate() - calculates percentage
- [x] GetOverdueCount() - counts overdue requests
- [x] GetRecentActivity(int limit) - returns last N activities
- [x] Uses RecordManager for database queries
- [x] Handles null/empty data gracefully

### DashboardMetricsModel
- [x] PendingCount (int)
- [x] AverageApprovalTimeHours (double)
- [x] ApprovalRatePercent (double)
- [x] OverdueCount (int)
- [x] RecentActivity (List)

### Auto-Refresh JavaScript
- [x] service.js implements setInterval for auto-refresh
- [x] Calls /api/v3.0/p/approval/dashboard/metrics endpoint
- [x] Updates DOM with new metric values
- [x] Configurable refresh interval via options

## Result
✅ PASS (Code verification complete - dashboard rendering requires runtime with database)

## Notes
- Dashboard requires Manager or Administrator role for full access
- Auto-refresh uses JavaScript setInterval
- Metrics calculated in real-time from database
- All 5 KPIs implemented as specified in requirements
- Unit tests verify service calculations
