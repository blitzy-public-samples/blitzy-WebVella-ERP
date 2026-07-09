using System;
using System.Collections.Generic;
using System.Linq;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Eql;
using WebVella.Erp.Plugins.Approval.Api;
using WebVella.Erp.Diagnostics; // BUGFIX (Bug 4): Log/LogType for structured error logging (log-then-rethrow)

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
                RecentActivity = GetRecentActivity(5)
            };

            return metrics;
        }

        /// <summary>
        /// Gets the count of approval requests in pending status where the user
        /// is an authorized approver for the current step.
        /// </summary>
        /// <param name="userId">The ID of the approver user.</param>
        /// <returns>Count of pending approval requests.</returns>
        public int GetPendingApprovalsCount(Guid userId)
        {
            // BUGFIX (Bug 6): also select current_step_id so the count can be scoped to the requests
            // whose current step this manager is authorized to approve (previously userId was ignored
            // and the returned count was organization-wide). eqlCommand is declared BEFORE the try so it
            // remains in scope inside the catch for the Bug 4 log-then-rethrow below.
            var eqlCommand = @"
                    SELECT id, current_step_id 
                    FROM approval_request 
                    WHERE status = @status";

            try
            {
                // BUGFIX (Bug 6): resolve the manager's authorized steps and count ONLY requests whose
                // current_step_id is in that set. This consumes userId without inventing an approver column.
                var authorizedStepIds = ResolveAuthorizedStepIds(userId);

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("status", "pending")
                };

                var result = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (result == null || !result.Any())
                    return 0;

                var count = 0;
                foreach (var record in result)
                {
                    // BUGFIX (Bug 6): null-safe skip of records without a current_step_id (cannot be scoped).
                    if (!record.Properties.ContainsKey("current_step_id") || record["current_step_id"] == null)
                        continue;

                    var stepId = (Guid)record["current_step_id"];
                    if (authorizedStepIds.Contains(stepId))
                        count++;
                }

                return count;
            }
            catch (Exception ex)
            {
                // BUGFIX (Bug 4): never mask a query failure as a legitimate zero. Log the offending EQL
                // text + exception, then rethrow so the controller/component boundary surfaces a real error.
                new Log().Create(LogType.Error, "DashboardMetricsService.GetPendingApprovalsCount", eqlCommand, ex);
                throw;
            }
        }

        /// <summary>
        /// Gets the count of pending requests that have exceeded their configured
        /// timeout threshold from the approval step.
        /// </summary>
        /// <param name="userId">The ID of the approver user.</param>
        /// <returns>Count of overdue approval requests.</returns>
        public int GetOverdueRequestsCount(Guid userId)
        {
            // BUGFIX (Bug 6 + Bug 7): also select current_step_id so the count can be scoped to the
            // manager (Bug 6) and each request's per-step timeout can be resolved (Bug 7). eqlCommand is
            // declared BEFORE the try so it remains in scope inside the catch for the Bug 4 log-then-rethrow.
            var eqlCommand = @"
                    SELECT id, created_on, current_step_id 
                    FROM approval_request 
                    WHERE status = @status";

            try
            {
                // BUGFIX (Bug 6): resolve the steps this manager may approve (previously userId was ignored
                // and the count was organization-wide).
                var authorizedStepIds = ResolveAuthorizedStepIds(userId);

                // BUGFIX (Bug 7): named fallback used ONLY when a step's configured timeout is unavailable.
                const int DEFAULT_TIMEOUT_HOURS = 24;

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("status", "pending")
                };

                var result = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (result == null || !result.Any())
                    return 0;

                // BUGFIX (Bug 7): load each step's configured timeout_hours ONCE, keyed by step id
                // (bounded, no per-row DB round-trips, and no $relation traversal since current_step_id has
                // no formal EntityRelation). Uses valid EQL (no IN/LIMIT). A failure of this lookup query
                // propagates to this method's single log-then-rethrow catch below (Bug 4) — it is NOT given
                // its own catch, so it is never silently masked.
                var stepTimeoutEql = @"
                    SELECT id, timeout_hours 
                    FROM approval_step";

                var stepTimeouts = new Dictionary<Guid, int>();
                var stepResult = new EqlCommand(stepTimeoutEql, new List<EqlParameter>()).Execute();
                if (stepResult != null)
                {
                    foreach (var step in stepResult)
                    {
                        if (!step.Properties.ContainsKey("id") || step["id"] == null)
                            continue;

                        var sId = (Guid)step["id"];

                        // timeout_hours is a NumberField (may surface as a boxed decimal); convert
                        // defensively and guard nulls, falling back to the default when it is unset.
                        var hrs = DEFAULT_TIMEOUT_HOURS;
                        if (step.Properties.ContainsKey("timeout_hours") && step["timeout_hours"] != null)
                            hrs = Convert.ToInt32(step["timeout_hours"]);

                        stepTimeouts[sId] = hrs;
                    }
                }

                var overdueCount = 0;
                var now = DateTime.UtcNow;

                foreach (var record in result)
                {
                    // BUGFIX (Bug 6): skip requests whose current step this manager may not approve, and
                    // requests without a current_step_id (cannot be scoped/authorized).
                    if (!record.Properties.ContainsKey("current_step_id") || record["current_step_id"] == null)
                        continue;

                    var stepId = (Guid)record["current_step_id"];
                    if (!authorizedStepIds.Contains(stepId))
                        continue;

                    // BUGFIX (Bug 7): use the step's configured timeout; fall back only when it is unknown.
                    var hours = stepTimeouts.ContainsKey(stepId) ? stepTimeouts[stepId] : DEFAULT_TIMEOUT_HOURS;

                    // BUGFIX (Bug 7): timeout_hours == 0 means "no timeout" -> the request is never overdue.
                    if (hours == 0)
                        continue;

                    if (record.Properties.ContainsKey("created_on") && record["created_on"] != null)
                    {
                        var createdOn = (DateTime)record["created_on"];
                        var deadline = createdOn.AddHours(hours);

                        if (now > deadline)
                        {
                            overdueCount++;
                        }
                    }
                }

                return overdueCount;
            }
            catch (Exception ex)
            {
                // BUGFIX (Bug 4): never mask a query failure as a legitimate zero. Log the offending EQL
                // text + exception, then rethrow so the controller/component boundary surfaces a real error.
                new Log().Create(LogType.Error, "DashboardMetricsService.GetOverdueRequestsCount", eqlCommand, ex);
                throw;
            }
        }

        /// <summary>
        /// Calculates the average time in hours from request creation to completion
        /// for all processed requests within the date range.
        /// </summary>
        /// <param name="fromDate">Start of the date range.</param>
        /// <param name="toDate">End of the date range.</param>
        /// <returns>Average processing time in hours.</returns>
        public decimal GetAverageApprovalTime(DateTime fromDate, DateTime toDate)
        {
            // Query completed approval requests within date range.
            // BUGFIX (Bug 2): EQL has no IN operator; use a parenthesized OR group. The parentheses are
            // REQUIRED because AND binds tighter than OR (EqlGrammar precedence AND=5 > OR=4), so the
            // AND-bound completed_on date range applies to BOTH statuses. eqlCommand is declared BEFORE the
            // try so it remains in scope inside the catch for the Bug 4 log-then-rethrow below.
            var eqlCommand = @"
                    SELECT id, created_on, completed_on 
                    FROM approval_request 
                    WHERE (status = @approvedStatus OR status = @rejectedStatus)
                    AND completed_on >= @fromDate
                    AND completed_on <= @toDate";

            try
            {
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
            catch (Exception ex)
            {
                // BUGFIX (Bug 4): never mask a query failure as a legitimate zero. Log the offending EQL
                // text + exception, then rethrow so the controller/component boundary surfaces a real error.
                new Log().Create(LogType.Error, "DashboardMetricsService.GetAverageApprovalTime", eqlCommand, ex);
                throw;
            }
        }

        /// <summary>
        /// Calculates the percentage of approved requests out of total processed
        /// requests within the date range.
        /// </summary>
        /// <param name="fromDate">Start of the date range.</param>
        /// <param name="toDate">End of the date range.</param>
        /// <returns>Approval rate as a percentage (0-100).</returns>
        public decimal GetApprovalRate(DateTime fromDate, DateTime toDate)
        {
            // Query all completed requests within date range.
            // BUGFIX (Bug 1): EQL has no IN operator; use a parenthesized OR group. The parentheses are
            // REQUIRED because AND binds tighter than OR (EqlGrammar precedence AND=5 > OR=4), so the
            // AND-bound completed_on date range applies to BOTH statuses. eqlCommand is declared BEFORE the
            // try so it remains in scope inside the catch for the Bug 4 log-then-rethrow below.
            var eqlCommand = @"
                    SELECT id, status 
                    FROM approval_request 
                    WHERE (status = @approvedStatus OR status = @rejectedStatus)
                    AND completed_on >= @fromDate
                    AND completed_on <= @toDate";

            try
            {
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
            catch (Exception ex)
            {
                // BUGFIX (Bug 4): never mask a query failure as a legitimate zero. Log the offending EQL
                // text + exception, then rethrow so the controller/component boundary surfaces a real error.
                new Log().Create(LogType.Error, "DashboardMetricsService.GetApprovalRate", eqlCommand, ex);
                throw;
            }
        }

        /// <summary>
        /// Retrieves the most recent approval history actions for display in the
        /// activity feed.
        /// </summary>
        /// <param name="limit">Maximum number of activity items to return.</param>
        /// <returns>List of recent activity items ordered by most recent first.</returns>
        public List<RecentActivityItem> GetRecentActivity(int limit)
        {
            // Query approval_history for recent actions.
            // BUGFIX (Bug 3): EQL paginates with PAGE/PAGESIZE, not LIMIT. ORDER BY performed_on DESC is
            // retained so PAGE 1 PAGESIZE @limit yields the newest-first top-N slice. eqlCommand is declared
            // BEFORE the try so it remains in scope inside the catch for the Bug 4 log-then-rethrow below.
            var eqlCommand = @"
                    SELECT id, action, performed_by, performed_on, request_id 
                    FROM approval_history 
                    ORDER BY performed_on DESC
                    PAGE 1 PAGESIZE @limit";

            try
            {
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
                        // Bug 5 (schema gap - INTENTIONAL NO-OP): no request_title column exists on
                        // approval_history or approval_request, and the recent-activity API contract omits
                        // any title field, so this guard always resolves to the "Approval Request" fallback.
                        // Selecting request_title would throw an unresolved-column EqlException; populating a
                        // real title requires provisioning a title field (or resolving the dynamic source
                        // record via source_entity + source_record_id) and is out of single-file scope.
                        RequestTitle = record.Properties.ContainsKey("request_title") 
                            ? (string)record["request_title"] ?? "Approval Request" 
                            : "Approval Request"
                    };

                    activityList.Add(item);
                }

                return activityList;
            }
            catch (Exception ex)
            {
                // BUGFIX (Bug 4): never mask a query failure as a legitimate empty list. Log the offending
                // EQL text + exception, then rethrow so the controller/component boundary surfaces a real error.
                new Log().Create(LogType.Error, "DashboardMetricsService.GetRecentActivity", eqlCommand, ex);
                throw;
            }
        }

        /// <summary>
        /// Resolves the set of approval_step IDs that the specified user is authorized to approve.
        /// Used to scope the pending/overdue counts to the manager's own queue (Bug 6) instead of
        /// returning organization-wide counts. Consumes <paramref name="userId"/> without introducing
        /// any non-existent approver column on approval_request.
        /// </summary>
        /// <param name="userId">The approver user ID whose authorized steps are being resolved.</param>
        /// <returns>The set of approval_step IDs the user may approve (empty when none can be proven).</returns>
        private HashSet<Guid> ResolveAuthorizedStepIds(Guid userId)
        {
            // BUGFIX (Bug 6): approval_request has NO approver column, so authorization is derived from
            // current_step_id -> approval_step. eqlCommand is declared BEFORE the try so it remains in
            // scope inside the catch for the Bug 4 log-then-rethrow below.
            //
            // CLARIFICATION (Bug 6 schema gap): STORY-002 (approval_step) does not define the exact JSON
            // shape of threshold_config for approver identifiers, nor a documented mechanism to resolve a
            // user's roles / department-head status. This resolution is therefore intentionally
            // CONSERVATIVE: a step is authorized only when it is a "user"-type step whose threshold_config
            // explicitly references this user's id. "role" / "department_head" steps are left unresolved
            // (they require membership infrastructure not specified in the schema) and are flagged for
            // clarification rather than resolved by inventing a column/contract.
            var eqlCommand = @"
                    SELECT id, approver_type, threshold_config 
                    FROM approval_step";

            var authorizedStepIds = new HashSet<Guid>();

            try
            {
                var result = new EqlCommand(eqlCommand, new List<EqlParameter>()).Execute();

                if (result == null || !result.Any())
                    return authorizedStepIds;

                var userIdText = userId.ToString();

                foreach (var record in result)
                {
                    if (!record.Properties.ContainsKey("id") || record["id"] == null)
                        continue;

                    var stepId = (Guid)record["id"];

                    var approverType = record.Properties.ContainsKey("approver_type")
                        ? record["approver_type"] as string
                        : null;

                    var thresholdConfig = record.Properties.ContainsKey("threshold_config")
                        ? record["threshold_config"] as string
                        : null;

                    // BUGFIX (Bug 6): consume userId conservatively. Authorize a "user"-type step when its
                    // threshold_config explicitly references this user's id (a 36-char GUID match is safe
                    // against accidental collisions). Other approver types remain unresolved (schema gap).
                    if (string.Equals(approverType, "user", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(thresholdConfig)
                        && thresholdConfig.IndexOf(userIdText, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        authorizedStepIds.Add(stepId);
                    }
                }

                return authorizedStepIds;
            }
            catch (Exception ex)
            {
                // BUGFIX (Bug 4): never mask a helper query failure as a legitimate empty result. Log the
                // offending EQL text + exception, then rethrow so the calling boundary surfaces a real error.
                new Log().Create(LogType.Error, "DashboardMetricsService.ResolveAuthorizedStepIds", eqlCommand, ex);
                throw;
            }
        }
    }
}
