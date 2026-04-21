using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace WebVella.Erp.Plugins.Approval.Api
{
    /// <summary>
    /// Data transfer object containing all dashboard metric values for the manager approval dashboard.
    /// This model is returned by the dashboard metrics API endpoint and consumed by the UI component.
    /// </summary>
    public class DashboardMetricsModel
    {
        /// <summary>
        /// Count of approval requests currently in pending status awaiting manager action.
        /// </summary>
        [JsonProperty(PropertyName = "pending_approvals_count")]
        public int PendingApprovalsCount { get; set; }

        /// <summary>
        /// Average time in hours taken to process (approve/reject) approval requests
        /// within the specified date range.
        /// </summary>
        [JsonProperty(PropertyName = "average_approval_time_hours")]
        public decimal AverageApprovalTimeHours { get; set; }

        /// <summary>
        /// Percentage of requests that were approved versus total processed requests
        /// within the specified date range (0-100).
        /// </summary>
        [JsonProperty(PropertyName = "approval_rate_percent")]
        public decimal ApprovalRatePercent { get; set; }

        /// <summary>
        /// Count of pending requests that have exceeded their configured timeout threshold.
        /// </summary>
        [JsonProperty(PropertyName = "overdue_requests_count")]
        public int OverdueRequestsCount { get; set; }

        /// <summary>
        /// List of the most recent approval actions taken, limited to the last 5 items.
        /// </summary>
        [JsonProperty(PropertyName = "recent_activity")]
        public List<RecentActivityItem> RecentActivity { get; set; } = new List<RecentActivityItem>();

        /// <summary>
        /// Timestamp indicating when the metrics were calculated (UTC).
        /// </summary>
        [JsonProperty(PropertyName = "metrics_as_of")]
        public DateTime MetricsAsOf { get; set; }

        /// <summary>
        /// Start of the date range used for calculating time-based metrics.
        /// </summary>
        [JsonProperty(PropertyName = "date_range_start")]
        public DateTime DateRangeStart { get; set; }

        /// <summary>
        /// End of the date range used for calculating time-based metrics.
        /// </summary>
        [JsonProperty(PropertyName = "date_range_end")]
        public DateTime DateRangeEnd { get; set; }
    }

    /// <summary>
    /// Represents a single item in the recent activity feed for the dashboard.
    /// </summary>
    public class RecentActivityItem
    {
        /// <summary>
        /// The action taken on the approval request (e.g., "approved", "rejected", "escalated").
        /// </summary>
        [JsonProperty(PropertyName = "action")]
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the user who performed the action.
        /// </summary>
        [JsonProperty(PropertyName = "performed_by")]
        public string PerformedBy { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the action was performed (UTC).
        /// </summary>
        [JsonProperty(PropertyName = "performed_on")]
        public DateTime PerformedOn { get; set; }

        /// <summary>
        /// Unique identifier of the approval request that was acted upon.
        /// </summary>
        [JsonProperty(PropertyName = "request_id")]
        public Guid RequestId { get; set; }

        /// <summary>
        /// Title or subject of the approval request for display purposes.
        /// </summary>
        [JsonProperty(PropertyName = "request_title")]
        public string RequestTitle { get; set; } = string.Empty;
    }
}
