using Newtonsoft.Json;

namespace WebVella.Erp.Plugins.Approval.Api
{
    /// <summary>
    /// Response DTO for dashboard metrics containing real-time KPIs for the approval workflow system.
    /// Consumed by PcApprovalDashboard component and returned by DashboardMetricsService.GetDashboardMetrics().
    /// </summary>
    public class DashboardMetricsModel
    {
        /// <summary>
        /// Gets or sets the count of pending approval requests awaiting action.
        /// This metric shows the current workload of approvals that need attention.
        /// </summary>
        [JsonProperty(PropertyName = "pendingCount")]
        public int PendingCount { get; set; }

        /// <summary>
        /// Gets or sets the average time in hours from request submission to completion.
        /// This metric helps measure the efficiency of the approval process.
        /// A lower value indicates faster approval turnaround times.
        /// </summary>
        [JsonProperty(PropertyName = "averageApprovalTimeHours")]
        public decimal AverageApprovalTimeHours { get; set; }

        /// <summary>
        /// Gets or sets the approval rate as a percentage (0-100) of approved requests
        /// versus total completed requests (approved + rejected).
        /// This metric indicates the overall approval success rate.
        /// </summary>
        [JsonProperty(PropertyName = "approvalRate")]
        public decimal ApprovalRate { get; set; }

        /// <summary>
        /// Gets or sets the count of overdue or escalated approval requests.
        /// These are requests that have exceeded their timeout period or have been escalated
        /// due to inaction. A high value may indicate bottlenecks in the approval process.
        /// </summary>
        [JsonProperty(PropertyName = "overdueCount")]
        public int OverdueCount { get; set; }

        /// <summary>
        /// Gets or sets the count of recent activity items (approvals, rejections, delegations)
        /// within a configurable time window (typically last 24-48 hours).
        /// This metric shows the current activity level in the approval system.
        /// </summary>
        [JsonProperty(PropertyName = "recentActivityCount")]
        public int RecentActivityCount { get; set; }
    }
}
