# STORY-008 Testing Steps - UI Components

## Prerequisites

### 1. Environment Configuration (CRITICAL - Do This First)

**Before ANY testing, set the environment variable:**

```bash
# Windows Command Prompt
set ASPNETCORE_ENVIRONMENT=Development

# Windows PowerShell
$env:ASPNETCORE_ENVIRONMENT="Development"

# Linux/Mac
export ASPNETCORE_ENVIRONMENT=Development
```

**Why:** This is required for static file serving and development-mode features. Without it, you may encounter 404 or 405 errors.

### 2. Build and Run the Project

```bash
# Navigate to repository root
cd /path/to/WebVella-ERP

# Restore and build
dotnet restore WebVella.ERP3.sln
dotnet build WebVella.ERP3.sln --configuration Release

# Run the application
cd WebVella.Erp.Site
dotnet run
```

**Default URL:** `http://localhost:5000`

### 3. Create Test Data

Before testing UI components, create test data:

1. **Create a workflow for purchase_order entity:**
   - Navigate to Page Builder
   - Use the PcApprovalWorkflowConfig component (already tested in STORY-003)
   - Create workflow: Name="Purchase Order Approval", Entity="purchase_order"
   - Add at least one step with approver_type="role"

2. **Create an approval request:**
   - Use the API directly or create a purchase order record that triggers the approval workflow
   - Note the `approval_request.id` (GUID) from the database for testing

3. **Query database for test data:**
```sql
SELECT id, status, source_entity_name, requested_on 
FROM approval_request 
WHERE status = 'pending' 
LIMIT 1;
```

---

## Testing PcApprovalRequestList (Approval List)

### Overview
This component displays a paginated list of approval requests with filtering options.

### Step-by-Step Testing

#### Step 1: Navigate to Page Builder
1. Open browser and go to `http://localhost:5000`
2. Login with admin credentials
3. Navigate to **SDK → Pages**
4. Click **"Create New Page"**

#### Step 2: Create Page
1. Set page details:
   - **Name:** "Approval List Test"
   - **URL:** "/test/approval-list"
   - **Layout:** Choose default layout
2. Click **Save**

#### Step 3: Add Component
1. Open the page in page builder
2. Click **"Add Component"** or drag from component library
3. Search for **"Approval"** in the component search
4. Find **"Approval Request List"** (PcApprovalRequestList) in the "Approval Workflow" category
5. Drag it to the page body area

#### Step 4: Configure Component Options
1. Click on the component to select it
2. Click the **Options** tab in the right panel
3. Configure:
   - **StatusFilter:** "pending" (to show only pending approvals)
   - **PageSize:** 10
   - **ShowMyRequestsOnly:** false (to see all requests)
4. Click **Save**

#### Step 5: Test Component
1. Click **"View Page"** or navigate to `/test/approval-list`
2. **Expected Results:**
   - ✅ Table displays with headers: Request ID, Status, Created Date, Source Entity, etc.
   - ✅ Pending approval requests are shown in the table (if any exist)
   - ✅ If no pending requests: Shows "No pending approval requests found" message (NOT "Record not found")
   - ✅ Pagination controls visible at bottom
   - ✅ Filter controls visible at top

#### Step 6: Troubleshooting
If you see "Record not found" or the table is empty despite data existing:

1. **Open DevTools Console (F12 → Console tab):**
   - Check for JavaScript errors
   - Should NOT see any red error messages

2. **Open DevTools Network tab (F12 → Network tab):**
   - Refresh the page
   - Look for request to `/api/v3.0/p/approval/pending`
   - Click on the request and check:
     - **Status:** Should be 200 OK
     - **Response tab:** Should contain JSON with `Success: true` and `Object: { Items: [...] }`

3. **Verify response structure:**
   - The API returns: `{ Success: true, Object: { Items: [...], TotalCount: N } }`
   - The service.js parses `data.Object.Items` or `data.Items`

#### Step 7: Screenshot
Save screenshot to: `validation/STORY-008/approval-list-working.png`

---

## Testing PcApprovalAction (Approval Action)

### Overview
This component displays approval request details and action buttons (Approve, Reject, Delegate).

### Step-by-Step Testing

#### Step 1: Get Approval Request ID
Query your database to get a pending approval request:
```sql
SELECT id FROM approval_request WHERE status = 'pending' LIMIT 1;
```
Copy the GUID (e.g., `123e4567-e89b-12d3-a456-426614174000`)

#### Step 2: Create Page
1. Navigate to **SDK → Pages**
2. Create page:
   - **Name:** "Approval Action Test"
   - **URL:** "/test/approval-action"
3. Click **Save**

#### Step 3: Add Component
1. Open page in page builder
2. Click **"Add Component"**
3. Find **"Approval Action"** (PcApprovalAction) in "Approval Workflow" category
4. Add it to the page

#### Step 4: Configure Component Options (CRITICAL)
1. Click on the component
2. Go to **Options** tab
3. **IMPORTANT - Set the RequestId:**
   - **RequestId:** Paste the approval request GUID from Step 1
   - Format: `123e4567-e89b-12d3-a456-426614174000`
4. Additional options:
   - **ShowApprove:** true
   - **ShowReject:** true
   - **ShowDelegate:** true
5. Click **Save**

#### Step 5: Test Component Display
1. Navigate to `/test/approval-action`
2. **Open DevTools Network tab BEFORE loading**
3. Refresh the page
4. **Expected Results:**
   - ✅ Request details section shows: Workflow name, Current Step, Status, Source Entity, Requested On
   - ✅ Three buttons visible: Approve (green), Reject (red), Delegate (blue)
   - ✅ If not authorized: Shows "You are not authorized" message
   - ✅ If request not pending: Shows status message (e.g., "This request has been approved")

#### Step 6: Test Approve Button
1. Click **Approve** button
2. Modal dialog should appear with:
   - Title: "Approve Request"
   - Comments textarea (optional)
   - Cancel and Approve buttons
3. Enter optional comments
4. Click **Approve** to confirm
5. **Expected:**
   - ✅ API call: POST `/api/v3.0/p/approval/request/{id}/approve`
   - ✅ Success toast notification
   - ✅ Page refreshes with updated status

#### Step 7: Test Reject Button
1. (Create a new pending request for testing)
2. Click **Reject** button
3. Modal should appear with:
   - Reason field (required)
   - Comments field (optional)
4. Enter reason and click **Reject**
5. **Expected:**
   - ✅ API call: POST `/api/v3.0/p/approval/request/{id}/reject`
   - ✅ Status changes to "rejected"

#### Step 8: Troubleshooting
If no buttons appear or details don't load:

1. **Check RequestId option:**
   - Go to page builder Options tab
   - Verify RequestId is set to a valid GUID
   - GUID must be lowercase and properly formatted

2. **Check Network tab:**
   - Look for GET `/api/v3.0/p/approval/request/{id}` call
   - Verify it returns 200 OK with request data

3. **Check Console for errors:**
   - JavaScript errors will prevent buttons from working

#### Step 9: Screenshot
Save screenshot to: `validation/STORY-008/approval-action-working.png`

---

## Testing PcApprovalHistory (Approval History)

### Overview
This component displays the approval audit trail as a timeline.

### Step-by-Step Testing

#### Step 1: Use Same Request ID
Use the same approval request ID from PcApprovalAction testing (preferably one that has been approved/rejected so it has history entries).

#### Step 2: Create Page
1. Navigate to **SDK → Pages**
2. Create page:
   - **Name:** "Approval History Test"
   - **URL:** "/test/approval-history"
3. Click **Save**

#### Step 3: Add Component
1. Open page in page builder
2. Click **"Add Component"**
3. Find **"Approval History"** (PcApprovalHistory) in "Approval Workflow" category
4. Add it to the page

#### Step 4: Configure Component Options
1. Click on the component
2. Go to **Options** tab
3. **Set RequestId:** Paste the approval request GUID
4. Click **Save**

#### Step 5: Test Component Display
1. Navigate to `/test/approval-history`
2. **Open DevTools Network tab BEFORE loading**
3. Refresh the page
4. **Expected Results:**
   - ✅ API call visible: GET `/api/v3.0/p/approval/request/{id}/history`
   - ✅ Timeline displays with history entries
   - ✅ Each entry shows:
     - Action type icon (Submitted, Approved, Rejected, etc.)
     - Action text with color coding
     - Performed by (username or "User")
     - Performed on (date/time)
     - Comments (if any)
   - ✅ If request was just created: Shows at least "Submitted" entry
   - ✅ If request was approved: Shows "Submitted" and "Approved" entries

#### Step 6: Verify History Details
For each history entry, verify:
- **Action types:** Submitted (blue), Approved (green), Rejected (red), Delegated (cyan), Escalated (orange)
- **User names:** Should show actual usernames, not just GUIDs
- **Dates:** Should be formatted as "dd MMM yyyy HH:mm"
- **Order:** Should be chronological (oldest to newest, or newest to oldest based on configuration)

#### Step 7: Troubleshooting
If timeline is empty or shows error:

1. **Check RequestId:**
   - Verify it's set in component options
   - Verify the request has history entries

2. **Check Network tab:**
   - Look for GET `/api/v3.0/p/approval/request/{id}/history`
   - Verify it returns array of history records

3. **Check data-approval-history-component attribute:**
   - Inspect the component HTML
   - Should have `data-approval-history-component` attribute on the container div

#### Step 8: Screenshot
Save screenshot to: `validation/STORY-008/approval-history-working.png`

---

## End-to-End Test Scenario

### Complete Workflow Test

This scenario tests the entire approval workflow from request creation to completion.

#### Step 1: Create Approval Workflow (if not already done)
1. Use PcApprovalWorkflowConfig component
2. Create workflow for "purchase_order" entity
3. Add Step 1 with approver_type="role"

#### Step 2: Trigger Approval Request
1. Create a purchase_order record via API or database
2. The hook should automatically create an approval_request
3. Note the new approval_request ID

#### Step 3: Verify in Approval List
1. Open the Approval List page (`/test/approval-list`)
2. **Verify:** New request appears in the table with status "pending"

#### Step 4: Configure Approval Action Page
1. Open Approval Action page in page builder
2. Set RequestId option to the new request ID
3. Save and view the page

#### Step 5: Perform Approval Action
1. Open Approval Action page (`/test/approval-action`)
2. **Verify:** Request details displayed correctly
3. Click **Approve** button
4. Enter comments: "Approved for testing"
5. Confirm approval

#### Step 6: Verify Status Change
1. Refresh Approval List page
2. **Verify:** Request should no longer appear (status is now "approved")

#### Step 7: View History
1. Open Approval History page (`/test/approval-history`)
2. **Verify:** Timeline shows two entries:
   - "Submitted for Approval" with submitter
   - "Approved" with approver and comments

---

## Common Issues and Solutions

| Issue | Possible Cause | Solution |
|-------|----------------|----------|
| API returns 405 Method Not Allowed | Environment not set to Development | Set `ASPNETCORE_ENVIRONMENT=Development` |
| "Record not found" in Request List | service.js parsing wrong property | Check `data.Object.Items` vs `data.requests` |
| No buttons in Approval Action | RequestId not configured | Set RequestId in component Options |
| Timeline empty in History | No history entries exist | Perform an approval action first |
| JavaScript errors in console | Missing dependencies | Verify jQuery and Bootstrap loaded |
| API not calling | Missing data attribute | Check for `data-approval-action-component` attribute |
| Status not updating after action | API call failed | Check Network tab for error response |
| User names showing as GUIDs | User lookup failed | Check user table has matching records |
| Dates showing "N/A" | Date parsing issue | Check date format from API |
| Buttons disabled | User not authorized | Login as user in approver role |

---

## Result Summary

### Test Checklist

- [ ] Environment variable set (ASPNETCORE_ENVIRONMENT=Development)
- [ ] Test data created (workflow, steps, approval request)
- [ ] PcApprovalRequestList displays pending requests
- [ ] PcApprovalRequestList pagination works
- [ ] PcApprovalAction shows request details
- [ ] PcApprovalAction Approve button works
- [ ] PcApprovalAction Reject button works
- [ ] PcApprovalAction Delegate button works
- [ ] PcApprovalHistory shows timeline
- [ ] PcApprovalHistory shows user names
- [ ] End-to-end workflow completed
- [ ] All screenshots captured

### Component Files Verified

| Component | Files | Status |
|-----------|-------|--------|
| PcApprovalWorkflowConfig | 7 | ✅ Complete |
| PcApprovalRequestList | 7 | ✅ Complete |
| PcApprovalAction | 7 | ✅ Complete |
| PcApprovalHistory | 7 | ✅ Complete |
| PcApprovalDashboard | 7 | ✅ Complete |

### All 566 Tests Pass
```
Passed!  - Failed: 0, Passed: 566, Skipped: 0, Total: 566
```
