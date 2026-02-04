using System;
using Newtonsoft.Json;

namespace WebVella.Erp.Plugins.Approval.Model
{
    /// <summary>
    /// Data Transfer Object (DTO) for dashboard metrics API response.
    /// Contains real-time calculated metrics for approval workflow monitoring and reporting.
    /// Used by DashboardMetricsService to transport calculated metrics to the API controller and UI components.
    /// </summary>
    public class DashboardMetricsModel
    {
        /// <summary>
        /// Gets or sets the count of approval requests currently in Pending status.
        /// Represents requests that are awaiting action from an approver.
        /// </summary>
        [JsonProperty(PropertyName = "pending_count")]
        public int PendingCount { get; set; }

        /// <summary>
        /// Gets or sets the count of approval requests that have exceeded their due date.
        /// Represents requests that are past their SLA deadline and may require escalation.
        /// </summary>
        [JsonProperty(PropertyName = "overdue_count")]
        public int OverdueCount { get; set; }

        /// <summary>
        /// Gets or sets the count of approval requests that were approved today.
        /// Calculated based on requests where the approval action occurred on the current calendar date.
        /// </summary>
        [JsonProperty(PropertyName = "approved_today_count")]
        public int ApprovedTodayCount { get; set; }

        /// <summary>
        /// Gets or sets the count of approval requests that were rejected today.
        /// Calculated based on requests where the rejection action occurred on the current calendar date.
        /// </summary>
        [JsonProperty(PropertyName = "rejected_today_count")]
        public int RejectedTodayCount { get; set; }

        /// <summary>
        /// Gets or sets the average time in hours from request creation to completion.
        /// Calculated across all completed (approved or rejected) requests within the specified date range.
        /// A lower value indicates faster approval processing.
        /// </summary>
        [JsonProperty(PropertyName = "average_approval_time_hours")]
        public decimal AverageApprovalTimeHours { get; set; }

        /// <summary>
        /// Gets or sets the approval rate as a percentage (0-100).
        /// Calculated as the percentage of approved requests versus total completed requests.
        /// Formula: (ApprovedCount / TotalCompletedCount) * 100
        /// </summary>
        [JsonProperty(PropertyName = "approval_rate")]
        public decimal ApprovalRate { get; set; }

        /// <summary>
        /// Gets or sets the optional start date for filtering metrics calculation.
        /// When specified, only requests created on or after this date are included in calculations.
        /// Null indicates no lower bound date filter.
        /// </summary>
        [JsonProperty(PropertyName = "start_date")]
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// Gets or sets the optional end date for filtering metrics calculation.
        /// When specified, only requests created on or before this date are included in calculations.
        /// Null indicates no upper bound date filter.
        /// </summary>
        [JsonProperty(PropertyName = "end_date")]
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Gets or sets the count of currently active approval workflows.
        /// Represents workflows that are enabled and available for processing new approval requests.
        /// </summary>
        [JsonProperty(PropertyName = "total_active_workflows")]
        public int TotalActiveWorkflows { get; set; }
    }
}
