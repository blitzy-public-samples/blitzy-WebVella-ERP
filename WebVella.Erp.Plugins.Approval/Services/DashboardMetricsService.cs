using System;
using System.Collections.Generic;
using System.Linq;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Eql;
using WebVella.Erp.Plugins.Approval.Api;
using WebVella.Erp.Diagnostics; // BUGFIX (Bug 4): Log/LogType for structured error logging (log-then-rethrow)
using System.Globalization; // REVIEW FIX (Finding 4): culture-invariant, defensive timeout_hours conversion
using Newtonsoft.Json; // REVIEW FIX (Finding 2): structural JSON parsing of threshold_config (JsonException)
using Newtonsoft.Json.Linq; // REVIEW FIX (Finding 2): JObject/JToken structural access + exact GUID comparison

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

        // BUGFIX (Bug 7): named fallback applied ONLY when a step's configured timeout_hours is
        // unavailable (missing/null) or cannot be parsed. Promoted to a class-level constant (was a
        // method-local const) so the overdue calculation and the LoadStepTimeouts/ConvertTimeoutHours
        // helpers introduced for REVIEW Findings 3 and 4 all share the same single source of truth.
        private const int DEFAULT_TIMEOUT_HOURS = 24;

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

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("status", "pending")
                };

                var result = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (result == null || !result.Any())
                    return 0;

                // BUGFIX (Bug 7): load each step's configured timeout_hours ONCE, keyed by step id
                // (bounded, no per-row DB round-trips, and no $relation traversal since current_step_id has
                // no formal EntityRelation). REVIEW FIX (Finding 3): the timeout lookup query is now executed
                // inside the dedicated LoadStepTimeouts() helper, which owns its OWN log-then-rethrow that
                // logs the ACTUAL offending timeout EQL text — previously a failure of this lookup was
                // (mis)logged by the outer catch below with the pending-request query text instead.
                var stepTimeouts = LoadStepTimeouts();

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
        /// Loads each approval_step's configured <c>timeout_hours</c> once, keyed by step id, for use by
        /// the overdue calculation (Bug 7). Extracted into its own method for REVIEW Finding 3 so the
        /// timeout lookup query owns a DEDICATED log-then-rethrow that logs the ACTUAL offending EQL text —
        /// previously a failure of this lookup was mislogged by GetOverdueRequestsCount using the
        /// pending-request query text, defeating the Bug 4 observability goal.
        /// </summary>
        /// <returns>A map of approval_step id -&gt; configured timeout in hours (bounded, loaded once).</returns>
        private Dictionary<Guid, int> LoadStepTimeouts()
        {
            // REVIEW FIX (Finding 3): stepTimeoutEql is declared BEFORE the try so it remains in scope for
            // this helper's OWN log-then-rethrow, guaranteeing the offending timeout query text is logged.
            // Uses valid EQL only (no IN/LIMIT), consistent with the Bug 1/2/3 grammar fixes. No $relation
            // traversal is used since current_step_id has no formal EntityRelation.
            var stepTimeoutEql = @"
                    SELECT id, timeout_hours 
                    FROM approval_step";

            var stepTimeouts = new Dictionary<Guid, int>();

            try
            {
                var stepResult = new EqlCommand(stepTimeoutEql, new List<EqlParameter>()).Execute();

                if (stepResult == null)
                    return stepTimeouts;

                foreach (var step in stepResult)
                {
                    if (!step.Properties.ContainsKey("id") || step["id"] == null)
                        continue;

                    var stepId = (Guid)step["id"];

                    // REVIEW FIX (Finding 4): timeout_hours is a NumberField that may surface as a boxed
                    // decimal/double/int/string. Missing/null falls back to DEFAULT_TIMEOUT_HOURS; any
                    // non-numeric / invalid / overflow value is handled defensively inside
                    // ConvertTimeoutHours rather than throwing an unhandled Convert.ToInt32 out of the
                    // metrics path (the previous inline Convert.ToInt32 had no such guard).
                    var hours = DEFAULT_TIMEOUT_HOURS;
                    if (step.Properties.ContainsKey("timeout_hours") && step["timeout_hours"] != null)
                        hours = ConvertTimeoutHours(step["timeout_hours"], stepId);

                    stepTimeouts[stepId] = hours;
                }

                return stepTimeouts;
            }
            catch (Exception ex)
            {
                // REVIEW FIX (Finding 3) / BUGFIX (Bug 4): log the ACTUAL offending timeout EQL text and
                // exception, then rethrow so the controller/component boundary surfaces a real error.
                new Log().Create(LogType.Error, "DashboardMetricsService.LoadStepTimeouts", stepTimeoutEql, ex);
                throw;
            }
        }

        /// <summary>
        /// Defensively converts a raw <c>timeout_hours</c> value (which may surface as a boxed
        /// decimal/double/int or a string) into a non-negative Int32 count of hours. Added for REVIEW
        /// Finding 4: a single malformed / non-numeric / out-of-range configuration value must NOT throw
        /// out of the metrics path. On any conversion problem a targeted diagnostic is logged and the
        /// DEFAULT_TIMEOUT_HOURS fallback is applied; a value of 0 is preserved (schema: 0 = no timeout).
        /// </summary>
        /// <param name="rawValue">The boxed timeout_hours value read from the approval_step record.</param>
        /// <param name="stepId">The owning approval_step id, included in the diagnostic for traceability.</param>
        /// <returns>The parsed non-negative timeout in hours, or DEFAULT_TIMEOUT_HOURS on any failure.</returns>
        private int ConvertTimeoutHours(object rawValue, Guid stepId)
        {
            try
            {
                // Convert through decimal first (culture-invariant) so decimal/double/string storage is
                // tolerated, then narrow to Int32. Convert.ToInt32 throws OverflowException for values
                // outside the Int32 range, which is caught below and mapped to the fallback.
                var asDecimal = Convert.ToDecimal(rawValue, CultureInfo.InvariantCulture);

                // Negative hours are not a valid configuration (schema: 0 = no timeout, >0 = hours). Treat
                // any negative value as invalid and apply the fallback with a targeted diagnostic instead
                // of producing a deadline that would flag every request as overdue.
                if (asDecimal < 0m)
                {
                    new Log().Create(
                        LogType.Error,
                        "DashboardMetricsService.ConvertTimeoutHours",
                        "Negative timeout_hours (" + asDecimal.ToString(CultureInfo.InvariantCulture) +
                            ") for approval_step " + stepId + "; applying DEFAULT_TIMEOUT_HOURS (" +
                            DEFAULT_TIMEOUT_HOURS + ") fallback.",
                        (string)null);
                    return DEFAULT_TIMEOUT_HOURS;
                }

                // 0 is preserved (= no timeout); Convert.ToInt32 narrows and throws OverflowException for
                // values outside the Int32 range (handled by the catch below -> fallback).
                return Convert.ToInt32(asDecimal);
            }
            catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is OverflowException)
            {
                // REVIEW FIX (Finding 4): non-numeric / invalid / overflow value -> log targeted context
                // and apply the DEFAULT_TIMEOUT_HOURS fallback instead of throwing out of the metrics path.
                new Log().Create(
                    LogType.Error,
                    "DashboardMetricsService.ConvertTimeoutHours",
                    "Invalid or out-of-range timeout_hours for approval_step " + stepId +
                        "; applying DEFAULT_TIMEOUT_HOURS (" + DEFAULT_TIMEOUT_HOURS + ") fallback.",
                    ex);
                return DEFAULT_TIMEOUT_HOURS;
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
            // FINDING A FIX (phantom column `completed_on`): approval_request has NO `completed_on` column
            // in the authoritative schema (STORY-002 AC4: id, source_record_id, source_entity, workflow_id,
            // current_step_id, status, created_on, created_by). Selecting/filtering it threw an unresolved-
            // column EqlException that -- once unmasked by the Bug 4 log-then-rethrow -- surfaced as an
            // HTTP 500 on the entire /metrics endpoint. Per STORY-009 ("Average Approval Time: mean time
            // from request creation to final approval decision, calculated from approval_history timestamp
            // differences"), the decision timestamp is approval_history.performed_on for a terminal
            // action_type (approved|rejected); the creation timestamp is approval_request.created_on.
            //
            // Query 1 (decisions): terminal approve/reject events within the range, from approval_history.
            // No IN operator (parenthesized OR; AND binds tighter than OR, so the performed_on range
            // applies to BOTH action types). Declared BEFORE the try so it stays in scope for the Bug 4
            // catch below.
            var eqlCommand = @"
                    SELECT request_id, performed_on
                    FROM approval_history
                    WHERE performed_on >= @fromDate
                    AND performed_on <= @toDate
                    AND (action_type = @approvedAction OR action_type = @rejectedAction)";

            // Query 2 (creation lookup): request id -> created_on, a bounded one-time load keyed by request
            // id (mirrors the LoadStepTimeouts dictionary pattern used for the Bug 7 fix). Restricted to
            // created_on <= @toDate: any decision performed on/before toDate implies its request was created
            // on/before toDate, so this trims the set without excluding a needed row.
            var createdOnEqlCommand = @"
                    SELECT id, created_on
                    FROM approval_request
                    WHERE created_on <= @toDate";

            try
            {
                // Build the request-id -> created_on lookup first so each decision row can be paired with
                // its originating request's creation time.
                var createdOnParams = new List<EqlParameter>
                {
                    new EqlParameter("toDate", toDate)
                };
                var createdOnResult = new EqlCommand(createdOnEqlCommand, createdOnParams).Execute();

                var requestCreatedOn = new Dictionary<Guid, DateTime>();
                if (createdOnResult != null)
                {
                    foreach (var reqRecord in createdOnResult)
                    {
                        if (reqRecord.Properties.ContainsKey("id") && reqRecord["id"] != null &&
                            reqRecord.Properties.ContainsKey("created_on") && reqRecord["created_on"] != null)
                        {
                            requestCreatedOn[(Guid)reqRecord["id"]] = (DateTime)reqRecord["created_on"];
                        }
                    }
                }

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("fromDate", fromDate),
                    new EqlParameter("toDate", toDate),
                    new EqlParameter("approvedAction", "approved"),
                    new EqlParameter("rejectedAction", "rejected")
                };

                var result = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (result == null || !result.Any())
                    return 0;

                var totalHours = 0m;
                var count = 0;

                foreach (var record in result)
                {
                    if (record.Properties.ContainsKey("request_id") && record["request_id"] != null &&
                        record.Properties.ContainsKey("performed_on") && record["performed_on"] != null)
                    {
                        var requestId = (Guid)record["request_id"];
                        var performedOn = (DateTime)record["performed_on"];

                        // Measurable only when the originating request's creation time is known and the
                        // decision is not chronologically before it (guards against data/clock anomalies).
                        if (requestCreatedOn.TryGetValue(requestId, out var createdOn) && performedOn >= createdOn)
                        {
                            totalHours += (decimal)(performedOn - createdOn).TotalHours;
                            count++;
                        }
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
            // FINDING A FIX (phantom column `completed_on`): approval_request has NO `completed_on` column
            // (STORY-002 AC4), so the previous `SELECT id, status ... WHERE completed_on ...` threw an
            // unresolved-column EqlException that surfaced as an HTTP 500 on /metrics once the Bug 4
            // log-then-rethrow unmasked it. Per STORY-009 ("Approval Rate: percentage of requests approved
            // versus total processed (approved + rejected)"), the processed set and its approve/reject
            // classification live in approval_history.action_type over the performed_on window. No IN
            // operator (parenthesized OR; AND binds tighter than OR so the range applies to BOTH actions).
            // eqlCommand is declared BEFORE the try so it stays in scope for the Bug 4 catch below.
            var eqlCommand = @"
                    SELECT id, action_type
                    FROM approval_history
                    WHERE performed_on >= @fromDate
                    AND performed_on <= @toDate
                    AND (action_type = @approvedAction OR action_type = @rejectedAction)";

            try
            {
                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("fromDate", fromDate),
                    new EqlParameter("toDate", toDate),
                    new EqlParameter("approvedAction", "approved"),
                    new EqlParameter("rejectedAction", "rejected")
                };

                var result = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (result == null || !result.Any())
                    return 0;

                // Each returned row is a terminal decision event (approved|rejected) within the window;
                // the approval rate is the share of those decisions that were approvals.
                var totalCount = result.Count;
                var approvedCount = result.Count(r => 
                    r.Properties.ContainsKey("action_type") && 
                    (string)r["action_type"] == "approved");

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
            // FINDING B FIX (phantom column `action`): the authoritative approval_history schema
            // (STORY-002 AC5) names this SelectField `action_type`, not `action`. Selecting `action`
            // threw an unresolved-column EqlException that -- once the Bug 3 LIMIT->PAGE/PAGESIZE fix let
            // this query build -- aborted GetRecentActivity and, via the GetDashboardMetrics initializer,
            // the whole /metrics endpoint (HTTP 500). The DTO stays RecentActivityItem.Action with
            // [JsonProperty("action")], so the emitted JSON contract is unchanged; only the queried
            // column name is corrected.
            // BUGFIX (Bug 3): EQL paginates with PAGE/PAGESIZE, not LIMIT. ORDER BY performed_on DESC is
            // retained so PAGE 1 PAGESIZE @limit yields the newest-first top-N slice. eqlCommand is declared
            // BEFORE the try so it remains in scope inside the catch for the Bug 4 log-then-rethrow below.
            var eqlCommand = @"
                    SELECT id, action_type, performed_by, performed_on, request_id
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
                        // FINDING B FIX: read the corrected `action_type` column (STORY-002 AC5) into the
                        // Action DTO property (whose JSON name remains "action").
                        Action = record.Properties.ContainsKey("action_type") 
                            ? (string)record["action_type"] ?? "unknown" 
                            : "unknown",
                        // BUGFIX (recent-activity mapping / QA Report 7 Issue 1): performed_by is a
                        // GuidField (STORY-002 approval_history), so record["performed_by"] is a boxed
                        // System.Guid. The previous hard (string) cast threw InvalidCastException at
                        // runtime -- a CRITICAL crash that was unmasked once the Bug 3 LIMIT -> PAGE/PAGESIZE
                        // fix allowed this query to execute and reach the mapping -- aborting GetRecentActivity
                        // and, via the GetDashboardMetrics object initializer, the entire /metrics endpoint.
                        // Use null-safe ?.ToString() so a Guid renders as its canonical string form and a
                        // null value still falls back to "Unknown User". PerformedBy is a string on the DTO,
                        // so this involves no public-signature, DTO, or schema change.
                        PerformedBy = record.Properties.ContainsKey("performed_by") 
                            ? record["performed_by"]?.ToString() ?? "Unknown User" 
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
        /// <remarks>
        /// REVIEW FIX (Finding 1): all three approver_type values defined by STORY-002 are now handled —
        /// "user", "role", and "department_head" — instead of only "user". The requesting user's roles are
        /// resolved through the existing SecurityManager/ErpUser security API (no invented schema fields),
        /// and role-based steps are matched by role id AND role name.
        /// REVIEW FIX (Finding 2): authorization no longer performs a raw substring scan of the whole
        /// threshold_config JSON. The config is parsed STRUCTURALLY and compared against the explicit
        /// approver key(s) using exact Guid.TryParse equality; malformed JSON fails CLOSED (never
        /// authorizes). This removes the over-authorization risk where a GUID appearing anywhere in the
        /// JSON (e.g. a threshold amount or an unrelated field) could otherwise grant access.
        /// </remarks>
        /// <param name="userId">The approver user ID whose authorized steps are being resolved.</param>
        /// <returns>The set of approval_step IDs the user may approve (empty when none can be proven).</returns>
        private HashSet<Guid> ResolveAuthorizedStepIds(Guid userId)
        {
            var authorizedStepIds = new HashSet<Guid>();

            // REVIEW FIX (Finding 1): resolve the requesting user's roles via the EXISTING security API so
            // "role"-type steps can be authorized by the user's ACTUAL roles (id or name). GetUser opens its
            // own system scope internally and returns null when the user cannot be found. A hard failure is
            // logged with a clear source + message and rethrown so it is never silently swallowed (the Bug 4
            // observability principle) — and, crucially, it is NOT (mis)logged against the approval_step EQL.
            ErpUser user;
            try
            {
                user = new SecurityManager().GetUser(userId);
            }
            catch (Exception ex)
            {
                new Log().Create(
                    LogType.Error,
                    "DashboardMetricsService.ResolveAuthorizedStepIds",
                    "Failed to resolve requesting user (userId=" + userId + ") for step authorization.",
                    ex);
                throw;
            }

            // Build the user's role-identity sets once for role-based step matching. Role names are compared
            // case-insensitively; role ids are compared exactly.
            var userRoleIds = new HashSet<Guid>();
            var userRoleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (user != null && user.Roles != null)
            {
                foreach (var role in user.Roles)
                {
                    userRoleIds.Add(role.Id);
                    if (!string.IsNullOrWhiteSpace(role.Name))
                        userRoleNames.Add(role.Name.Trim());
                }
            }

            // BUGFIX (Bug 6): approval_request has NO approver column, so authorization is derived from
            // current_step_id -> approval_step. eqlCommand is declared BEFORE the try so it remains in
            // scope inside the catch for the Bug 4 log-then-rethrow below.
            var eqlCommand = @"
                    SELECT id, approver_type, threshold_config 
                    FROM approval_step";

            try
            {
                var result = new EqlCommand(eqlCommand, new List<EqlParameter>()).Execute();

                if (result == null || !result.Any())
                    return authorizedStepIds;

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

                    // REVIEW FIX (Findings 1 & 2): decide authorization by STRUCTURALLY parsing the config
                    // and matching the explicit approver key(s) for the step's approver_type. Only steps the
                    // user can actually be proven to approve are added (fail closed otherwise).
                    if (IsUserAuthorizedForStep(approverType, thresholdConfig, userId, userRoleIds, userRoleNames))
                        authorizedStepIds.Add(stepId);
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

        // REVIEW FIX (Finding 2): well-known threshold_config keys that may carry explicit approver
        // identifiers. threshold_config is documented only as "JSON configuration for amount thresholds and
        // approver IDs" (STORY-002), so a small, defensive set of conventional aliases is accepted. Each key
        // may hold a single scalar OR an array; values are matched EXACTLY (never by substring).
        private static readonly string[] ApproverUserIdKeys =
        {
            "approver_user_id", "approver_user_ids", "user_id", "user_ids", "approver_id", "approver_ids"
        };

        private static readonly string[] ApproverRoleIdKeys =
        {
            "approver_role_id", "approver_role_ids", "role_id", "role_ids"
        };

        private static readonly string[] ApproverRoleNameKeys =
        {
            "approver_role", "approver_role_name", "approver_role_names", "role", "role_name", "role_names"
        };

        /// <summary>
        /// Determines whether the given user (identity + resolved roles) is an authorized approver for a
        /// single approval_step, based on the step's <paramref name="approverType"/> and its
        /// <paramref name="thresholdConfig"/> JSON. Added for REVIEW Findings 1 &amp; 2.
        /// </summary>
        /// <remarks>
        /// Fails CLOSED: returns false for a missing/blank config, malformed JSON, an unknown approver type,
        /// or when no explicit approver identifier matches. This guarantees a parsing/lookup problem can
        /// never OVER-authorize (the security concern raised by Finding 2).
        /// </remarks>
        private bool IsUserAuthorizedForStep(
            string approverType,
            string thresholdConfig,
            Guid userId,
            HashSet<Guid> userRoleIds,
            HashSet<string> userRoleNames)
        {
            // Fail closed: with no config we cannot PROVE the user approves this step.
            if (string.IsNullOrWhiteSpace(thresholdConfig))
                return false;

            JObject config;
            try
            {
                config = JObject.Parse(thresholdConfig);
            }
            catch (JsonException ex)
            {
                // REVIEW FIX (Finding 2): fail CLOSED on malformed JSON (never authorize), and log the
                // problem for observability instead of silently swallowing it.
                new Log().Create(
                    LogType.Error,
                    "DashboardMetricsService.IsUserAuthorizedForStep",
                    "Malformed approval_step threshold_config JSON (approver_type=" +
                        (approverType ?? "<null>") + "); failing closed (not authorized).",
                    ex);
                return false;
            }

            var type = (approverType ?? string.Empty).Trim().ToLowerInvariant();

            switch (type)
            {
                case "user":
                    // Authorized when the config explicitly names this user's id.
                    return MatchesConfiguredUser(config, userId);

                case "role":
                    // Authorized when the config explicitly names a role the user actually holds.
                    return MatchesConfiguredRole(config, userRoleIds, userRoleNames);

                case "department_head":
                    // REVIEW FIX (Finding 1) — supported-contract handling for the documented schema gap:
                    // no department / org-hierarchy is modeled anywhere in STORY-002 or ErpUser, so
                    // "is this user the head of the step's department" cannot be resolved WITHOUT inventing a
                    // schema field (explicitly forbidden by scope). The supported contract is therefore to
                    // honor EXPLICIT approver identifiers already present in threshold_config: authorize when
                    // the config explicitly names this user OR a role the user holds. Absent any explicit
                    // identifier the step fails closed (never org-wide). True department-hierarchy resolution
                    // requires org/department data that is out of single-file scope and is flagged for
                    // design follow-up.
                    return MatchesConfiguredUser(config, userId)
                        || MatchesConfiguredRole(config, userRoleIds, userRoleNames);

                default:
                    // Unknown/absent approver type -> fail closed.
                    return false;
            }
        }

        /// <summary>
        /// Returns true when <paramref name="config"/> explicitly names <paramref name="userId"/> under any
        /// recognized approver-user key. Values are compared with exact Guid.TryParse equality (REVIEW
        /// Finding 2) — never by substring — so an unrelated GUID elsewhere in the JSON cannot grant access.
        /// </summary>
        private static bool MatchesConfiguredUser(JObject config, Guid userId)
        {
            foreach (var key in ApproverUserIdKeys)
            {
                foreach (var raw in EnumerateStringValues(config[key]))
                {
                    if (Guid.TryParse(raw, out var candidate) && candidate == userId)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns true when <paramref name="config"/> explicitly names a role the user holds — either by
        /// exact role-id (Guid.TryParse) or by case-insensitive role-name (REVIEW Finding 1). Never uses
        /// substring matching.
        /// </summary>
        private static bool MatchesConfiguredRole(JObject config, HashSet<Guid> userRoleIds, HashSet<string> userRoleNames)
        {
            // Match by explicit role id.
            foreach (var key in ApproverRoleIdKeys)
            {
                foreach (var raw in EnumerateStringValues(config[key]))
                {
                    if (Guid.TryParse(raw, out var candidate) && userRoleIds.Contains(candidate))
                        return true;
                }
            }

            // Match by explicit role name (case-insensitive, exact value — not substring).
            foreach (var key in ApproverRoleNameKeys)
            {
                foreach (var raw in EnumerateStringValues(config[key]))
                {
                    if (!string.IsNullOrWhiteSpace(raw) && userRoleNames.Contains(raw.Trim()))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Yields the scalar string value(s) held by a threshold_config token: a single primitive is yielded
        /// as-is, an array yields each of its primitive elements, and null / object / nested-array tokens are
        /// skipped. Centralizes the "single value OR array" tolerance used by the approver-matching helpers.
        /// </summary>
        private static IEnumerable<string> EnumerateStringValues(JToken token)
        {
            if (token == null)
                yield break;

            if (token.Type == JTokenType.Array)
            {
                foreach (var element in (JArray)token)
                {
                    if (element == null)
                        continue;
                    if (element.Type == JTokenType.Null || element.Type == JTokenType.Object || element.Type == JTokenType.Array)
                        continue;

                    var value = element.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                        yield return value;
                }
            }
            else if (token.Type != JTokenType.Null && token.Type != JTokenType.Object)
            {
                var value = token.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    yield return value;
            }
        }
    }
}
