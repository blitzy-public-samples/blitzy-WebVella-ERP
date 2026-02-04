using Newtonsoft.Json;

namespace WebVella.Erp.Plugins.Approval.Api
{
	/// <summary>
	/// Input DTO for approve action requests.
	/// Used by ApprovalController POST endpoint for /api/v3.0/p/approval/request/{id}/approve.
	/// Contains optional approval comments that an approver can provide when approving a request.
	/// </summary>
	public class ApproveRequestModel
	{
		/// <summary>
		/// Optional comments provided by the approver when approving the request.
		/// These comments are stored in the approval history for audit purposes.
		/// </summary>
		[JsonProperty(PropertyName = "comments")]
		public string Comments { get; set; }
	}
}
