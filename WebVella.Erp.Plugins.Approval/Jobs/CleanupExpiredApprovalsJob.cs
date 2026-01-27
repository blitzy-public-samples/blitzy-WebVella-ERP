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
    /// Background job for archiving completed approval requests.
    /// Per STORY-006 AC12/AC13: Runs daily to archive approval_request records with terminal 
    /// status (Approved, Rejected, Cancelled) where created_on is older than the configured 
    /// retention period (default 365 days).
    /// Archives eligible records by setting is_archived flag to true (soft delete pattern),
    /// preserving data for compliance queries while excluding from active workflow queries.
    /// </summary>
    /// <remarks>
    /// This job ensures database performance by archiving old completed records while
    /// maintaining full audit trail for compliance requirements.
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
        /// Per AC12: Configurable retention period in days (default 365 days).
        /// Terminal status approval requests older than this period will be archived.
        /// </summary>
        private const int RETENTION_DAYS = 365;

        /// <summary>
        /// Maximum number of records to process per job execution to prevent timeout.
        /// </summary>
        private const int BATCH_SIZE = 100;

        /// <summary>
        /// Terminal status indicating approved request.
        /// </summary>
        private const string STATUS_APPROVED = "approved";

        /// <summary>
        /// Terminal status indicating rejected request.
        /// </summary>
        private const string STATUS_REJECTED = "rejected";

        /// <summary>
        /// Terminal status indicating cancelled request.
        /// </summary>
        private const string STATUS_CANCELLED = "cancelled";

        /// <summary>
        /// System user GUID used for automated operations.
        /// This represents the system performing the archival action automatically.
        /// </summary>
        private static readonly Guid SYSTEM_USER_ID = new Guid("00000000-0000-0000-0000-000000000001");

        #endregion

        #region << Public Methods >>

        /// <summary>
        /// Executes the approval archival job per STORY-006 AC12-AC14.
        /// Queries terminal status approval requests older than the configured retention period,
        /// archives them by setting is_archived flag, and logs cleanup statistics.
        /// </summary>
        /// <param name="context">The job execution context provided by the scheduler.</param>
        public override void Execute(JobContext context)
        {
            using (SecurityContext.OpenSystemScope())
            {
                var recMan = new RecordManager();
                
                // Per AC12: Calculate the cutoff date based on retention period (365 days)
                var cutoffDate = DateTime.UtcNow.AddDays(-RETENTION_DAYS);
                
                // Per AC12: Query terminal status requests older than retention period
                var recordsToArchive = GetTerminalStatusRequestsForArchival(cutoffDate);
                
                if (recordsToArchive == null || !recordsToArchive.Any())
                {
                    // No records to archive - this is normal operation
                    return;
                }

                int archivedCount = 0;
                int errorCount = 0;

                foreach (var request in recordsToArchive)
                {
                    try
                    {
                        // Per AC13: Archive by setting is_archived flag to true
                        ArchiveRequest(request, recMan);
                        archivedCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        // Log the error but continue processing remaining requests
                        LogError(request, ex);
                    }
                }

                // Per AC14: Log cleanup statistics
                if (archivedCount > 0 || errorCount > 0)
                {
                    LogSummary(archivedCount, errorCount);
                }
            }
        }

        #endregion

        #region << Private Methods >>

        /// <summary>
        /// Per AC12: Queries approval_request records with terminal status (Approved, Rejected, Cancelled)
        /// where created_on is older than the configured retention period.
        /// </summary>
        /// <param name="cutoffDate">The date before which requests are eligible for archival.</param>
        /// <returns>A list of EntityRecord objects representing records to archive.</returns>
        private List<EntityRecord> GetTerminalStatusRequestsForArchival(DateTime cutoffDate)
        {
            try
            {
                // Per AC12: Query terminal status records older than retention period
                // Per AC13: Only include non-archived records
                var eqlCommand = @"SELECT id, workflow_id, current_step_id, source_entity, source_record_id, 
                                          status, requested_by, requested_on, completed_on, is_archived 
                                   FROM approval_request 
                                   WHERE (status = @statusApproved OR status = @statusRejected OR status = @statusCancelled)
                                   AND requested_on < @cutoffDate
                                   AND (is_archived = @notArchived OR is_archived = NULL)
                                   ORDER BY requested_on ASC
                                   PAGE 1 PAGESIZE @batchSize";

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("statusApproved", STATUS_APPROVED),
                    new EqlParameter("statusRejected", STATUS_REJECTED),
                    new EqlParameter("statusCancelled", STATUS_CANCELLED),
                    new EqlParameter("cutoffDate", cutoffDate),
                    new EqlParameter("notArchived", false),
                    new EqlParameter("batchSize", BATCH_SIZE)
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
                // Log error querying records for archival
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
        /// Per AC13: Archives a single approval request by setting is_archived flag to true.
        /// This implements the soft delete pattern for compliance while excluding from active queries.
        /// </summary>
        /// <param name="request">The approval request record to archive.</param>
        /// <param name="recMan">The RecordManager instance for database operations.</param>
        private void ArchiveRequest(EntityRecord request, RecordManager recMan)
        {
            var requestId = (Guid)request["id"];
            var archivedOn = DateTime.UtcNow;

            // Per AC13: Set is_archived flag to true (soft delete pattern)
            var patchRecord = new EntityRecord();
            patchRecord["id"] = requestId;
            patchRecord["is_archived"] = true;
            patchRecord["archived_on"] = archivedOn;

            // Update the approval request record
            var updateResult = recMan.UpdateRecord(APPROVAL_REQUEST_ENTITY, patchRecord);

            if (!updateResult.Success)
            {
                throw new Exception($"Failed to archive approval request {requestId}: {updateResult.Message}");
            }
        }

        /// <summary>
        /// Logs an error that occurred while archiving an individual approval request.
        /// </summary>
        /// <param name="request">The approval request that caused the error.</param>
        /// <param name="ex">The exception that was thrown.</param>
        private void LogError(EntityRecord request, Exception ex)
        {
            try
            {
                var requestId = request["id"] != null ? request["id"].ToString() : "unknown";
                new Log().Create(LogType.Error, "CleanupExpiredApprovalsJob", 
                    $"Error archiving approval request {requestId}", ex);
            }
            catch
            {
                // Suppress any errors during logging to prevent cascading failures
            }
        }

        /// <summary>
        /// Per AC14: Logs cleanup statistics for operational monitoring.
        /// </summary>
        /// <param name="archivedCount">The number of successfully archived requests.</param>
        /// <param name="errorCount">The number of requests that failed to archive.</param>
        private void LogSummary(int archivedCount, int errorCount)
        {
            try
            {
                var logType = errorCount > 0 ? LogType.Error : LogType.Info;
                var message = $"Archival job completed: {archivedCount} records archived, {errorCount} errors encountered";
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
