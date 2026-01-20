using System;
using System.Collections.Generic;
using System.Linq;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Eql;
using WebVella.Erp.Plugins.Approval.Api;

namespace WebVella.Erp.Plugins.Approval.Services
{
    /// <summary>
    /// Service class for calculating approval workflow dashboard metrics.
    /// Provides methods for retrieving KPIs displayed in the Manager Approval Dashboard.
    /// </summary>
    public class ApprovalMetricsService
    {
        private readonly RecordManager _recordManager;
        private readonly SecurityManager _securityManager;

        // Entity names used in queries
        private const string ENTITY_APPROVAL_REQUEST = "approval_request";
        private const string ENTITY_APPROVAL_HISTORY = "approval_history";
        private const string ENTITY_APPROVAL_STEP = "approval_step";
        private const string ENTITY_USER = "user";

        // Status constants
        private const string STATUS_PENDING = "pending";
        private const string STATUS_APPROVED = "approved";
        private const string STATUS_REJECTED = "rejected";

        // Action type constants
        private const string ACTION_SUBMITTED = "submitted";
        private const string ACTION_APPROVED = "approved";
        private const string ACTION_REJECTED = "rejected";
        private const string ACTION_DELEGATED = "delegated";
        private const string ACTION_ESCALATED = "escalated";

        /// <summary>
        /// Creates a new instance of the ApprovalMetricsService.
        /// </summary>
        public ApprovalMetricsService()
        {
            _recordManager = new RecordManager();
            _securityManager = new SecurityManager();
        }

        /// <summary>
        /// Retrieves all dashboard metrics for the specified user and date range.
        /// </summary>
        /// <param name="userId">The ID of the user requesting metrics (typically the current manager)</param>
        /// <param name="dateRangeDays">Number of days to include in time-based calculations</param>
        /// <param name="activityCount">Maximum number of recent activities to return</param>
        /// <returns>DashboardMetricsModel containing all calculated metrics</returns>
        public DashboardMetricsModel GetDashboardMetrics(Guid userId, int dateRangeDays = 30, int activityCount = 5)
        {
            var startDate = DateTime.UtcNow.AddDays(-dateRangeDays);
            var endDate = DateTime.UtcNow;

            var metrics = new DashboardMetricsModel
            {
                DateRangeDays = dateRangeDays,
                MetricsAsOf = DateTime.UtcNow,
                PendingApprovalsCount = GetPendingApprovalsCount(userId),
                AverageApprovalTimeHours = GetAverageApprovalTime(startDate, endDate),
                ApprovalRatePercent = GetApprovalRate(startDate, endDate),
                OverdueRequestsCount = GetOverdueRequestsCount(userId),
                RecentActivity = GetRecentActivity(activityCount)
            };

            return metrics;
        }

        /// <summary>
        /// Gets the count of pending approval requests where the user is an authorized approver.
        /// </summary>
        /// <param name="userId">The user ID to check authorization for</param>
        /// <returns>Count of pending approvals</returns>
        public int GetPendingApprovalsCount(Guid userId)
        {
            try
            {
                // Query pending approval requests
                // In a full implementation, this would filter by user's role as authorized approver
                var eqlCommand = new EqlCommand($"SELECT id FROM {ENTITY_APPROVAL_REQUEST} WHERE status = @status");
                eqlCommand.Parameters.Add(new EqlParameter("status", STATUS_PENDING));

                var result = eqlCommand.Execute();
                return result?.Count ?? 0;
            }
            catch (Exception)
            {
                // If entities don't exist yet (first deployment), return 0
                return 0;
            }
        }

        /// <summary>
        /// Calculates the average time in hours from request submission to completion.
        /// </summary>
        /// <param name="startDate">Start of the date range</param>
        /// <param name="endDate">End of the date range</param>
        /// <returns>Average hours to completion, or -1 if no data</returns>
        public double GetAverageApprovalTime(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Query completed requests within the date range
                var eqlCommand = new EqlCommand($"SELECT id, created_on, updated_on FROM {ENTITY_APPROVAL_REQUEST} WHERE status IN (@approved, @rejected) AND updated_on >= @startDate AND updated_on <= @endDate");
                eqlCommand.Parameters.Add(new EqlParameter("approved", STATUS_APPROVED));
                eqlCommand.Parameters.Add(new EqlParameter("rejected", STATUS_REJECTED));
                eqlCommand.Parameters.Add(new EqlParameter("startDate", startDate));
                eqlCommand.Parameters.Add(new EqlParameter("endDate", endDate));

                var result = eqlCommand.Execute();
                if (result == null || result.Count == 0)
                    return -1;

                double totalHours = 0;
                int count = 0;

                foreach (var record in result)
                {
                    var createdOn = record["created_on"] as DateTime?;
                    var updatedOn = record["updated_on"] as DateTime?;

                    if (createdOn.HasValue && updatedOn.HasValue)
                    {
                        var duration = updatedOn.Value - createdOn.Value;
                        totalHours += duration.TotalHours;
                        count++;
                    }
                }

                return count > 0 ? Math.Round(totalHours / count, 1) : -1;
            }
            catch (Exception)
            {
                // If entities don't exist yet, return -1
                return -1;
            }
        }

        /// <summary>
        /// Calculates the approval rate as percentage of approved vs total processed.
        /// </summary>
        /// <param name="startDate">Start of the date range</param>
        /// <param name="endDate">End of the date range</param>
        /// <returns>Approval rate percentage (0-100)</returns>
        public decimal GetApprovalRate(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Query for approved requests in date range
                var approvedCommand = new EqlCommand($"SELECT id FROM {ENTITY_APPROVAL_REQUEST} WHERE status = @status AND updated_on >= @startDate AND updated_on <= @endDate");
                approvedCommand.Parameters.Add(new EqlParameter("status", STATUS_APPROVED));
                approvedCommand.Parameters.Add(new EqlParameter("startDate", startDate));
                approvedCommand.Parameters.Add(new EqlParameter("endDate", endDate));
                var approvedResult = approvedCommand.Execute();
                int approvedCount = approvedResult?.Count ?? 0;

                // Query for rejected requests in date range
                var rejectedCommand = new EqlCommand($"SELECT id FROM {ENTITY_APPROVAL_REQUEST} WHERE status = @status AND updated_on >= @startDate AND updated_on <= @endDate");
                rejectedCommand.Parameters.Add(new EqlParameter("status", STATUS_REJECTED));
                rejectedCommand.Parameters.Add(new EqlParameter("startDate", startDate));
                rejectedCommand.Parameters.Add(new EqlParameter("endDate", endDate));
                var rejectedResult = rejectedCommand.Execute();
                int rejectedCount = rejectedResult?.Count ?? 0;

                int totalProcessed = approvedCount + rejectedCount;
                if (totalProcessed == 0)
                    return 0;

                return Math.Round((decimal)approvedCount / totalProcessed * 100, 1);
            }
            catch (Exception)
            {
                // If entities don't exist yet, return 0
                return 0;
            }
        }

        /// <summary>
        /// Gets the count of pending requests that have exceeded their timeout threshold.
        /// </summary>
        /// <param name="userId">The user ID for scoping (for future use)</param>
        /// <returns>Count of overdue requests</returns>
        public int GetOverdueRequestsCount(Guid userId)
        {
            try
            {
                // Query pending requests and check against timeout
                // This simplified version counts all pending requests older than 24 hours
                // Full implementation would join with approval_step for actual timeout_hours
                var cutoffDate = DateTime.UtcNow.AddHours(-24);
                var eqlCommand = new EqlCommand($"SELECT id FROM {ENTITY_APPROVAL_REQUEST} WHERE status = @status AND created_on <= @cutoff");
                eqlCommand.Parameters.Add(new EqlParameter("status", STATUS_PENDING));
                eqlCommand.Parameters.Add(new EqlParameter("cutoff", cutoffDate));

                var result = eqlCommand.Execute();
                return result?.Count ?? 0;
            }
            catch (Exception)
            {
                // If entities don't exist yet, return 0
                return 0;
            }
        }

        /// <summary>
        /// Gets the most recent approval actions for the activity feed.
        /// </summary>
        /// <param name="count">Maximum number of activities to return</param>
        /// <returns>List of recent activity models</returns>
        public List<RecentActivityModel> GetRecentActivity(int count = 5)
        {
            var activities = new List<RecentActivityModel>();

            try
            {
                // Query recent history entries
                var eqlCommand = new EqlCommand($"SELECT id, request_id, action_type, performed_by, performed_on, comments FROM {ENTITY_APPROVAL_HISTORY} ORDER BY performed_on DESC PAGE 1 PAGESIZE @count");
                eqlCommand.Parameters.Add(new EqlParameter("count", count));

                var result = eqlCommand.Execute();
                if (result == null || result.Count == 0)
                    return activities;

                foreach (var record in result)
                {
                    var activity = new RecentActivityModel
                    {
                        RequestId = record["request_id"] as Guid? ?? Guid.Empty,
                        ActionType = record["action_type"] as string ?? string.Empty,
                        PerformedById = record["performed_by"] as Guid? ?? Guid.Empty,
                        PerformedOn = record["performed_on"] as DateTime? ?? DateTime.MinValue,
                        Comments = record["comments"] as string ?? string.Empty
                    };

                    // Get user display name
                    activity.PerformedByName = GetUserDisplayName(activity.PerformedById);
                    activity.RelativeTime = GetRelativeTimeString(activity.PerformedOn);

                    activities.Add(activity);
                }
            }
            catch (Exception)
            {
                // If entities don't exist yet, return empty list
            }

            return activities;
        }

        /// <summary>
        /// Gets the display name for a user by their ID.
        /// </summary>
        /// <param name="userId">The user ID to look up</param>
        /// <returns>Display name in "First Last" format, or username, or "Unknown"</returns>
        private string GetUserDisplayName(Guid userId)
        {
            if (userId == Guid.Empty)
                return "Unknown";

            try
            {
                var eqlCommand = new EqlCommand($"SELECT username, first_name, last_name FROM {ENTITY_USER} WHERE id = @id");
                eqlCommand.Parameters.Add(new EqlParameter("id", userId));

                var result = eqlCommand.Execute();
                if (result != null && result.Count > 0)
                {
                    var record = result[0];
                    var firstName = record["first_name"] as string;
                    var lastName = record["last_name"] as string;
                    var username = record["username"] as string;

                    if (!string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName))
                        return $"{firstName} {lastName}";
                    else if (!string.IsNullOrWhiteSpace(firstName))
                        return firstName;
                    else if (!string.IsNullOrWhiteSpace(username))
                        return username;
                }
            }
            catch (Exception)
            {
                // If user lookup fails, return Unknown
            }

            return "Unknown";
        }

        /// <summary>
        /// Converts a DateTime to a human-readable relative time string.
        /// </summary>
        /// <param name="dateTime">The date/time to convert</param>
        /// <returns>Relative time string like "2 hours ago" or "3 days ago"</returns>
        private string GetRelativeTimeString(DateTime dateTime)
        {
            var timeSpan = DateTime.UtcNow - dateTime;

            if (timeSpan.TotalMinutes < 1)
                return "just now";
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes} minute{((int)timeSpan.TotalMinutes == 1 ? "" : "s")} ago";
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours} hour{((int)timeSpan.TotalHours == 1 ? "" : "s")} ago";
            if (timeSpan.TotalDays < 30)
                return $"{(int)timeSpan.TotalDays} day{((int)timeSpan.TotalDays == 1 ? "" : "s")} ago";
            if (timeSpan.TotalDays < 365)
                return $"{(int)(timeSpan.TotalDays / 30)} month{((int)(timeSpan.TotalDays / 30) == 1 ? "" : "s")} ago";

            return $"{(int)(timeSpan.TotalDays / 365)} year{((int)(timeSpan.TotalDays / 365) == 1 ? "" : "s")} ago";
        }
    }
}
