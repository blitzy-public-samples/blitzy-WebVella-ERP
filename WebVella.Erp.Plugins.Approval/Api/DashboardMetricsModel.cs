using Newtonsoft.Json;
using System;
using System.Collections.Generic;

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

        /// <summary>
        /// Gets or sets the list of recent activity items showing the last 5 actions taken.
        /// This provides a feed of recent approval workflow activity for the dashboard.
        /// </summary>
        [JsonProperty(PropertyName = "recentActivity")]
        public List<RecentActivityItem> RecentActivity { get; set; }
    }

    /// <summary>
    /// Represents a single recent activity item for the dashboard activity feed.
    /// Contains details about an approval action including who performed it and when.
    /// </summary>
    public class RecentActivityItem
    {
        /// <summary>
        /// Gets or sets the unique identifier of the approval history record.
        /// </summary>
        [JsonProperty(PropertyName = "id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the approval request.
        /// Used to link to the request details page.
        /// </summary>
        [JsonProperty(PropertyName = "requestId")]
        public Guid RequestId { get; set; }

        /// <summary>
        /// Gets or sets the action type (approved, rejected, delegated, etc.).
        /// </summary>
        [JsonProperty(PropertyName = "actionType")]
        public string ActionType { get; set; }

        /// <summary>
        /// Gets or sets the user ID who performed the action.
        /// </summary>
        [JsonProperty(PropertyName = "performedById")]
        public Guid PerformedById { get; set; }

        /// <summary>
        /// Gets or sets the display name of the user who performed the action.
        /// </summary>
        [JsonProperty(PropertyName = "performedByName")]
        public string PerformedByName { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the action was performed.
        /// </summary>
        [JsonProperty(PropertyName = "performedOn")]
        public DateTime PerformedOn { get; set; }

        /// <summary>
        /// Gets or sets optional comments associated with the action.
        /// </summary>
        [JsonProperty(PropertyName = "comments")]
        public string Comments { get; set; }

        /// <summary>
        /// Gets or sets the link to view the request details.
        /// </summary>
        [JsonProperty(PropertyName = "requestLink")]
        public string RequestLink { get; set; }
    }
}
