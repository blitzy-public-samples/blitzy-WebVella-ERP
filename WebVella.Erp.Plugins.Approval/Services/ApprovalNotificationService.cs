using System;
using System.Collections.Generic;
using System.Linq;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Eql;
using WebVella.Erp.Plugins.Approval.Api;

namespace WebVella.Erp.Plugins.Approval.Services
{
	/// <summary>
	/// Service for composing and managing email notifications for the Approval plugin.
	/// Provides methods for sending approval-related notifications using the WebVella.Erp.Plugins.Mail infrastructure.
	/// Handles notification composition, queuing, and delivery tracking for all approval workflow events.
	/// </summary>
	/// <remarks>
	/// This service manages the following notification types:
	/// - Approval request notifications (notifying approvers of new requests)
	/// - Approval completed notifications (notifying requesters of completed approvals)
	/// - Escalation notifications (alerting next approvers when escalation occurs)
	/// 
	/// The service uses the approval_notification entity for queue management, allowing
	/// background jobs to process notifications in batches and track delivery status.
	/// </remarks>
	public class ApprovalNotificationService
	{
		#region Constants

		/// <summary>
		/// Entity name for approval notification queue records.
		/// </summary>
		private const string NOTIFICATION_ENTITY_NAME = "approval_notification";

		/// <summary>
		/// Entity name for the Mail plugin's email queue.
		/// Used for direct email integration with WebVella.Erp.Plugins.Mail.
		/// </summary>
		private const string EMAIL_ENTITY_NAME = "email";

		/// <summary>
		/// Entity name for approval request records.
		/// </summary>
		private const string REQUEST_ENTITY_NAME = "approval_request";

		/// <summary>
		/// Entity name for user records.
		/// </summary>
		private const string USER_ENTITY_NAME = "user";

		/// <summary>
		/// Notification type for new approval request.
		/// </summary>
		private const string NOTIFICATION_TYPE_REQUEST = "request";

		/// <summary>
		/// Notification type for approval completion.
		/// </summary>
		private const string NOTIFICATION_TYPE_COMPLETED = "completed";

		/// <summary>
		/// Notification type for escalation alert.
		/// </summary>
		private const string NOTIFICATION_TYPE_ESCALATION = "escalation";

		/// <summary>
		/// Notification status for pending delivery.
		/// </summary>
		private const string STATUS_PENDING = "pending";

		/// <summary>
		/// Notification status for sent/delivered.
		/// </summary>
		private const string STATUS_SENT = "sent";

		/// <summary>
		/// Notification status for failed delivery.
		/// </summary>
		private const string STATUS_FAILED = "failed";

		/// <summary>
		/// Normal email priority for the Mail plugin.
		/// </summary>
		private const int PRIORITY_NORMAL = 3;

		#endregion

		#region Properties

		/// <summary>
		/// Record manager for CRUD operations on notification records and querying approval_request/user entities.
		/// Initialized inline following WebVella service pattern.
		/// </summary>
		protected RecordManager RecMan { get; private set; } = new RecordManager();

		/// <summary>
		/// Security manager for user lookups to retrieve approver email addresses.
		/// </summary>
		protected SecurityManager SecMan { get; private set; } = new SecurityManager();

		#endregion

		#region Public Methods

		/// <summary>
		/// Sends an approval request notification to the specified approver.
		/// Creates a notification record in the queue and composes the notification content
		/// based on the approval request details.
		/// </summary>
		/// <param name="requestId">The unique identifier of the approval request.</param>
		/// <param name="approverId">The unique identifier of the approver user to notify.</param>
		/// <returns>The unique identifier of the created notification record.</returns>
		/// <exception cref="Exception">Thrown when the request or approver cannot be found, or notification creation fails.</exception>
		/// <remarks>
		/// This method:
		/// 1. Loads request details via EQL query
		/// 2. Loads approver email address using SecurityManager
		/// 3. Creates a notification record with composed subject and body
		/// 4. The notification will be processed by the ProcessApprovalNotificationsJob background job
		/// </remarks>
		public Guid SendApprovalRequestNotification(Guid requestId, Guid approverId)
		{
			if (requestId == Guid.Empty)
			{
				throw new ArgumentException("Request ID cannot be empty.", nameof(requestId));
			}

			if (approverId == Guid.Empty)
			{
				throw new ArgumentException("Approver ID cannot be empty.", nameof(approverId));
			}

			// Load request details via EQL
			var requestRecord = LoadRequestDetails(requestId);
			if (requestRecord == null)
			{
				throw new Exception($"Approval request with ID '{requestId}' not found.");
			}

			// Load approver user details
			var approverUser = SecMan.GetUser(approverId);
			if (approverUser == null)
			{
				throw new Exception($"Approver user with ID '{approverId}' not found.");
			}

			// Extract request fields for notification content
			var sourceEntityName = requestRecord["source_entity"]?.ToString() ?? "Unknown Entity";
			var sourceRecordId = requestRecord["source_record_id"] != null 
				? (Guid)requestRecord["source_record_id"] 
				: Guid.Empty;
			var requestedOn = requestRecord["requested_on"] != null 
				? (DateTime)requestRecord["requested_on"] 
				: DateTime.UtcNow;

			// Compose notification content
			var subject = $"Approval Required: {sourceEntityName} Request";
			var body = ComposeRequestNotificationBody(sourceEntityName, sourceRecordId, requestedOn);

			// Get approver email
			var approverEmail = GetUserEmail(approverUser);

			// Create notification record
			return CreateNotificationRecord(
				requestId: requestId,
				recipientUserId: approverId,
				recipientEmail: approverEmail,
				notificationType: NOTIFICATION_TYPE_REQUEST,
				subject: subject,
				body: body
			);
		}

		/// <summary>
		/// Sends an approval completed notification to the requester.
		/// Notifies the user who originally submitted the approval request about the final outcome.
		/// </summary>
		/// <param name="requestId">The unique identifier of the completed approval request.</param>
		/// <param name="status">The final status of the request (e.g., "approved", "rejected", "expired").</param>
		/// <returns>The unique identifier of the created notification record.</returns>
		/// <exception cref="Exception">Thrown when the request or requester cannot be found, or notification creation fails.</exception>
		/// <remarks>
		/// This method is called when an approval request reaches a terminal state (approved, rejected, or expired).
		/// The notification informs the original requester about the outcome of their request.
		/// </remarks>
		public Guid SendApprovalCompletedNotification(Guid requestId, string status)
		{
			if (requestId == Guid.Empty)
			{
				throw new ArgumentException("Request ID cannot be empty.", nameof(requestId));
			}

			if (string.IsNullOrWhiteSpace(status))
			{
				throw new ArgumentException("Status cannot be null or empty.", nameof(status));
			}

			// Load request details via EQL
			var requestRecord = LoadRequestDetails(requestId);
			if (requestRecord == null)
			{
				throw new Exception($"Approval request with ID '{requestId}' not found.");
			}

			// Get requester user ID
			var requestedBy = requestRecord["requested_by"] != null 
				? (Guid)requestRecord["requested_by"] 
				: Guid.Empty;

			if (requestedBy == Guid.Empty)
			{
				throw new Exception($"Requester not found for approval request '{requestId}'.");
			}

			// Load requester user details
			var requesterUser = SecMan.GetUser(requestedBy);
			if (requesterUser == null)
			{
				throw new Exception($"Requester user with ID '{requestedBy}' not found.");
			}

			// Extract request fields for notification content
			var sourceEntityName = requestRecord["source_entity"]?.ToString() ?? "Unknown Entity";
			var sourceRecordId = requestRecord["source_record_id"] != null 
				? (Guid)requestRecord["source_record_id"] 
				: Guid.Empty;

			// Compose notification content based on status
			var statusDisplay = FormatStatusForDisplay(status);
			var subject = $"Approval {statusDisplay}: {sourceEntityName} Request";
			var body = ComposeCompletedNotificationBody(sourceEntityName, sourceRecordId, status);

			// Get requester email
			var requesterEmail = GetUserEmail(requesterUser);

			// Create notification record
			return CreateNotificationRecord(
				requestId: requestId,
				recipientUserId: requestedBy,
				recipientEmail: requesterEmail,
				notificationType: NOTIFICATION_TYPE_COMPLETED,
				subject: subject,
				body: body
			);
		}

		/// <summary>
		/// Sends an escalation notification to the next approver in the workflow.
		/// Used when an approval request is escalated due to timeout or manual escalation action.
		/// </summary>
		/// <param name="requestId">The unique identifier of the escalated approval request.</param>
		/// <param name="nextApproverId">The unique identifier of the next approver user to notify.</param>
		/// <returns>The unique identifier of the created notification record.</returns>
		/// <exception cref="Exception">Thrown when the request or next approver cannot be found, or notification creation fails.</exception>
		/// <remarks>
		/// Escalation notifications have higher urgency indicators to ensure timely attention from the next approver.
		/// This method is typically called by the ProcessApprovalEscalationsJob background job.
		/// </remarks>
		public Guid SendEscalationNotification(Guid requestId, Guid nextApproverId)
		{
			if (requestId == Guid.Empty)
			{
				throw new ArgumentException("Request ID cannot be empty.", nameof(requestId));
			}

			if (nextApproverId == Guid.Empty)
			{
				throw new ArgumentException("Next approver ID cannot be empty.", nameof(nextApproverId));
			}

			// Load request details via EQL
			var requestRecord = LoadRequestDetails(requestId);
			if (requestRecord == null)
			{
				throw new Exception($"Approval request with ID '{requestId}' not found.");
			}

			// Load next approver user details
			var nextApproverUser = SecMan.GetUser(nextApproverId);
			if (nextApproverUser == null)
			{
				throw new Exception($"Next approver user with ID '{nextApproverId}' not found.");
			}

			// Extract request fields for notification content
			var sourceEntityName = requestRecord["source_entity"]?.ToString() ?? "Unknown Entity";
			var sourceRecordId = requestRecord["source_record_id"] != null 
				? (Guid)requestRecord["source_record_id"] 
				: Guid.Empty;
			var requestedOn = requestRecord["requested_on"] != null 
				? (DateTime)requestRecord["requested_on"] 
				: DateTime.UtcNow;

			// Compose escalation notification content with urgency indicators
			var subject = $"[URGENT] Escalated Approval Required: {sourceEntityName} Request";
			var body = ComposeEscalationNotificationBody(sourceEntityName, sourceRecordId, requestedOn);

			// Get next approver email
			var nextApproverEmail = GetUserEmail(nextApproverUser);

			// Create notification record
			return CreateNotificationRecord(
				requestId: requestId,
				recipientUserId: nextApproverId,
				recipientEmail: nextApproverEmail,
				notificationType: NOTIFICATION_TYPE_ESCALATION,
				subject: subject,
				body: body
			);
		}

		/// <summary>
		/// Retrieves all pending notification records for batch processing by background jobs.
		/// Returns notifications that have not yet been sent and are ready for delivery.
		/// </summary>
		/// <returns>A list of pending notification EntityRecords ordered by creation date.</returns>
		/// <remarks>
		/// This method is typically called by the ProcessApprovalNotificationsJob background job
		/// to retrieve all notifications that need to be processed. Each notification record contains:
		/// - id: Unique identifier
		/// - request_id: Related approval request
		/// - recipient_user_id: User to receive the notification
		/// - recipient_email: Email address for delivery
		/// - notification_type: Type of notification (request, completed, escalation)
		/// - subject: Email subject line
		/// - body: Email body content
		/// - status: Current status (pending, sent, failed)
		/// - created_on: Timestamp of creation
		/// </remarks>
		public List<EntityRecord> GetPendingNotifications()
		{
			try
			{
				var eqlCommand = @"SELECT * FROM approval_notification 
                                   WHERE status = @status 
                                   ORDER BY created_on ASC";

				var eqlParams = new List<EqlParameter>
				{
					new EqlParameter("status", STATUS_PENDING)
				};

				var result = new EqlCommand(eqlCommand, eqlParams).Execute();

				if (result == null || !result.Any())
				{
					return new List<EntityRecord>();
				}

				return result.ToList();
			}
			catch (Exception ex)
			{
				// Log the error and return empty list to allow job to continue
				System.Diagnostics.Debug.WriteLine($"Error retrieving pending notifications: {ex.Message}");
				return new List<EntityRecord>();
			}
		}

		/// <summary>
		/// Marks a notification as sent after successful delivery.
		/// Updates the notification status to prevent duplicate sends and records the delivery timestamp.
		/// </summary>
		/// <param name="notificationId">The unique identifier of the notification to mark as sent.</param>
		/// <exception cref="Exception">Thrown when the notification cannot be found or update fails.</exception>
		/// <remarks>
		/// This method should be called after successful email delivery to update the notification status.
		/// It records the sent_on timestamp for audit and tracking purposes.
		/// </remarks>
		public void MarkNotificationSent(Guid notificationId)
		{
			if (notificationId == Guid.Empty)
			{
				throw new ArgumentException("Notification ID cannot be empty.", nameof(notificationId));
			}

			try
			{
				var updateRecord = new EntityRecord();
				updateRecord["id"] = notificationId;
				updateRecord["status"] = STATUS_SENT;
				updateRecord["sent_on"] = DateTime.UtcNow;

				var response = RecMan.UpdateRecord(NOTIFICATION_ENTITY_NAME, updateRecord);
				if (!response.Success)
				{
					throw new Exception($"Failed to mark notification as sent: {response.Message}");
				}
			}
			catch (Exception ex)
			{
				throw new Exception($"Error marking notification '{notificationId}' as sent: {ex.Message}", ex);
			}
		}

		/// <summary>
		/// Marks a notification as failed after delivery failure.
		/// Updates the notification status and records the error message for troubleshooting.
		/// </summary>
		/// <param name="notificationId">The unique identifier of the notification that failed.</param>
		/// <param name="errorMessage">The error message describing the failure reason.</param>
		/// <exception cref="Exception">Thrown when the notification cannot be found or update fails.</exception>
		public void MarkNotificationFailed(Guid notificationId, string errorMessage)
		{
			if (notificationId == Guid.Empty)
			{
				throw new ArgumentException("Notification ID cannot be empty.", nameof(notificationId));
			}

			try
			{
				var updateRecord = new EntityRecord();
				updateRecord["id"] = notificationId;
				updateRecord["status"] = STATUS_FAILED;
				updateRecord["error_message"] = errorMessage ?? "Unknown error";
				updateRecord["sent_on"] = DateTime.UtcNow;

				var response = RecMan.UpdateRecord(NOTIFICATION_ENTITY_NAME, updateRecord);
				if (!response.Success)
				{
					throw new Exception($"Failed to mark notification as failed: {response.Message}");
				}
			}
			catch (Exception ex)
			{
				throw new Exception($"Error marking notification '{notificationId}' as failed: {ex.Message}", ex);
			}
		}

		#endregion

		#region Private Methods

		/// <summary>
		/// Loads approval request details by ID using EQL query.
		/// </summary>
		/// <param name="requestId">The unique identifier of the approval request.</param>
		/// <returns>The EntityRecord containing request details, or null if not found.</returns>
		private EntityRecord LoadRequestDetails(Guid requestId)
		{
			try
			{
				var eqlCommand = @"SELECT id, workflow_id, current_step_id, source_entity, 
                                   source_record_id, status, requested_by, requested_on, completed_on 
                                   FROM approval_request 
                                   WHERE id = @id";

				var eqlParams = new List<EqlParameter>
				{
					new EqlParameter("id", requestId)
				};

				var result = new EqlCommand(eqlCommand, eqlParams).Execute();

				if (result == null || !result.Any())
				{
					return null;
				}

				return result.FirstOrDefault();
			}
			catch (Exception ex)
			{
				throw new Exception($"Error loading request details for ID '{requestId}': {ex.Message}", ex);
			}
		}

		/// <summary>
		/// Creates a notification record by sending an email through the Mail plugin.
		/// Uses the Mail plugin's email entity for direct email integration.
		/// </summary>
		/// <param name="requestId">The related approval request ID.</param>
		/// <param name="recipientUserId">The recipient user ID.</param>
		/// <param name="recipientEmail">The recipient email address.</param>
		/// <param name="notificationType">The type of notification.</param>
		/// <param name="subject">The notification subject.</param>
		/// <param name="body">The notification body content.</param>
		/// <returns>The unique identifier of the created email record.</returns>
		private Guid CreateNotificationRecord(
			Guid requestId,
			Guid recipientUserId,
			string recipientEmail,
			string notificationType,
			string subject,
			string body)
		{
			try
			{
				var emailId = Guid.NewGuid();

				// Get the default SMTP service ID
				var serviceId = GetDefaultSmtpServiceId();
				if (serviceId == Guid.Empty)
				{
					throw new Exception("No default SMTP service configured. Please configure an SMTP service in the Mail plugin settings.");
				}

				// Format recipients as JSON array as required by Mail plugin
				var recipientsJson = $"[{{\n  \"email\": \"{recipientEmail}\"\n}}]";

				// Convert plain text body to HTML
				var htmlBody = $"<html><body><pre style=\"font-family: Arial, sans-serif; white-space: pre-wrap;\">{System.Net.WebUtility.HtmlEncode(body)}</pre></body></html>";

				var record = new EntityRecord();
				record["id"] = emailId;
				record["service_id"] = serviceId;
				record["recipients"] = recipientsJson;
				record["subject"] = subject;
				record["content_html"] = htmlBody;
				record["status"] = STATUS_PENDING;
				record["scheduled_on"] = DateTime.UtcNow;
				record["priority"] = PRIORITY_NORMAL;

				var response = RecMan.CreateRecord(EMAIL_ENTITY_NAME, record);
				if (!response.Success)
				{
					throw new Exception($"Failed to create email record: {response.Message}");
				}

				return emailId;
			}
			catch (Exception ex)
			{
				throw new Exception($"Error creating email notification record: {ex.Message}", ex);
			}
		}

		/// <summary>
		/// Gets the default SMTP service ID from the Mail plugin configuration.
		/// </summary>
		/// <returns>The default SMTP service ID, or Guid.Empty if not configured.</returns>
		private Guid GetDefaultSmtpServiceId()
		{
			try
			{
				var eqlCommand = "SELECT id FROM smtp_service WHERE is_default = @is_default";
				var eqlParams = new List<EqlParameter>
				{
					new EqlParameter("is_default", true)
				};

				var result = new EqlCommand(eqlCommand, eqlParams).Execute();

				if (result == null || !result.Any())
				{
					return Guid.Empty;
				}

				return (Guid)result[0]["id"];
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error retrieving default SMTP service: {ex.Message}");
				return Guid.Empty;
			}
		}

		/// <summary>
		/// Gets the email address from a user record.
		/// </summary>
		/// <param name="user">The user record.</param>
		/// <returns>The email address, or empty string if not found.</returns>
		private string GetUserEmail(ErpUser user)
		{
			if (user == null)
			{
				return string.Empty;
			}

			return user.Email ?? string.Empty;
		}

		/// <summary>
		/// Composes the body content for a new approval request notification.
		/// </summary>
		/// <param name="sourceEntityName">The name of the source entity.</param>
		/// <param name="sourceRecordId">The ID of the source record.</param>
		/// <param name="requestedOn">The timestamp when the request was submitted.</param>
		/// <returns>The composed notification body.</returns>
		private string ComposeRequestNotificationBody(
			string sourceEntityName,
			Guid sourceRecordId,
			DateTime requestedOn)
		{
			return $@"A new approval request requires your attention.

Entity Type: {sourceEntityName}
Record ID: {sourceRecordId}
Submitted On: {requestedOn:yyyy-MM-dd HH:mm:ss} UTC

Please review and take appropriate action (approve, reject, or delegate) at your earliest convenience.

This is an automated notification from the Approval Workflow System.";
		}

		/// <summary>
		/// Composes the body content for an approval completed notification.
		/// </summary>
		/// <param name="sourceEntityName">The name of the source entity.</param>
		/// <param name="sourceRecordId">The ID of the source record.</param>
		/// <param name="status">The final status of the approval request.</param>
		/// <returns>The composed notification body.</returns>
		private string ComposeCompletedNotificationBody(
			string sourceEntityName,
			Guid sourceRecordId,
			string status)
		{
			var statusDisplay = FormatStatusForDisplay(status);
			var statusMessage = GetStatusMessage(status);

			return $@"Your approval request has been {statusDisplay.ToLowerInvariant()}.

Entity Type: {sourceEntityName}
Record ID: {sourceRecordId}
Final Status: {statusDisplay}

{statusMessage}

This is an automated notification from the Approval Workflow System.";
		}

		/// <summary>
		/// Composes the body content for an escalation notification.
		/// </summary>
		/// <param name="sourceEntityName">The name of the source entity.</param>
		/// <param name="sourceRecordId">The ID of the source record.</param>
		/// <param name="requestedOn">The timestamp when the request was originally submitted.</param>
		/// <returns>The composed notification body with urgency indicators.</returns>
		private string ComposeEscalationNotificationBody(
			string sourceEntityName,
			Guid sourceRecordId,
			DateTime requestedOn)
		{
			var pendingDuration = DateTime.UtcNow - requestedOn;
			var daysWaiting = Math.Round(pendingDuration.TotalDays, 1);

			return $@"*** URGENT: ESCALATED APPROVAL REQUEST ***

An approval request has been escalated to you and requires immediate attention.

Entity Type: {sourceEntityName}
Record ID: {sourceRecordId}
Originally Submitted: {requestedOn:yyyy-MM-dd HH:mm:ss} UTC
Waiting Period: {daysWaiting} days

This request has exceeded the normal approval timeframe and has been escalated for urgent review.

Please review and take appropriate action (approve, reject, or delegate) as soon as possible.

This is an automated notification from the Approval Workflow System.";
		}

		/// <summary>
		/// Formats a status string for display in notifications.
		/// </summary>
		/// <param name="status">The raw status string.</param>
		/// <returns>A formatted, user-friendly status string.</returns>
		private string FormatStatusForDisplay(string status)
		{
			if (string.IsNullOrWhiteSpace(status))
			{
				return "Unknown";
			}

			switch (status.ToLowerInvariant())
			{
				case "pending":
					return "Pending";
				case "approved":
					return "Approved";
				case "rejected":
					return "Rejected";
				case "escalated":
					return "Escalated";
				case "expired":
					return "Expired";
				default:
					return char.ToUpperInvariant(status[0]) + status.Substring(1).ToLowerInvariant();
			}
		}

		/// <summary>
		/// Gets a status-specific message for completed notifications.
		/// </summary>
		/// <param name="status">The approval status.</param>
		/// <returns>A contextual message based on the status.</returns>
		private string GetStatusMessage(string status)
		{
			switch (status.ToLowerInvariant())
			{
				case "approved":
					return "Your request has been approved and is ready to proceed. No further action is required from you.";
				case "rejected":
					return "Your request has been rejected. Please review the feedback and contact the approver if you have questions.";
				case "expired":
					return "Your request has expired without receiving approval within the required timeframe. You may need to resubmit your request.";
				case "escalated":
					return "Your request has been escalated to a higher authority for review.";
				default:
					return "Please check the system for more details about the status of your request.";
			}
		}

		#endregion
	}
}
