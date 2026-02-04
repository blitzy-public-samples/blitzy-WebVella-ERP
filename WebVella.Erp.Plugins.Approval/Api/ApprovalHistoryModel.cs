using System;
using Newtonsoft.Json;
using WebVella.Erp.Api.Models;

namespace WebVella.Erp.Plugins.Approval.Api
{
	/// <summary>
	/// Enumeration representing the types of actions that can be performed in an approval workflow.
	/// Each value is decorated with a SelectOption attribute for UI dropdown/select rendering.
	/// </summary>
	public enum ApprovalHistoryAction
	{
		/// <summary>
		/// The approval request was initially submitted for review.
		/// </summary>
		[SelectOption(Label = "submitted")]
		Submitted = 0,

		/// <summary>
		/// The approval request was approved by an approver.
		/// </summary>
		[SelectOption(Label = "approved")]
		Approved = 1,

		/// <summary>
		/// The approval request was rejected by an approver.
		/// </summary>
		[SelectOption(Label = "rejected")]
		Rejected = 2,

		/// <summary>
		/// The approval request was delegated to another user.
		/// </summary>
		[SelectOption(Label = "delegated")]
		Delegated = 3,

		/// <summary>
		/// The approval request was escalated due to timeout or other reasons.
		/// </summary>
		[SelectOption(Label = "escalated")]
		Escalated = 4
	}

	/// <summary>
	/// Entity DTO representing an approval history record for audit trail purposes.
	/// This model captures each action taken during the approval workflow lifecycle,
	/// including who performed the action, when it was performed, and any associated comments.
	/// Used by ApprovalHistoryService for audit operations and displayed in PcApprovalHistory component.
	/// </summary>
	public class ApprovalHistoryModel
	{
		/// <summary>
		/// Gets or sets the unique identifier for this history record.
		/// </summary>
		[JsonProperty(PropertyName = "id")]
		public Guid Id { get; set; }

		/// <summary>
		/// Gets or sets the unique identifier of the approval request this history record belongs to.
		/// Foreign key reference to the approval_request entity.
		/// </summary>
		[JsonProperty(PropertyName = "request_id")]
		public Guid RequestId { get; set; }

		/// <summary>
		/// Gets or sets the unique identifier of the approval step at which this action was performed.
		/// Foreign key reference to the approval_step entity.
		/// </summary>
		[JsonProperty(PropertyName = "step_id")]
		public Guid StepId { get; set; }

		/// <summary>
		/// Gets or sets the type of action that was performed.
		/// Valid values: submitted, approved, rejected, delegated, escalated.
		/// Stored as string for database compatibility and JSON serialization.
		/// </summary>
		[JsonProperty(PropertyName = "action")]
		public string Action { get; set; }

		/// <summary>
		/// Gets or sets the unique identifier of the user who performed this action.
		/// Foreign key reference to the user entity.
		/// </summary>
		[JsonProperty(PropertyName = "performed_by")]
		public Guid PerformedBy { get; set; }

		/// <summary>
		/// Gets or sets the date and time when this action was performed.
		/// Automatically set when the history record is created.
		/// </summary>
		[JsonProperty(PropertyName = "performed_on")]
		public DateTime PerformedOn { get; set; }

		/// <summary>
		/// Gets or sets optional comments provided by the user when performing the action.
		/// May contain approval notes, rejection reasons, delegation instructions, etc.
		/// This field is nullable.
		/// </summary>
		[JsonProperty(PropertyName = "comments")]
		public string Comments { get; set; }

		/// <summary>
		/// Gets or sets the status of the request before this action was performed.
		/// Used for audit trail and rollback purposes.
		/// This field is nullable for the initial submission action.
		/// </summary>
		[JsonProperty(PropertyName = "previous_status")]
		public string PreviousStatus { get; set; }

		/// <summary>
		/// Gets or sets the status of the request after this action was performed.
		/// Used for audit trail and status tracking.
		/// </summary>
		[JsonProperty(PropertyName = "new_status")]
		public string NewStatus { get; set; }

		/// <summary>
		/// Gets or sets the username of the user who performed this action.
		/// Populated from user relation expansion for display purposes.
		/// </summary>
		[JsonProperty(PropertyName = "performer_username")]
		public string PerformerUsername { get; set; }

		/// <summary>
		/// Gets or sets the first name of the user who performed this action.
		/// Populated from user relation expansion for display purposes.
		/// </summary>
		[JsonProperty(PropertyName = "performer_first_name")]
		public string PerformerFirstName { get; set; }

		/// <summary>
		/// Gets or sets the last name of the user who performed this action.
		/// Populated from user relation expansion for display purposes.
		/// </summary>
		[JsonProperty(PropertyName = "performer_last_name")]
		public string PerformerLastName { get; set; }

		/// <summary>
		/// Gets or sets the email of the user who performed this action.
		/// Populated from user relation expansion for display purposes.
		/// </summary>
		[JsonProperty(PropertyName = "performer_email")]
		public string PerformerEmail { get; set; }

		/// <summary>
		/// Gets the full display name of the performer, combining first and last name.
		/// Returns username if first/last name are not available.
		/// </summary>
		[JsonIgnore]
		public string PerformerDisplayName
		{
			get
			{
				if (!string.IsNullOrWhiteSpace(PerformerFirstName) || !string.IsNullOrWhiteSpace(PerformerLastName))
				{
					return $"{PerformerFirstName} {PerformerLastName}".Trim();
				}
				return PerformerUsername ?? string.Empty;
			}
		}
	}
}
