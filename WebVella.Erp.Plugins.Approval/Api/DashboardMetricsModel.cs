using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace WebVella.Erp.Plugins.Approval.Api
{
    /// <summary>
    /// Data transfer object for dashboard metrics API response.
    /// Contains all calculated KPI values for the Manager Approval Dashboard.
    /// </summary>
    public class DashboardMetricsModel
    {
        /// <summary>
        /// Count of approval requests currently in 'pending' status
        /// where the current user is an authorized approver for the current step.
        /// </summary>
        [JsonProperty(PropertyName = "pending_approvals_count")]
        public int PendingApprovalsCount { get; set; }

        /// <summary>
        /// Average time in hours from request creation to final approval decision.
        /// Calculated from approval_history timestamp differences within the date range.
        /// Value is -1 if no completed approvals exist in the date range.
        /// </summary>
        [JsonProperty(PropertyName = "average_approval_time_hours")]
        public double AverageApprovalTimeHours { get; set; }

        /// <summary>
        /// Percentage of requests approved versus total processed (approved + rejected).
        /// Formula: (approved count / (approved + rejected count)) * 100
        /// Value is 0 if no requests have been processed in the date range.
        /// </summary>
        [JsonProperty(PropertyName = "approval_rate_percent")]
        public decimal ApprovalRatePercent { get; set; }

        /// <summary>
        /// Count of pending requests that have exceeded their configured timeout_hours
        /// from the associated approval_step, indicating SLA violations.
        /// </summary>
        [JsonProperty(PropertyName = "overdue_requests_count")]
        public int OverdueRequestsCount { get; set; }

        /// <summary>
        /// List of recent approval actions performed, limited to the configured count.
        /// Sorted by performed_on descending (most recent first).
        /// </summary>
        [JsonProperty(PropertyName = "recent_activity")]
        public List<RecentActivityModel> RecentActivity { get; set; } = new List<RecentActivityModel>();

        /// <summary>
        /// Timestamp when these metrics were calculated.
        /// Used for display and cache invalidation purposes.
        /// </summary>
        [JsonProperty(PropertyName = "metrics_as_of")]
        public DateTime MetricsAsOf { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The date range in days used to calculate time-based metrics
        /// (average approval time, approval rate).
        /// </summary>
        [JsonProperty(PropertyName = "date_range_days")]
        public int DateRangeDays { get; set; }
    }
}
