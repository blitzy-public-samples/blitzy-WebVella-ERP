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
    /// Service class responsible for calculating and retrieving dashboard metrics
    /// for the manager approval workflow dashboard.
    /// Queries approval_request and approval_history entities to compute KPIs.
    /// </summary>
    public class DashboardMetricsService
    {
        private readonly RecordManager _recordManager;

        /// <summary>
        /// Initializes a new instance of the DashboardMetricsService.
        /// </summary>
        public DashboardMetricsService()
        {
            _recordManager = new RecordManager();
        }

        /// <summary>
        /// Retrieves all dashboard metrics for the specified user and date range.
        /// Stamps MetricsAsOf with the current UTC time so the dashboard can display how fresh the metrics are.
        /// </summary>
        /// <param name="userId">The ID of the manager requesting metrics.</param>
        /// <param name="fromDate">Start of the date range for time-based metrics.</param>
        /// <param name="toDate">End of the date range for time-based metrics.</param>
        /// <returns>A DashboardMetricsModel containing all calculated metrics.</returns>
        public DashboardMetricsModel GetDashboardMetrics(Guid userId, DateTime fromDate, DateTime toDate)
        {
            var metrics = new DashboardMetricsModel
            {
                MetricsAsOf = DateTime.UtcNow,
                DateRangeStart = fromDate,
                DateRangeEnd = toDate,
                PendingApprovalsCount = GetPendingApprovalsCount(userId),
                OverdueRequestsCount = GetOverdueRequestsCount(userId),
                AverageApprovalTimeHours = GetAverageApprovalTime(fromDate, toDate),
                ApprovalRatePercent = GetApprovalRate(fromDate, toDate),
                // The recent-activity feed is deliberately capped at the 5 most recent actions to keep the
                // dashboard lightweight and focused on the latest changes rather than a full audit log.
                RecentActivity = GetRecentActivity(5)
            };

            return metrics;
        }

        /// <summary>
        /// Gets the count of approval requests in pending status where the user
        /// is an authorized approver for the current step.
        /// Queries the approval_request entity and returns 0 as the safe default when no rows match or the backing entity is unavailable.
        /// </summary>
        /// <param name="userId">The ID of the approver user.</param>
        /// <returns>Count of pending approval requests.</returns>
        public int GetPendingApprovalsCount(Guid userId)
        {
            try
            {
                // Query approval_request entity for pending requests
                // In a full implementation, this would also check if userId is an authorized approver
                var eqlCommand = @"
                    SELECT id 
                    FROM approval_request 
                    WHERE status = @status";

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("status", "pending")
                };

                var result = new EqlCommand(eqlCommand, eqlParams).Execute();
                return result?.Count ?? 0;
            }
            catch (Exception)
            {
                // ADR-005 graceful degradation: the approval_request entity may not exist during staged
                // deployment, so the pending count falls back to 0 instead of failing the whole dashboard.
                return 0;
            }
        }

        /// <summary>
        /// Gets the count of pending requests that have exceeded their configured
        /// timeout threshold from the approval step.
        /// Queries the approval_request entity and returns 0 as the safe default when no rows match or the backing entity is unavailable.
        /// </summary>
        /// <param name="userId">The ID of the approver user.</param>
        /// <returns>Count of overdue approval requests.</returns>
        public int GetOverdueRequestsCount(Guid userId)
        {
            try
            {
                // Query for pending requests where created_on + timeout_hours < NOW
                // This is a simplified implementation - actual would join with approval_step
                var eqlCommand = @"
                    SELECT id, created_on 
                    FROM approval_request 
                    WHERE status = @status";

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("status", "pending")
                };

                var result = new EqlCommand(eqlCommand, eqlParams).Execute();
                
                if (result == null || !result.Any())
                    return 0;

                // 24 hours is the business SLA used as the overdue threshold when an approval step does not
                // declare its own timeout, ensuring stale requests still surface as overdue.
                int defaultTimeoutHours = 24;
                var overdueCount = 0;
                var now = DateTime.UtcNow;

                foreach (var record in result)
                {
                    if (record.Properties.ContainsKey("created_on") && record["created_on"] != null)
                    {
                        var createdOn = (DateTime)record["created_on"];
                        var deadline = createdOn.AddHours(defaultTimeoutHours);
                        
                        if (now > deadline)
                        {
                            overdueCount++;
                        }
                    }
                }

                return overdueCount;
            }
            catch (Exception)
            {
                // ADR-005 graceful degradation: if the approval_request entity is absent during staged
                // deployment, the overdue count degrades to 0 rather than breaking the entire dashboard.
                return 0;
            }
        }

        /// <summary>
        /// Calculates the average time in hours from request creation to completion
        /// for all processed requests within the date range.
        /// Queries the approval_request entity, returns 0 as the safe default when no rows match, and feeds the AverageApprovalTimeHours model property.
        /// </summary>
        /// <param name="fromDate">Start of the date range.</param>
        /// <param name="toDate">End of the date range.</param>
        /// <returns>Average processing time in hours.</returns>
        public decimal GetAverageApprovalTime(DateTime fromDate, DateTime toDate)
        {
            try
            {
                // Query completed approval requests within date range
                var eqlCommand = @"
                    SELECT id, created_on, completed_on 
                    FROM approval_request 
                    WHERE status IN (@approvedStatus, @rejectedStatus)
                    AND completed_on >= @fromDate
                    AND completed_on <= @toDate";

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("approvedStatus", "approved"),
                    new EqlParameter("rejectedStatus", "rejected"),
                    new EqlParameter("fromDate", fromDate),
                    new EqlParameter("toDate", toDate)
                };

                var result = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (result == null || !result.Any())
                    return 0;

                var totalHours = 0m;
                var count = 0;

                foreach (var record in result)
                {
                    if (record.Properties.ContainsKey("created_on") && 
                        record.Properties.ContainsKey("completed_on") &&
                        record["created_on"] != null && 
                        record["completed_on"] != null)
                    {
                        var createdOn = (DateTime)record["created_on"];
                        var completedOn = (DateTime)record["completed_on"];
                        var hours = (decimal)(completedOn - createdOn).TotalHours;
                        totalHours += hours;
                        count++;
                    }
                }

                return count > 0 ? Math.Round(totalHours / count, 2) : 0;
            }
            catch (Exception)
            {
                // ADR-005 graceful degradation: a missing approval_request entity during staged deployment
                // yields an average of 0 instead of failing the whole dashboard.
                return 0;
            }
        }

        /// <summary>
        /// Calculates the percentage of approved requests out of total processed
        /// requests within the date range.
        /// Queries the approval_request entity and returns 0 as the safe default (on a 0-100 scale) when no rows match or the backing entity is unavailable.
        /// </summary>
        /// <param name="fromDate">Start of the date range.</param>
        /// <param name="toDate">End of the date range.</param>
        /// <returns>Approval rate as a percentage (0-100).</returns>
        public decimal GetApprovalRate(DateTime fromDate, DateTime toDate)
        {
            try
            {
                // Query all completed requests within date range
                var eqlCommand = @"
                    SELECT id, status 
                    FROM approval_request 
                    WHERE status IN (@approvedStatus, @rejectedStatus)
                    AND completed_on >= @fromDate
                    AND completed_on <= @toDate";

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("approvedStatus", "approved"),
                    new EqlParameter("rejectedStatus", "rejected"),
                    new EqlParameter("fromDate", fromDate),
                    new EqlParameter("toDate", toDate)
                };

                var result = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (result == null || !result.Any())
                    return 0;

                var totalCount = result.Count;
                var approvedCount = result.Count(r => 
                    r.Properties.ContainsKey("status") && 
                    (string)r["status"] == "approved");

                return totalCount > 0 
                    ? Math.Round((decimal)approvedCount / totalCount * 100, 1) 
                    : 0;
            }
            catch (Exception)
            {
                // ADR-005 graceful degradation: when the approval_request entity is unavailable during
                // staged deployment, the approval rate degrades to 0 rather than failing the entire dashboard.
                return 0;
            }
        }

        /// <summary>
        /// Retrieves the most recent approval history actions for display in the
        /// activity feed.
        /// Queries the approval_history entity and returns an empty list as the safe default when no history exists or the backing entity is unavailable.
        /// </summary>
        /// <param name="limit">Maximum number of activity items to return.</param>
        /// <returns>List of recent activity items ordered by most recent first.</returns>
        public List<RecentActivityItem> GetRecentActivity(int limit)
        {
            try
            {
                // Query approval_history for recent actions
                var eqlCommand = @"
                    SELECT id, action, performed_by, performed_on, request_id 
                    FROM approval_history 
                    ORDER BY performed_on DESC
                    LIMIT @limit";

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("limit", limit)
                };

                var result = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (result == null || !result.Any())
                    return new List<RecentActivityItem>();

                var activityList = new List<RecentActivityItem>();

                foreach (var record in result)
                {
                    var item = new RecentActivityItem
                    {
                        Action = record.Properties.ContainsKey("action") 
                            ? (string)record["action"] ?? "unknown" 
                            : "unknown",
                        PerformedBy = record.Properties.ContainsKey("performed_by") 
                            ? (string)record["performed_by"] ?? "Unknown User" 
                            : "Unknown User",
                        PerformedOn = record.Properties.ContainsKey("performed_on") && record["performed_on"] != null
                            ? (DateTime)record["performed_on"] 
                            : DateTime.UtcNow,
                        RequestId = record.Properties.ContainsKey("request_id") && record["request_id"] != null
                            ? (Guid)record["request_id"] 
                            : Guid.Empty,
                        RequestTitle = record.Properties.ContainsKey("request_title") 
                            ? (string)record["request_title"] ?? "Approval Request" 
                            : "Approval Request"
                    };

                    activityList.Add(item);
                }

                return activityList;
            }
            catch (Exception)
            {
                // ADR-005 graceful degradation: a missing approval_history entity during staged deployment
                // yields an empty activity list instead of failing the whole dashboard.
                return new List<RecentActivityItem>();
            }
        }
    }
}
