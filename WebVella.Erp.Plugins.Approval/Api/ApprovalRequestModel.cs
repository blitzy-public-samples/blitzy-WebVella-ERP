using System;
using Newtonsoft.Json;
using WebVella.Erp.Api.Models;

namespace WebVella.Erp.Plugins.Approval.Api
{
	/// <summary>
	/// Enumeration representing the possible states of an approval request throughout its lifecycle.
	/// Used by the approval workflow system to track the progression of requests through the approval process.
	/// </summary>
	public enum ApprovalRequestStatus
	{
		/// <summary>
		/// Request is awaiting approval action. This is the initial state when a request is submitted
		/// and remains active until an approver takes action (approve, reject, delegate) or the request
		/// times out/expires.
		/// </summary>
		[SelectOption(Label = "pending")]
		Pending = 0,

		/// <summary>
		/// Request has been approved by all required approvers. This is a terminal state indicating
		/// successful completion of the approval workflow. The associated record can proceed with
		/// its intended action.
		/// </summary>
		[SelectOption(Label = "approved")]
		Approved = 1,

		/// <summary>
		/// Request has been rejected by an approver. This is a terminal state indicating the approval
		/// workflow was terminated with a negative outcome. The associated record should not proceed
		/// with its intended action.
		/// </summary>
		[SelectOption(Label = "rejected")]
		Rejected = 2,

		/// <summary>
		/// Request has been escalated to a higher authority due to timeout or manual escalation.
		/// This state indicates the request requires attention from escalation handlers and may
		/// transition to pending once reassigned.
		/// </summary>
		[SelectOption(Label = "escalated")]
		Escalated = 3,

		/// <summary>
		/// Request has expired without action being taken within the configured time limit.
		/// This is a terminal state indicating the approval workflow was not completed in time.
		/// Cleanup jobs may archive or handle expired requests according to business rules.
		/// </summary>
		[SelectOption(Label = "expired")]
		Expired = 4
	}

	/// <summary>
	/// Entity DTO representing an approval request instance in the workflow system.
	/// This model captures all information about a single approval request including its current state,
	/// relationship to the workflow and step configuration, and the source entity that triggered the request.
	/// Used as the core DTO for ApprovalRequestService lifecycle operations (create, approve, reject, delegate).
	/// </summary>
	public class ApprovalRequestModel
	{
		/// <summary>
		/// Unique identifier for this approval request.
		/// Auto-generated when the request is created.
		/// </summary>
		[JsonProperty(PropertyName = "id")]
		public Guid Id { get; set; }

		/// <summary>
		/// Foreign key reference to the approval_workflow entity.
		/// Identifies which workflow definition governs this request's approval process.
		/// </summary>
		[JsonProperty(PropertyName = "workflow_id")]
		public Guid WorkflowId { get; set; }

		/// <summary>
		/// Foreign key reference to the current approval_step entity.
		/// Nullable because the request may be in a terminal state (approved, rejected, expired)
		/// where no current step applies, or may be newly created and not yet assigned to a step.
		/// </summary>
		[JsonProperty(PropertyName = "current_step_id")]
		public Guid? CurrentStepId { get; set; }

		/// <summary>
		/// Name of the entity that triggered this approval request.
		/// Examples: "purchase_order", "expense_request".
		/// Used to navigate back to the source record for context during approval.
		/// </summary>
		[JsonProperty(PropertyName = "source_entity")]
		public string SourceEntityName { get; set; }

		/// <summary>
		/// Unique identifier of the source record that triggered this approval request.
		/// Combined with SourceEntityName, provides a complete reference to retrieve the
		/// original record requiring approval.
		/// </summary>
		[JsonProperty(PropertyName = "source_record_id")]
		public Guid SourceRecordId { get; set; }

		/// <summary>
		/// Current status of the approval request.
		/// String representation matching ApprovalRequestStatus enum values:
		/// "pending", "approved", "rejected", "escalated", "expired".
		/// Drives the state machine logic in ApprovalRequestService.
		/// </summary>
		[JsonProperty(PropertyName = "status")]
		public string Status { get; set; }

		/// <summary>
		/// Foreign key reference to the user who initiated the approval request.
		/// Typically the user who created or submitted the source record that triggered
		/// the workflow. Used for notifications and audit trail purposes.
		/// </summary>
		[JsonProperty(PropertyName = "requested_by")]
		public Guid RequestedBy { get; set; }

		/// <summary>
		/// Timestamp when the approval request was created.
		/// Auto-populated at creation time. Used for calculating age, timeout conditions,
		/// and displaying request timeline information.
		/// </summary>
		[JsonProperty(PropertyName = "requested_on")]
		public DateTime RequestedOn { get; set; }

		/// <summary>
		/// Timestamp when the approval request reached a terminal state (approved, rejected, expired).
		/// Nullable because active (pending, escalated) requests have not yet completed.
		/// Used for calculating approval cycle times and audit reporting.
		/// </summary>
		[JsonProperty(PropertyName = "completed_on")]
		public DateTime? CompletedOn { get; set; }

		/// <summary>
		/// Display title for the approval request.
		/// Provides a human-readable summary of what is being approved.
		/// Used in UI listings and notifications.
		/// </summary>
		[JsonProperty(PropertyName = "title")]
		public string Title { get; set; }

		/// <summary>
		/// Timestamp when the last notification was sent for this request.
		/// Nullable - null indicates no notifications have been sent yet.
		/// Used by the notification job to determine notification frequency.
		/// </summary>
		[JsonProperty(PropertyName = "last_notification_sent")]
		public DateTime? LastNotificationSent { get; set; }

		/// <summary>
		/// Number of notifications sent for this request.
		/// Defaults to 0. Incremented each time a notification is sent.
		/// Used for notification throttling and escalation logic.
		/// </summary>
		[JsonProperty(PropertyName = "notification_count")]
		public int NotificationCount { get; set; } = 0;

		/// <summary>
		/// Indicates whether this request has been archived.
		/// Archived requests are excluded from active listings but retained for audit purposes.
		/// Defaults to false.
		/// </summary>
		[JsonProperty(PropertyName = "is_archived")]
		public bool IsArchived { get; set; } = false;

		/// <summary>
		/// Timestamp when the request was archived.
		/// Nullable - null indicates the request has not been archived.
		/// Set by the cleanup job when archiving completed requests.
		/// </summary>
		[JsonProperty(PropertyName = "archived_on")]
		public DateTime? ArchivedOn { get; set; }
	}
}
