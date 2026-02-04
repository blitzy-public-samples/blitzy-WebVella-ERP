using System;
using System.Collections.Generic;
using System.Linq;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Eql;
using WebVella.Erp.Plugins.Approval.Model;

namespace WebVella.Erp.Plugins.Approval.Services
{
    /// <summary>
    /// Service class for calculating real-time dashboard metrics from approval_request and approval_history entities.
    /// Provides aggregated metrics including pending approvals count, average approval time, approval rate percentage,
    /// overdue requests count, total active workflows, and recent activity feed for the manager dashboard display.
    /// </summary>
    /// <remarks>
    /// This service is used by PcApprovalDashboard component and ApprovalController to render real-time metric cards.
    /// 
    /// Key metrics provided:
    /// - PendingCount: Number of approval requests awaiting action by the specified user
    /// - OverdueCount: Number of pending requests that have exceeded their SLA timeout
    /// - AverageApprovalTimeHours: Mean time from request creation to completion
    /// - ApprovalRate: Percentage of approved vs total completed requests
    /// - TotalActiveWorkflows: Count of enabled workflow configurations
    /// - RecentActivity: Latest approval actions for activity feed display
    /// 
    /// All methods include error handling to gracefully return default values when entities
    /// don't exist or queries fail, ensuring the dashboard remains functional during initial setup.
    /// 
    /// The service uses ApprovalRouteService to determine user authorization for steps,
    /// enabling user-specific filtering of pending and overdue counts.
    /// </remarks>
    public class DashboardMetricsService : BaseService
    {
        #region Constants

        /// <summary>
        /// Entity name for approval requests.
        /// </summary>
        private const string ENTITY_REQUEST = "approval_request";

        /// <summary>
        /// Entity name for approval history.
        /// </summary>
        private const string ENTITY_HISTORY = "approval_history";

        /// <summary>
        /// Entity name for approval workflows.
        /// </summary>
        private const string ENTITY_WORKFLOW = "approval_workflow";

        /// <summary>
        /// Entity name for approval steps.
        /// </summary>
        private const string ENTITY_STEP = "approval_step";

        /// <summary>
        /// Field name for record ID.
        /// </summary>
        private const string FIELD_ID = "id";

        /// <summary>
        /// Field name for status.
        /// </summary>
        private const string FIELD_STATUS = "status";

        /// <summary>
        /// Field name for created_on timestamp.
        /// </summary>
        private const string FIELD_CREATED_ON = "created_on";

        /// <summary>
        /// Field name for completed_on timestamp.
        /// </summary>
        private const string FIELD_COMPLETED_ON = "completed_on";

        /// <summary>
        /// Field name for current step ID.
        /// </summary>
        private const string FIELD_CURRENT_STEP_ID = "current_step_id";

        /// <summary>
        /// Field name for is_active flag.
        /// </summary>
        private const string FIELD_IS_ACTIVE = "is_active";

        /// <summary>
        /// Field name for action_type in history.
        /// </summary>
        private const string FIELD_ACTION_TYPE = "action_type";

        /// <summary>
        /// Field name for performed_on timestamp.
        /// </summary>
        private const string FIELD_PERFORMED_ON = "performed_on";

        /// <summary>
        /// Field name for performed_by user ID.
        /// </summary>
        private const string FIELD_PERFORMED_BY = "performed_by";

        /// <summary>
        /// Field name for request_id foreign key.
        /// </summary>
        private const string FIELD_REQUEST_ID = "request_id";

        /// <summary>
        /// Field name for comments.
        /// </summary>
        private const string FIELD_COMMENTS = "comments";

        /// <summary>
        /// Field name for SLA hours on approval_step.
        /// </summary>
        private const string FIELD_SLA_HOURS = "sla_hours";

        /// <summary>
        /// Default SLA timeout in hours when step configuration is unavailable.
        /// </summary>
        private const int DEFAULT_SLA_HOURS = 24;

        #endregion

        #region Private Fields

        /// <summary>
        /// Route service for determining user authorization on approval steps.
        /// Used by IsUserAuthorizedApprover to check if a user can act on a pending request.
        /// </summary>
        private readonly ApprovalRouteService _routeService;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the DashboardMetricsService class.
        /// Creates the internal ApprovalRouteService instance for authorization checks.
        /// </summary>
        public DashboardMetricsService()
        {
            _routeService = new ApprovalRouteService();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets comprehensive dashboard metrics aggregated for a specific user within a date range.
        /// Assembles all individual metrics into a single DashboardMetricsModel response.
        /// </summary>
        /// <param name="userId">The user ID for user-specific metrics (pending/overdue counts).</param>
        /// <param name="fromDate">Start date for time-based metrics (average approval time, approval rate).</param>
        /// <param name="toDate">End date for time-based metrics.</param>
        /// <returns>
        /// DashboardMetricsModel containing all aggregated metrics including:
        /// - PendingCount: Requests awaiting action by the specified user
        /// - OverdueCount: Requests past their SLA deadline
        /// - AverageApprovalTimeHours: Mean completion time in hours
        /// - ApprovalRate: Percentage of approved requests
        /// - TotalActiveWorkflows: Count of active workflow configurations
        /// - ApprovedTodayCount: Number of approvals today
        /// - RejectedTodayCount: Number of rejections today
        /// </returns>
        /// <remarks>
        /// This method aggregates all individual metric calculations into a single response object.
        /// Each individual metric method handles its own error conditions and returns safe default values.
        /// The date range parameters are stored in the response for reference by consuming components.
        /// </remarks>
        public DashboardMetricsModel GetDashboardMetrics(Guid userId, DateTime fromDate, DateTime toDate)
        {
            return new DashboardMetricsModel
            {
                PendingCount = GetPendingCount(userId),
                OverdueCount = GetOverdueCount(userId),
                AverageApprovalTimeHours = (decimal)GetAverageApprovalTime(fromDate, toDate),
                ApprovalRate = (decimal)GetApprovalRate(fromDate, toDate),
                TotalActiveWorkflows = GetTotalActiveWorkflows(),
                ApprovedTodayCount = GetApprovedTodayCount(),
                RejectedTodayCount = GetRejectedTodayCount(),
                StartDate = fromDate,
                EndDate = toDate
            };
        }

        /// <summary>
        /// Gets the count of pending approval requests where the specified user is an authorized approver.
        /// Queries approval_request with status='pending' and filters by user authorization on the current step.
        /// </summary>
        /// <param name="userId">The user ID to check authorization for. If null, returns 0.</param>
        /// <returns>
        /// Count of pending approval requests where the user is authorized to approve.
        /// Returns 0 if userId is null, no pending requests exist, or if an error occurs.
        /// </returns>
        /// <remarks>
        /// This method:
        /// 1. Returns 0 immediately if userId is null (requires authenticated user)
        /// 2. Queries all pending approval requests
        /// 3. For each request, checks if the user is an authorized approver via IsUserAuthorizedApprover
        /// 4. Returns the count of requests where the user can take action
        /// 
        /// Uses ApprovalRouteService.GetApproversForStep() to resolve authorized approvers for each step.
        /// </remarks>
        public int GetPendingCount(Guid? userId)
        {
            try
            {
                // If no user is specified, return 0 (user must be authenticated to see pending approvals)
                if (!userId.HasValue)
                {
                    return 0;
                }

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter(FIELD_STATUS, ApprovalStatus.Pending.ToString().ToLowerInvariant())
                };

                string eql = $"SELECT {FIELD_ID}, {FIELD_CURRENT_STEP_ID} FROM {ENTITY_REQUEST} WHERE {FIELD_STATUS} = @{FIELD_STATUS}";
                var result = new EqlCommand(eql, eqlParams).Execute();

                if (result == null || !result.Any())
                {
                    return 0;
                }

                // Filter by user authorization on current step
                int authorizedCount = 0;
                foreach (var request in result)
                {
                    if (IsUserAuthorizedApprover(userId.Value, request))
                    {
                        authorizedCount++;
                    }
                }

                return authorizedCount;
            }
            catch (Exception)
            {
                // Entity may not exist yet during initial setup, return default value
                return 0;
            }
        }

        /// <summary>
        /// Calculates the average approval time in hours for completed requests within a date range.
        /// Considers both approved and rejected requests as "completed" for this calculation.
        /// </summary>
        /// <param name="fromDate">Start of the date range for filtering completed requests.</param>
        /// <param name="toDate">End of the date range for filtering completed requests.</param>
        /// <returns>
        /// Average approval time in hours, rounded to 1 decimal place.
        /// Returns 0.0 if no completed requests exist within the date range or if an error occurs.
        /// </returns>
        /// <remarks>
        /// Calculation formula:
        /// 1. Query all requests with status = approved OR rejected within the date range
        /// 2. For each request, calculate hours between created_on and completed_on
        /// 3. Return the arithmetic mean of all durations
        /// 
        /// The completed_on field is expected to be set when the request reaches a terminal status.
        /// If completed_on is not available, the calculation uses DateTime.UtcNow as a fallback.
        /// </remarks>
        public double GetAverageApprovalTime(DateTime fromDate, DateTime toDate)
        {
            try
            {
                var statusApproved = ApprovalStatus.Approved.ToString().ToLowerInvariant();
                var statusRejected = ApprovalStatus.Rejected.ToString().ToLowerInvariant();

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("from_date", fromDate),
                    new EqlParameter("to_date", toDate),
                    new EqlParameter("status_approved", statusApproved),
                    new EqlParameter("status_rejected", statusRejected)
                };

                // Get completed requests within date range
                string eql = $@"SELECT {FIELD_ID}, {FIELD_CREATED_ON}, {FIELD_COMPLETED_ON} FROM {ENTITY_REQUEST} 
                               WHERE ({FIELD_STATUS} = @status_approved OR {FIELD_STATUS} = @status_rejected)
                               AND {FIELD_CREATED_ON} >= @from_date AND {FIELD_CREATED_ON} <= @to_date";
                var requests = new EqlCommand(eql, eqlParams).Execute();

                if (requests == null || !requests.Any())
                {
                    return 0.0;
                }

                double totalHours = 0;
                int validCount = 0;

                foreach (var request in requests)
                {
                    double hours = CalculateApprovalTimeHours(request);
                    if (hours > 0)
                    {
                        totalHours += hours;
                        validCount++;
                    }
                }

                if (validCount == 0)
                {
                    return 0.0;
                }

                return Math.Round(totalHours / validCount, 1);
            }
            catch (Exception)
            {
                // Entity may not exist yet during initial setup, return default value
                return 0.0;
            }
        }

        /// <summary>
        /// Calculates the approval rate as a percentage of approved vs total completed requests within a date range.
        /// Uses approval_history records to count approved and rejected actions.
        /// </summary>
        /// <param name="fromDate">Start of the date range for filtering history records.</param>
        /// <param name="toDate">End of the date range for filtering history records.</param>
        /// <returns>
        /// Approval rate as a percentage (0-100), rounded to 1 decimal place.
        /// Returns 0.0 if no completed requests exist within the date range or if an error occurs.
        /// </returns>
        /// <remarks>
        /// Calculation formula: (approved_count / (approved_count + rejected_count)) * 100
        /// 
        /// This method queries approval_history (not approval_request) to count actual approval/rejection
        /// actions, providing an accurate view of decision patterns within the specified period.
        /// 
        /// Note: A single request may have multiple history entries if it was escalated or delegated,
        /// so this measures action frequency rather than unique request outcomes.
        /// </remarks>
        public double GetApprovalRate(DateTime fromDate, DateTime toDate)
        {
            try
            {
                var actionApproved = ApprovalActionType.Approve.ToString().ToLowerInvariant();
                var actionRejected = ApprovalActionType.Reject.ToString().ToLowerInvariant();

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("from_date", fromDate),
                    new EqlParameter("to_date", toDate),
                    new EqlParameter("action_approved", actionApproved),
                    new EqlParameter("action_rejected", actionRejected)
                };

                // Count approved actions within date range
                string approvedEql = $@"SELECT {FIELD_ID} FROM {ENTITY_HISTORY} 
                                       WHERE {FIELD_ACTION_TYPE} = @action_approved 
                                       AND {FIELD_PERFORMED_ON} >= @from_date AND {FIELD_PERFORMED_ON} <= @to_date";
                var approvedResult = new EqlCommand(approvedEql, eqlParams).Execute();
                int approvedCount = approvedResult?.Count ?? 0;

                // Count rejected actions within date range
                string rejectedEql = $@"SELECT {FIELD_ID} FROM {ENTITY_HISTORY} 
                                       WHERE {FIELD_ACTION_TYPE} = @action_rejected 
                                       AND {FIELD_PERFORMED_ON} >= @from_date AND {FIELD_PERFORMED_ON} <= @to_date";
                var rejectedResult = new EqlCommand(rejectedEql, eqlParams).Execute();
                int rejectedCount = rejectedResult?.Count ?? 0;

                int totalDecisions = approvedCount + rejectedCount;
                if (totalDecisions == 0)
                {
                    return 0.0;
                }

                return Math.Round((double)approvedCount / totalDecisions * 100, 1);
            }
            catch (Exception)
            {
                // Entity may not exist yet during initial setup, return default value
                return 0.0;
            }
        }

        /// <summary>
        /// Gets the count of overdue approval requests where the specified user is an authorized approver.
        /// A request is considered overdue when it has been pending longer than the step's SLA hours.
        /// </summary>
        /// <param name="userId">The user ID to check authorization for. If null, returns 0.</param>
        /// <returns>
        /// Count of overdue pending approval requests where the user is authorized to approve.
        /// Returns 0 if userId is null, no overdue requests exist, or if an error occurs.
        /// </returns>
        /// <remarks>
        /// This method:
        /// 1. Returns 0 immediately if userId is null (requires authenticated user)
        /// 2. Queries all pending approval requests
        /// 3. For each request, checks if the user is authorized via IsUserAuthorizedApprover
        /// 4. For authorized requests, checks if created_on + sla_hours has been exceeded
        /// 5. Returns count of both authorized AND overdue requests
        /// 
        /// The SLA hours are read from the approval_step entity for each request's current step.
        /// If step configuration is unavailable, a default of 24 hours is used.
        /// </remarks>
        public int GetOverdueCount(Guid? userId)
        {
            try
            {
                // If no user is specified, return 0 (user must be authenticated to see overdue approvals)
                if (!userId.HasValue)
                {
                    return 0;
                }

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter(FIELD_STATUS, ApprovalStatus.Pending.ToString().ToLowerInvariant())
                };

                // Get all pending requests with their creation time and current step
                string eql = $@"SELECT {FIELD_ID}, {FIELD_CREATED_ON}, {FIELD_CURRENT_STEP_ID} 
                               FROM {ENTITY_REQUEST} WHERE {FIELD_STATUS} = @{FIELD_STATUS}";
                var requests = new EqlCommand(eql, eqlParams).Execute();

                if (requests == null || !requests.Any())
                {
                    return 0;
                }

                int overdueCount = 0;
                foreach (var request in requests)
                {
                    // Check user authorization first
                    if (IsUserAuthorizedApprover(userId.Value, request))
                    {
                        // Then check if overdue
                        if (IsRequestOverdue(request))
                        {
                            overdueCount++;
                        }
                    }
                }

                return overdueCount;
            }
            catch (Exception)
            {
                // Entity may not exist yet during initial setup, return default value
                return 0;
            }
        }

        /// <summary>
        /// Gets the count of active approval workflows in the system.
        /// An active workflow has is_active = true and is available for processing new requests.
        /// </summary>
        /// <returns>
        /// Count of active approval workflows.
        /// Returns 0 if no workflows exist or if an error occurs.
        /// </returns>
        /// <remarks>
        /// This is a simple count query against the approval_workflow entity.
        /// Used to display the total number of configured and enabled workflows on the dashboard.
        /// </remarks>
        public int GetTotalActiveWorkflows()
        {
            try
            {
                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter(FIELD_IS_ACTIVE, true)
                };

                string eql = $"SELECT {FIELD_ID} FROM {ENTITY_WORKFLOW} WHERE {FIELD_IS_ACTIVE} = @{FIELD_IS_ACTIVE}";
                var result = new EqlCommand(eql, eqlParams).Execute();

                return result?.Count ?? 0;
            }
            catch (Exception)
            {
                // Entity may not exist yet during initial setup, return default value
                return 0;
            }
        }

        /// <summary>
        /// Gets recent approval activity records for display in the dashboard activity feed.
        /// Returns the most recent approval history entries ordered by timestamp descending.
        /// </summary>
        /// <param name="count">Maximum number of activity records to return. Default is 5.</param>
        /// <returns>
        /// List of EntityRecord objects containing recent approval history data including:
        /// - id: History record ID
        /// - request_id: Associated approval request ID
        /// - action_type: Type of action (approve, reject, delegate, etc.)
        /// - performed_by: User ID who performed the action
        /// - performed_on: Timestamp of the action
        /// - comments: Optional comments associated with the action
        /// 
        /// Returns empty list if no history exists or if an error occurs.
        /// </returns>
        /// <remarks>
        /// This method provides data for the activity feed panel on the manager dashboard.
        /// The results are ordered by performed_on descending to show most recent activity first.
        /// Pagination is applied using EQL PAGE/PAGESIZE syntax to limit the result set.
        /// </remarks>
        public List<EntityRecord> GetRecentActivity(int count = 5)
        {
            try
            {
                // Ensure count is within reasonable bounds
                if (count <= 0)
                {
                    count = 5;
                }
                if (count > 100)
                {
                    count = 100;
                }

                var eqlParams = new List<EqlParameter>();

                string eql = $@"SELECT {FIELD_ID}, {FIELD_REQUEST_ID}, {FIELD_ACTION_TYPE}, 
                               {FIELD_PERFORMED_BY}, {FIELD_PERFORMED_ON}, {FIELD_COMMENTS} 
                               FROM {ENTITY_HISTORY} 
                               ORDER BY {FIELD_PERFORMED_ON} DESC 
                               PAGE 1 PAGESIZE {count}";
                var result = new EqlCommand(eql, eqlParams).Execute();

                return result?.ToList() ?? new List<EntityRecord>();
            }
            catch (Exception)
            {
                // Entity may not exist yet during initial setup, return empty list
                return new List<EntityRecord>();
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Checks if a user is authorized to act as an approver for a specific approval request.
        /// Uses ApprovalRouteService to resolve the list of authorized approvers for the request's current step.
        /// </summary>
        /// <param name="userId">The user ID to check authorization for.</param>
        /// <param name="request">The approval request EntityRecord to check against.</param>
        /// <returns>
        /// True if the user is in the list of authorized approvers for the request's current step.
        /// Returns false if the request has no current step, step lookup fails, or user is not authorized.
        /// </returns>
        /// <remarks>
        /// Authorization is determined by:
        /// 1. Getting the current_step_id from the request
        /// 2. Calling ApprovalRouteService.GetApproversForStep() to get authorized user IDs
        /// 3. Checking if the specified userId is in the returned list
        /// 
        /// This method is used by GetPendingCount and GetOverdueCount to filter requests
        /// to only those the specified user can act upon.
        /// </remarks>
        private bool IsUserAuthorizedApprover(Guid userId, EntityRecord request)
        {
            try
            {
                if (request == null)
                {
                    return false;
                }

                // Get current step ID from request
                if (!request.Properties.ContainsKey(FIELD_CURRENT_STEP_ID))
                {
                    return false;
                }

                var currentStepId = request[FIELD_CURRENT_STEP_ID] as Guid?;
                if (!currentStepId.HasValue)
                {
                    return false;
                }

                // Get authorized approvers for this step using ApprovalRouteService
                List<Guid> authorizedApprovers = _routeService.GetApproversForStep(currentStepId.Value);

                // Check if user is in the list of authorized approvers
                return authorizedApprovers != null && authorizedApprovers.Contains(userId);
            }
            catch (Exception)
            {
                // If authorization check fails, default to not authorized
                return false;
            }
        }

        /// <summary>
        /// Calculates the time in hours from request creation to completion.
        /// Used by GetAverageApprovalTime to compute individual request durations.
        /// </summary>
        /// <param name="request">The completed approval request EntityRecord.</param>
        /// <returns>
        /// Hours between created_on and completed_on timestamps.
        /// Returns 0.0 if either timestamp is missing or invalid.
        /// </returns>
        /// <remarks>
        /// The method expects the request to have:
        /// - created_on: DateTime when the request was created
        /// - completed_on: DateTime when the request reached terminal status
        /// 
        /// If completed_on is not set (for edge cases), DateTime.UtcNow is used as a fallback
        /// to provide an estimated duration for requests in progress.
        /// </remarks>
        private double CalculateApprovalTimeHours(EntityRecord request)
        {
            try
            {
                if (request == null)
                {
                    return 0.0;
                }

                // Check for created_on field
                if (!request.Properties.ContainsKey(FIELD_CREATED_ON) || request[FIELD_CREATED_ON] == null)
                {
                    return 0.0;
                }

                var createdOn = (DateTime)request[FIELD_CREATED_ON];
                DateTime completedOn;

                // Check for completed_on field
                if (request.Properties.ContainsKey(FIELD_COMPLETED_ON) && request[FIELD_COMPLETED_ON] != null)
                {
                    completedOn = (DateTime)request[FIELD_COMPLETED_ON];
                }
                else
                {
                    // Fallback to current time if completed_on is not set
                    completedOn = DateTime.UtcNow;
                }

                // Calculate duration
                var duration = completedOn - createdOn;

                // Return positive hours only
                return duration.TotalHours > 0 ? duration.TotalHours : 0.0;
            }
            catch (Exception)
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Checks if a pending approval request has exceeded its SLA timeout.
        /// Compares the request's created_on timestamp plus SLA hours against the current time.
        /// </summary>
        /// <param name="request">The pending approval request EntityRecord to check.</param>
        /// <returns>
        /// True if the current time exceeds created_on + sla_hours.
        /// Returns false if the request is not overdue, timestamps are missing, or check fails.
        /// </returns>
        /// <remarks>
        /// SLA hours are retrieved from the approval_step entity based on the request's current_step_id.
        /// If the step configuration cannot be retrieved, a default of 24 hours is used.
        /// 
        /// The formula is: IsOverdue = DateTime.UtcNow > (created_on + TimeSpan.FromHours(sla_hours))
        /// </remarks>
        private bool IsRequestOverdue(EntityRecord request)
        {
            try
            {
                if (request == null)
                {
                    return false;
                }

                // Check for created_on field
                if (!request.Properties.ContainsKey(FIELD_CREATED_ON) || request[FIELD_CREATED_ON] == null)
                {
                    return false;
                }

                var createdOn = (DateTime)request[FIELD_CREATED_ON];
                var currentStepId = request.Properties.ContainsKey(FIELD_CURRENT_STEP_ID) 
                    ? request[FIELD_CURRENT_STEP_ID] as Guid? 
                    : null;

                // Default timeout hours
                int slaHours = DEFAULT_SLA_HOURS;

                // Try to get SLA hours from the current step configuration
                if (currentStepId.HasValue)
                {
                    try
                    {
                        var stepParams = new List<EqlParameter>
                        {
                            new EqlParameter(FIELD_ID, currentStepId.Value)
                        };

                        string stepEql = $"SELECT {FIELD_SLA_HOURS} FROM {ENTITY_STEP} WHERE {FIELD_ID} = @{FIELD_ID}";
                        var stepResult = new EqlCommand(stepEql, stepParams).Execute();

                        if (stepResult != null && stepResult.Any())
                        {
                            var step = stepResult.First();
                            if (step.Properties.ContainsKey(FIELD_SLA_HOURS) && step[FIELD_SLA_HOURS] != null)
                            {
                                slaHours = Convert.ToInt32(step[FIELD_SLA_HOURS]);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Use default timeout if step query fails
                    }
                }

                // Calculate due date and compare with current time
                var dueDate = createdOn.AddHours(slaHours);
                return DateTime.UtcNow > dueDate;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the count of approval requests that were approved today.
        /// Queries approval_history for approve actions performed on the current calendar date (UTC).
        /// </summary>
        /// <returns>
        /// Count of approval actions performed today.
        /// Returns 0 if no approvals occurred today or if an error occurs.
        /// </returns>
        /// <remarks>
        /// "Today" is defined as the UTC calendar date from 00:00:00 to 23:59:59.
        /// This metric is used for the dashboard's "Approved Today" card display.
        /// </remarks>
        public int GetApprovedTodayCount()
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var tomorrow = today.AddDays(1);
                var actionApproved = ApprovalActionType.Approve.ToString().ToLowerInvariant();

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("from_date", today),
                    new EqlParameter("to_date", tomorrow),
                    new EqlParameter(FIELD_ACTION_TYPE, actionApproved)
                };

                string eql = $@"SELECT {FIELD_ID} FROM {ENTITY_HISTORY} 
                               WHERE {FIELD_ACTION_TYPE} = @{FIELD_ACTION_TYPE} 
                               AND {FIELD_PERFORMED_ON} >= @from_date AND {FIELD_PERFORMED_ON} < @to_date";
                var result = new EqlCommand(eql, eqlParams).Execute();

                return result?.Count ?? 0;
            }
            catch (Exception)
            {
                // Entity may not exist yet during initial setup, return default value
                return 0;
            }
        }

        /// <summary>
        /// Gets the count of approval requests that were rejected today.
        /// Queries approval_history for reject actions performed on the current calendar date (UTC).
        /// </summary>
        /// <returns>
        /// Count of rejection actions performed today.
        /// Returns 0 if no rejections occurred today or if an error occurs.
        /// </returns>
        /// <remarks>
        /// "Today" is defined as the UTC calendar date from 00:00:00 to 23:59:59.
        /// This metric is used for the dashboard's "Rejected Today" card display.
        /// </remarks>
        public int GetRejectedTodayCount()
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var tomorrow = today.AddDays(1);
                var actionRejected = ApprovalActionType.Reject.ToString().ToLowerInvariant();

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("from_date", today),
                    new EqlParameter("to_date", tomorrow),
                    new EqlParameter(FIELD_ACTION_TYPE, actionRejected)
                };

                string eql = $@"SELECT {FIELD_ID} FROM {ENTITY_HISTORY} 
                               WHERE {FIELD_ACTION_TYPE} = @{FIELD_ACTION_TYPE} 
                               AND {FIELD_PERFORMED_ON} >= @from_date AND {FIELD_PERFORMED_ON} < @to_date";
                var result = new EqlCommand(eql, eqlParams).Execute();

                return result?.Count ?? 0;
            }
            catch (Exception)
            {
                // Entity may not exist yet during initial setup, return default value
                return 0;
            }
        }

        #endregion
    }
}
