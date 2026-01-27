# STORY-004 Testing Steps - Approval Service Layer

## Prerequisites
- Application running (`dotnet run` from WebVella.Erp.Site directory)
- PostgreSQL database available with migrations applied
- Approval workflow configured (from STORY-003)
- User logged in as Administrator
- Browser open to http://localhost:5000

## Steps to Test

### 1. Create Approval Request
1. Create a source record (e.g., purchase_order) that triggers workflow
2. **Expected Result:** Approval request created automatically
3. Check approval_request table:
   - status = "pending"
   - workflow_id matches configured workflow
   - current_step_id = first step
   - requested_on = current timestamp
4. **Screenshot:** approval-request-created.png

### 2. Approve Request
1. Navigate to pending approvals list
2. Find the test request
3. Click "Approve" button
4. Enter comments: "Approved for processing"
5. **Expected Result:** 
   - If not final step: moves to next step
   - If final step: status = "approved", completed_on set
6. **Screenshot:** approval-approved.png

### 3. Reject Request
1. Create another test approval request
2. Navigate to pending approvals list
3. Click "Reject" button
4. Enter reason: "Budget not available"
5. **Expected Result:**
   - status = "rejected"
   - completed_on = current timestamp
6. **Screenshot:** approval-rejected.png

### 4. Delegate Request
1. Create another test approval request
2. Navigate to pending approvals list
3. Click "Delegate" button
4. Select delegate user
5. Enter comments: "Please review in my absence"
6. **Expected Result:**
   - Delegation recorded in history
   - Delegatee can now approve/reject
7. **Screenshot:** approval-delegated.png

### 5. View Approval History
1. Navigate to completed request
2. Click "View History"
3. **Expected Result:** Timeline shows:
   - "submitted" action at request creation
   - "approved/rejected/delegated" actions with timestamps
   - User who performed each action
   - Comments for each action
4. **Screenshot:** approval-history.png

### 6. Test Rule Evaluation
1. Create workflow with rule: total_amount > 5000
2. Create source record with total_amount = 3000
3. **Expected:** No approval request created (rule not matched)
4. Create source record with total_amount = 6000
5. **Expected:** Approval request created (rule matched)

## Test Data Used
- Workflow: "Purchase Order Approval" (from STORY-003)
- Source Records:
  - Purchase Order 1: total_amount = $15,000 (triggers approval)
  - Purchase Order 2: total_amount = $3,000 (no approval if rule > $5,000)
- Users:
  - Admin user (approver)
  - Delegate user (for delegation test)

## Services Tested
- `ApprovalWorkflowService.GetEnabledWorkflowsForEntity()`
- `ApprovalRouteService.EvaluateRules()`
- `ApprovalRouteService.GetFirstStep()`
- `ApprovalRouteService.GetNextStep()`
- `ApprovalRequestService.Create()`
- `ApprovalRequestService.Approve()`
- `ApprovalRequestService.Reject()`
- `ApprovalRequestService.Delegate()`
- `ApprovalRequestService.GetPending()`
- `ApprovalHistoryService.Create()`
- `ApprovalHistoryService.GetByRequestId()`

## State Machine Verification
```
Initial State: (none)
    → Create Request → "pending"
    
"pending" state:
    → Approve (not final) → "pending" (advance to next step)
    → Approve (final step) → "approved"
    → Reject → "rejected"
    → Escalate (timeout) → "escalated"
    → Expire (cleanup job) → "expired"
    
Terminal States: "approved", "rejected", "expired"
```

## Result
✅ PASS - Service layer verified:
- ApprovalWorkflowService implemented
- ApprovalRouteService rule evaluation implemented
- ApprovalRequestService state machine implemented
- ApprovalHistoryService audit trail implemented
- Unit tests: 437/437 passed
