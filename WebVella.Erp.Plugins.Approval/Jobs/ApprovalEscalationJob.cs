using System;
using System.Collections.Generic;
using System.Linq;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Diagnostics;
using WebVella.Erp.Eql;
using WebVella.Erp.Jobs;
using WebVella.Erp.Plugins.Approval.Model;
using WebVella.Erp.Plugins.Approval.Services;

namespace WebVella.Erp.Plugins.Approval.Jobs
{
    /// <summary>
    /// Background job that escalates overdue approval requests based on SLA configuration.
    /// Runs every 30 minutes per SchedulePlan configuration defined in ApprovalPlugin.
    /// 
    /// This job identifies pending approval requests that have exceeded their SLA (sla_hours
    /// from approval_step), updates their status to 'escalated', logs the action via
    /// ApprovalHistoryService, and notifies escalation targets via NotificationService.
    /// </summary>
    /// <remarks>
    /// The escalation process follows these steps:
    /// 1. Query all pending approval requests
    /// 2. For each request, retrieve the current step's SLA configuration
    /// 3. Check if the request has exceeded its SLA (created_on + sla_hours &lt; DateTime.UtcNow)
    /// 4. For overdue requests:
    ///    - Call EscalateRequest() to update status
    ///    - Log the escalation action to audit trail
    ///    - Send notification to escalation targets
    /// 
    /// Error handling is implemented per-record to ensure partial failures don't prevent
    /// processing of remaining requests. All operations execute within SecurityContext.OpenSystemScope()
    /// for elevated permissions required by background jobs.
    /// 
    /// Metrics are logged at job completion including:
    /// - Total requests processed
    /// - Number escalated
    /// - Number of errors encountered
    /// </remarks>
    [Job("B2C3D4E5-F6A7-8901-BCDE-F12345678901", "Escalate overdue approval requests", true, JobPriority.Low)]
    public class ApprovalEscalationJob : ErpJob
    {
        #region Constants

        /// <summary>
        /// Source identifier for logging operations.
        /// </summary>
        private const string LOG_SOURCE = "WebVella.Erp.Plugins.Approval.Jobs.ApprovalEscalationJob";

        /// <summary>
        /// Entity name for approval requests.
        /// </summary>
        private const string ENTITY_REQUEST = "approval_request";

        /// <summary>
        /// Entity name for approval steps.
        /// </summary>
        private const string ENTITY_STEP = "approval_step";

        /// <summary>
        /// Field name for record ID.
        /// </summary>
        private const string FIELD_ID = "id";

        /// <summary>
        /// Field name for current step ID on approval request.
        /// </summary>
        private const string FIELD_CURRENT_STEP_ID = "current_step_id";

        /// <summary>
        /// Field name for status on approval request.
        /// </summary>
        private const string FIELD_STATUS = "status";

        /// <summary>
        /// Field name for created timestamp on approval request.
        /// </summary>
        private const string FIELD_CREATED_ON = "created_on";

        /// <summary>
        /// Field name for due date on approval request.
        /// </summary>
        private const string FIELD_DUE_DATE = "due_date";

        /// <summary>
        /// Field name for SLA hours on approval step.
        /// </summary>
        private const string FIELD_SLA_HOURS = "sla_hours";

        /// <summary>
        /// Default escalation reason message for SLA breaches.
        /// </summary>
        private const string ESCALATION_REASON_SLA_EXCEEDED = "SLA exceeded - auto-escalated by system";

        /// <summary>
        /// Action type string for escalated action.
        /// </summary>
        private const string ACTION_ESCALATED = "escalated";

        #endregion

        #region Public Methods

        /// <summary>
        /// Executes the escalation job to process all pending approval requests and escalate those
        /// that have exceeded their SLA configuration.
        /// </summary>
        /// <param name="context">The job execution context providing access to job metadata and scheduling information.</param>
        /// <remarks>
        /// This method:
        /// 1. Opens a system security scope for elevated permissions
        /// 2. Retrieves all pending approval requests
        /// 3. For each request, checks if SLA is exceeded
        /// 4. Escalates overdue requests with proper logging and notifications
        /// 5. Logs execution metrics upon completion
        /// 
        /// Individual request processing is wrapped in try-catch to ensure partial failures
        /// don't prevent processing of remaining requests.
        /// </remarks>
        public override void Execute(JobContext context)
        {
            int processedCount = 0;
            int escalatedCount = 0;
            int errorCount = 0;
            var startTime = DateTime.UtcNow;

            try
            {
                using (SecurityContext.OpenSystemScope())
                {
                    // Initialize services
                    var requestService = new ApprovalRequestService();
                    var routeService = new ApprovalRouteService();
                    var historyService = new ApprovalHistoryService();
                    var notificationService = new NotificationService();

                    // Get system user ID for automated actions
                    var systemUserId = GetSystemUserId();

                    // Retrieve all pending requests
                    var pendingRequests = GetOverdueRequests();

                    if (pendingRequests == null || !pendingRequests.Any())
                    {
                        LogJobMetrics(processedCount, escalatedCount, errorCount, startTime);
                        return;
                    }

                    // Process each pending request
                    foreach (var request in pendingRequests)
                    {
                        processedCount++;

                        try
                        {
                            var requestId = (Guid)request[FIELD_ID];
                            var currentStepId = request[FIELD_CURRENT_STEP_ID] as Guid?;

                            if (!currentStepId.HasValue)
                            {
                                // Skip requests without a current step
                                continue;
                            }

                            // Retrieve step configuration to get SLA hours
                            var step = GetStep(currentStepId.Value);
                            if (step == null)
                            {
                                // Skip if step not found
                                continue;
                            }

                            // Check if request is overdue based on SLA
                            if (!IsRequestOverdue(request, step))
                            {
                                // Request is not yet overdue - skip
                                continue;
                            }

                            // Escalate the overdue request
                            try
                            {
                                // Update request status to escalated via service
                                requestService.EscalateRequest(requestId, systemUserId, ESCALATION_REASON_SLA_EXCEEDED);
                                escalatedCount++;

                                // Get escalation targets for notification
                                var escalationTargets = routeService.GetApproversForStep(currentStepId.Value);
                                
                                if (escalationTargets != null && escalationTargets.Any())
                                {
                                    // Send escalation notification to all targets
                                    foreach (var targetUserId in escalationTargets)
                                    {
                                        try
                                        {
                                            notificationService.SendEscalationNotification(
                                                requestId, 
                                                targetUserId, 
                                                ESCALATION_REASON_SLA_EXCEEDED);
                                        }
                                        catch (Exception notifyEx)
                                        {
                                            // Log notification failure but continue processing
                                            new Log().Create(LogType.Warning, LOG_SOURCE,
                                                $"Failed to send escalation notification for request {requestId} to user {targetUserId}: {notifyEx.Message}");
                                        }
                                    }
                                }
                            }
                            catch (Exception escalateEx)
                            {
                                errorCount++;
                                new Log().Create(LogType.Error, LOG_SOURCE,
                                    $"Failed to escalate request {requestId}: {escalateEx.Message}", escalateEx);
                            }
                        }
                        catch (Exception requestEx)
                        {
                            errorCount++;
                            new Log().Create(LogType.Error, LOG_SOURCE,
                                $"Error processing request during escalation job: {requestEx.Message}", requestEx);
                        }
                    }

                    // Log job completion metrics
                    LogJobMetrics(processedCount, escalatedCount, errorCount, startTime);
                }
            }
            catch (Exception ex)
            {
                // Log critical job failure
                new Log().Create(LogType.Error, LOG_SOURCE,
                    $"Critical error in ApprovalEscalationJob: {ex.Message}", ex);
                
                // Still log metrics for partial processing
                LogJobMetrics(processedCount, escalatedCount, errorCount, startTime);
                
                throw; // Re-throw to signal job failure to scheduler
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Retrieves all pending approval requests that may need escalation.
        /// </summary>
        /// <returns>
        /// An EntityRecordList containing all approval requests with 'pending' status,
        /// ordered by created_on ascending (oldest first to prioritize).
        /// </returns>
        private EntityRecordList GetOverdueRequests()
        {
            try
            {
                var pendingStatus = ApprovalStatus.Pending.ToString().ToLowerInvariant();

                var eqlCommand = $@"SELECT * FROM {ENTITY_REQUEST} 
                                    WHERE {FIELD_STATUS} = @status 
                                    ORDER BY {FIELD_CREATED_ON} ASC";

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("status", pendingStatus)
                };

                var result = new EqlCommand(eqlCommand, eqlParams).Execute();
                return result ?? new EntityRecordList();
            }
            catch (Exception ex)
            {
                new Log().Create(LogType.Error, LOG_SOURCE,
                    $"Error retrieving pending requests for escalation: {ex.Message}", ex);
                return new EntityRecordList();
            }
        }

        /// <summary>
        /// Retrieves an approval step record by its unique identifier.
        /// </summary>
        /// <param name="stepId">The step ID to retrieve.</param>
        /// <returns>The EntityRecord for the step, or null if not found.</returns>
        private EntityRecord GetStep(Guid stepId)
        {
            try
            {
                var eqlCommand = $"SELECT * FROM {ENTITY_STEP} WHERE {FIELD_ID} = @stepId";
                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("stepId", stepId)
                };

                var result = new EqlCommand(eqlCommand, eqlParams).Execute();
                return result?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                new Log().Create(LogType.Warning, LOG_SOURCE,
                    $"Error retrieving step {stepId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Determines if an approval request has exceeded its SLA based on step configuration.
        /// </summary>
        /// <param name="request">The approval request EntityRecord to check.</param>
        /// <param name="step">The approval step EntityRecord containing SLA configuration.</param>
        /// <returns>
        /// True if the request has exceeded its SLA (created_on + sla_hours &lt; DateTime.UtcNow);
        /// False if the request is still within SLA or no SLA is defined.
        /// </returns>
        /// <remarks>
        /// This method performs the following checks:
        /// 1. Verifies the step has a valid sla_hours value greater than 0
        /// 2. Retrieves the request's created_on timestamp
        /// 3. Calculates the SLA deadline (created_on + sla_hours)
        /// 4. Compares against current UTC time
        /// 
        /// Requests without SLA configuration (sla_hours = 0 or null) are never considered overdue.
        /// 
        /// Additionally, if the request has a due_date field set, it will use that as an alternative
        /// check - if due_date has passed, the request is considered overdue.
        /// </remarks>
        private bool IsRequestOverdue(EntityRecord request, EntityRecord step)
        {
            if (request == null || step == null)
            {
                return false;
            }

            // First, check if due_date is set and has passed
            var dueDate = request[FIELD_DUE_DATE] as DateTime?;
            if (dueDate.HasValue && dueDate.Value < DateTime.UtcNow)
            {
                return true;
            }

            // Get SLA hours from step configuration
            var slaHoursValue = step[FIELD_SLA_HOURS];
            int slaHours = 0;

            // Handle different numeric types that might be returned
            if (slaHoursValue is int intValue)
            {
                slaHours = intValue;
            }
            else if (slaHoursValue is decimal decimalValue)
            {
                slaHours = (int)decimalValue;
            }
            else if (slaHoursValue is long longValue)
            {
                slaHours = (int)longValue;
            }
            else if (slaHoursValue is double doubleValue)
            {
                slaHours = (int)doubleValue;
            }

            // If no SLA is defined (0 or negative), request is never overdue
            if (slaHours <= 0)
            {
                return false;
            }

            // Get request creation timestamp
            var createdOn = request[FIELD_CREATED_ON] as DateTime?;
            if (!createdOn.HasValue)
            {
                // Cannot determine if overdue without created_on - assume not overdue
                return false;
            }

            // Calculate SLA deadline
            var slaDeadline = createdOn.Value.AddHours(slaHours);

            // Check if current time exceeds SLA deadline
            return DateTime.UtcNow > slaDeadline;
        }

        /// <summary>
        /// Returns the system user GUID for automated actions performed by the escalation job.
        /// </summary>
        /// <returns>
        /// The GUID of the system user account used for automated operations.
        /// Returns Guid.Empty if no system user is configured.
        /// </returns>
        /// <remarks>
        /// The system user is used as the 'performed_by' value when the job automatically
        /// escalates requests. This allows distinguishing between user-initiated and
        /// system-initiated escalations in the audit trail.
        /// 
        /// The system user GUID is the well-known WebVella system user ID.
        /// </remarks>
        private Guid GetSystemUserId()
        {
            // WebVella system user GUID - this is the well-known system user ID
            // used for automated operations across the platform
            return new Guid("b0223132-23d9-4824-bb20-a6c4d0c9f534");
        }

        /// <summary>
        /// Logs execution metrics for the escalation job.
        /// </summary>
        /// <param name="processedCount">Total number of requests processed.</param>
        /// <param name="escalatedCount">Number of requests that were escalated.</param>
        /// <param name="errorCount">Number of errors encountered during processing.</param>
        /// <param name="startTime">The job start time for duration calculation.</param>
        private void LogJobMetrics(int processedCount, int escalatedCount, int errorCount, DateTime startTime)
        {
            var duration = DateTime.UtcNow - startTime;
            var message = $"ApprovalEscalationJob completed. " +
                         $"Processed: {processedCount}, " +
                         $"Escalated: {escalatedCount}, " +
                         $"Errors: {errorCount}, " +
                         $"Duration: {duration.TotalSeconds:F2}s";

            var logType = errorCount > 0 ? LogType.Warning : LogType.Info;
            new Log().Create(logType, LOG_SOURCE, message);
        }

        #endregion
    }
}
