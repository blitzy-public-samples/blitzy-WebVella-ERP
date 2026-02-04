using Newtonsoft.Json;
using System;

namespace WebVella.Erp.Plugins.Approval.Api
{
    /// <summary>
    /// Input DTO for delegate action requests.
    /// Contains the target user ID and optional delegation notes when an approver delegates a request.
    /// Used by ApprovalController POST endpoint for /api/v3.0/p/approval/request/{id}/delegate.
    /// </summary>
    public class DelegateRequestModel
    {
        /// <summary>
        /// Gets or sets the unique identifier of the user to delegate the approval request to.
        /// This field is required and must be a valid user GUID.
        /// </summary>
        [JsonProperty(PropertyName = "delegateToUserId")]
        public Guid DelegateToUserId { get; set; }

        /// <summary>
        /// Gets or sets optional comments or notes explaining the reason for delegation.
        /// This field is nullable and can be left empty if no additional context is needed.
        /// </summary>
        [JsonProperty(PropertyName = "comments")]
        public string Comments { get; set; }
    }
}
