using System;
using WebVella.Erp.Api.Models;

namespace WebVella.Erp.Plugins.Approval.Model
{
	/// <summary>
	/// Defines the types of actions that can be performed on an approval request.
	/// Used by the approval_history entity to record what action was taken at each step,
	/// and by ApprovalRequestService to handle action execution and workflow progression.
	/// </summary>
	public enum ApprovalActionType
	{
		/// <summary>
		/// Approve the request and advance the workflow to the next step.
		/// If this is the final step, the request status becomes Approved.
		/// </summary>
		[SelectOption(Label = "approve")]
		Approve = 0,

		/// <summary>
		/// Reject the request and terminate the workflow.
		/// The request status becomes Rejected and no further steps are processed.
		/// </summary>
		[SelectOption(Label = "reject")]
		Reject = 1,

		/// <summary>
		/// Delegate the approval responsibility to another user.
		/// The current approver transfers their approval authority to a specified user.
		/// </summary>
		[SelectOption(Label = "delegate")]
		Delegate = 2,

		/// <summary>
		/// Escalate the request to a higher authority.
		/// Used when the current approver cannot make a decision or when SLA is exceeded.
		/// </summary>
		[SelectOption(Label = "escalate")]
		Escalate = 3,

		/// <summary>
		/// Add a comment to the request without taking any workflow action.
		/// The request remains in its current state and step.
		/// </summary>
		[SelectOption(Label = "comment")]
		Comment = 4
	}
}
