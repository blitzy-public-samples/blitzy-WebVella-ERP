using System;
using System.Collections.Generic;
using System.Linq;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Eql;
using WebVella.Erp.Exceptions;
using WebVella.Erp.Plugins.Approval.Api;

namespace WebVella.Erp.Plugins.Approval.Services
{
    /// <summary>
    /// Service for managing approval audit trail via the approval_history entity.
    /// Provides methods to log approval events (submitted, approved, rejected, delegated, escalated)
    /// with user, timestamp, and comments. Supports querying history by request, user, or recent activity.
    /// Follows WebVella patterns using RecordManager for data persistence.
    /// </summary>
    public class ApprovalHistoryService
    {
        #region << Properties >>

        /// <summary>
        /// RecordManager instance for CRUD operations on approval_history entity.
        /// </summary>
        protected RecordManager RecMan { get; private set; } = new RecordManager();

        #endregion

        #region << Constants >>

        /// <summary>
        /// Entity name constant for the approval history table.
        /// </summary>
        private const string ENTITY_NAME = "approval_history";

        #endregion

        #region << Public Methods >>

        /// <summary>
        /// Logs an action in the approval history audit trail.
        /// Creates a new history record capturing who performed what action, when, and with optional comments.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request.</param>
        /// <param name="stepId">The unique identifier of the approval step at which the action was performed.</param>
        /// <param name="action">The type of action performed (submitted, approved, rejected, delegated, escalated).</param>
        /// <param name="performedBy">The unique identifier of the user who performed the action.</param>
        /// <param name="comments">Optional comments or notes associated with the action.</param>
        /// <returns>The created ApprovalHistoryModel representing the logged action.</returns>
        /// <exception cref="ValidationException">Thrown when the record creation fails.</exception>
        /// <exception cref="ArgumentException">Thrown when requestId, stepId, or performedBy is empty.</exception>
        public ApprovalHistoryModel LogAction(Guid requestId, Guid stepId, string action, Guid performedBy, string comments = null, string previousStatus = null, string newStatus = null)
        {
            // Validate required parameters
            if (requestId == Guid.Empty)
            {
                throw new ArgumentException("Request ID cannot be empty.", nameof(requestId));
            }

            if (stepId == Guid.Empty)
            {
                throw new ArgumentException("Step ID cannot be empty.", nameof(stepId));
            }

            if (performedBy == Guid.Empty)
            {
                throw new ArgumentException("Performed by user ID cannot be empty.", nameof(performedBy));
            }

            if (string.IsNullOrWhiteSpace(action))
            {
                throw new ArgumentException("Action cannot be null or empty.", nameof(action));
            }

            // Validate action value is one of the allowed values
            var validActions = new[] { "submitted", "approved", "rejected", "delegated", "escalated" };
            if (!validActions.Contains(action.ToLowerInvariant()))
            {
                throw new ArgumentException(
                    $"Invalid action value '{action}'. Must be one of: {string.Join(", ", validActions)}",
                    nameof(action));
            }

            try
            {
                // Create the history record
                var newId = Guid.NewGuid();
                var performedOn = DateTime.UtcNow;

                var record = new EntityRecord();
                record["id"] = newId;
                record["request_id"] = requestId;
                record["step_id"] = stepId;
                record["action"] = action.ToLowerInvariant();
                record["performed_by"] = performedBy;
                record["performed_on"] = performedOn;
                record["comments"] = comments;
                record["previous_status"] = previousStatus;
                record["new_status"] = newStatus;

                var response = RecMan.CreateRecord(ENTITY_NAME, record);

                if (!response.Success)
                {
                    throw new ValidationException(response.Message);
                }

                // Return the created model
                return new ApprovalHistoryModel
                {
                    Id = newId,
                    RequestId = requestId,
                    StepId = stepId,
                    Action = action.ToLowerInvariant(),
                    PerformedBy = performedBy,
                    PerformedOn = performedOn,
                    Comments = comments,
                    PreviousStatus = previousStatus,
                    NewStatus = newStatus
                };
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ValidationException($"Failed to log approval action: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves all history records for a specific approval request, ordered by performed_on ascending.
        /// This provides the complete audit trail for the request from submission to completion.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request.</param>
        /// <returns>A list of ApprovalHistoryModel records for the request, ordered chronologically.</returns>
        /// <exception cref="ArgumentException">Thrown when requestId is empty.</exception>
        public List<ApprovalHistoryModel> GetByRequestId(Guid requestId)
        {
            if (requestId == Guid.Empty)
            {
                throw new ArgumentException("Request ID cannot be empty.", nameof(requestId));
            }

            try
            {
                var eqlCommand = @"SELECT id, request_id, step_id, action, performed_by, performed_on, comments, previous_status, new_status 
                                   FROM approval_history 
                                   WHERE request_id = @requestId 
                                   ORDER BY performed_on ASC";

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("requestId", requestId)
                };

                var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (eqlResult == null || !eqlResult.Any())
                {
                    return new List<ApprovalHistoryModel>();
                }

                return eqlResult.Select(record => MapToModel(record)).ToList();
            }
            catch (Exception ex)
            {
                throw new ValidationException($"Failed to retrieve history for request {requestId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves history records for actions performed by a specific user, ordered by performed_on descending.
        /// Useful for displaying a user's recent approval activity or for audit purposes.
        /// </summary>
        /// <param name="userId">The unique identifier of the user who performed the actions.</param>
        /// <param name="limit">Optional maximum number of records to return. If null, returns all matching records.</param>
        /// <returns>A list of ApprovalHistoryModel records for the user, ordered by most recent first.</returns>
        /// <exception cref="ArgumentException">Thrown when userId is empty.</exception>
        public List<ApprovalHistoryModel> GetByUserId(Guid userId, int? limit = null)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            }

            try
            {
                var eqlCommand = @"SELECT id, request_id, step_id, action, performed_by, performed_on, comments, previous_status, new_status 
                                   FROM approval_history 
                                   WHERE performed_by = @userId 
                                   ORDER BY performed_on DESC";

                // Add PAGESIZE if limit is specified
                if (limit.HasValue && limit.Value > 0)
                {
                    eqlCommand += $" PAGESIZE {limit.Value}";
                }

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("userId", userId)
                };

                var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (eqlResult == null || !eqlResult.Any())
                {
                    return new List<ApprovalHistoryModel>();
                }

                return eqlResult.Select(record => MapToModel(record)).ToList();
            }
            catch (Exception ex)
            {
                throw new ValidationException($"Failed to retrieve history for user {userId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves recent approval activity within the specified number of hours.
        /// Used for dashboard displays and monitoring recent approval workflow activity.
        /// </summary>
        /// <param name="hours">The number of hours to look back for activity. Defaults to 24 hours.</param>
        /// <returns>A list of ApprovalHistoryModel records from the specified time period, ordered by most recent first.</returns>
        /// <exception cref="ArgumentException">Thrown when hours is less than or equal to zero.</exception>
        public List<ApprovalHistoryModel> GetRecentActivity(int hours = 24)
        {
            if (hours <= 0)
            {
                throw new ArgumentException("Hours must be a positive number.", nameof(hours));
            }

            try
            {
                // Calculate the cutoff timestamp
                var cutoff = DateTime.UtcNow.AddHours(-hours);

                var eqlCommand = @"SELECT id, request_id, step_id, action, performed_by, performed_on, comments, previous_status, new_status 
                                   FROM approval_history 
                                   WHERE performed_on >= @cutoff 
                                   ORDER BY performed_on DESC";

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("cutoff", cutoff)
                };

                var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (eqlResult == null || !eqlResult.Any())
                {
                    return new List<ApprovalHistoryModel>();
                }

                return eqlResult.Select(record => MapToModel(record)).ToList();
            }
            catch (Exception ex)
            {
                throw new ValidationException($"Failed to retrieve recent activity for the last {hours} hours: {ex.Message}");
            }
        }

        #endregion

        #region << Private Methods >>

        /// <summary>
        /// Maps an EntityRecord from the database to an ApprovalHistoryModel DTO.
        /// Handles type conversion and null checks for all fields.
        /// </summary>
        /// <param name="record">The EntityRecord retrieved from the database.</param>
        /// <returns>An ApprovalHistoryModel populated with values from the record.</returns>
        private ApprovalHistoryModel MapToModel(EntityRecord record)
        {
            if (record == null)
            {
                return null;
            }

            var model = new ApprovalHistoryModel();

            // Map Id
            if (record.Properties.ContainsKey("id") && record["id"] != null)
            {
                model.Id = (Guid)record["id"];
            }

            // Map RequestId
            if (record.Properties.ContainsKey("request_id") && record["request_id"] != null)
            {
                model.RequestId = (Guid)record["request_id"];
            }

            // Map StepId
            if (record.Properties.ContainsKey("step_id") && record["step_id"] != null)
            {
                model.StepId = (Guid)record["step_id"];
            }

            // Map Action
            if (record.Properties.ContainsKey("action") && record["action"] != null)
            {
                model.Action = (string)record["action"];
            }

            // Map PerformedBy
            if (record.Properties.ContainsKey("performed_by") && record["performed_by"] != null)
            {
                model.PerformedBy = (Guid)record["performed_by"];
            }

            // Map PerformedOn
            if (record.Properties.ContainsKey("performed_on") && record["performed_on"] != null)
            {
                model.PerformedOn = (DateTime)record["performed_on"];
            }

            // Map Comments (nullable)
            if (record.Properties.ContainsKey("comments") && record["comments"] != null)
            {
                model.Comments = (string)record["comments"];
            }

            // Map PreviousStatus (nullable)
            if (record.Properties.ContainsKey("previous_status") && record["previous_status"] != null)
            {
                model.PreviousStatus = (string)record["previous_status"];
            }

            // Map NewStatus (nullable)
            if (record.Properties.ContainsKey("new_status") && record["new_status"] != null)
            {
                model.NewStatus = (string)record["new_status"];
            }

            return model;
        }

        #endregion
    }
}
