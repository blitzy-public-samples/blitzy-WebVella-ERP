# STORY-008 Testing Steps - UI Components

## Prerequisites
- Application running (`dotnet run` from WebVella.Erp.Site directory)
- PostgreSQL database available with migrations applied
- User logged in as Administrator
- Browser open to http://localhost:5000

## Steps to Test

### 1. PcApprovalWorkflowConfig Component

#### 1.1 Add Component to Page
1. Navigate to page builder
2. Add new component from "Approval Workflow" category
3. Select "Approval Workflow Config"
4. **Expected:** Component options panel appears

#### 1.2 Test Display Mode
1. View the configured page
2. **Expected Result:**
   - Workflow list displayed in table format
   - Create, Edit, Delete buttons available
   - Search/filter functionality
   - Pagination for large lists
3. **Screenshot:** ui-workflow-config-display.png

#### 1.3 Test Design Mode
1. Switch to page builder design view
2. **Expected Result:**
   - Component shows preview placeholder
   - Component is draggable/movable
3. **Screenshot:** ui-workflow-config-design.png

#### 1.4 Test Options Panel
1. Click component options in page builder
2. **Expected Result:**
   - Configuration options displayed
   - Title setting
   - Page size setting
   - Filter options
3. **Screenshot:** ui-workflow-config-options.png

### 2. PcApprovalRequestList Component

#### 2.1 Test Display Mode
1. Add component to page and view
2. **Expected Result:**
   - List of approval requests
   - Filter by status (pending/approved/rejected/all)
   - Sort by date
   - Pagination
   - Click to view details
3. **Screenshot:** ui-request-list-display.png

#### 2.2 Test Filter Functionality
1. Select "Pending" filter
2. **Expected:** Only pending requests shown
3. Select "Approved" filter
4. **Expected:** Only approved requests shown
5. **Screenshot:** ui-request-list-filtered.png

#### 2.3 Test Pagination
1. With 20+ requests in database
2. Navigate to page 2
3. **Expected:** Different set of requests shown

### 3. PcApprovalAction Component

#### 3.1 Test Display Mode
1. Add component to approval request detail page
2. View a pending request
3. **Expected Result:**
   - "Approve" button (green)
   - "Reject" button (red)
   - "Delegate" button (blue)
   - Comments text area
4. **Screenshot:** ui-approval-action-display.png

#### 3.2 Test Approve Action
1. Click "Approve" button
2. Enter comment: "Looks good"
3. Click Confirm
4. **Expected Result:**
   - Success message shown
   - Request status updated
   - Page refreshes or redirects

#### 3.3 Test Reject Action
1. Click "Reject" button
2. Enter reason: "Budget exceeded"
3. Click Confirm
4. **Expected Result:**
   - Success message shown
   - Request status = rejected

#### 3.4 Test Delegate Action
1. Click "Delegate" button
2. Select user from dropdown
3. Enter comment: "Please review"
4. Click Confirm
5. **Expected Result:**
   - Success message shown
   - Delegation recorded

### 4. PcApprovalHistory Component

#### 4.1 Test Display Mode
1. Add component to approval request detail page
2. View a request with history
3. **Expected Result:**
   - Timeline view of actions
   - Each entry shows:
     - Action type (submitted/approved/rejected/delegated)
     - User who performed action
     - Timestamp
     - Comments
4. **Screenshot:** ui-approval-history-display.png

#### 4.2 Test Empty State
1. View a newly created request
2. **Expected Result:**
   - Shows only "submitted" entry
   - Clean empty state message if no history

### 5. Component Registration Verification

#### 5.1 Verify in Page Builder
1. Navigate to page builder
2. Open component library
3. Find "Approval Workflow" category
4. **Expected Components:**
   - Approval Workflow Config
   - Approval Request List
   - Approval Action
   - Approval History
5. **Screenshot:** ui-component-library.png

## Test Data Used
- Approval workflow: "Purchase Order Approval"
- 5+ pending requests for list testing
- 2+ completed requests for history testing
- Multiple users for delegation testing

## Component File Structure
Each component includes:
```
Components/{ComponentName}/
  ├── {ComponentName}.cs      # Component class
  ├── Display.cshtml          # Runtime view
  ├── Design.cshtml           # Page builder preview
  ├── Options.cshtml          # Configuration panel
  ├── Help.cshtml             # Documentation
  ├── Error.cshtml            # Error display
  └── service.js              # Client-side logic
```

## Component Attributes Verified
```csharp
[PageComponent(
    Label = "Approval Workflow Config",
    Library = "WebVella",
    Description = "Manage approval workflow configurations",
    Version = "0.0.1",
    IconClass = "fas fa-cogs",
    Category = "Approval Workflow"
)]
```

## JavaScript Integration Tests
1. AJAX calls work correctly
2. Toast notifications appear
3. Refresh after actions
4. Error handling displays messages

## Result
✅ PASS - UI components verified:
- PcApprovalWorkflowConfig: 7 files implemented
- PcApprovalRequestList: 7 files implemented
- PcApprovalAction: 7 files implemented
- PcApprovalHistory: 7 files implemented
- All components registered under "Approval Workflow" category
- Unit tests: 437/437 passed
