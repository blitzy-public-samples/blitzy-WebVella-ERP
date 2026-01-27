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
    /// Background job for processing approval notifications per STORY-006 AC1-AC5.
    /// Runs on a 5-minute interval to query pending approval requests and send
    /// notification emails to assigned approvers.
    /// </summary>
    /// <remarks>
    /// Per AC2: Queries approval_request records with status "Pending" where last_notification_sent
    /// is null or older than configured reminder interval (24 hours default).
    /// Per AC3: Resolves current step approvers via ApprovalRouteService.GetApproversForStep()
    /// and queues notification emails.
    /// Per AC4: Updates approval_request.last_notification_sent timestamp and increments
    /// notification_count after successful email queue.
    /// Per AC5: Completes within 60 seconds for typical workloads.
    /// </remarks>
    [Job("A7B3C1D2-E4F5-6789-ABCD-EF0123456789", "Process approval notifications", true, JobPriority.Low)]
    public class ProcessApprovalNotificationsJob : ErpJob
    {
        #region Constants

        /// <summary>
        /// Entity name for approval request records.
        /// </summary>
        private const string APPROVAL_REQUEST_ENTITY = "approval_request";

        /// <summary>
        /// Per AC2: Batch size limit - process up to 50 records per execution cycle.
        /// </summary>
        private const int BATCH_SIZE = 50;

        /// <summary>
        /// Reminder interval in hours. Requests will receive notifications if
        /// last_notification_sent is null or older than this interval.
        /// </summary>
        private const int REMINDER_INTERVAL_HOURS = 24;

        /// <summary>
        /// Status value for pending requests.
        /// </summary>
        private const string STATUS_PENDING = "pending";

        /// <summary>
        /// System user GUID used for automated operations.
        /// </summary>
        private static readonly Guid SYSTEM_USER_ID = new Guid("00000000-0000-0000-0000-000000000001");

        #endregion

        #region Public Methods

        /// <summary>
        /// Executes the notification processing job per STORY-006 AC1-AC5.
        /// Queries pending approval requests, resolves approvers, and sends notifications.
        /// </summary>
        /// <param name="context">The job execution context provided by the scheduler.</param>
        public override void Execute(JobContext context)
        {
            using (SecurityContext.OpenSystemScope())
            {
                var recMan = new RecordManager();
                var routeService = new ApprovalRouteService();
                var notificationService = new ApprovalNotificationService();
                
                int processedCount = 0;
                int errorCount = 0;
                
                try
                {
                    // Per AC2: Query pending requests needing notification
                    var pendingRequests = GetPendingRequestsForNotification();
                    
                    if (pendingRequests == null || !pendingRequests.Any())
                    {
                        // No pending notifications - job completes successfully
                        return;
                    }

                    foreach (var request in pendingRequests)
                    {
                        try
                        {
                            // Process single request notification
                            ProcessRequestNotification(request, recMan, routeService, notificationService);
                            processedCount++;
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            LogRequestError(request, ex);
                            // Continue processing remaining requests per AC18
                        }
                    }

                    // Log summary if there was any activity
                    if (processedCount > 0 || errorCount > 0)
                    {
                        LogSummary(processedCount, errorCount);
                    }
                }
                catch (Exception ex)
                {
                    // Log critical job-level failure
                    new Log().Create(LogType.Error, "ProcessApprovalNotificationsJob",
                        "Critical error during notification job execution", ex);
                    throw;
                }
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Per AC2: Queries approval_request records with status "Pending" where
        /// last_notification_sent is null or older than the configured reminder interval.
        /// </summary>
        /// <returns>List of EntityRecord objects representing requests needing notification.</returns>
        private List<EntityRecord> GetPendingRequestsForNotification()
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddHours(-REMINDER_INTERVAL_HOURS);

                // Per AC2: Query pending requests where last_notification_sent is null or older than interval
                var eqlCommand = @"SELECT id, workflow_id, current_step_id, source_entity, source_record_id, 
                                          status, requested_by, requested_on, last_notification_sent, notification_count
                                   FROM approval_request 
                                   WHERE status = @status 
                                   AND (last_notification_sent = NULL OR last_notification_sent < @cutoff)
                                   ORDER BY requested_on ASC 
                                   PAGE 1 PAGESIZE @batchSize";

                var parameters = new List<EqlParameter>
                {
                    new EqlParameter("status", STATUS_PENDING),
                    new EqlParameter("cutoff", cutoffTime),
                    new EqlParameter("batchSize", BATCH_SIZE)
                };

                var result = new EqlCommand(eqlCommand, parameters).Execute();

                if (result == null)
                {
                    return new List<EntityRecord>();
                }

                return result.ToList();
            }
            catch (Exception ex)
            {
                new Log().Create(LogType.Error, "ProcessApprovalNotificationsJob",
                    "Error querying pending requests for notification", ex);
                return new List<EntityRecord>();
            }
        }

        /// <summary>
        /// Per AC3: Processes a single request notification by resolving approvers and sending emails.
        /// Per AC4: Updates notification tracking fields after successful notification.
        /// </summary>
        /// <param name="request">The approval request record.</param>
        /// <param name="recMan">RecordManager for database operations.</param>
        /// <param name="routeService">ApprovalRouteService for approver resolution.</param>
        /// <param name="notificationService">ApprovalNotificationService for sending emails.</param>
        private void ProcessRequestNotification(
            EntityRecord request,
            RecordManager recMan,
            ApprovalRouteService routeService,
            ApprovalNotificationService notificationService)
        {
            var requestId = (Guid)request["id"];
            var currentStepId = request["current_step_id"] != null ? (Guid?)request["current_step_id"] : null;

            if (!currentStepId.HasValue)
            {
                // No current step - cannot determine approvers
                return;
            }

            // Per AC3: Resolve current step approvers
            var approverIds = routeService.GetApproversForStep(currentStepId.Value);

            if (approverIds == null || !approverIds.Any())
            {
                // No approvers configured for this step
                return;
            }

            // Send notification to each approver
            bool notificationSent = false;
            foreach (var approverId in approverIds)
            {
                try
                {
                    // Send approval request notification
                    notificationService.SendApprovalRequestNotification(requestId, approverId);
                    notificationSent = true;
                }
                catch (Exception ex)
                {
                    // Log individual notification failure but continue with others
                    new Log().Create(LogType.Info, "ProcessApprovalNotificationsJob",
                        $"Failed to send notification for request {requestId} to approver {approverId}: {ex.Message}", string.Empty);
                }
            }

            // Per AC4: Update notification tracking on the approval_request
            if (notificationSent)
            {
                UpdateNotificationTracking(requestId, request, recMan);
            }
        }

        /// <summary>
        /// Per AC4: Updates approval_request.last_notification_sent timestamp and
        /// increments notification_count after successful email queue.
        /// </summary>
        /// <param name="requestId">The request ID to update.</param>
        /// <param name="request">The original request record (to read current notification_count).</param>
        /// <param name="recMan">RecordManager for database operations.</param>
        private void UpdateNotificationTracking(Guid requestId, EntityRecord request, RecordManager recMan)
        {
            try
            {
                // Get current notification count (default to 0)
                var currentCount = request["notification_count"] != null
                    ? Convert.ToInt32(request["notification_count"])
                    : 0;

                var patchRecord = new EntityRecord();
                patchRecord["id"] = requestId;
                patchRecord["last_notification_sent"] = DateTime.UtcNow;
                patchRecord["notification_count"] = currentCount + 1;

                var updateResult = recMan.UpdateRecord(APPROVAL_REQUEST_ENTITY, patchRecord);

                if (!updateResult.Success)
                {
                    new Log().Create(LogType.Info, "ProcessApprovalNotificationsJob",
                        $"Failed to update notification tracking for request {requestId}: {updateResult.Message}", string.Empty);
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail - notification was already sent
                new Log().Create(LogType.Info, "ProcessApprovalNotificationsJob",
                    $"Error updating notification tracking for request {requestId}", ex);
            }
        }

        /// <summary>
        /// Logs an error that occurred while processing a specific request.
        /// </summary>
        /// <param name="request">The request that caused the error.</param>
        /// <param name="ex">The exception that occurred.</param>
        private void LogRequestError(EntityRecord request, Exception ex)
        {
            try
            {
                var requestId = request["id"] != null ? request["id"].ToString() : "unknown";
                new Log().Create(LogType.Error, "ProcessApprovalNotificationsJob",
                    $"Error processing notification for request {requestId}", ex);
            }
            catch
            {
                // Suppress logging errors
            }
        }

        /// <summary>
        /// Logs a summary of the notification job execution.
        /// </summary>
        /// <param name="processedCount">Number of requests successfully processed.</param>
        /// <param name="errorCount">Number of requests that failed.</param>
        private void LogSummary(int processedCount, int errorCount)
        {
            try
            {
                var logType = errorCount > 0 ? LogType.Error : LogType.Info;
                var message = $"Notification job completed: {processedCount} requests processed, {errorCount} errors";
                new Log().Create(logType, "ProcessApprovalNotificationsJob", message, string.Empty);
            }
            catch
            {
                // Suppress logging errors
            }
        }

        #endregion
    }
}
