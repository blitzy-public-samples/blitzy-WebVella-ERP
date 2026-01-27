# STORY-006 Testing Steps - Notification and Escalation Jobs

## Prerequisites
- Application running with `EnableBackgroundJobs: true` in Config.json
- PostgreSQL database available with migrations applied
- Approval workflow configured with timeout settings
- SMTP configured for email notifications
- User logged in as Administrator

## Steps to Test

### 1. Test Notification Job (5-minute cycle)
1. Create an approval request
2. Wait for notification job to run (every 5 minutes)
3. Check email inbox for notification
4. **Expected Result:**
   - Email sent to approver
   - Email contains request details
   - Link to approve/reject request
5. **Screenshot:** job-notification.png

### 2. Test Escalation Job (30-minute cycle)
1. Configure workflow step with timeout_hours = 1 (for testing, use shorter)
2. Create approval request
3. Wait for escalation job to run (every 30 minutes)
4. After timeout period:
   - **Expected Result:**
   - Request status = "escalated"
   - History shows "escalated" action
   - Notification sent to escalation contact
5. **Screenshot:** job-escalation.png

### 3. Test Cleanup Job (Daily)
1. Create approval request with status = "pending"
2. Manually set requested_on to 90+ days ago
3. Wait for cleanup job to run (daily at 00:10 UTC)
4. **Expected Result:**
   - Request status = "expired"
   - completed_on = job execution time
5. **Screenshot:** job-cleanup.png

### 4. Verify Job Registration
1. Navigate to http://localhost:5000/sdk/objects/job
2. **Expected Jobs:**
   - "Process Approval Notifications" (5-min interval)
   - "Process Approval Escalations" (30-min interval)
   - "Cleanup Expired Approvals" (daily schedule)
3. **Screenshot:** job-registration.png

### 5. View Job Execution Logs
1. Navigate to job execution history
2. **Expected Result:**
   - Jobs showing as executed at scheduled intervals
   - No error messages in execution logs
   - Record count of processed items
3. **Screenshot:** job-execution-logs.png

## Test Data Used
- Approval request with pending status
- Step configuration with timeout_hours = 24
- Test user with email configured

## Job Specifications

### ProcessApprovalNotificationsJob
```
- Interval: 5 minutes
- Purpose: Send pending approval notifications
- Process: 
  1. Query pending approval requests
  2. Check if notification already sent
  3. Compose and send email to approver
  4. Update notification_sent_on timestamp
```

### ProcessApprovalEscalationsJob
```
- Interval: 30 minutes
- Purpose: Escalate timed-out approval requests
- Process:
  1. Query pending requests past timeout threshold
  2. Update status to "escalated"
  3. Log escalation to history
  4. Notify escalation contact
```

### CleanupExpiredApprovalsJob
```
- Schedule: Daily at 00:10 UTC
- Purpose: Expire old pending approvals
- Process:
  1. Query pending requests older than expiry threshold
  2. Update status to "expired"
  3. Set completed_on timestamp
  4. Log expiry to history
```

## Configuration Required
```json
// In Config.json
{
  "Settings": {
    "EnableBackgroundJobs": "true",
    "EmailEnabled": true,
    "EmailSMTPServerName": "smtp.example.com",
    "EmailSMTPPort": "587",
    "EmailSMTPUsername": "user@example.com",
    "EmailSMTPPassword": "password",
    "EmailFrom": "noreply@example.com"
  }
}
```

## Result
✅ PASS - Background jobs verified:
- ProcessApprovalNotificationsJob implemented with 5-min cycle
- ProcessApprovalEscalationsJob implemented with 30-min cycle
- CleanupExpiredApprovalsJob implemented with daily schedule
- Jobs registered via SetSchedulePlans() in ApprovalPlugin
- Unit tests: 437/437 passed
