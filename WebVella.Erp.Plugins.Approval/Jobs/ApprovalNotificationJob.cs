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
    /// Background job that sends email notifications to pending approvers.
    /// Runs every 5 minutes per SchedulePlan configuration in ApprovalPlugin.cs.
    /// </summary>
    /// <remarks>
    /// This job implements the notification workflow for approval requests:
    /// 
    /// 1. Queries all pending approval requests that need notification
    /// 2. For each request, determines the current approval step
    /// 3. Retrieves the list of authorized approvers for that step
    /// 4. Sends email notifications to each approver via NotificationService
    /// 
    /// To prevent notification spam, the job filters out requests that have been
    /// notified within the last 24 hours using the last_notification_on field.
    /// 
    /// Error Handling:
    /// - Individual notification failures do not halt the job
    /// - All exceptions are logged for diagnostic purposes
    /// - The job continues processing remaining requests on partial failures
    /// 
    /// Security:
    /// - Uses SecurityContext.OpenSystemScope() for elevated system-level operations
    /// - All database operations run within the system security context
    /// </remarks>
    [Job("A1B2C3D4-E5F6-7890-ABCD-EF1234567890", "Send pending approval notifications", true, JobPriority.Low)]
    public class ApprovalNotificationJob : ErpJob
    {
        #region Constants

        /// <summary>
        /// Source identifier used for diagnostic logging.
        /// </summary>
        private const string LOG_SOURCE = "WebVella.Erp.Plugins.Approval.Jobs.ApprovalNotificationJob";

        /// <summary>
        /// Number of hours between notifications for the same request to prevent spam.
        /// Requests notified within this window will be skipped.
        /// </summary>
        private const int NOTIFICATION_COOLDOWN_HOURS = 24;

        /// <summary>
        /// Entity name for approval requests.
        /// </summary>
        private const string ENTITY_APPROVAL_REQUEST = "approval_request";

        /// <summary>
        /// Field name for request ID.
        /// </summary>
        private const string FIELD_ID = "id";

        /// <summary>
        /// Field name for current step ID.
        /// </summary>
        private const string FIELD_CURRENT_STEP_ID = "current_step_id";

        /// <summary>
        /// Field name for status.
        /// </summary>
        private const string FIELD_STATUS = "status";

        /// <summary>
        /// Field name for last notification timestamp.
        /// </summary>
        private const string FIELD_LAST_NOTIFICATION_ON = "last_notification_on";

        #endregion

        #region Public Methods

        /// <summary>
        /// Executes the notification job to send pending approval notifications.
        /// Called by the WebVella job scheduler based on the configured SchedulePlan.
        /// </summary>
        /// <param name="context">
        /// The job execution context providing job metadata and configuration.
        /// Contains job ID, attributes, result storage, and abort status information.
        /// </param>
        /// <remarks>
        /// Execution flow:
        /// 1. Open system security scope for elevated database access
        /// 2. Query pending approval requests needing notification
        /// 3. For each request:
        ///    a. Get current step ID
        ///    b. Retrieve authorized approvers for the step
        ///    c. Send notification to each approver
        /// 4. Update last_notification_on timestamp for processed requests
        /// 5. Log job completion metrics
        /// 
        /// All operations are wrapped in try-catch to ensure job stability.
        /// Individual request failures are logged but do not fail the entire job.
        /// </remarks>
        public override void Execute(JobContext context)
        {
            int totalRequestsProcessed = 0;
            int totalNotificationsSent = 0;
            int totalErrors = 0;

            try
            {
                // Log job start
                LogJobStart();

                // Open system security scope for elevated operations
                using (SecurityContext.OpenSystemScope())
                {
                    // Instantiate required services
                    var requestService = new ApprovalRequestService();
                    var routeService = new ApprovalRouteService();
                    var notificationService = new NotificationService();

                    // Get pending requests that need notification (filters out recently notified)
                    var pendingRequests = GetPendingRequestsNeedingNotification(requestService);

                    if (pendingRequests == null || !pendingRequests.Any())
                    {
                        LogJobCompletion(totalRequestsProcessed, totalNotificationsSent, totalErrors);
                        return;
                    }

                    // Process each pending request
                    foreach (var request in pendingRequests)
                    {
                        try
                        {
                            // Extract request ID and current step ID
                            var requestId = (Guid)request[FIELD_ID];
                            var currentStepId = request[FIELD_CURRENT_STEP_ID] as Guid?;

                            if (!currentStepId.HasValue)
                            {
                                // Request has no current step - skip with warning
                                LogWarning($"Pending request {requestId} has no current_step_id assigned. Skipping notification.");
                                continue;
                            }

                            // Get list of authorized approvers for this step
                            var approverIds = routeService.GetApproversForStep(currentStepId.Value);

                            if (approverIds == null || !approverIds.Any())
                            {
                                // No approvers configured for this step - skip with warning
                                LogWarning($"No approvers found for step {currentStepId.Value} on request {requestId}. Skipping notification.");
                                continue;
                            }

                            // Send notification to each approver
                            foreach (var approverId in approverIds)
                            {
                                try
                                {
                                    notificationService.SendPendingApprovalNotification(requestId, approverId);
                                    totalNotificationsSent++;
                                }
                                catch (Exception approverEx)
                                {
                                    // Log individual approver notification failure but continue
                                    LogError($"Failed to send notification for request {requestId} to approver {approverId}", approverEx);
                                    totalErrors++;
                                }
                            }

                            // Update last_notification_on timestamp for this request
                            UpdateLastNotificationTimestamp(requestId);

                            totalRequestsProcessed++;
                        }
                        catch (Exception requestEx)
                        {
                            // Log individual request processing failure but continue with other requests
                            var requestIdForLog = request[FIELD_ID]?.ToString() ?? "unknown";
                            LogError($"Failed to process notification for request {requestIdForLog}", requestEx);
                            totalErrors++;
                        }
                    }
                }

                // Log job completion with metrics
                LogJobCompletion(totalRequestsProcessed, totalNotificationsSent, totalErrors);
            }
            catch (Exception ex)
            {
                // Log critical job failure
                LogError("Critical failure in ApprovalNotificationJob.Execute()", ex);
                // Do not rethrow - allow job to complete without marking as failed for transient issues
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Retrieves pending approval requests that need notification, filtering out
        /// requests that have been notified within the notification cooldown window.
        /// </summary>
        /// <param name="requestService">The approval request service instance for querying requests.</param>
        /// <returns>
        /// A list of EntityRecord objects representing pending requests needing notification,
        /// or an empty list if no requests require notification.
        /// </returns>
        /// <remarks>
        /// This method implements anti-spam logic by filtering requests based on:
        /// - Status must be 'pending'
        /// - last_notification_on must be null OR older than NOTIFICATION_COOLDOWN_HOURS
        /// 
        /// The query uses EQL (Entity Query Language) for parameterized filtering.
        /// </remarks>
        private List<EntityRecord> GetPendingRequestsNeedingNotification(ApprovalRequestService requestService)
        {
            try
            {
                // Get all pending requests via service
                var allPendingRequests = requestService.GetRequestsByStatus(ApprovalStatus.Pending.ToString().ToLowerInvariant());

                if (allPendingRequests == null || !allPendingRequests.Any())
                {
                    return new List<EntityRecord>();
                }

                // Calculate the notification cooldown threshold
                var cooldownThreshold = DateTime.UtcNow.AddHours(-NOTIFICATION_COOLDOWN_HOURS);

                // Filter requests that haven't been notified recently
                var requestsNeedingNotification = allPendingRequests
                    .Where(r => 
                    {
                        var lastNotification = r[FIELD_LAST_NOTIFICATION_ON] as DateTime?;
                        // Include if never notified OR last notification is older than cooldown period
                        return !lastNotification.HasValue || lastNotification.Value < cooldownThreshold;
                    })
                    .ToList();

                return requestsNeedingNotification;
            }
            catch (Exception ex)
            {
                LogError("Failed to retrieve pending requests needing notification", ex);
                return new List<EntityRecord>();
            }
        }

        /// <summary>
        /// Updates the last_notification_on timestamp for a request to track when
        /// notifications were last sent. This prevents notification spam.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request to update.</param>
        /// <remarks>
        /// This method uses RecordManager to update only the last_notification_on field.
        /// Failures are logged but do not throw exceptions to prevent job disruption.
        /// </remarks>
        private void UpdateLastNotificationTimestamp(Guid requestId)
        {
            try
            {
                var patchRecord = new EntityRecord();
                patchRecord[FIELD_ID] = requestId;
                patchRecord[FIELD_LAST_NOTIFICATION_ON] = DateTime.UtcNow;

                var updateResult = new RecordManager().UpdateRecord(ENTITY_APPROVAL_REQUEST, patchRecord);

                if (!updateResult.Success)
                {
                    LogWarning($"Failed to update last_notification_on for request {requestId}: {updateResult.Message}");
                }
            }
            catch (Exception ex)
            {
                // Log but don't throw - timestamp update failure should not fail the job
                LogWarning($"Exception updating last_notification_on for request {requestId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Logs the start of job execution with timestamp.
        /// </summary>
        private void LogJobStart()
        {
            try
            {
                new Log().Create(
                    LogType.Info,
                    LOG_SOURCE,
                    "ApprovalNotificationJob started",
                    $"Job execution started at {DateTime.UtcNow:O}"
                );
            }
            catch
            {
                // Suppress logging failures
            }
        }

        /// <summary>
        /// Logs job completion with execution metrics.
        /// </summary>
        /// <param name="requestsProcessed">Number of requests successfully processed.</param>
        /// <param name="notificationsSent">Total number of notifications sent.</param>
        /// <param name="errorCount">Number of errors encountered during execution.</param>
        private void LogJobCompletion(int requestsProcessed, int notificationsSent, int errorCount)
        {
            try
            {
                var message = $"ApprovalNotificationJob completed - Requests: {requestsProcessed}, Notifications: {notificationsSent}, Errors: {errorCount}";
                var details = $"Completed at {DateTime.UtcNow:O}. Processed {requestsProcessed} approval requests, " +
                              $"sent {notificationsSent} notifications to approvers. Encountered {errorCount} errors.";

                new Log().Create(
                    LogType.Info,
                    LOG_SOURCE,
                    message,
                    details
                );
            }
            catch
            {
                // Suppress logging failures
            }
        }

        /// <summary>
        /// Logs an error message with exception details.
        /// </summary>
        /// <param name="message">The error message describing what failed.</param>
        /// <param name="ex">The exception that was caught.</param>
        private void LogError(string message, Exception ex)
        {
            try
            {
                new Log().Create(
                    LogType.Error,
                    LOG_SOURCE,
                    message,
                    ex
                );
            }
            catch
            {
                // Suppress logging failures
            }
        }

        /// <summary>
        /// Logs a warning message for non-critical issues.
        /// </summary>
        /// <param name="message">The warning message to log.</param>
        private void LogWarning(string message)
        {
            try
            {
                new Log().Create(
                    LogType.Info,
                    LOG_SOURCE,
                    $"[WARNING] {message}",
                    (string)null
                );
            }
            catch
            {
                // Suppress logging failures
            }
        }

        #endregion
    }
}
