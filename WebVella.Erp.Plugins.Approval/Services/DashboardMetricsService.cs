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
    /// Service for calculating and aggregating dashboard metrics for the Approval plugin.
    /// Provides real-time KPIs for the manager dashboard including pending count, average approval time,
    /// approval rate, overdue count, and recent activity count.
    /// </summary>
    public class DashboardMetricsService
    {
        private RecordManager _recordManager;

        /// <summary>
        /// Gets the RecordManager instance for database operations.
        /// Lazy-initialized to avoid constructor exceptions when ERP system is not initialized.
        /// </summary>
        protected RecordManager RecMan
        {
            get
            {
                if (_recordManager == null)
                {
                    _recordManager = new RecordManager();
                }
                return _recordManager;
            }
        }

        /// <summary>
        /// Retrieves the dashboard metrics containing real-time KPIs for the approval workflow system.
        /// This overload uses default date range (all time) and no user filtering.
        /// </summary>
        /// <returns>A DashboardMetricsModel containing all KPIs.</returns>
        public DashboardMetricsModel GetDashboardMetrics()
        {
            return GetDashboardMetrics(Guid.Empty, DateTime.MinValue, DateTime.MaxValue);
        }

        /// <summary>
        /// Retrieves the dashboard metrics containing real-time KPIs for the approval workflow system.
        /// Filters metrics by the specified date range and user authorization.
        /// </summary>
        /// <param name="userId">The user ID to filter pending requests by authorization. Use Guid.Empty for no user filtering.</param>
        /// <param name="fromDate">Start date for filtering metrics. Use DateTime.MinValue for no start date filter.</param>
        /// <param name="toDate">End date for filtering metrics. Use DateTime.MaxValue for no end date filter.</param>
        /// <returns>A DashboardMetricsModel containing all KPIs filtered by the specified parameters.</returns>
        public DashboardMetricsModel GetDashboardMetrics(Guid userId, DateTime fromDate, DateTime toDate)
        {
            var result = new DashboardMetricsModel();

            try
            {
                // Calculate pending count - filtered by user authorization if userId specified AND by date range
                // STORY-009 AC4: Pending count reflects requests awaiting user's action
                // STORY-009 AC3: All metrics filter by date range
                result.PendingCount = GetPendingCount(userId, fromDate, toDate);

                // Calculate average approval time in hours - filtered by date range
                result.AverageApprovalTimeHours = GetAverageApprovalTimeHours(fromDate, toDate);

                // Calculate approval rate as percentage of approved vs total completed - filtered by date range
                result.ApprovalRate = GetApprovalRate(fromDate, toDate);

                // Calculate overdue/escalated count - filtered by date range
                // STORY-009 AC3: All metrics filter by date range
                result.OverdueCount = GetOverdueCount(fromDate, toDate);

                // Calculate recent activity count - filtered by date range
                // STORY-009 AC3: All metrics filter by date range
                result.RecentActivityCount = GetRecentActivityCount(fromDate, toDate);

                // Get the last 5 recent activity items - filtered by date range
                // STORY-009 AC3: All metrics filter by date range
                result.RecentActivity = GetRecentActivity(5, fromDate, toDate);
            }
            catch (Exception)
            {
                // On error, return empty metrics model with zeros
                // This allows dashboard to display even if entities don't exist yet
                result.RecentActivity = new List<RecentActivityItem>();
            }

            return result;
        }

        /// <summary>
        /// Gets the count of pending approval requests that the specified user is authorized to approve.
        /// STORY-009 AC4: Filters to requests where user is the authorized approver for the current step.
        /// STORY-009 AC3: Filters by date range when specified.
        /// </summary>
        /// <param name="userId">The user ID to filter by authorization. Use Guid.Empty for all pending requests.</param>
        /// <param name="fromDate">Start date for filtering. Use DateTime.MinValue for no start date filter.</param>
        /// <param name="toDate">End date for filtering. Use DateTime.MaxValue for no end date filter.</param>
        /// <returns>Count of pending requests awaiting the user's action.</returns>
        private int GetPendingCount(Guid userId, DateTime fromDate, DateTime toDate)
        {
            try
            {
                // Get all pending requests with date filtering
                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("status", "pending")
                };
                var eql = "SELECT id, current_step_id FROM approval_request WHERE status = @status";
                
                // Add date filtering - filter by requested_on for pending requests
                if (fromDate > DateTime.MinValue)
                {
                    eqlParams.Add(new EqlParameter("fromDate", fromDate));
                    eql += " AND requested_on >= @fromDate";
                }
                
                if (toDate < DateTime.MaxValue)
                {
                    eqlParams.Add(new EqlParameter("toDate", toDate));
                    eql += " AND requested_on <= @toDate";
                }
                
                var records = new EqlCommand(eql, eqlParams).Execute();

                if (records == null || !records.Any())
                    return 0;

                // If no user filtering, return total count
                if (userId == Guid.Empty)
                    return records.Count;

                // Get user's roles for authorization check
                var userRoles = GetUserRoles(userId);

                // Filter by user authorization
                int count = 0;
                foreach (var record in records)
                {
                    var currentStepId = record["current_step_id"];
                    if (currentStepId == null)
                        continue;

                    Guid stepId;
                    if (currentStepId is Guid g)
                        stepId = g;
                    else if (!Guid.TryParse(currentStepId.ToString(), out stepId))
                        continue;

                    // Check if user is authorized for this step
                    if (IsUserAuthorizedForStep(userId, userRoles, stepId))
                        count++;
                }

                return count;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets the roles assigned to a user.
        /// </summary>
        /// <param name="userId">The user ID to get roles for.</param>
        /// <returns>List of role IDs the user belongs to.</returns>
        private List<Guid> GetUserRoles(Guid userId)
        {
            var roles = new List<Guid>();
            try
            {
                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("user_id", userId)
                };
                // Query user's role assignments from the user_role relation
                var eql = "SELECT id, $user_role.id FROM user WHERE id = @user_id";
                var userRecords = new EqlCommand(eql, eqlParams).Execute();

                if (userRecords != null && userRecords.Any())
                {
                    var user = userRecords.First();
                    var roleData = user["$user_role"];
                    if (roleData is List<EntityRecord> roleRecords)
                    {
                        foreach (var role in roleRecords)
                        {
                            if (role["id"] is Guid roleId)
                                roles.Add(roleId);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Return empty list on error
            }
            return roles;
        }

        /// <summary>
        /// Checks if a user is authorized to approve a specific step.
        /// Authorization is based on: user ID matches step's approver_user_id OR user's role matches step's approver_role_id.
        /// </summary>
        /// <param name="userId">The user ID to check authorization for.</param>
        /// <param name="userRoles">The user's role IDs.</param>
        /// <param name="stepId">The step ID to check authorization against.</param>
        /// <returns>True if the user is authorized; otherwise, false.</returns>
        private bool IsUserAuthorizedForStep(Guid userId, List<Guid> userRoles, Guid stepId)
        {
            try
            {
                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("step_id", stepId)
                };
                var eql = "SELECT id, approver_type, approver_id FROM approval_step WHERE id = @step_id";
                var stepRecords = new EqlCommand(eql, eqlParams).Execute();

                if (stepRecords == null || !stepRecords.Any())
                    return false;

                var step = stepRecords.First();
                var approverType = step["approver_type"]?.ToString()?.ToLowerInvariant();

                switch (approverType)
                {
                    case "user":
                        // Check if user ID matches approver_id
                        var approverId = step["approver_id"];
                        if (approverId != null)
                        {
                            Guid approverUserId;
                            if (approverId is Guid g)
                                approverUserId = g;
                            else if (Guid.TryParse(approverId.ToString(), out approverUserId))
                            { }
                            else
                                return false;

                            return approverUserId == userId;
                        }
                        break;

                    case "role":
                        // Check if user's role matches approver_id (which stores role ID when approver_type is "role")
                        var roleId = step["approver_id"];
                        if (roleId != null)
                        {
                            Guid approverRoleId;
                            if (roleId is Guid g)
                                approverRoleId = g;
                            else if (Guid.TryParse(roleId.ToString(), out approverRoleId))
                            { }
                            else
                                return false;

                            return userRoles.Contains(approverRoleId);
                        }
                        break;

                    case "department_head":
                        // For department head type, check if user has manager/admin role
                        // This is a simplified check - real implementation may need department hierarchy
                        return userRoles.Any(); // Any role is sufficient for now
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Calculates the average approval time in hours for completed requests within the specified date range.
        /// STORY-009 AC3: Filters by date range when specified.
        /// </summary>
        /// <param name="fromDate">Start date for filtering. Use DateTime.MinValue for no start date filter.</param>
        /// <param name="toDate">End date for filtering. Use DateTime.MaxValue for no end date filter.</param>
        /// <returns>Average approval time in hours.</returns>
        private decimal GetAverageApprovalTimeHours(DateTime fromDate, DateTime toDate)
        {
            try
            {
                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("status", "approved")
                };
                
                // Build query with date filtering if specified
                var eql = "SELECT id, requested_on, completed_on FROM approval_request WHERE status = @status";
                
                if (fromDate > DateTime.MinValue)
                {
                    eqlParams.Add(new EqlParameter("fromDate", fromDate));
                    eql += " AND completed_on >= @fromDate";
                }
                
                if (toDate < DateTime.MaxValue)
                {
                    eqlParams.Add(new EqlParameter("toDate", toDate));
                    eql += " AND completed_on <= @toDate";
                }
                
                var records = new EqlCommand(eql, eqlParams).Execute();

                if (records == null || !records.Any())
                    return 0m;

                decimal totalHours = 0m;
                int count = 0;

                foreach (var record in records)
                {
                    DateTime? requestedOn = null;
                    DateTime? completedOn = null;
                    
                    // Handle different possible types from EQL result
                    if (record["requested_on"] != null)
                    {
                        if (record["requested_on"] is DateTime dt)
                            requestedOn = dt;
                        else if (DateTime.TryParse(record["requested_on"].ToString(), out DateTime parsed))
                            requestedOn = parsed;
                    }
                    
                    if (record["completed_on"] != null)
                    {
                        if (record["completed_on"] is DateTime dt)
                            completedOn = dt;
                        else if (DateTime.TryParse(record["completed_on"].ToString(), out DateTime parsed))
                            completedOn = parsed;
                    }

                    if (requestedOn.HasValue && completedOn.HasValue)
                    {
                        var timeSpan = completedOn.Value - requestedOn.Value;
                        totalHours += (decimal)timeSpan.TotalHours;
                        count++;
                    }
                }

                if (count == 0)
                    return 0m;

                return Math.Round(totalHours / count, 1);
            }
            catch (Exception)
            {
                return 0m;
            }
        }

        /// <summary>
        /// Calculates the approval rate as a percentage of approved requests vs total completed requests
        /// within the specified date range.
        /// STORY-009 AC3: Filters by date range when specified.
        /// </summary>
        /// <param name="fromDate">Start date for filtering. Use DateTime.MinValue for no start date filter.</param>
        /// <param name="toDate">End date for filtering. Use DateTime.MaxValue for no end date filter.</param>
        /// <returns>Approval rate percentage (0-100).</returns>
        private decimal GetApprovalRate(DateTime fromDate, DateTime toDate)
        {
            try
            {
                // Build date filter clause if needed
                var dateFilterClause = "";
                var approvedParams = new List<EqlParameter>
                {
                    new EqlParameter("status", "approved")
                };
                var rejectedParams = new List<EqlParameter>
                {
                    new EqlParameter("status", "rejected")
                };
                
                if (fromDate > DateTime.MinValue)
                {
                    approvedParams.Add(new EqlParameter("fromDate", fromDate));
                    rejectedParams.Add(new EqlParameter("fromDate", fromDate));
                    dateFilterClause += " AND completed_on >= @fromDate";
                }
                
                if (toDate < DateTime.MaxValue)
                {
                    approvedParams.Add(new EqlParameter("toDate", toDate));
                    rejectedParams.Add(new EqlParameter("toDate", toDate));
                    dateFilterClause += " AND completed_on <= @toDate";
                }

                // Get approved count
                var approvedEql = "SELECT id FROM approval_request WHERE status = @status" + dateFilterClause;
                var approvedRecords = new EqlCommand(approvedEql, approvedParams).Execute();
                var approvedCount = approvedRecords?.Count ?? 0;

                // Get rejected count
                var rejectedEql = "SELECT id FROM approval_request WHERE status = @status" + dateFilterClause;
                var rejectedRecords = new EqlCommand(rejectedEql, rejectedParams).Execute();
                var rejectedCount = rejectedRecords?.Count ?? 0;

                var totalCompleted = approvedCount + rejectedCount;

                if (totalCompleted == 0)
                    return 0m;

                return Math.Round((decimal)approvedCount * 100 / totalCompleted, 1);
            }
            catch (Exception)
            {
                return 0m;
            }
        }

        /// <summary>
        /// Gets the count of overdue or escalated approval requests.
        /// Counts both explicitly escalated/expired requests AND pending requests that have exceeded their step timeout.
        /// STORY-009 AC3: Filters by date range when specified.
        /// </summary>
        /// <param name="fromDate">Start date for filtering. Use DateTime.MinValue for no start date filter.</param>
        /// <param name="toDate">End date for filtering. Use DateTime.MaxValue for no end date filter.</param>
        /// <returns>Count of overdue/escalated requests.</returns>
        private int GetOverdueCount(DateTime fromDate, DateTime toDate)
        {
            try
            {
                int count = 0;

                // Build date filter clause for escalated/expired status queries
                var dateFilterClause = "";
                var statusParams = new List<EqlParameter>
                {
                    new EqlParameter("escalated", "escalated"),
                    new EqlParameter("expired", "expired")
                };
                
                if (fromDate > DateTime.MinValue)
                {
                    statusParams.Add(new EqlParameter("fromDate", fromDate));
                    dateFilterClause += " AND requested_on >= @fromDate";
                }
                
                if (toDate < DateTime.MaxValue)
                {
                    statusParams.Add(new EqlParameter("toDate", toDate));
                    dateFilterClause += " AND requested_on <= @toDate";
                }
                
                // Count requests with escalated or expired status
                var statusEql = "SELECT id FROM approval_request WHERE (status = @escalated OR status = @expired)" + dateFilterClause;
                var statusRecords = new EqlCommand(statusEql, statusParams).Execute();
                count += statusRecords?.Count ?? 0;

                // Also count pending requests that have exceeded their step timeout
                // This requires checking approval_request.requested_on + approval_step.timeout_hours < now
                var pendingParams = new List<EqlParameter>
                {
                    new EqlParameter("status", "pending")
                };
                var pendingEql = "SELECT id, requested_on, current_step_id FROM approval_request WHERE status = @status";
                
                // Add date filter for pending requests too
                if (fromDate > DateTime.MinValue)
                {
                    pendingParams.Add(new EqlParameter("fromDate", fromDate));
                    pendingEql += " AND requested_on >= @fromDate";
                }
                
                if (toDate < DateTime.MaxValue)
                {
                    pendingParams.Add(new EqlParameter("toDate", toDate));
                    pendingEql += " AND requested_on <= @toDate";
                }
                
                var pendingRecords = new EqlCommand(pendingEql, pendingParams).Execute();

                if (pendingRecords != null && pendingRecords.Any())
                {
                    var now = DateTime.UtcNow;
                    var stepIds = pendingRecords
                        .Where(r => r["current_step_id"] != null)
                        .Select(r => (Guid)r["current_step_id"])
                        .Distinct()
                        .ToList();

                    // Get timeout_hours for all relevant steps
                    var stepTimeouts = new Dictionary<Guid, decimal?>();
                    foreach (var stepId in stepIds)
                    {
                        try
                        {
                            var stepParams = new List<EqlParameter>
                            {
                                new EqlParameter("step_id", stepId)
                            };
                            var stepEql = "SELECT id, timeout_hours FROM approval_step WHERE id = @step_id";
                            var stepRecords = new EqlCommand(stepEql, stepParams).Execute();
                            if (stepRecords != null && stepRecords.Any())
                            {
                                var timeoutValue = stepRecords.First()["timeout_hours"];
                                stepTimeouts[stepId] = timeoutValue as decimal?;
                            }
                        }
                        catch
                        {
                            // Skip if step not found
                        }
                    }

                    // Count pending requests that have exceeded their timeout
                    foreach (var record in pendingRecords)
                    {
                        DateTime? requestedOn = null;
                        Guid? currentStepId = null;
                        
                        // Handle DateTime parsing
                        if (record["requested_on"] != null)
                        {
                            if (record["requested_on"] is DateTime dt)
                                requestedOn = dt;
                            else if (DateTime.TryParse(record["requested_on"].ToString(), out DateTime parsed))
                                requestedOn = parsed;
                        }
                        
                        // Handle Guid parsing
                        if (record["current_step_id"] != null)
                        {
                            if (record["current_step_id"] is Guid g)
                                currentStepId = g;
                            else if (Guid.TryParse(record["current_step_id"].ToString(), out Guid parsedGuid))
                                currentStepId = parsedGuid;
                        }

                        if (requestedOn.HasValue && currentStepId.HasValue && stepTimeouts.ContainsKey(currentStepId.Value))
                        {
                            var timeoutHours = stepTimeouts[currentStepId.Value];
                            if (timeoutHours.HasValue && timeoutHours.Value > 0)
                            {
                                var deadline = requestedOn.Value.AddHours((double)timeoutHours.Value);
                                if (now > deadline)
                                {
                                    count++;
                                }
                            }
                        }
                    }
                }

                return count;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets the count of recent approval activity within the specified date range.
        /// STORY-009 AC3: Filters by date range when specified.
        /// </summary>
        /// <param name="fromDate">Start date for filtering. Use DateTime.MinValue for no start date filter (defaults to last 24 hours).</param>
        /// <param name="toDate">End date for filtering. Use DateTime.MaxValue for no end date filter.</param>
        /// <returns>Count of recent activity items.</returns>
        private int GetRecentActivityCount(DateTime fromDate, DateTime toDate)
        {
            try
            {
                var eqlParams = new List<EqlParameter>();
                var eql = "SELECT id FROM approval_history WHERE 1=1";
                
                // Apply date filter - use provided dates or default to last 24 hours if no dates specified
                if (fromDate > DateTime.MinValue)
                {
                    eqlParams.Add(new EqlParameter("fromDate", fromDate));
                    eql += " AND performed_on >= @fromDate";
                }
                else
                {
                    // Default to last 24 hours when no date range specified
                    var cutoffTime = DateTime.UtcNow.AddHours(-24);
                    eqlParams.Add(new EqlParameter("fromDate", cutoffTime));
                    eql += " AND performed_on >= @fromDate";
                }
                
                if (toDate < DateTime.MaxValue)
                {
                    eqlParams.Add(new EqlParameter("toDate", toDate));
                    eql += " AND performed_on <= @toDate";
                }
                
                var records = new EqlCommand(eql, eqlParams).Execute();
                return records?.Count ?? 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets the most recent approval activity items filtered by date range.
        /// STORY-009 Issue 3: Must display list of recent activities with action type, performer, time, and links.
        /// STORY-009 AC3: Filters by date range when specified.
        /// </summary>
        /// <param name="count">Maximum number of activity items to return.</param>
        /// <param name="fromDate">Start date for filtering. Use DateTime.MinValue for no start date filter.</param>
        /// <param name="toDate">End date for filtering. Use DateTime.MaxValue for no end date filter.</param>
        /// <returns>List of recent activity items.</returns>
        private List<RecentActivityItem> GetRecentActivity(int count, DateTime fromDate, DateTime toDate)
        {
            var result = new List<RecentActivityItem>();
            
            try
            {
                // Build EQL query with date filtering
                // Query for the most recent activity items, ordered by most recent first
                // Join with user to get performer details
                var eqlParams = new List<EqlParameter>();
                var whereClause = "";
                
                // Apply date filter for performed_on
                if (fromDate > DateTime.MinValue)
                {
                    eqlParams.Add(new EqlParameter("fromDate", fromDate));
                    whereClause += " WHERE performed_on >= @fromDate";
                }
                
                if (toDate < DateTime.MaxValue)
                {
                    eqlParams.Add(new EqlParameter("toDate", toDate));
                    if (string.IsNullOrEmpty(whereClause))
                        whereClause = " WHERE performed_on <= @toDate";
                    else
                        whereClause += " AND performed_on <= @toDate";
                }
                
                var eql = $@"SELECT id, request_id, step_id, action, performed_by, performed_on, comments,
                           $user_1n_history_performed_by.username, $user_1n_history_performed_by.first_name, $user_1n_history_performed_by.last_name
                           FROM approval_history{whereClause}
                           ORDER BY performed_on DESC";
                
                var records = new EqlCommand(eql, eqlParams).Execute();
                
                if (records != null && records.Any())
                {
                    // Take only the specified count
                    var recentRecords = records.Take(count).ToList();
                    
                    foreach (var record in recentRecords)
                    {
                        var item = new RecentActivityItem();
                        
                        // Parse ID
                        if (record["id"] != null)
                        {
                            if (record["id"] is Guid g)
                                item.Id = g;
                            else if (Guid.TryParse(record["id"].ToString(), out Guid parsed))
                                item.Id = parsed;
                        }
                        
                        // Parse request ID
                        if (record["request_id"] != null)
                        {
                            if (record["request_id"] is Guid g)
                                item.RequestId = g;
                            else if (Guid.TryParse(record["request_id"].ToString(), out Guid parsed))
                                item.RequestId = parsed;
                        }
                        
                        // Parse action type
                        item.ActionType = record["action"]?.ToString() ?? "unknown";
                        
                        // Parse performer details
                        if (record["performed_by"] != null)
                        {
                            if (record["performed_by"] is Guid g)
                                item.PerformedById = g;
                            else if (Guid.TryParse(record["performed_by"].ToString(), out Guid parsed))
                                item.PerformedById = parsed;
                        }
                        
                        // Build performer name from user details (via user_1n_history_performed_by relation)
                        // The relation returns a List<EntityRecord> or EntityRecord - handle both cases
                        var firstName = "";
                        var lastName = "";
                        var username = "";

                        var userData = record["$user_1n_history_performed_by"];

                        if (userData is List<EntityRecord> userRecords && userRecords.Any())
                        {
                            var userRecord = userRecords.First();
                            firstName = userRecord["first_name"]?.ToString() ?? "";
                            lastName = userRecord["last_name"]?.ToString() ?? "";
                            username = userRecord["username"]?.ToString() ?? "";
                        }
                        else if (userData is EntityRecord singleUserRecord)
                        {
                            firstName = singleUserRecord["first_name"]?.ToString() ?? "";
                            lastName = singleUserRecord["last_name"]?.ToString() ?? "";
                            username = singleUserRecord["username"]?.ToString() ?? "";
                        }
                        
                        if (!string.IsNullOrWhiteSpace(firstName) || !string.IsNullOrWhiteSpace(lastName))
                        {
                            item.PerformedByName = $"{firstName} {lastName}".Trim();
                        }
                        else if (!string.IsNullOrWhiteSpace(username))
                        {
                            item.PerformedByName = username;
                        }
                        else
                        {
                            item.PerformedByName = "Unknown User";
                        }
                        
                        // Parse timestamp
                        if (record["performed_on"] != null)
                        {
                            if (record["performed_on"] is DateTime dt)
                                item.PerformedOn = dt;
                            else if (DateTime.TryParse(record["performed_on"].ToString(), out DateTime parsed))
                                item.PerformedOn = parsed;
                        }
                        
                        // Parse comments
                        item.Comments = record["comments"]?.ToString();
                        
                        // Build request link - this would be the URL to view the request
                        item.RequestLink = $"/approval/request/{item.RequestId}";
                        
                        result.Add(item);
                    }
                }
            }
            catch (Exception)
            {
                // Return empty list on error
            }
            
            return result;
        }
    }
}
