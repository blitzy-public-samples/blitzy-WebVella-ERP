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
    /// Background job for cleaning up expired approval requests.
    /// Runs daily at 00:10 UTC and queries pending approval requests that have been 
    /// pending beyond a configurable expiration threshold (default 30 days).
    /// For each expired request, marks status as 'expired', sets completed_on to 
    /// current timestamp, and logs a history entry recording the expiration.
    /// </summary>
    /// <remarks>
    /// This job ensures that stale approval requests don't remain in pending state indefinitely.
    /// The expiration period can be adjusted via the EXPIRATION_DAYS constant.
    /// All operations are performed within a system security scope for elevated permissions.
    /// Individual request failures are caught and logged to prevent one failure from 
    /// stopping the entire batch processing.
    /// </remarks>
    [Job("E8C7B4A3-5D6F-4E2A-9B1C-8D7E6F5A4B3C", "Cleanup expired approvals", true, JobPriority.Low)]
    public class CleanupExpiredApprovalsJob : ErpJob
    {
        #region << Constants >>

        /// <summary>
        /// Entity name for approval request records.
        /// </summary>
        private const string APPROVAL_REQUEST_ENTITY = "approval_request";

        /// <summary>
        /// Configurable expiration period in days.
        /// Approval requests pending longer than this period will be marked as expired.
        /// </summary>
        private const int EXPIRATION_DAYS = 30;

        /// <summary>
        /// System user GUID used for automated operations.
        /// This represents the system performing the expiration action automatically.
        /// </summary>
        private static readonly Guid SYSTEM_USER_ID = new Guid("00000000-0000-0000-0000-000000000001");

        #endregion

        #region << Public Methods >>

        /// <summary>
        /// Executes the expired approval cleanup job.
        /// Queries all pending approval requests older than the configured expiration threshold,
        /// marks them as expired, and logs the history action.
        /// </summary>
        /// <param name="context">The job execution context provided by the scheduler.</param>
        public override void Execute(JobContext context)
        {
            using (SecurityContext.OpenSystemScope())
            {
                var recMan = new RecordManager();
                var historyService = new ApprovalHistoryService();
                
                // Calculate the cutoff date for expiration
                var cutoffDate = DateTime.UtcNow.AddDays(-EXPIRATION_DAYS);
                
                // Query pending approval requests that are older than the cutoff date
                var expiredRequests = GetExpiredPendingRequests(cutoffDate);
                
                if (expiredRequests == null || !expiredRequests.Any())
                {
                    // No expired requests to process
                    return;
                }

                int successCount = 0;
                int failureCount = 0;

                foreach (var request in expiredRequests)
                {
                    try
                    {
                        ProcessExpiredRequest(request, recMan, historyService);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        // Log the error but continue processing remaining requests
                        LogError(request, ex);
                    }
                }

                // Log summary of the cleanup operation
                if (successCount > 0 || failureCount > 0)
                {
                    LogSummary(successCount, failureCount);
                }
            }
        }

        #endregion

        #region << Private Methods >>

        /// <summary>
        /// Queries the database for pending approval requests that have exceeded the expiration threshold.
        /// </summary>
        /// <param name="cutoffDate">The date before which requests are considered expired.</param>
        /// <returns>A list of EntityRecord objects representing expired pending requests.</returns>
        private List<EntityRecord> GetExpiredPendingRequests(DateTime cutoffDate)
        {
            try
            {
                var eqlCommand = @"SELECT id, workflow_id, current_step_id, source_entity_name, source_record_id, 
                                          status, requested_by, requested_on 
                                   FROM approval_request 
                                   WHERE status = @status AND requested_on < @cutoffDate";

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("status", "pending"),
                    new EqlParameter("cutoffDate", cutoffDate)
                };

                var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (eqlResult == null)
                {
                    return new List<EntityRecord>();
                }

                return eqlResult.ToList();
            }
            catch (Exception ex)
            {
                // Log error querying expired requests
                try
                {
                    new Log().Create(LogType.Error, "CleanupExpiredApprovalsJob", ex);
                }
                catch
                {
                    // Suppress logging errors
                }
                return new List<EntityRecord>();
            }
        }

        /// <summary>
        /// Processes a single expired approval request by updating its status and logging history.
        /// </summary>
        /// <param name="request">The approval request record to process.</param>
        /// <param name="recMan">The RecordManager instance for database operations.</param>
        /// <param name="historyService">The ApprovalHistoryService for audit logging.</param>
        private void ProcessExpiredRequest(EntityRecord request, RecordManager recMan, ApprovalHistoryService historyService)
        {
            var requestId = (Guid)request["id"];
            var currentStepId = request["current_step_id"] != null ? (Guid)request["current_step_id"] : Guid.Empty;
            var completedOn = DateTime.UtcNow;

            // Create patch record to update the request status to 'expired'
            var patchRecord = new EntityRecord();
            patchRecord["id"] = requestId;
            patchRecord["status"] = "expired";
            patchRecord["completed_on"] = completedOn;

            // Update the approval request record
            var updateResult = recMan.UpdateRecord(APPROVAL_REQUEST_ENTITY, patchRecord);

            if (!updateResult.Success)
            {
                throw new Exception($"Failed to update approval request {requestId}: {updateResult.Message}");
            }

            // Log the expiration action to the audit trail
            // Note: The 'expired' action may not be in the standard valid actions list.
            // We attempt to log it but handle any validation errors gracefully.
            try
            {
                // If current_step_id is empty, we use a placeholder step ID
                var stepIdForHistory = currentStepId != Guid.Empty ? currentStepId : requestId;
                
                historyService.LogAction(
                    requestId: requestId,
                    stepId: stepIdForHistory,
                    action: "expired",
                    performedBy: SYSTEM_USER_ID,
                    comments: $"Automatically expired after {EXPIRATION_DAYS} days of inactivity"
                );
            }
            catch (ArgumentException)
            {
                // The 'expired' action may not be in the valid actions list
                // Log info (since Warning doesn't exist in WebVella LogType) but don't fail the overall expiration process
                try
                {
                    new Log().Create(LogType.Info, "CleanupExpiredApprovalsJob", 
                        $"Could not log history for expired request {requestId}. " +
                        "The 'expired' action may not be in the valid actions list.", string.Empty);
                }
                catch
                {
                    // Suppress logging errors
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail the expiration process
                try
                {
                    new Log().Create(LogType.Info, "CleanupExpiredApprovalsJob", 
                        $"Failed to log history for request {requestId}: {ex.Message}", string.Empty);
                }
                catch
                {
                    // Suppress logging errors
                }
            }
        }

        /// <summary>
        /// Logs an error that occurred while processing an individual approval request.
        /// </summary>
        /// <param name="request">The approval request that caused the error.</param>
        /// <param name="ex">The exception that was thrown.</param>
        private void LogError(EntityRecord request, Exception ex)
        {
            try
            {
                var requestId = request["id"] != null ? request["id"].ToString() : "unknown";
                new Log().Create(LogType.Error, "CleanupExpiredApprovalsJob", 
                    $"Error processing expired approval request {requestId}", ex);
            }
            catch
            {
                // Suppress any errors during logging to prevent cascading failures
            }
        }

        /// <summary>
        /// Logs a summary of the cleanup operation results.
        /// </summary>
        /// <param name="successCount">The number of successfully processed requests.</param>
        /// <param name="failureCount">The number of requests that failed to process.</param>
        private void LogSummary(int successCount, int failureCount)
        {
            try
            {
                // Use Error type for failures, Info for success (Warning doesn't exist in WebVella LogType)
                var logType = failureCount > 0 ? LogType.Error : LogType.Info;
                var message = $"Cleanup completed: {successCount} expired requests processed successfully, {failureCount} failures";
                new Log().Create(logType, "CleanupExpiredApprovalsJob", message, string.Empty);
            }
            catch
            {
                // Suppress any errors during logging
            }
        }

        #endregion
    }
}
