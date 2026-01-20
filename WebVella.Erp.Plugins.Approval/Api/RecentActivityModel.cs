using Newtonsoft.Json;
using System;

namespace WebVella.Erp.Plugins.Approval.Api
{
    /// <summary>
    /// Data transfer object for individual activity feed items in the dashboard.
    /// Represents a single approval action from the approval_history entity.
    /// </summary>
    public class RecentActivityModel
    {
        /// <summary>
        /// Unique identifier of the approval request this action belongs to.
        /// </summary>
        [JsonProperty(PropertyName = "request_id")]
        public Guid RequestId { get; set; }

        /// <summary>
        /// Type of action performed: 'submitted', 'approved', 'rejected', 'delegated', 'escalated', 'cancelled'.
        /// Maps to the action_type field in approval_history entity.
        /// </summary>
        [JsonProperty(PropertyName = "action_type")]
        public string ActionType { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the user who performed the action.
        /// Format: "First Last" or username if names are not available.
        /// </summary>
        [JsonProperty(PropertyName = "performed_by_name")]
        public string PerformedByName { get; set; } = string.Empty;

        /// <summary>
        /// Unique identifier of the user who performed the action.
        /// </summary>
        [JsonProperty(PropertyName = "performed_by_id")]
        public Guid PerformedById { get; set; }

        /// <summary>
        /// UTC timestamp when the action was performed.
        /// </summary>
        [JsonProperty(PropertyName = "performed_on")]
        public DateTime PerformedOn { get; set; }

        /// <summary>
        /// Optional comments provided with the action.
        /// May be null or empty if no comments were entered.
        /// </summary>
        [JsonProperty(PropertyName = "comments")]
        public string Comments { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable relative time string (e.g., "2 hours ago").
        /// Calculated on the server for consistent display.
        /// </summary>
        [JsonProperty(PropertyName = "relative_time")]
        public string RelativeTime { get; set; } = string.Empty;
    }
}
