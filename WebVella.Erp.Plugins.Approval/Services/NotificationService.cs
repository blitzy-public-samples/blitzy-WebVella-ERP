using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Diagnostics;
using WebVella.Erp.Eql;
using WebVella.Erp.Plugins.Approval.Model;

namespace WebVella.Erp.Plugins.Approval.Services
{
    /// <summary>
    /// Service class for sending email notifications related to approval workflow events.
    /// Handles pending approval notifications, escalation alerts, rejection notices, 
    /// and approval confirmations. Integrates with WebVella Mail plugin for email delivery
    /// and provides templated notification content for various approval actions.
    /// </summary>
    /// <remarks>
    /// This service inherits from BaseService to access SecurityManager for user email retrieval
    /// and RecordManager for EQL query execution when loading approval request details.
    /// 
    /// Email notifications are queued through the WebVella Mail plugin's SMTP service infrastructure,
    /// which handles actual delivery asynchronously via background jobs.
    /// 
    /// All notification operations are logged for audit and debugging purposes using the
    /// WebVella diagnostics system.
    /// </remarks>
    public class NotificationService : BaseService
    {
        #region Constants

        /// <summary>
        /// Source identifier used for logging notification operations.
        /// </summary>
        private const string LOG_SOURCE = "WebVella.Erp.Plugins.Approval.NotificationService";

        /// <summary>
        /// Entity name for approval requests.
        /// </summary>
        private const string ENTITY_APPROVAL_REQUEST = "approval_request";

        /// <summary>
        /// Entity name for approval workflows.
        /// </summary>
        private const string ENTITY_APPROVAL_WORKFLOW = "approval_workflow";

        /// <summary>
        /// Entity name for approval steps.
        /// </summary>
        private const string ENTITY_APPROVAL_STEP = "approval_step";

        /// <summary>
        /// Entity name for WebVella users.
        /// </summary>
        private const string ENTITY_USER = "user";

        #endregion

        #region Public Methods

        /// <summary>
        /// Sends a notification to an approver about a pending approval request requiring their action.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request.</param>
        /// <param name="approverId">The unique identifier of the user who needs to approve the request.</param>
        /// <remarks>
        /// This method loads the approval request details via EQL query, retrieves the approver's
        /// email address from SecurityManager, constructs a notification email with request context,
        /// and logs the notification attempt for audit purposes.
        /// 
        /// If the approval request or approver cannot be found, the notification is logged as failed
        /// but no exception is thrown to prevent disruption of the calling workflow.
        /// </remarks>
        public void SendPendingApprovalNotification(Guid requestId, Guid approverId)
        {
            string recipientEmail = string.Empty;
            bool success = false;

            try
            {
                // Load approval request details
                var request = LoadApprovalRequest(requestId);
                if (request == null)
                {
                    LogNotificationAttempt(requestId, "PendingApproval", string.Empty, false, 
                        "Approval request not found");
                    return;
                }

                // Get approver email
                recipientEmail = GetUserEmail(approverId);
                if (string.IsNullOrWhiteSpace(recipientEmail))
                {
                    LogNotificationAttempt(requestId, "PendingApproval", string.Empty, false, 
                        $"Approver email not found for user ID: {approverId}");
                    return;
                }

                // Build notification content
                var requestInfo = GetRequestDisplayInfo(request);
                var subject = BuildNotificationSubject(ApprovalActionType.Approve.ToString(), requestInfo);
                var body = BuildNotificationBody(request, "PendingApproval", 
                    "You have a new approval request pending your review and action.");

                // Queue email for delivery
                QueueNotificationEmail(recipientEmail, subject, body);
                success = true;
            }
            catch (Exception ex)
            {
                new Log().Create(LogType.Error, LOG_SOURCE, 
                    $"Failed to send pending approval notification for request {requestId}", ex);
            }
            finally
            {
                LogNotificationAttempt(requestId, "PendingApproval", recipientEmail, success);
            }
        }

        /// <summary>
        /// Sends a confirmation notification to the request initiator when their approval request 
        /// has been fully approved.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request.</param>
        /// <param name="initiatorId">The unique identifier of the user who initiated the request.</param>
        /// <remarks>
        /// This method notifies the original requester that their request has completed the 
        /// approval workflow successfully. The notification includes the final status and 
        /// any relevant comments from the approval process.
        /// </remarks>
        public void SendApprovalConfirmation(Guid requestId, Guid initiatorId)
        {
            string recipientEmail = string.Empty;
            bool success = false;

            try
            {
                // Load approval request details
                var request = LoadApprovalRequest(requestId);
                if (request == null)
                {
                    LogNotificationAttempt(requestId, "ApprovalConfirmation", string.Empty, false, 
                        "Approval request not found");
                    return;
                }

                // Get initiator email
                recipientEmail = GetUserEmail(initiatorId);
                if (string.IsNullOrWhiteSpace(recipientEmail))
                {
                    LogNotificationAttempt(requestId, "ApprovalConfirmation", string.Empty, false, 
                        $"Initiator email not found for user ID: {initiatorId}");
                    return;
                }

                // Build notification content
                var requestInfo = GetRequestDisplayInfo(request);
                var subject = BuildNotificationSubject("Approved", requestInfo);
                var body = BuildNotificationBody(request, "Approved", 
                    "Your approval request has been approved and the workflow has completed successfully.");

                // Queue email for delivery
                QueueNotificationEmail(recipientEmail, subject, body);
                success = true;
            }
            catch (Exception ex)
            {
                new Log().Create(LogType.Error, LOG_SOURCE, 
                    $"Failed to send approval confirmation for request {requestId}", ex);
            }
            finally
            {
                LogNotificationAttempt(requestId, "ApprovalConfirmation", recipientEmail, success);
            }
        }

        /// <summary>
        /// Sends a rejection notification to the request initiator when their approval request 
        /// has been rejected.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request.</param>
        /// <param name="initiatorId">The unique identifier of the user who initiated the request.</param>
        /// <param name="reason">The reason provided for the rejection.</param>
        /// <remarks>
        /// This method notifies the original requester that their request has been rejected.
        /// The notification includes the rejection reason and comments provided by the approver
        /// to help the requester understand why the request was not approved.
        /// </remarks>
        public void SendRejectionNotification(Guid requestId, Guid initiatorId, string reason)
        {
            string recipientEmail = string.Empty;
            bool success = false;

            try
            {
                // Load approval request details
                var request = LoadApprovalRequest(requestId);
                if (request == null)
                {
                    LogNotificationAttempt(requestId, "Rejection", string.Empty, false, 
                        "Approval request not found");
                    return;
                }

                // Get initiator email
                recipientEmail = GetUserEmail(initiatorId);
                if (string.IsNullOrWhiteSpace(recipientEmail))
                {
                    LogNotificationAttempt(requestId, "Rejection", string.Empty, false, 
                        $"Initiator email not found for user ID: {initiatorId}");
                    return;
                }

                // Build notification content with rejection reason
                var requestInfo = GetRequestDisplayInfo(request);
                var subject = BuildNotificationSubject("Rejected", requestInfo);
                var additionalInfo = string.IsNullOrWhiteSpace(reason) 
                    ? "Your approval request has been rejected." 
                    : $"Your approval request has been rejected. Reason: {reason}";
                var body = BuildNotificationBody(request, "Rejected", additionalInfo);

                // Queue email for delivery
                QueueNotificationEmail(recipientEmail, subject, body);
                success = true;
            }
            catch (Exception ex)
            {
                new Log().Create(LogType.Error, LOG_SOURCE, 
                    $"Failed to send rejection notification for request {requestId}", ex);
            }
            finally
            {
                LogNotificationAttempt(requestId, "Rejection", recipientEmail, success);
            }
        }

        /// <summary>
        /// Sends an escalation notification to a user when an approval request has been 
        /// escalated to them due to SLA breach or manual escalation.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request.</param>
        /// <param name="escalatedToId">The unique identifier of the user receiving the escalation.</param>
        /// <param name="reason">The reason for the escalation (e.g., SLA exceeded, manual escalation).</param>
        /// <remarks>
        /// This method notifies the escalation target user that they need to handle an 
        /// approval request that has been escalated. The notification includes the escalation
        /// reason and full request context to help them take appropriate action.
        /// 
        /// Escalations typically occur when the original approver does not act within the 
        /// defined SLA timeframe or when an approver manually escalates a request.
        /// </remarks>
        public void SendEscalationNotification(Guid requestId, Guid escalatedToId, string reason)
        {
            string recipientEmail = string.Empty;
            bool success = false;

            try
            {
                // Load approval request details
                var request = LoadApprovalRequest(requestId);
                if (request == null)
                {
                    LogNotificationAttempt(requestId, "Escalation", string.Empty, false, 
                        "Approval request not found");
                    return;
                }

                // Get escalation target email
                recipientEmail = GetUserEmail(escalatedToId);
                if (string.IsNullOrWhiteSpace(recipientEmail))
                {
                    LogNotificationAttempt(requestId, "Escalation", string.Empty, false, 
                        $"Escalation target email not found for user ID: {escalatedToId}");
                    return;
                }

                // Build notification content with escalation reason
                var requestInfo = GetRequestDisplayInfo(request);
                var subject = BuildNotificationSubject(ApprovalActionType.Escalate.ToString(), requestInfo);
                var additionalInfo = string.IsNullOrWhiteSpace(reason) 
                    ? "An approval request has been escalated to you and requires your immediate attention." 
                    : $"An approval request has been escalated to you. Reason: {reason}";
                var body = BuildNotificationBody(request, ApprovalActionType.Escalate.ToString(), additionalInfo);

                // Queue email for delivery
                QueueNotificationEmail(recipientEmail, subject, body);
                success = true;
            }
            catch (Exception ex)
            {
                new Log().Create(LogType.Error, LOG_SOURCE, 
                    $"Failed to send escalation notification for request {requestId}", ex);
            }
            finally
            {
                LogNotificationAttempt(requestId, "Escalation", recipientEmail, success);
            }
        }

        /// <summary>
        /// Sends a delegation notification to a user when an approval request has been 
        /// delegated to them by another approver.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request.</param>
        /// <param name="delegatedToId">The unique identifier of the user receiving the delegation.</param>
        /// <param name="delegatedById">The unique identifier of the user who delegated the request.</param>
        /// <remarks>
        /// This method notifies the delegation target user that they have been assigned 
        /// responsibility for an approval request. The notification includes information
        /// about who delegated the request and the full request context.
        /// 
        /// Delegations occur when an approver transfers their approval authority to 
        /// another user, typically due to absence or the need for specialized expertise.
        /// </remarks>
        public void SendDelegationNotification(Guid requestId, Guid delegatedToId, Guid delegatedById)
        {
            string recipientEmail = string.Empty;
            bool success = false;

            try
            {
                // Load approval request details
                var request = LoadApprovalRequest(requestId);
                if (request == null)
                {
                    LogNotificationAttempt(requestId, "Delegation", string.Empty, false, 
                        "Approval request not found");
                    return;
                }

                // Get delegation target email
                recipientEmail = GetUserEmail(delegatedToId);
                if (string.IsNullOrWhiteSpace(recipientEmail))
                {
                    LogNotificationAttempt(requestId, "Delegation", string.Empty, false, 
                        $"Delegation target email not found for user ID: {delegatedToId}");
                    return;
                }

                // Get delegator information for the notification
                var delegatorName = GetUserDisplayName(delegatedById);
                if (string.IsNullOrWhiteSpace(delegatorName))
                {
                    delegatorName = delegatedById.ToString();
                }

                // Build notification content with delegator info
                var requestInfo = GetRequestDisplayInfo(request);
                var subject = BuildNotificationSubject(ApprovalActionType.Delegate.ToString(), requestInfo);
                var additionalInfo = $"An approval request has been delegated to you by {delegatorName}. " +
                    "Please review and take appropriate action.";
                var body = BuildNotificationBody(request, ApprovalActionType.Delegate.ToString(), additionalInfo);

                // Queue email for delivery
                QueueNotificationEmail(recipientEmail, subject, body);
                success = true;
            }
            catch (Exception ex)
            {
                new Log().Create(LogType.Error, LOG_SOURCE, 
                    $"Failed to send delegation notification for request {requestId}", ex);
            }
            finally
            {
                LogNotificationAttempt(requestId, "Delegation", recipientEmail, success);
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Retrieves the email address for a specified user from the SecurityManager.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>The user's email address, or null if not found.</returns>
        private string GetUserEmail(Guid userId)
        {
            try
            {
                var user = SecMan.GetUser(userId);
                if (user != null)
                {
                    return user.Email;
                }

                // Fallback to EQL query if SecurityManager doesn't return the user
                var eqlCommand = new EqlCommand(
                    "SELECT email FROM user WHERE id = @userId",
                    new EqlParameter("userId", userId));

                var result = eqlCommand.Execute();
                if (result != null && result.Any())
                {
                    return result[0]["email"]?.ToString();
                }
            }
            catch (Exception ex)
            {
                new Log().Create(LogType.Error, LOG_SOURCE, 
                    $"Failed to retrieve email for user {userId}", ex);
            }

            return null;
        }

        /// <summary>
        /// Retrieves the display name for a specified user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>The user's display name (first + last name), username, or null if not found.</returns>
        private string GetUserDisplayName(Guid userId)
        {
            try
            {
                var user = SecMan.GetUser(userId);
                if (user != null)
                {
                    // Try to build a display name from first/last name
                    var firstName = !string.IsNullOrWhiteSpace(user.FirstName) ? user.FirstName : string.Empty;
                    var lastName = !string.IsNullOrWhiteSpace(user.LastName) ? user.LastName : string.Empty;
                    
                    var fullName = $"{firstName} {lastName}".Trim();
                    if (!string.IsNullOrWhiteSpace(fullName))
                    {
                        return fullName;
                    }

                    // Fall back to username
                    if (!string.IsNullOrWhiteSpace(user.Username))
                    {
                        return user.Username;
                    }

                    // Fall back to email
                    return user.Email;
                }
            }
            catch (Exception ex)
            {
                new Log().Create(LogType.Error, LOG_SOURCE, 
                    $"Failed to retrieve display name for user {userId}", ex);
            }

            return null;
        }

        /// <summary>
        /// Loads an approval request record by its ID using EQL.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request.</param>
        /// <returns>The approval request EntityRecord, or null if not found.</returns>
        private EntityRecord LoadApprovalRequest(Guid requestId)
        {
            try
            {
                var eqlCommand = new EqlCommand(
                    $"SELECT *,$approval_workflow_approval_request.name,$approval_step_approval_request.name " +
                    $"FROM {ENTITY_APPROVAL_REQUEST} WHERE id = @requestId",
                    new EqlParameter("requestId", requestId));

                var result = eqlCommand.Execute();
                if (result != null && result.Any())
                {
                    return result.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                new Log().Create(LogType.Error, LOG_SOURCE, 
                    $"Failed to load approval request {requestId}", ex);
            }

            return null;
        }

        /// <summary>
        /// Builds a standardized notification email subject line.
        /// </summary>
        /// <param name="actionType">The type of action (e.g., "Approve", "Reject", "Escalate").</param>
        /// <param name="requestInfo">Brief information about the request for context.</param>
        /// <returns>A formatted subject line for the notification email.</returns>
        private string BuildNotificationSubject(string actionType, string requestInfo)
        {
            var prefix = "[Approval Workflow]";
            
            switch (actionType?.ToLowerInvariant())
            {
                case "approve":
                case "pendingapproval":
                    return $"{prefix} Action Required: {requestInfo}";
                
                case "approved":
                    return $"{prefix} Approved: {requestInfo}";
                
                case "reject":
                case "rejected":
                    return $"{prefix} Rejected: {requestInfo}";
                
                case "delegate":
                    return $"{prefix} Delegated to You: {requestInfo}";
                
                case "escalate":
                    return $"{prefix} Escalated to You: {requestInfo}";
                
                default:
                    return $"{prefix} Notification: {requestInfo}";
            }
        }

        /// <summary>
        /// Builds the HTML body content for a notification email.
        /// </summary>
        /// <param name="request">The approval request EntityRecord containing request details.</param>
        /// <param name="actionType">The type of action for styling and content purposes.</param>
        /// <param name="additionalInfo">Additional context or message to include in the body.</param>
        /// <returns>An HTML-formatted email body string.</returns>
        private string BuildNotificationBody(EntityRecord request, string actionType, string additionalInfo)
        {
            var workflowName = GetRecordField(request, "$approval_workflow_approval_request", "name") 
                ?? GetRecordField(request, "workflow_id")?.ToString() 
                ?? "Unknown Workflow";
            
            var stepName = GetRecordField(request, "$approval_step_approval_request", "name") 
                ?? GetRecordField(request, "current_step_id")?.ToString() 
                ?? "Unknown Step";
            
            var entityName = GetRecordField(request, "entity_name")?.ToString() ?? "Unknown Entity";
            var recordId = GetRecordField(request, "record_id")?.ToString() ?? "Unknown";
            var status = GetRecordField(request, "status")?.ToString() ?? ApprovalStatus.Pending.ToString().ToLowerInvariant();
            var createdOn = GetRecordField(request, "created_on") as DateTime? ?? DateTime.UtcNow;
            var dueDate = GetRecordField(request, "due_date") as DateTime?;

            // Determine status color
            var statusColor = GetStatusColor(status);
            var actionColor = GetActionColor(actionType);

            var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Approval Workflow Notification</title>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background-color: {actionColor}; color: white; padding: 15px 20px; border-radius: 5px 5px 0 0;'>
        <h2 style='margin: 0; font-size: 18px;'>Approval Workflow Notification</h2>
    </div>
    
    <div style='background-color: #f9f9f9; padding: 20px; border: 1px solid #ddd; border-top: none;'>
        <p style='margin-top: 0;'>{additionalInfo}</p>
        
        <table style='width: 100%; border-collapse: collapse; margin-top: 15px;'>
            <tr>
                <td style='padding: 8px 0; border-bottom: 1px solid #eee; font-weight: bold; width: 140px;'>Workflow:</td>
                <td style='padding: 8px 0; border-bottom: 1px solid #eee;'>{System.Web.HttpUtility.HtmlEncode(workflowName)}</td>
            </tr>
            <tr>
                <td style='padding: 8px 0; border-bottom: 1px solid #eee; font-weight: bold;'>Current Step:</td>
                <td style='padding: 8px 0; border-bottom: 1px solid #eee;'>{System.Web.HttpUtility.HtmlEncode(stepName)}</td>
            </tr>
            <tr>
                <td style='padding: 8px 0; border-bottom: 1px solid #eee; font-weight: bold;'>Entity:</td>
                <td style='padding: 8px 0; border-bottom: 1px solid #eee;'>{System.Web.HttpUtility.HtmlEncode(entityName)}</td>
            </tr>
            <tr>
                <td style='padding: 8px 0; border-bottom: 1px solid #eee; font-weight: bold;'>Record ID:</td>
                <td style='padding: 8px 0; border-bottom: 1px solid #eee;'>{System.Web.HttpUtility.HtmlEncode(recordId)}</td>
            </tr>
            <tr>
                <td style='padding: 8px 0; border-bottom: 1px solid #eee; font-weight: bold;'>Status:</td>
                <td style='padding: 8px 0; border-bottom: 1px solid #eee;'>
                    <span style='background-color: {statusColor}; color: white; padding: 2px 8px; border-radius: 3px; font-size: 12px;'>
                        {System.Web.HttpUtility.HtmlEncode(status.ToUpperInvariant())}
                    </span>
                </td>
            </tr>
            <tr>
                <td style='padding: 8px 0; border-bottom: 1px solid #eee; font-weight: bold;'>Created On:</td>
                <td style='padding: 8px 0; border-bottom: 1px solid #eee;'>{createdOn:yyyy-MM-dd HH:mm:ss} UTC</td>
            </tr>
            {(dueDate.HasValue ? $@"
            <tr>
                <td style='padding: 8px 0; border-bottom: 1px solid #eee; font-weight: bold;'>Due Date:</td>
                <td style='padding: 8px 0; border-bottom: 1px solid #eee;'>{dueDate.Value:yyyy-MM-dd HH:mm:ss} UTC</td>
            </tr>" : "")}
        </table>
    </div>
    
    <div style='background-color: #f5f5f5; padding: 15px 20px; border: 1px solid #ddd; border-top: none; border-radius: 0 0 5px 5px; font-size: 12px; color: #666;'>
        <p style='margin: 0;'>This is an automated notification from the WebVella ERP Approval Workflow system.</p>
        <p style='margin: 5px 0 0 0;'>Please log in to the system to take action on this request.</p>
    </div>
</body>
</html>";

            return html;
        }

        /// <summary>
        /// Gets a display-friendly string for the approval request.
        /// </summary>
        /// <param name="request">The approval request EntityRecord.</param>
        /// <returns>A short descriptive string for the request.</returns>
        private string GetRequestDisplayInfo(EntityRecord request)
        {
            if (request == null)
            {
                return "Approval Request";
            }

            var workflowName = GetRecordField(request, "$approval_workflow_approval_request", "name")?.ToString();
            var entityName = GetRecordField(request, "entity_name")?.ToString();

            if (!string.IsNullOrWhiteSpace(workflowName))
            {
                return workflowName;
            }

            if (!string.IsNullOrWhiteSpace(entityName))
            {
                return $"{entityName} Approval";
            }

            return "Approval Request";
        }

        /// <summary>
        /// Safely retrieves a field value from an EntityRecord, supporting nested relation fields.
        /// </summary>
        /// <param name="record">The EntityRecord to retrieve the field from.</param>
        /// <param name="fieldName">The field name, or relation prefix for nested fields.</param>
        /// <param name="nestedFieldName">Optional nested field name when accessing related entity fields.</param>
        /// <returns>The field value, or null if not found.</returns>
        private object GetRecordField(EntityRecord record, string fieldName, string nestedFieldName = null)
        {
            if (record == null || string.IsNullOrWhiteSpace(fieldName))
            {
                return null;
            }

            try
            {
                // Handle nested relation fields
                if (!string.IsNullOrWhiteSpace(nestedFieldName))
                {
                    if (record.Properties.ContainsKey(fieldName))
                    {
                        var relatedRecords = record[fieldName] as List<EntityRecord>;
                        if (relatedRecords != null && relatedRecords.Any())
                        {
                            var relatedRecord = relatedRecords.FirstOrDefault();
                            if (relatedRecord != null && relatedRecord.Properties.ContainsKey(nestedFieldName))
                            {
                                return relatedRecord[nestedFieldName];
                            }
                        }
                    }
                    return null;
                }

                // Handle simple fields
                if (record.Properties.ContainsKey(fieldName))
                {
                    return record[fieldName];
                }
            }
            catch
            {
                // Silently handle any access issues
            }

            return null;
        }

        /// <summary>
        /// Gets the color code for a given approval status.
        /// </summary>
        /// <param name="status">The status string.</param>
        /// <returns>A hex color code for the status.</returns>
        private string GetStatusColor(string status)
        {
            switch (status?.ToLowerInvariant())
            {
                case "pending":
                    return "#f0ad4e"; // Orange/Warning
                
                case "approved":
                    return "#5cb85c"; // Green/Success
                
                case "rejected":
                    return "#d9534f"; // Red/Danger
                
                case "delegated":
                    return "#5bc0de"; // Light Blue/Info
                
                case "escalated":
                    return "#d9534f"; // Red/Danger
                
                case "cancelled":
                    return "#777777"; // Gray
                
                default:
                    return "#337ab7"; // Default Blue
            }
        }

        /// <summary>
        /// Gets the header color for a given action type.
        /// </summary>
        /// <param name="actionType">The action type string.</param>
        /// <returns>A hex color code for the action header.</returns>
        private string GetActionColor(string actionType)
        {
            switch (actionType?.ToLowerInvariant())
            {
                case "approve":
                case "pendingapproval":
                    return "#337ab7"; // Blue for pending action
                
                case "approved":
                    return "#5cb85c"; // Green for approved
                
                case "reject":
                case "rejected":
                    return "#d9534f"; // Red for rejected
                
                case "delegate":
                    return "#5bc0de"; // Light Blue for delegated
                
                case "escalate":
                    return "#f0ad4e"; // Orange for escalated
                
                default:
                    return "#337ab7"; // Default Blue
            }
        }

        /// <summary>
        /// Logs a notification attempt to the system log for audit and debugging purposes.
        /// </summary>
        /// <param name="requestId">The approval request ID.</param>
        /// <param name="notificationType">The type of notification (e.g., "PendingApproval", "Rejection").</param>
        /// <param name="recipientEmail">The recipient's email address.</param>
        /// <param name="success">Whether the notification was successfully queued.</param>
        /// <param name="errorMessage">Optional error message if the notification failed.</param>
        private void LogNotificationAttempt(Guid requestId, string notificationType, string recipientEmail, 
            bool success, string errorMessage = null)
        {
            try
            {
                var log = new Log();
                var logType = success ? LogType.Info : LogType.Error;
                var message = success 
                    ? $"Notification '{notificationType}' queued successfully for request {requestId} to {recipientEmail}"
                    : $"Notification '{notificationType}' failed for request {requestId}. {errorMessage}";

                log.Create(logType, LOG_SOURCE, message, string.Empty);
            }
            catch
            {
                // Silently ignore logging failures to prevent cascade errors
            }
        }

        /// <summary>
        /// Queues an email for delivery through the WebVella Mail plugin.
        /// Uses the default SMTP service configured in the system.
        /// </summary>
        /// <param name="recipientEmail">The recipient's email address.</param>
        /// <param name="subject">The email subject.</param>
        /// <param name="htmlBody">The HTML body content.</param>
        /// <remarks>
        /// This method attempts to use the WebVella Mail plugin's SMTP service infrastructure.
        /// If the Mail plugin is not configured or unavailable, it logs the notification
        /// attempt but does not throw an exception to prevent workflow disruption.
        /// 
        /// The email is queued for asynchronous delivery, not sent immediately.
        /// </remarks>
        private void QueueNotificationEmail(string recipientEmail, string subject, string htmlBody)
        {
            try
            {
                // Attempt to get the default SMTP service from the Mail plugin
                // The Mail plugin provides EmailServiceManager for this purpose
                var emailServiceManagerType = Type.GetType(
                    "WebVella.Erp.Plugins.Mail.Api.EmailServiceManager, WebVella.Erp.Plugins.Mail");
                
                if (emailServiceManagerType == null)
                {
                    new Log().Create(LogType.Info, LOG_SOURCE, 
                        $"Mail plugin not available. Email notification to {recipientEmail} logged but not sent.", 
                        $"Subject: {subject}");
                    return;
                }

                // Create EmailServiceManager instance
                var emailServiceManager = Activator.CreateInstance(emailServiceManagerType);
                
                // Get the GetSmtpService method (returns default service when called with no params)
                var getSmtpServiceMethod = emailServiceManagerType.GetMethod("GetSmtpService", 
                    new Type[] { typeof(string) });
                
                if (getSmtpServiceMethod == null)
                {
                    new Log().Create(LogType.Error, LOG_SOURCE, 
                        "Could not find GetSmtpService method on EmailServiceManager", string.Empty);
                    return;
                }

                // Get the default SMTP service (null parameter returns default)
                var smtpService = getSmtpServiceMethod.Invoke(emailServiceManager, new object[] { null });
                
                if (smtpService == null)
                {
                    new Log().Create(LogType.Error, LOG_SOURCE, 
                        "No default SMTP service configured. Email notification not sent.", 
                        $"Subject: {subject}, Recipient: {recipientEmail}");
                    return;
                }

                // Get the EmailAddress type from the Mail plugin
                var emailAddressType = Type.GetType(
                    "WebVella.Erp.Plugins.Mail.Api.EmailAddress, WebVella.Erp.Plugins.Mail");
                
                if (emailAddressType == null)
                {
                    new Log().Create(LogType.Error, LOG_SOURCE, 
                        "Could not find EmailAddress type in Mail plugin", string.Empty);
                    return;
                }

                // Create recipient EmailAddress
                var recipient = Activator.CreateInstance(emailAddressType);
                var addressProperty = emailAddressType.GetProperty("Address");
                addressProperty?.SetValue(recipient, recipientEmail);

                // Get the QueueEmail method
                var queueEmailMethod = smtpService.GetType().GetMethod("QueueEmail", 
                    new Type[] { emailAddressType, typeof(string), typeof(string), typeof(string), 
                        Type.GetType("WebVella.Erp.Plugins.Mail.Api.EmailPriority, WebVella.Erp.Plugins.Mail"),
                        typeof(List<string>) });

                if (queueEmailMethod != null)
                {
                    // Get EmailPriority.Normal
                    var emailPriorityType = Type.GetType(
                        "WebVella.Erp.Plugins.Mail.Api.EmailPriority, WebVella.Erp.Plugins.Mail");
                    var normalPriority = Enum.Parse(emailPriorityType, "Normal");

                    // Create plain text version from HTML (basic strip)
                    var plainTextBody = StripHtmlTags(htmlBody);

                    // Queue the email
                    queueEmailMethod.Invoke(smtpService, new object[] { 
                        recipient, subject, plainTextBody, htmlBody, normalPriority, null });
                }
                else
                {
                    new Log().Create(LogType.Error, LOG_SOURCE, 
                        "Could not find QueueEmail method on SmtpService", string.Empty);
                }
            }
            catch (Exception ex)
            {
                // Log but don't throw - email delivery should not break the workflow
                new Log().Create(LogType.Error, LOG_SOURCE, 
                    $"Failed to queue notification email to {recipientEmail}", ex);
            }
        }

        /// <summary>
        /// Strips HTML tags from a string to create a plain text version.
        /// </summary>
        /// <param name="html">The HTML string to process.</param>
        /// <returns>A plain text version of the content.</returns>
        private string StripHtmlTags(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            // Simple regex-free approach for basic HTML stripping
            var text = html;
            
            // Remove style and script blocks
            while (text.Contains("<style"))
            {
                var start = text.IndexOf("<style", StringComparison.OrdinalIgnoreCase);
                var end = text.IndexOf("</style>", start, StringComparison.OrdinalIgnoreCase);
                if (end > start)
                {
                    text = text.Remove(start, end - start + 8);
                }
                else
                {
                    break;
                }
            }

            while (text.Contains("<script"))
            {
                var start = text.IndexOf("<script", StringComparison.OrdinalIgnoreCase);
                var end = text.IndexOf("</script>", start, StringComparison.OrdinalIgnoreCase);
                if (end > start)
                {
                    text = text.Remove(start, end - start + 9);
                }
                else
                {
                    break;
                }
            }

            // Replace common block elements with newlines
            text = text.Replace("</p>", "\n\n");
            text = text.Replace("</div>", "\n");
            text = text.Replace("</tr>", "\n");
            text = text.Replace("<br>", "\n");
            text = text.Replace("<br/>", "\n");
            text = text.Replace("<br />", "\n");

            // Remove all remaining HTML tags
            while (text.Contains("<"))
            {
                var start = text.IndexOf('<');
                var end = text.IndexOf('>', start);
                if (end > start)
                {
                    text = text.Remove(start, end - start + 1);
                }
                else
                {
                    break;
                }
            }

            // Decode common HTML entities
            text = System.Web.HttpUtility.HtmlDecode(text);

            // Normalize whitespace
            while (text.Contains("  "))
            {
                text = text.Replace("  ", " ");
            }

            while (text.Contains("\n\n\n"))
            {
                text = text.Replace("\n\n\n", "\n\n");
            }

            return text.Trim();
        }

        #endregion
    }
}
