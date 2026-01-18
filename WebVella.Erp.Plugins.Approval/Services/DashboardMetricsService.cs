using System;
using System.Collections.Generic;
using System.Linq;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Plugins.Approval.Api;

namespace WebVella.Erp.Plugins.Approval.Services
{
    /// <summary>
    /// Service for calculating dashboard metrics from approval entities.
    /// Provides aggregated metrics for the Manager Approval Dashboard display.
    /// </summary>
    public class DashboardMetricsService
    {
        private readonly RecordManager recMan;
        
        /// <summary>
        /// Default timeout in hours for overdue calculation when not specified on step.
        /// </summary>
        private const int DEFAULT_TIMEOUT_HOURS = 48;

        /// <summary>
        /// Initializes a new instance of the DashboardMetricsService.
        /// </summary>
        public DashboardMetricsService()
        {
            recMan = new RecordManager();
        }

        /// <summary>
        /// Initializes a new instance with a custom RecordManager.
        /// Used primarily for unit testing with mocked dependencies.
        /// </summary>
        /// <param name="recordManager">Custom RecordManager instance</param>
        public DashboardMetricsService(RecordManager recordManager)
        {
            recMan = recordManager ?? new RecordManager();
        }

        /// <summary>
        /// Gets all dashboard metrics for the current user within the specified date range.
        /// This is the main entry point for retrieving all KPIs.
        /// </summary>
        /// <param name="userId">Current manager user ID</param>
        /// <param name="fromDate">Start of date range (inclusive)</param>
        /// <param name="toDate">End of date range (inclusive)</param>
        /// <returns>Dashboard metrics model with all calculated KPIs</returns>
        public DashboardMetricsModel GetDashboardMetrics(Guid userId, DateTime fromDate, DateTime toDate)
        {
            var metrics = new DashboardMetricsModel
            {
                PendingApprovalsCount = GetPendingApprovalsCount(userId),
                AverageApprovalTimeHours = GetAverageApprovalTime(userId, fromDate, toDate),
                ApprovalRatePercent = GetApprovalRate(userId, fromDate, toDate),
                OverdueRequestsCount = GetOverdueRequestsCount(userId),
                RecentActivity = GetRecentActivity(userId, 5),
                MetricsAsOf = DateTime.UtcNow,
                DateRangeStart = fromDate,
                DateRangeEnd = toDate
            };

            return metrics;
        }

        /// <summary>
        /// Gets count of pending approvals awaiting the user's action.
        /// Filters to requests where status='pending' and user is an authorized approver.
        /// </summary>
        /// <param name="userId">Current manager user ID</param>
        /// <returns>Number of pending approval requests</returns>
        public int GetPendingApprovalsCount(Guid userId)
        {
            try
            {
                // Query approval_request where status='pending'
                var query = new EntityQuery("approval_request", "*",
                    EntityQuery.QueryEQ("status", "pending"));
                
                var result = recMan.Find(query);

                if (!result.Success || result.Object?.Data == null)
                    return 0;

                // Filter to only requests where user is authorized approver
                return result.Object.Data.Count(r => IsUserAuthorizedApprover(userId, r));
            }
            catch (Exception)
            {
                // Return 0 if entity doesn't exist yet (first run scenario)
                return 0;
            }
        }

        /// <summary>
        /// Gets average approval time in hours for completed requests.
        /// Calculated from the difference between request creation and final action timestamp.
        /// </summary>
        /// <param name="userId">Current manager user ID</param>
        /// <param name="fromDate">Start of date range</param>
        /// <param name="toDate">End of date range</param>
        /// <returns>Average approval time in hours, rounded to 1 decimal place</returns>
        public double GetAverageApprovalTime(Guid userId, DateTime fromDate, DateTime toDate)
        {
            try
            {
                // Query completed approval actions within date range
                var query = new EntityQuery("approval_history", "*",
                    EntityQuery.QueryAND(
                        EntityQuery.QueryGTE("performed_on", fromDate),
                        EntityQuery.QueryLTE("performed_on", toDate),
                        EntityQuery.QueryOR(
                            EntityQuery.QueryEQ("action", "approved"),
                            EntityQuery.QueryEQ("action", "rejected")
                        )
                    ));
                
                var result = recMan.Find(query);

                if (!result.Success || result.Object?.Data == null || !result.Object.Data.Any())
                    return 0;

                var records = result.Object.Data.ToList();
                if (records.Count == 0)
                    return 0;

                var totalHours = records.Sum(r => CalculateApprovalTimeHours(r));
                
                return Math.Round(totalHours / records.Count, 1);
            }
            catch (Exception)
            {
                // Return 0 if entity doesn't exist yet
                return 0;
            }
        }

        /// <summary>
        /// Gets approval rate percentage for the date range.
        /// Calculated as (approved / (approved + rejected)) * 100.
        /// </summary>
        /// <param name="userId">Current manager user ID</param>
        /// <param name="fromDate">Start of date range</param>
        /// <param name="toDate">End of date range</param>
        /// <returns>Approval rate percentage, rounded to 1 decimal place</returns>
        public double GetApprovalRate(Guid userId, DateTime fromDate, DateTime toDate)
        {
            try
            {
                // Query all approval history within date range
                var query = new EntityQuery("approval_history", "*",
                    EntityQuery.QueryAND(
                        EntityQuery.QueryGTE("performed_on", fromDate),
                        EntityQuery.QueryLTE("performed_on", toDate)
                    ));
                
                var result = recMan.Find(query);

                if (!result.Success || result.Object?.Data == null || !result.Object.Data.Any())
                    return 0;

                var records = result.Object.Data.ToList();
                
                var approved = records.Count(r =>
                    r["action"]?.ToString()?.ToLower() == "approved");
                var rejected = records.Count(r =>
                    r["action"]?.ToString()?.ToLower() == "rejected");
                
                var total = approved + rejected;
                if (total == 0)
                    return 0;

                return Math.Round((double)approved / total * 100, 1);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets count of overdue requests exceeding SLA timeout.
        /// Compares request creation time + timeout_hours against current time.
        /// </summary>
        /// <param name="userId">Current manager user ID</param>
        /// <returns>Number of overdue requests</returns>
        public int GetOverdueRequestsCount(Guid userId)
        {
            try
            {
                // Find pending requests
                var query = new EntityQuery("approval_request", "*",
                    EntityQuery.QueryEQ("status", "pending"));
                
                var result = recMan.Find(query);

                if (!result.Success || result.Object?.Data == null)
                    return 0;

                // Filter to overdue requests where user is authorized
                return result.Object.Data.Count(r =>
                    IsUserAuthorizedApprover(userId, r) && IsRequestOverdue(r));
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets recent approval activity for the activity feed.
        /// Returns the most recent N approval actions ordered by timestamp descending.
        /// </summary>
        /// <param name="userId">Current manager user ID</param>
        /// <param name="count">Number of recent items to return</param>
        /// <returns>List of recent activity items</returns>
        public List<RecentActivityItem> GetRecentActivity(Guid userId, int count)
        {
            var activity = new List<RecentActivityItem>();
            
            try
            {
                var query = new EntityQuery("approval_history", "*", null,
                    new[] { new QuerySortObject("performed_on", QuerySortType.Descending) },
                    null, count);
                
                var result = recMan.Find(query);

                if (result.Success && result.Object?.Data != null)
                {
                    foreach (var record in result.Object.Data)
                    {
                        activity.Add(new RecentActivityItem
                        {
                            Action = record["action"]?.ToString() ?? "unknown",
                            PerformedBy = GetPerformerDisplayName(record),
                            PerformedOn = ParseDateTime(record["performed_on"]),
                            RequestId = ParseGuid(record["request_id"]),
                            RequestSubject = GetRequestSubject(record)
                        });
                    }
                }
            }
            catch (Exception)
            {
                // Return empty list if entity doesn't exist
            }

            return activity;
        }

        #region Helper Methods

        /// <summary>
        /// Checks if the specified user is an authorized approver for the request.
        /// Examines the current_step to determine if user is in the approver list.
        /// </summary>
        private bool IsUserAuthorizedApprover(Guid userId, EntityRecord request)
        {
            if (request == null)
                return false;

            // Check if user is explicitly assigned as approver
            var currentApproverId = ParseGuid(request["current_approver_id"]);
            if (currentApproverId == userId)
                return true;

            // Check approver_ids list if available
            var approverIds = request["approver_ids"]?.ToString();
            if (!string.IsNullOrEmpty(approverIds))
            {
                var approverList = approverIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (approverList.Contains(userId.ToString()))
                    return true;
            }

            // Default: allow if user is the assigned approver or no specific assignment
            return currentApproverId == Guid.Empty;
        }

        /// <summary>
        /// Calculates the approval time in hours from the history record.
        /// Uses the difference between request creation and action timestamp.
        /// </summary>
        private double CalculateApprovalTimeHours(EntityRecord history)
        {
            if (history == null)
                return 0;

            var performedOn = ParseDateTime(history["performed_on"]);
            var createdOn = ParseDateTime(history["created_on"]);
            
            // If created_on is not available, use a default time span
            if (createdOn == DateTime.MinValue)
            {
                // Try to get from related request
                var requestId = ParseGuid(history["request_id"]);
                if (requestId != Guid.Empty)
                {
                    try
                    {
                        var requestQuery = new EntityQuery("approval_request", "*",
                            EntityQuery.QueryEQ("id", requestId));
                        var requestResult = recMan.Find(requestQuery);
                        if (requestResult.Success && requestResult.Object?.Data?.Any() == true)
                        {
                            createdOn = ParseDateTime(requestResult.Object.Data.First()["created_on"]);
                        }
                    }
                    catch
                    {
                        // Ignore and use default
                    }
                }
            }

            if (createdOn == DateTime.MinValue || performedOn == DateTime.MinValue)
                return 0;

            var timeSpan = performedOn - createdOn;
            return Math.Max(0, timeSpan.TotalHours);
        }

        /// <summary>
        /// Determines if a request is overdue based on its timeout configuration.
        /// </summary>
        private bool IsRequestOverdue(EntityRecord request)
        {
            if (request == null)
                return false;

            var createdOn = ParseDateTime(request["created_on"]);
            if (createdOn == DateTime.MinValue)
                return false;

            // Get timeout hours from request or use default
            var timeoutHours = DEFAULT_TIMEOUT_HOURS;
            var timeoutValue = request["timeout_hours"];
            if (timeoutValue != null && int.TryParse(timeoutValue.ToString(), out int parsedTimeout))
            {
                timeoutHours = parsedTimeout;
            }

            var deadline = createdOn.AddHours(timeoutHours);
            return DateTime.UtcNow > deadline;
        }

        /// <summary>
        /// Gets the display name for the user who performed an action.
        /// </summary>
        private string GetPerformerDisplayName(EntityRecord record)
        {
            var performedById = ParseGuid(record["performed_by"]);
            var performerName = record["performed_by_name"]?.ToString();
            
            if (!string.IsNullOrEmpty(performerName))
                return performerName;

            if (performedById != Guid.Empty)
            {
                try
                {
                    var userQuery = new EntityQuery("user", "username,email",
                        EntityQuery.QueryEQ("id", performedById));
                    var userResult = recMan.Find(userQuery);
                    if (userResult.Success && userResult.Object?.Data?.Any() == true)
                    {
                        var user = userResult.Object.Data.First();
                        return user["username"]?.ToString() ?? user["email"]?.ToString() ?? "Unknown";
                    }
                }
                catch
                {
                    // Ignore and return default
                }
            }

            return "Unknown";
        }

        /// <summary>
        /// Gets the subject/description of the related approval request.
        /// </summary>
        private string GetRequestSubject(EntityRecord historyRecord)
        {
            var requestId = ParseGuid(historyRecord["request_id"]);
            if (requestId == Guid.Empty)
                return "Approval Request";

            try
            {
                var requestQuery = new EntityQuery("approval_request", "subject,title",
                    EntityQuery.QueryEQ("id", requestId));
                var requestResult = recMan.Find(requestQuery);
                if (requestResult.Success && requestResult.Object?.Data?.Any() == true)
                {
                    var request = requestResult.Object.Data.First();
                    return request["subject"]?.ToString() 
                        ?? request["title"]?.ToString() 
                        ?? "Approval Request";
                }
            }
            catch
            {
                // Ignore and return default
            }

            return "Approval Request";
        }

        /// <summary>
        /// Safely parses a DateTime value from an object.
        /// </summary>
        private DateTime ParseDateTime(object value)
        {
            if (value == null)
                return DateTime.MinValue;

            if (value is DateTime dt)
                return dt;

            if (DateTime.TryParse(value.ToString(), out DateTime parsed))
                return parsed;

            return DateTime.MinValue;
        }

        /// <summary>
        /// Safely parses a Guid value from an object.
        /// </summary>
        private Guid ParseGuid(object value)
        {
            if (value == null)
                return Guid.Empty;

            if (value is Guid guid)
                return guid;

            if (Guid.TryParse(value.ToString(), out Guid parsed))
                return parsed;

            return Guid.Empty;
        }

        #endregion
    }
}
