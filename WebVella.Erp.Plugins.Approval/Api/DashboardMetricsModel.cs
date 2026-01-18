using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace WebVella.Erp.Plugins.Approval.Api
{
    /// <summary>
    /// Response model for dashboard metrics endpoint.
    /// Contains all KPI values displayed on the Manager Approval Dashboard.
    /// </summary>
    public class DashboardMetricsModel
    {
        /// <summary>
        /// Number of approval requests pending the manager's action.
        /// Includes only requests where the current user is an authorized approver.
        /// </summary>
        [JsonProperty(PropertyName = "pending_approvals_count")]
        public int PendingApprovalsCount { get; set; }

        /// <summary>
        /// Average time in hours from request creation to final approval decision.
        /// Calculated from completed requests within the selected date range.
        /// </summary>
        [JsonProperty(PropertyName = "average_approval_time_hours")]
        public double AverageApprovalTimeHours { get; set; }

        /// <summary>
        /// Percentage of requests approved versus total processed (approved + rejected).
        /// Returns 0 if no requests have been processed in the date range.
        /// </summary>
        [JsonProperty(PropertyName = "approval_rate_percent")]
        public double ApprovalRatePercent { get; set; }

        /// <summary>
        /// Count of pending requests exceeding their configured SLA timeout.
        /// Based on the timeout_hours configuration from the associated approval_step.
        /// </summary>
        [JsonProperty(PropertyName = "overdue_requests_count")]
        public int OverdueRequestsCount { get; set; }

        /// <summary>
        /// Recent approval activity items showing last N actions performed.
        /// Ordered by performed_on timestamp descending.
        /// </summary>
        [JsonProperty(PropertyName = "recent_activity")]
        public List<RecentActivityItem> RecentActivity { get; set; } = new List<RecentActivityItem>();

        /// <summary>
        /// UTC timestamp when these metrics were calculated.
        /// Used by the client to display "last updated" information.
        /// </summary>
        [JsonProperty(PropertyName = "metrics_as_of")]
        public DateTime MetricsAsOf { get; set; }

        /// <summary>
        /// Start of the date range used for metrics calculation.
        /// Passed through from the API request parameters.
        /// </summary>
        [JsonProperty(PropertyName = "date_range_start")]
        public DateTime DateRangeStart { get; set; }

        /// <summary>
        /// End of the date range used for metrics calculation.
        /// Passed through from the API request parameters.
        /// </summary>
        [JsonProperty(PropertyName = "date_range_end")]
        public DateTime DateRangeEnd { get; set; }
    }

    /// <summary>
    /// Individual activity item representing a single approval action.
    /// Used in the Recent Activity feed on the dashboard.
    /// </summary>
    public class RecentActivityItem
    {
        /// <summary>
        /// Type of action performed: approved, rejected, or delegated.
        /// </summary>
        [JsonProperty(PropertyName = "action")]
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Display name or username of the user who performed the action.
        /// </summary>
        [JsonProperty(PropertyName = "performed_by")]
        public string PerformedBy { get; set; } = string.Empty;

        /// <summary>
        /// UTC timestamp when the action was performed.
        /// </summary>
        [JsonProperty(PropertyName = "performed_on")]
        public DateTime PerformedOn { get; set; }

        /// <summary>
        /// ID of the related approval request.
        /// Can be used to link to request details.
        /// </summary>
        [JsonProperty(PropertyName = "request_id")]
        public Guid RequestId { get; set; }

        /// <summary>
        /// Brief description of the request subject for display purposes.
        /// </summary>
        [JsonProperty(PropertyName = "request_subject")]
        public string RequestSubject { get; set; } = string.Empty;
    }
}
