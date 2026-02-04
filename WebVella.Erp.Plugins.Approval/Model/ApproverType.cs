using System;
using WebVella.Erp.Api.Models;

namespace WebVella.Erp.Plugins.Approval.Model
{
	/// <summary>
	/// Defines the types of approvers that can be assigned to an approval step.
	/// This enum is used by the approval_step entity to determine how the approver 
	/// for a given step should be resolved during workflow execution.
	/// </summary>
	public enum ApproverType
	{
		/// <summary>
		/// A specific user is designated as the approver.
		/// The approver_id field in the approval_step entity contains the user's GUID.
		/// Use this type when a particular individual must always approve requests at this step.
		/// </summary>
		[SelectOption(Label = "user")]
		User = 0,

		/// <summary>
		/// Any user with the specified role can approve requests at this step.
		/// The approver_id field in the approval_step entity contains the role's GUID.
		/// The first available user with the role can process the approval request.
		/// </summary>
		[SelectOption(Label = "role")]
		Role = 1,

		/// <summary>
		/// The manager of the user who initiated the approval request is the approver.
		/// The approver_id field may be null or contain a fallback user GUID.
		/// The manager relationship is resolved from the requesting user's profile.
		/// </summary>
		[SelectOption(Label = "manager")]
		Manager = 2,

		/// <summary>
		/// Custom approver resolution logic is applied via plugin configuration.
		/// The approver_id field may contain a custom resolver identifier or be null.
		/// Custom logic can be implemented in hooks or services to determine the approver
		/// based on business rules, record data, or external system integration.
		/// </summary>
		[SelectOption(Label = "custom")]
		Custom = 3
	}
}
