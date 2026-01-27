using System;
using System.Collections.Generic;
using System.Linq;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Diagnostics;
using WebVella.Erp.Eql;
using WebVella.Erp.Jobs;
using WebVella.Erp.Plugins.Approval.Services;

namespace WebVella.Erp.Plugins.Approval.Jobs
{
    /// <summary>
    /// Background job for processing approval escalations.
    /// Runs on 30-minute interval cycle to identify and handle timed-out approval requests.
    /// Queries pending approval requests where timeout has been exceeded based on the step's
    /// timeout_hours setting and request's requested_on timestamp. For each timed-out request,
    /// escalates to next approver or marks as escalated status.
    /// </summary>
    /// <remarks>
    /// This job is a critical component of the approval workflow system, ensuring that:
    /// - Requests do not remain pending indefinitely when approvers are unavailable
    /// - Escalation notifications are sent to appropriate supervisors or alternate approvers
    /// - Audit trail is maintained for all escalation events
    /// 
    /// The job processes requests one at a time with individual error handling to ensure
    /// that a failure processing one request does not prevent others from being processed.
    /// 
    /// Schedule: Every 30 minutes (configured via ApprovalPlugin.SetSchedulePlans)
    /// </remarks>
    [Job("A7B3C9D1-E4F5-4A6B-8C7D-9E0F1A2B3C4D", "Process approval escalations", true, JobPriority.Low)]
    public class ProcessApprovalEscalationsJob : ErpJob
    {
        #region << Constants >>

        /// <summary>
        /// Entity name for approval requests.
        /// </summary>
        private const string APPROVAL_REQUEST_ENTITY = "approval_request";

        /// <summary>
        /// Entity name for approval steps.
        /// </summary>
        private const string APPROVAL_STEP_ENTITY = "approval_step";

        /// <summary>
        /// Status value for pending requests.
        /// </summary>
        private const string STATUS_PENDING = "pending";

        /// <summary>
        /// Status value for escalated requests.
        /// </summary>
        private const string STATUS_ESCALATED = "escalated";

        /// <summary>
        /// Action value for escalation events in history.
        /// </summary>
        private const string ACTION_ESCALATED = "escalated";

        /// <summary>
        /// System user ID used for automated escalation actions.
        /// This GUID represents the system/automated user for audit purposes.
        /// </summary>
        private static readonly Guid SYSTEM_USER_ID = new Guid("B0B1C2D3-E4F5-4A6B-8C7D-9E0F1A2B3C4E");

        #endregion

        #region << Execute Method >>

        /// <summary>
        /// Executes the escalation processing job.
        /// Queries all pending approval requests, evaluates timeout conditions for each,
        /// and escalates requests that have exceeded their step's timeout threshold.
        /// </summary>
        /// <param name="context">The job execution context provided by the WebVella job scheduler.</param>
        /// <remarks>
        /// This method:
        /// 1. Opens a system security scope for elevated permissions
        /// 2. Retrieves all pending approval requests
        /// 3. For each request with a current step:
        ///    - Loads the step configuration to get timeout_hours
        ///    - Calculates if the timeout has been exceeded
        ///    - If exceeded: updates status, logs history, sends notification
        /// 4. Handles errors per-request to ensure processing continues
        /// 5. Logs summary and any errors for monitoring
        /// </remarks>
        public override void Execute(JobContext context)
        {
            using (SecurityContext.OpenSystemScope())
            {
                // Initialize services
                var approvalRequestService = new ApprovalRequestService();
                var approvalHistoryService = new ApprovalHistoryService();
                var approvalNotificationService = new ApprovalNotificationService();
                var recordManager = new RecordManager();

                // Track processing statistics
                int processedCount = 0;
                int escalatedCount = 0;
                int errorCount = 0;

                try
                {
                    // Step 1: Get all pending approval requests
                    var pendingRequests = approvalRequestService.GetByStatus(STATUS_PENDING);

                    if (pendingRequests == null || !pendingRequests.Any())
                    {
                        // No pending requests to process - this is normal
                        return;
                    }

                    // Step 2: Process each pending request
                    foreach (var request in pendingRequests)
                    {
                        processedCount++;

                        try
                        {
                            // Skip requests without a current step (shouldn't happen but be safe)
                            if (!request.CurrentStepId.HasValue)
                            {
                                continue;
                            }

                            // Step 3: Load the current step to check timeout configuration
                            var currentStepId = request.CurrentStepId.Value;
                            var step = LoadApprovalStep(currentStepId);

                            if (step == null)
                            {
                                // Step not found - log warning and continue
                                new Log().Create(LogType.Info, "ProcessApprovalEscalationsJob",
                                    $"Approval step with ID '{currentStepId}' not found for request '{request.Id}'.", string.Empty);
                                continue;
                            }

                            // Step 4: Check if step has timeout configured
                            var timeoutHours = GetTimeoutHours(step);
                            if (!timeoutHours.HasValue || timeoutHours.Value <= 0)
                            {
                                // No timeout configured for this step - skip
                                continue;
                            }

                            // Step 5: Calculate if timeout has been exceeded
                            var requestedOn = request.RequestedOn;
                            var timeoutThreshold = requestedOn.AddHours(timeoutHours.Value);

                            if (DateTime.UtcNow < timeoutThreshold)
                            {
                                // Timeout not yet exceeded - skip
                                continue;
                            }

                            // Step 6: Request has timed out - process escalation
                            ProcessEscalation(
                                request,
                                step,
                                recordManager,
                                approvalHistoryService,
                                approvalNotificationService
                            );

                            escalatedCount++;
                        }
                        catch (Exception requestEx)
                        {
                            // Log error for this specific request but continue processing others
                            errorCount++;
                            new Log().Create(LogType.Error, "ProcessApprovalEscalationsJob",
                                $"Error processing escalation for request '{request.Id}'", requestEx);
                        }
                    }

                    // Log summary
                    if (escalatedCount > 0 || errorCount > 0)
                    {
                        new Log().Create(LogType.Info, "ProcessApprovalEscalationsJob",
                            $"Escalation job completed. Processed: {processedCount}, Escalated: {escalatedCount}, Errors: {errorCount}", string.Empty);
                    }
                }
                catch (Exception ex)
                {
                    // Log fatal error that prevented job execution
                    new Log().Create(LogType.Error, "ProcessApprovalEscalationsJob", 
                        "Fatal error in escalation job", ex);
                    throw;
                }
            }
        }

        #endregion

        #region << Private Helper Methods >>

        /// <summary>
        /// Loads an approval step record by its ID using EQL query.
        /// </summary>
        /// <param name="stepId">The unique identifier of the approval step.</param>
        /// <returns>The EntityRecord for the step, or null if not found.</returns>
        private EntityRecord LoadApprovalStep(Guid stepId)
        {
            if (stepId == Guid.Empty)
            {
                return null;
            }

            try
            {
                var eqlCommand = @"SELECT id, workflow_id, step_order, name, approver_type, 
                                          approver_id, timeout_hours, is_final 
                                   FROM approval_step 
                                   WHERE id = @stepId";

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("stepId", stepId)
                };

                var result = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (result != null && result.Any())
                {
                    return result.First();
                }
            }
            catch (Exception ex)
            {
                new Log().Create(LogType.Error, "ProcessApprovalEscalationsJob",
                    $"Error loading approval step '{stepId}'", ex);
            }

            return null;
        }

        /// <summary>
        /// Extracts the timeout_hours value from a step record.
        /// </summary>
        /// <param name="step">The approval step entity record.</param>
        /// <returns>The timeout hours if configured, or null if not set.</returns>
        private int? GetTimeoutHours(EntityRecord step)
        {
            if (step == null || !step.Properties.ContainsKey("timeout_hours"))
            {
                return null;
            }

            var timeoutValue = step["timeout_hours"];
            if (timeoutValue == null)
            {
                return null;
            }

            // Handle various numeric types that might be returned
            if (timeoutValue is int intValue)
            {
                return intValue;
            }
            else if (timeoutValue is decimal decimalValue)
            {
                return (int)decimalValue;
            }
            else if (timeoutValue is long longValue)
            {
                return (int)longValue;
            }
            else if (timeoutValue is double doubleValue)
            {
                return (int)doubleValue;
            }
            else if (int.TryParse(timeoutValue.ToString(), out int parsedValue))
            {
                return parsedValue;
            }

            return null;
        }

        /// <summary>
        /// Gets the approver ID from a step record for escalation notification.
        /// </summary>
        /// <param name="step">The approval step entity record.</param>
        /// <returns>The approver ID if available, or Guid.Empty if not configured.</returns>
        private Guid GetApproverId(EntityRecord step)
        {
            if (step == null || !step.Properties.ContainsKey("approver_id"))
            {
                return Guid.Empty;
            }

            var approverValue = step["approver_id"];
            if (approverValue == null)
            {
                return Guid.Empty;
            }

            if (approverValue is Guid guidValue)
            {
                return guidValue;
            }

            if (Guid.TryParse(approverValue.ToString(), out Guid parsedGuid))
            {
                return parsedGuid;
            }

            return Guid.Empty;
        }

        /// <summary>
        /// Processes the escalation for a timed-out approval request.
        /// Per AC8: Evaluates next step to determine escalation target and updates current_step_id
        /// to the escalation step, or marks request as "escalated" if no further steps exist.
        /// Logs the action to history and sends an escalation notification.
        /// </summary>
        /// <param name="request">The approval request to escalate.</param>
        /// <param name="step">The current step of the request.</param>
        /// <param name="recordManager">RecordManager instance for database operations.</param>
        /// <param name="historyService">ApprovalHistoryService for logging the escalation action.</param>
        /// <param name="notificationService">ApprovalNotificationService for sending escalation alerts.</param>
        private void ProcessEscalation(
            Api.ApprovalRequestModel request,
            EntityRecord step,
            RecordManager recordManager,
            ApprovalHistoryService historyService,
            ApprovalNotificationService notificationService)
        {
            var requestId = request.Id;
            var currentStepId = request.CurrentStepId.Value;
            var previousStatus = request.Status;
            
            // Per AC8: Evaluate next step to determine escalation target
            var routeService = new ApprovalRouteService();
            var nextStep = routeService.GetNextStep(request.WorkflowId, currentStepId);
            
            var patchRecord = new EntityRecord();
            patchRecord["id"] = requestId;
            
            string newStatus;
            string escalationComments;
            Guid notificationTargetStep;
            
            if (nextStep != null)
            {
                // Advance workflow to next step (escalation by advancing)
                patchRecord["current_step_id"] = nextStep.Id;
                newStatus = STATUS_PENDING; // Status remains pending, but at higher level
                escalationComments = $"Request automatically escalated due to timeout. " +
                                    $"Step timeout: {GetTimeoutHours(step)} hours exceeded. " +
                                    $"Advanced from step '{step["name"]}' to step '{nextStep.Name}'.";
                notificationTargetStep = nextStep.Id;
            }
            else
            {
                // No further steps exist - mark as escalated status (terminal)
                patchRecord["status"] = STATUS_ESCALATED;
                newStatus = STATUS_ESCALATED;
                escalationComments = $"Request automatically escalated due to timeout. " +
                                    $"Step timeout: {GetTimeoutHours(step)} hours exceeded. " +
                                    $"No further escalation steps available - marked as escalated.";
                notificationTargetStep = currentStepId;
            }

            var updateResult = recordManager.UpdateRecord(APPROVAL_REQUEST_ENTITY, patchRecord);
            if (!updateResult.Success)
            {
                throw new Exception($"Failed to update request during escalation: {updateResult.Message}");
            }

            // Step 2: Log the escalation action to history with status tracking
            historyService.LogAction(
                requestId: requestId,
                stepId: currentStepId,
                action: ACTION_ESCALATED,
                performedBy: SYSTEM_USER_ID,
                comments: escalationComments,
                previousStatus: previousStatus,
                newStatus: newStatus
            );

            // Step 3: Send escalation notification
            // Get the approver ID from the target step for notification
            var approverId = nextStep != null ? nextStep.ApproverId : GetApproverId(step);
            if (approverId.HasValue && approverId.Value != Guid.Empty)
            {
                try
                {
                    notificationService.SendEscalationNotification(requestId, approverId.Value);
                }
                catch (Exception notifyEx)
                {
                    // Log notification failure but don't fail the escalation
                    new Log().Create(LogType.Info, "ProcessApprovalEscalationsJob",
                        $"Failed to send escalation notification for request '{requestId}'", notifyEx);
                }
            }
            else
            {
                // Log that no approver was configured for notification
                new Log().Create(LogType.Info, "ProcessApprovalEscalationsJob",
                    $"No approver configured for escalation notification on request '{requestId}'.", string.Empty);
            }
        }

        #endregion
    }
}
