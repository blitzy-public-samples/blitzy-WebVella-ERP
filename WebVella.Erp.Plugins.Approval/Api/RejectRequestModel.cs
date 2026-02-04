using Newtonsoft.Json;

namespace WebVella.Erp.Plugins.Approval.Api
{
	/// <summary>
	/// Input Data Transfer Object for reject action requests.
	/// Used by ApprovalController POST endpoint for /api/v3.0/p/approval/request/{id}/reject.
	/// Captures rejection reason (required) and optional comments when an approver rejects a request.
	/// </summary>
	public class RejectRequestModel
	{
		/// <summary>
		/// The reason for rejecting the approval request.
		/// This field is required and must be provided when rejecting an approval.
		/// Provides mandatory justification for the rejection decision.
		/// </summary>
		[JsonProperty(PropertyName = "reason")]
		public string Reason { get; set; }

		/// <summary>
		/// Additional comments or notes about the rejection.
		/// This field is optional and can be used to provide supplementary information
		/// beyond the required rejection reason.
		/// </summary>
		[JsonProperty(PropertyName = "comments")]
		public string Comments { get; set; }
	}
}
