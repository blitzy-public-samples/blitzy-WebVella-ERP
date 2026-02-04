using System;
using System.Collections.Generic;
using System.Text;
using WebVella.Erp.Api.Models;

namespace WebVella.Erp.Plugins.Approval.Model
{
	/// <summary>
	/// Defines the possible statuses of an approval request throughout its lifecycle.
	/// Used by the approval_request entity and throughout the service layer for status management.
	/// Each status represents a distinct state in the approval workflow process.
	/// </summary>
	public enum ApprovalStatus
	{
		/// <summary>
		/// The approval request is awaiting action from an approver.
		/// This is the initial status when a request is first created.
		/// </summary>
		[SelectOption(Label = "pending")]
		Pending = 0,

		/// <summary>
		/// The approval request has been approved by the designated approver.
		/// This is a terminal status indicating successful completion of the approval process.
		/// </summary>
		[SelectOption(Label = "approved")]
		Approved = 1,

		/// <summary>
		/// The approval request has been rejected by the designated approver.
		/// This is a terminal status indicating the request was denied.
		/// </summary>
		[SelectOption(Label = "rejected")]
		Rejected = 2,

		/// <summary>
		/// The approval request has been delegated to another user or role.
		/// The request remains active but assigned to a different approver.
		/// </summary>
		[SelectOption(Label = "delegated")]
		Delegated = 3,

		/// <summary>
		/// The approval request has been escalated due to SLA breach or timeout.
		/// Typically occurs when the original approver does not act within the defined timeframe.
		/// </summary>
		[SelectOption(Label = "escalated")]
		Escalated = 4,

		/// <summary>
		/// The approval request has been cancelled before completion.
		/// This is a terminal status indicating the request was intentionally terminated.
		/// </summary>
		[SelectOption(Label = "cancelled")]
		Cancelled = 5
	}
}
