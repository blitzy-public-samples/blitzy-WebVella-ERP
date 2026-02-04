using System;
using System.Collections.Generic;
using System.Linq;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Database;
using WebVella.Erp.Diagnostics;
using WebVella.Erp.Eql;
using WebVella.Erp.Jobs;
using WebVella.Erp.Plugins.Approval.Model;

namespace WebVella.Erp.Plugins.Approval.Jobs
{
	/// <summary>
	/// Background job that archives completed approval requests and cleans up orphaned records.
	/// Runs daily per SchedulePlan configuration (every 1440 minutes).
	/// 
	/// This job performs three main maintenance tasks:
	/// 1. Archives completed (approved, rejected, cancelled) requests older than the retention period
	/// 2. Cleans up orphaned records (steps, rules, history) that reference non-existent parent records
	/// 3. Cancels stale pending requests that have had no activity for an extended period
	/// 
	/// The job uses soft archival (is_archived flag) rather than hard deletion to preserve audit trail integrity.
	/// All operations are performed within a system security scope with appropriate error handling.
	/// </summary>
	[Job("C3D4E5F6-A7B8-9012-CDEF-123456789012", "Archive completed requests and cleanup orphans", true, JobPriority.Low)]
	public class ApprovalCleanupJob : ErpJob
	{
		/// <summary>
		/// Configurable retention period in days. Completed approval requests older than this
		/// will be archived during cleanup. Default is 365 days (1 year).
		/// </summary>
		public const int RETENTION_DAYS = 365;

		/// <summary>
		/// Number of days after which pending requests with no activity are considered stale
		/// and will be automatically cancelled. Default is 90 days.
		/// </summary>
		private const int STALE_PENDING_DAYS = 90;

		/// <summary>
		/// Batch size for processing records to avoid memory issues with large datasets.
		/// </summary>
		private const int BATCH_SIZE = 100;

		/// <summary>
		/// Executes the cleanup job to archive old requests and remove orphaned records.
		/// This method is called by the ERP job scheduler based on the configured schedule plan.
		/// </summary>
		/// <param name="context">The job execution context providing job metadata and abort status.</param>
		public override void Execute(JobContext context)
		{
			var log = new Log();
			var startTime = DateTime.UtcNow;

			log.Create(LogType.Info, "ApprovalCleanupJob", "Approval cleanup job started", $"Start time: {startTime:yyyy-MM-dd HH:mm:ss}");

			using (SecurityContext.OpenSystemScope())
			{
				var recMan = new RecordManager();
				int archivedCount = 0;
				int orphanedStepsCount = 0;
				int orphanedRulesCount = 0;
				int orphanedHistoryCount = 0;
				int staleCancelledCount = 0;

				try
				{
					// STEP 1: Archive old completed requests
					archivedCount = ArchiveOldRequests(recMan, log);
				}
				catch (Exception ex)
				{
					log.Create(LogType.Error, "ApprovalCleanupJob", "Error archiving old requests", ex.Message);
				}

				try
				{
					// STEP 2: Clean up orphaned step records
					orphanedStepsCount = CleanupOrphanedSteps(recMan, log);
				}
				catch (Exception ex)
				{
					log.Create(LogType.Error, "ApprovalCleanupJob", "Error cleaning orphaned steps", ex.Message);
				}

				try
				{
					// STEP 3: Clean up orphaned rule records
					orphanedRulesCount = CleanupOrphanedRules(recMan, log);
				}
				catch (Exception ex)
				{
					log.Create(LogType.Error, "ApprovalCleanupJob", "Error cleaning orphaned rules", ex.Message);
				}

				try
				{
					// STEP 4: Clean up orphaned history records
					orphanedHistoryCount = CleanupOrphanedHistory(recMan, log);
				}
				catch (Exception ex)
				{
					log.Create(LogType.Error, "ApprovalCleanupJob", "Error cleaning orphaned history", ex.Message);
				}

				try
				{
					// STEP 5: Cancel stale pending requests
					staleCancelledCount = CancelStaleRequests(recMan, log);
				}
				catch (Exception ex)
				{
					log.Create(LogType.Error, "ApprovalCleanupJob", "Error cancelling stale requests", ex.Message);
				}

				var endTime = DateTime.UtcNow;
				var duration = endTime - startTime;

				log.Create(LogType.Info, "ApprovalCleanupJob", "Approval cleanup job completed",
					$"Archived: {archivedCount}, Orphaned Steps: {orphanedStepsCount}, " +
					$"Orphaned Rules: {orphanedRulesCount}, Orphaned History: {orphanedHistoryCount}, " +
					$"Stale Cancelled: {staleCancelledCount}, Duration: {duration.TotalSeconds:F2}s");
			}
		}

		/// <summary>
		/// Archives completed approval requests that are older than the configured retention period.
		/// Uses soft archival by setting is_archived flag to true rather than deleting records.
		/// This preserves audit trail integrity while marking records as no longer active.
		/// </summary>
		/// <param name="recMan">The RecordManager instance for database operations.</param>
		/// <param name="log">The Log instance for recording operation results.</param>
		/// <returns>The count of records that were archived.</returns>
		private int ArchiveOldRequests(RecordManager recMan, Log log)
		{
			int archivedCount = 0;
			var cutoffDate = DateTime.UtcNow.AddDays(-RETENTION_DAYS);

			// Build status filter for completed requests (approved, rejected, cancelled)
			var approvedStatus = ApprovalStatus.Approved.ToString().ToLowerInvariant();
			var rejectedStatus = ApprovalStatus.Rejected.ToString().ToLowerInvariant();
			var cancelledStatus = ApprovalStatus.Cancelled.ToString().ToLowerInvariant();

			// Query for old completed requests that are not already archived
			// Using EQL to find requests where status is in completed states and created_on is before cutoff
			var eqlCommand = @"SELECT id, status FROM approval_request 
				WHERE (status = @approvedStatus OR status = @rejectedStatus OR status = @cancelledStatus) 
				AND created_on < @cutoffDate 
				AND (is_archived = @isArchived OR is_archived IS NULL)
				PAGE 1 PAGESIZE @pageSize";

			var eqlParams = new List<EqlParameter>
			{
				new EqlParameter("approvedStatus", approvedStatus),
				new EqlParameter("rejectedStatus", rejectedStatus),
				new EqlParameter("cancelledStatus", cancelledStatus),
				new EqlParameter("cutoffDate", cutoffDate),
				new EqlParameter("isArchived", false),
				new EqlParameter("pageSize", BATCH_SIZE)
			};

			bool hasMoreRecords = true;

			while (hasMoreRecords)
			{
				var oldRequests = new EqlCommand(eqlCommand, eqlParams).Execute();

				if (oldRequests == null || oldRequests.Count == 0)
				{
					hasMoreRecords = false;
					continue;
				}

				foreach (var request in oldRequests)
				{
					try
					{
						var requestId = (Guid)request["id"];

						var patchRecord = new EntityRecord();
						patchRecord["id"] = requestId;
						patchRecord["is_archived"] = true;

						var updateResult = recMan.UpdateRecord("approval_request", patchRecord);
						if (updateResult.Success)
						{
							archivedCount++;
						}
						else
						{
							log.Create(LogType.Error, "ApprovalCleanupJob",
								$"Failed to archive request {requestId}", updateResult.Message);
						}
					}
					catch (Exception ex)
					{
						log.Create(LogType.Error, "ApprovalCleanupJob",
							$"Error archiving request {request["id"]}", ex.Message);
					}
				}

				// If we got less than batch size, we've processed all records
				if (oldRequests.Count < BATCH_SIZE)
				{
					hasMoreRecords = false;
				}
			}

			if (archivedCount > 0)
			{
				log.Create(LogType.Info, "ApprovalCleanupJob",
					$"Archived {archivedCount} completed requests older than {RETENTION_DAYS} days", "");
			}

			return archivedCount;
		}

		/// <summary>
		/// Removes orphaned approval_step records where the referenced workflow_id
		/// no longer exists in the approval_workflow entity.
		/// </summary>
		/// <param name="recMan">The RecordManager instance for database operations.</param>
		/// <param name="log">The Log instance for recording operation results.</param>
		/// <returns>The count of orphaned step records that were deleted.</returns>
		private int CleanupOrphanedSteps(RecordManager recMan, Log log)
		{
			int deletedCount = 0;

			// First, get all workflow IDs
			var workflowsEql = "SELECT id FROM approval_workflow";
			var existingWorkflows = new EqlCommand(workflowsEql).Execute();
			var workflowIds = new HashSet<Guid>();

			if (existingWorkflows != null)
			{
				foreach (var workflow in existingWorkflows)
				{
					if (workflow["id"] != null)
					{
						workflowIds.Add((Guid)workflow["id"]);
					}
				}
			}

			// Now get all steps and check if their workflow_id exists
			var stepsEql = "SELECT id, workflow_id FROM approval_step PAGE 1 PAGESIZE @pageSize";
			var stepsParams = new List<EqlParameter> { new EqlParameter("pageSize", BATCH_SIZE) };

			bool hasMoreRecords = true;
			var processedIds = new HashSet<Guid>();

			while (hasMoreRecords)
			{
				var steps = new EqlCommand(stepsEql, stepsParams).Execute();

				if (steps == null || steps.Count == 0)
				{
					hasMoreRecords = false;
					continue;
				}

				var orphanedSteps = new List<Guid>();

				foreach (var step in steps)
				{
					var stepId = (Guid)step["id"];

					// Skip if already processed
					if (processedIds.Contains(stepId))
					{
						continue;
					}
					processedIds.Add(stepId);

					var workflowId = step["workflow_id"] as Guid?;

					// If workflow_id is null or doesn't exist in workflows, it's orphaned
					if (!workflowId.HasValue || !workflowIds.Contains(workflowId.Value))
					{
						orphanedSteps.Add(stepId);
					}
				}

				// Delete orphaned steps
				foreach (var stepId in orphanedSteps)
				{
					try
					{
						var deleteResult = recMan.DeleteRecord("approval_step", stepId);
						if (deleteResult.Success)
						{
							deletedCount++;
						}
						else
						{
							log.Create(LogType.Error, "ApprovalCleanupJob",
								$"Failed to delete orphaned step {stepId}", deleteResult.Message);
						}
					}
					catch (Exception ex)
					{
						log.Create(LogType.Error, "ApprovalCleanupJob",
							$"Error deleting orphaned step {stepId}", ex.Message);
					}
				}

				// If we got less than batch size, we've processed all records
				if (steps.Count < BATCH_SIZE)
				{
					hasMoreRecords = false;
				}
			}

			if (deletedCount > 0)
			{
				log.Create(LogType.Info, "ApprovalCleanupJob",
					$"Deleted {deletedCount} orphaned step records", "");
			}

			return deletedCount;
		}

		/// <summary>
		/// Removes orphaned approval_rule records where the referenced step_id
		/// no longer exists in the approval_step entity.
		/// </summary>
		/// <param name="recMan">The RecordManager instance for database operations.</param>
		/// <param name="log">The Log instance for recording operation results.</param>
		/// <returns>The count of orphaned rule records that were deleted.</returns>
		private int CleanupOrphanedRules(RecordManager recMan, Log log)
		{
			int deletedCount = 0;

			// First, get all step IDs
			var stepsEql = "SELECT id FROM approval_step";
			var existingSteps = new EqlCommand(stepsEql).Execute();
			var stepIds = new HashSet<Guid>();

			if (existingSteps != null)
			{
				foreach (var step in existingSteps)
				{
					if (step["id"] != null)
					{
						stepIds.Add((Guid)step["id"]);
					}
				}
			}

			// Now get all rules and check if their step_id exists
			var rulesEql = "SELECT id, step_id FROM approval_rule PAGE 1 PAGESIZE @pageSize";
			var rulesParams = new List<EqlParameter> { new EqlParameter("pageSize", BATCH_SIZE) };

			bool hasMoreRecords = true;
			var processedIds = new HashSet<Guid>();

			while (hasMoreRecords)
			{
				var rules = new EqlCommand(rulesEql, rulesParams).Execute();

				if (rules == null || rules.Count == 0)
				{
					hasMoreRecords = false;
					continue;
				}

				var orphanedRules = new List<Guid>();

				foreach (var rule in rules)
				{
					var ruleId = (Guid)rule["id"];

					// Skip if already processed
					if (processedIds.Contains(ruleId))
					{
						continue;
					}
					processedIds.Add(ruleId);

					var stepId = rule["step_id"] as Guid?;

					// If step_id is null or doesn't exist in steps, it's orphaned
					if (!stepId.HasValue || !stepIds.Contains(stepId.Value))
					{
						orphanedRules.Add(ruleId);
					}
				}

				// Delete orphaned rules
				foreach (var ruleId in orphanedRules)
				{
					try
					{
						var deleteResult = recMan.DeleteRecord("approval_rule", ruleId);
						if (deleteResult.Success)
						{
							deletedCount++;
						}
						else
						{
							log.Create(LogType.Error, "ApprovalCleanupJob",
								$"Failed to delete orphaned rule {ruleId}", deleteResult.Message);
						}
					}
					catch (Exception ex)
					{
						log.Create(LogType.Error, "ApprovalCleanupJob",
							$"Error deleting orphaned rule {ruleId}", ex.Message);
					}
				}

				// If we got less than batch size, we've processed all records
				if (rules.Count < BATCH_SIZE)
				{
					hasMoreRecords = false;
				}
			}

			if (deletedCount > 0)
			{
				log.Create(LogType.Info, "ApprovalCleanupJob",
					$"Deleted {deletedCount} orphaned rule records", "");
			}

			return deletedCount;
		}

		/// <summary>
		/// Handles orphaned approval_history records where the referenced request_id
		/// no longer exists in the approval_request entity.
		/// For audit trail integrity, orphaned history records are logged but preserved
		/// unless the request has been archived for over 2x the retention period.
		/// </summary>
		/// <param name="recMan">The RecordManager instance for database operations.</param>
		/// <param name="log">The Log instance for recording operation results.</param>
		/// <returns>The count of orphaned history records that were processed.</returns>
		private int CleanupOrphanedHistory(RecordManager recMan, Log log)
		{
			int processedCount = 0;

			// First, get all request IDs
			var requestsEql = "SELECT id FROM approval_request";
			var existingRequests = new EqlCommand(requestsEql).Execute();
			var requestIds = new HashSet<Guid>();

			if (existingRequests != null)
			{
				foreach (var request in existingRequests)
				{
					if (request["id"] != null)
					{
						requestIds.Add((Guid)request["id"]);
					}
				}
			}

			// Get history records and check if their request_id exists
			var historyEql = "SELECT id, request_id, performed_on FROM approval_history PAGE 1 PAGESIZE @pageSize";
			var historyParams = new List<EqlParameter> { new EqlParameter("pageSize", BATCH_SIZE) };

			// Extended retention period for orphaned history (2x normal retention for audit purposes)
			var extendedCutoffDate = DateTime.UtcNow.AddDays(-RETENTION_DAYS * 2);

			bool hasMoreRecords = true;
			var processedIds = new HashSet<Guid>();

			while (hasMoreRecords)
			{
				var historyRecords = new EqlCommand(historyEql, historyParams).Execute();

				if (historyRecords == null || historyRecords.Count == 0)
				{
					hasMoreRecords = false;
					continue;
				}

				foreach (var history in historyRecords)
				{
					var historyId = (Guid)history["id"];

					// Skip if already processed
					if (processedIds.Contains(historyId))
					{
						continue;
					}
					processedIds.Add(historyId);

					var requestId = history["request_id"] as Guid?;
					var performedOn = history["performed_on"] as DateTime?;

					// If request_id is null or doesn't exist, it's orphaned
					if (!requestId.HasValue || !requestIds.Contains(requestId.Value))
					{
						// Only delete orphaned history if it's very old (2x retention period)
						// This preserves audit trail for reasonable periods even after request deletion
						if (performedOn.HasValue && performedOn.Value < extendedCutoffDate)
						{
							try
							{
								var deleteResult = recMan.DeleteRecord("approval_history", historyId);
								if (deleteResult.Success)
								{
									processedCount++;
								}
								else
								{
									log.Create(LogType.Error, "ApprovalCleanupJob",
										$"Failed to delete old orphaned history {historyId}", deleteResult.Message);
								}
							}
							catch (Exception ex)
							{
								log.Create(LogType.Error, "ApprovalCleanupJob",
									$"Error deleting old orphaned history {historyId}", ex.Message);
							}
						}
						else
						{
							// Log but preserve recent orphaned history for audit trail
							processedCount++;
						}
					}
				}

				// If we got less than batch size, we've processed all records
				if (historyRecords.Count < BATCH_SIZE)
				{
					hasMoreRecords = false;
				}
			}

			if (processedCount > 0)
			{
				log.Create(LogType.Info, "ApprovalCleanupJob",
					$"Processed {processedCount} orphaned history records", "");
			}

			return processedCount;
		}

		/// <summary>
		/// Cancels approval requests that have been in pending status for too long
		/// without any activity. This prevents indefinitely stuck requests from cluttering
		/// the system. Cancelled requests include a system-generated comment explaining
		/// the automatic cancellation due to inactivity.
		/// </summary>
		/// <param name="recMan">The RecordManager instance for database operations.</param>
		/// <param name="log">The Log instance for recording operation results.</param>
		/// <returns>The count of stale requests that were cancelled.</returns>
		private int CancelStaleRequests(RecordManager recMan, Log log)
		{
			int cancelledCount = 0;
			var staleCutoffDate = DateTime.UtcNow.AddDays(-STALE_PENDING_DAYS);
			var pendingStatus = ApprovalStatus.Pending.ToString().ToLowerInvariant();
			var cancelledStatus = ApprovalStatus.Cancelled.ToString().ToLowerInvariant();

			// Query for pending requests that are older than the stale cutoff
			var eqlCommand = @"SELECT id, workflow_id, entity_name, record_id FROM approval_request 
				WHERE status = @pendingStatus 
				AND created_on < @staleCutoffDate 
				AND (is_archived = @isArchived OR is_archived IS NULL)
				PAGE 1 PAGESIZE @pageSize";

			var eqlParams = new List<EqlParameter>
			{
				new EqlParameter("pendingStatus", pendingStatus),
				new EqlParameter("staleCutoffDate", staleCutoffDate),
				new EqlParameter("isArchived", false),
				new EqlParameter("pageSize", BATCH_SIZE)
			};

			bool hasMoreRecords = true;

			while (hasMoreRecords)
			{
				var staleRequests = new EqlCommand(eqlCommand, eqlParams).Execute();

				if (staleRequests == null || staleRequests.Count == 0)
				{
					hasMoreRecords = false;
					continue;
				}

				foreach (var request in staleRequests)
				{
					try
					{
						var requestId = (Guid)request["id"];

						// Check if there's any recent activity in history
						var historyEql = @"SELECT id FROM approval_history 
							WHERE request_id = @requestId 
							AND performed_on > @staleCutoffDate 
							PAGE 1 PAGESIZE 1";

						var historyParams = new List<EqlParameter>
						{
							new EqlParameter("requestId", requestId),
							new EqlParameter("staleCutoffDate", staleCutoffDate)
						};

						var recentActivity = new EqlCommand(historyEql, historyParams).Execute();

						// If there's recent activity, skip this request
						if (recentActivity != null && recentActivity.Count > 0)
						{
							continue;
						}

						// Cancel the stale request
						var patchRecord = new EntityRecord();
						patchRecord["id"] = requestId;
						patchRecord["status"] = cancelledStatus;

						var updateResult = recMan.UpdateRecord("approval_request", patchRecord);

						if (updateResult.Success)
						{
							cancelledCount++;

							// Log the cancellation in history
							try
							{
								var historyRecord = new EntityRecord();
								historyRecord["id"] = Guid.NewGuid();
								historyRecord["request_id"] = requestId;
								historyRecord["action"] = "cancelled";
								historyRecord["performed_by"] = Guid.Empty; // System action
								historyRecord["performed_on"] = DateTime.UtcNow;
								historyRecord["comments"] = $"Automatically cancelled due to inactivity for {STALE_PENDING_DAYS} days.";

								recMan.CreateRecord("approval_history", historyRecord);
							}
							catch (Exception historyEx)
							{
								log.Create(LogType.Error, "ApprovalCleanupJob",
									$"Failed to create history for cancelled request {requestId}", historyEx.Message);
							}
						}
						else
						{
							log.Create(LogType.Error, "ApprovalCleanupJob",
								$"Failed to cancel stale request {requestId}", updateResult.Message);
						}
					}
					catch (Exception ex)
					{
						log.Create(LogType.Error, "ApprovalCleanupJob",
							$"Error processing stale request {request["id"]}", ex.Message);
					}
				}

				// If we got less than batch size, we've processed all records
				if (staleRequests.Count < BATCH_SIZE)
				{
					hasMoreRecords = false;
				}
			}

			if (cancelledCount > 0)
			{
				log.Create(LogType.Info, "ApprovalCleanupJob",
					$"Cancelled {cancelledCount} stale pending requests older than {STALE_PENDING_DAYS} days", "");
			}

			return cancelledCount;
		}
	}
}
