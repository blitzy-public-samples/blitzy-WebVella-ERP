using System;
using System.Collections.Generic;
using System.Linq;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Eql;
using WebVella.Erp.Exceptions;
using WebVella.Erp.Plugins.Approval.Model;

namespace WebVella.Erp.Plugins.Approval.Services
{
    /// <summary>
    /// Service class for managing the approval audit trail by providing immutable logging of all approval actions.
    /// Creates approval_history records with server-generated timestamps for regulatory compliance (SOX, GDPR)
    /// and internal governance requirements.
    /// </summary>
    /// <remarks>
    /// This service follows the immutability principle - once an approval history record is created,
    /// it cannot be modified or deleted. This ensures a complete, tamper-proof audit trail of all
    /// approval workflow activities.
    /// 
    /// Supported action types include:
    /// - submitted: Initial request submission
    /// - approved: Request approved at a step
    /// - rejected: Request rejected
    /// - escalated: Request escalated to higher authority
    /// - delegated: Approval delegated to another user
    /// - recalled: Request recalled by submitter
    /// - commented: Comment added without status change
    /// 
    /// All timestamps are server-generated using DateTime.UtcNow to ensure consistency
    /// and prevent client-side timestamp manipulation.
    /// </remarks>
    public class ApprovalHistoryService : BaseService
    {
        /// <summary>
        /// Entity name constant for approval history records.
        /// </summary>
        private const string ApprovalHistoryEntityName = "approval_history";

        /// <summary>
        /// Logs an approval action to the audit trail. Creates an immutable approval_history record
        /// with a server-generated timestamp.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request.</param>
        /// <param name="actionType">The type of action performed (submitted, approved, rejected, escalated, delegated, recalled, commented).</param>
        /// <param name="performedBy">The unique identifier of the user who performed the action.</param>
        /// <param name="comments">Optional comments or notes about the action.</param>
        /// <param name="previousStatus">The status of the request before this action.</param>
        /// <param name="newStatus">The status of the request after this action.</param>
        /// <returns>The created EntityRecord containing the audit trail entry.</returns>
        /// <exception cref="ValidationException">Thrown when the record creation fails.</exception>
        /// <exception cref="ArgumentException">Thrown when requestId or performedBy is empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when actionType is null or empty.</exception>
        public EntityRecord LogApprovalAction(Guid requestId, string actionType, Guid performedBy, 
            string comments, string previousStatus, string newStatus)
        {
            // Validate input parameters
            if (requestId == Guid.Empty)
            {
                throw new ArgumentException("Request ID cannot be empty.", nameof(requestId));
            }

            if (performedBy == Guid.Empty)
            {
                throw new ArgumentException("Performed by user ID cannot be empty.", nameof(performedBy));
            }

            if (string.IsNullOrWhiteSpace(actionType))
            {
                throw new ArgumentNullException(nameof(actionType), "Action type cannot be null or empty.");
            }

            try
            {
                // Create the immutable history record
                var record = new EntityRecord();
                record["id"] = Guid.NewGuid();
                record["request_id"] = requestId;
                record["action_type"] = actionType.ToLowerInvariant();
                record["performed_by"] = performedBy;
                record["performed_on"] = DateTime.UtcNow; // Server-generated timestamp for compliance
                record["comments"] = comments ?? string.Empty;
                record["previous_status"] = previousStatus ?? string.Empty;
                record["new_status"] = newStatus ?? string.Empty;

                // Persist the record using RecordManager
                var response = RecMan.CreateRecord(ApprovalHistoryEntityName, record);
                if (!response.Success)
                {
                    throw new ValidationException(response.Message);
                }

                return record;
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ValidationException($"Failed to log approval action: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves the complete history of actions for a specific approval request.
        /// Returns all audit trail entries ordered by performed date descending (most recent first).
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request.</param>
        /// <returns>An EntityRecordList containing all history records for the request, ordered by performed_on descending.</returns>
        /// <exception cref="ArgumentException">Thrown when requestId is empty.</exception>
        public EntityRecordList GetRequestHistory(Guid requestId)
        {
            if (requestId == Guid.Empty)
            {
                throw new ArgumentException("Request ID cannot be empty.", nameof(requestId));
            }

            try
            {
                // Query approval_history with user details via relation expansion
                // The $user_approval_history relation provides user information for performed_by
                var eqlCommand = @"SELECT *, $user_approval_history.username, $user_approval_history.email 
                                   FROM approval_history 
                                   WHERE request_id = @requestId 
                                   ORDER BY performed_on DESC";

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("requestId", requestId)
                };

                var result = new EqlCommand(eqlCommand, eqlParams).Execute();
                return result;
            }
            catch (Exception ex)
            {
                throw new ValidationException($"Failed to retrieve request history: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves the approval action history for a specific user.
        /// Returns all actions performed by the user ordered by performed date descending.
        /// </summary>
        /// <param name="userId">The unique identifier of the user whose history to retrieve.</param>
        /// <param name="limit">The maximum number of records to return. Defaults to 100.</param>
        /// <returns>An EntityRecordList containing the user's approval actions, limited to the specified count.</returns>
        /// <exception cref="ArgumentException">Thrown when userId is empty or limit is less than 1.</exception>
        public EntityRecordList GetUserApprovalHistory(Guid userId, int limit = 100)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            }

            if (limit < 1)
            {
                throw new ArgumentException("Limit must be at least 1.", nameof(limit));
            }

            try
            {
                // Query approval_history with request details via relation expansion
                // The relation provides request information for context
                var eqlCommand = @"SELECT *, $approval_request_history.id, $approval_request_history.status 
                                   FROM approval_history 
                                   WHERE performed_by = @userId 
                                   ORDER BY performed_on DESC 
                                   PAGE 1 PAGESIZE @pageSize";

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("userId", userId),
                    new EqlParameter("pageSize", limit)
                };

                var result = new EqlCommand(eqlCommand, eqlParams).Execute();
                return result;
            }
            catch (Exception ex)
            {
                throw new ValidationException($"Failed to retrieve user approval history: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves approval history records filtered by action type and date range.
        /// Useful for generating audit reports and compliance documentation.
        /// </summary>
        /// <param name="actionType">The type of action to filter by (e.g., "approved", "rejected").</param>
        /// <param name="fromDate">The start date of the date range (inclusive).</param>
        /// <param name="toDate">The end date of the date range (inclusive).</param>
        /// <returns>An EntityRecordList containing the filtered history records, ordered by performed_on descending.</returns>
        /// <exception cref="ArgumentNullException">Thrown when actionType is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown when fromDate is greater than toDate.</exception>
        public EntityRecordList GetHistoryByActionType(string actionType, DateTime fromDate, DateTime toDate)
        {
            if (string.IsNullOrWhiteSpace(actionType))
            {
                throw new ArgumentNullException(nameof(actionType), "Action type cannot be null or empty.");
            }

            if (fromDate > toDate)
            {
                throw new ArgumentException("From date cannot be greater than to date.", nameof(fromDate));
            }

            try
            {
                var eqlCommand = @"SELECT *, $user_approval_history.username, $user_approval_history.email 
                                   FROM approval_history 
                                   WHERE action_type = @actionType 
                                   AND performed_on >= @fromDate 
                                   AND performed_on <= @toDate 
                                   ORDER BY performed_on DESC";

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("actionType", actionType.ToLowerInvariant()),
                    new EqlParameter("fromDate", fromDate),
                    new EqlParameter("toDate", toDate)
                };

                var result = new EqlCommand(eqlCommand, eqlParams).Execute();
                return result;
            }
            catch (Exception ex)
            {
                throw new ValidationException($"Failed to retrieve history by action type: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns the count of approval history records matching the specified action type and date range.
        /// Used for dashboard metrics and reporting aggregations.
        /// </summary>
        /// <param name="actionType">The type of action to filter by (e.g., "approved", "rejected").</param>
        /// <param name="fromDate">The start date of the date range (inclusive).</param>
        /// <param name="toDate">The end date of the date range (inclusive).</param>
        /// <returns>The count of matching history records.</returns>
        /// <exception cref="ArgumentNullException">Thrown when actionType is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown when fromDate is greater than toDate.</exception>
        public int GetHistoryCountByActionType(string actionType, DateTime fromDate, DateTime toDate)
        {
            if (string.IsNullOrWhiteSpace(actionType))
            {
                throw new ArgumentNullException(nameof(actionType), "Action type cannot be null or empty.");
            }

            if (fromDate > toDate)
            {
                throw new ArgumentException("From date cannot be greater than to date.", nameof(fromDate));
            }

            try
            {
                // Use a simple query to get records and count them
                // Note: For large datasets, consider implementing a COUNT query at the database level
                var eqlCommand = @"SELECT id FROM approval_history 
                                   WHERE action_type = @actionType 
                                   AND performed_on >= @fromDate 
                                   AND performed_on <= @toDate";

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("actionType", actionType.ToLowerInvariant()),
                    new EqlParameter("fromDate", fromDate),
                    new EqlParameter("toDate", toDate)
                };

                var result = new EqlCommand(eqlCommand, eqlParams).Execute();
                return result.Count();
            }
            catch (Exception ex)
            {
                throw new ValidationException($"Failed to get history count by action type: {ex.Message}");
            }
        }

        /// <summary>
        /// Logs a submission action when a new approval request is created.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request.</param>
        /// <param name="submittedBy">The unique identifier of the user who submitted the request.</param>
        /// <param name="comments">Optional comments about the submission.</param>
        /// <returns>The created EntityRecord containing the audit trail entry.</returns>
        public EntityRecord LogSubmission(Guid requestId, Guid submittedBy, string comments = null)
        {
            return LogApprovalAction(requestId, "submitted", submittedBy, comments, null, "pending");
        }

        /// <summary>
        /// Logs an approval action when a request is approved at a step.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request.</param>
        /// <param name="approvedBy">The unique identifier of the user who approved the request.</param>
        /// <param name="comments">Optional comments about the approval.</param>
        /// <param name="previousStatus">The status before approval.</param>
        /// <param name="newStatus">The status after approval.</param>
        /// <returns>The created EntityRecord containing the audit trail entry.</returns>
        public EntityRecord LogApproval(Guid requestId, Guid approvedBy, string comments, 
            string previousStatus, string newStatus)
        {
            return LogApprovalAction(requestId, ApprovalActionType.Approve.ToString().ToLowerInvariant(), 
                approvedBy, comments, previousStatus, newStatus);
        }

        /// <summary>
        /// Logs a rejection action when a request is rejected.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request.</param>
        /// <param name="rejectedBy">The unique identifier of the user who rejected the request.</param>
        /// <param name="comments">Optional comments about the rejection.</param>
        /// <param name="previousStatus">The status before rejection.</param>
        /// <returns>The created EntityRecord containing the audit trail entry.</returns>
        public EntityRecord LogRejection(Guid requestId, Guid rejectedBy, string comments, string previousStatus)
        {
            return LogApprovalAction(requestId, ApprovalActionType.Reject.ToString().ToLowerInvariant(), 
                rejectedBy, comments, previousStatus, "rejected");
        }

        /// <summary>
        /// Logs an escalation action when a request is escalated to a higher authority.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request.</param>
        /// <param name="escalatedBy">The unique identifier of the user who escalated the request.</param>
        /// <param name="comments">Optional comments about the escalation.</param>
        /// <param name="previousStatus">The status before escalation.</param>
        /// <returns>The created EntityRecord containing the audit trail entry.</returns>
        public EntityRecord LogEscalation(Guid requestId, Guid escalatedBy, string comments, string previousStatus)
        {
            return LogApprovalAction(requestId, ApprovalActionType.Escalate.ToString().ToLowerInvariant(), 
                escalatedBy, comments, previousStatus, "escalated");
        }

        /// <summary>
        /// Logs a delegation action when a request is delegated to another user.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request.</param>
        /// <param name="delegatedBy">The unique identifier of the user who delegated the request.</param>
        /// <param name="comments">Optional comments about the delegation.</param>
        /// <param name="previousStatus">The status before delegation.</param>
        /// <returns>The created EntityRecord containing the audit trail entry.</returns>
        public EntityRecord LogDelegation(Guid requestId, Guid delegatedBy, string comments, string previousStatus)
        {
            return LogApprovalAction(requestId, ApprovalActionType.Delegate.ToString().ToLowerInvariant(), 
                delegatedBy, comments, previousStatus, "delegated");
        }

        /// <summary>
        /// Logs a comment action when a user adds a comment without changing the request status.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request.</param>
        /// <param name="commentedBy">The unique identifier of the user who added the comment.</param>
        /// <param name="comments">The comment text.</param>
        /// <param name="currentStatus">The current status of the request (remains unchanged).</param>
        /// <returns>The created EntityRecord containing the audit trail entry.</returns>
        public EntityRecord LogComment(Guid requestId, Guid commentedBy, string comments, string currentStatus)
        {
            return LogApprovalAction(requestId, ApprovalActionType.Comment.ToString().ToLowerInvariant(), 
                commentedBy, comments, currentStatus, currentStatus);
        }

        /// <summary>
        /// Logs a recall action when a submitter recalls their request.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request.</param>
        /// <param name="recalledBy">The unique identifier of the user who recalled the request.</param>
        /// <param name="comments">Optional comments about the recall.</param>
        /// <param name="previousStatus">The status before recall.</param>
        /// <returns>The created EntityRecord containing the audit trail entry.</returns>
        public EntityRecord LogRecall(Guid requestId, Guid recalledBy, string comments, string previousStatus)
        {
            return LogApprovalAction(requestId, "recalled", recalledBy, comments, previousStatus, "cancelled");
        }

        /// <summary>
        /// Gets the most recent history entry for a specific approval request.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request.</param>
        /// <returns>The most recent EntityRecord for the request, or null if no history exists.</returns>
        /// <exception cref="ArgumentException">Thrown when requestId is empty.</exception>
        public EntityRecord GetLatestHistoryEntry(Guid requestId)
        {
            if (requestId == Guid.Empty)
            {
                throw new ArgumentException("Request ID cannot be empty.", nameof(requestId));
            }

            try
            {
                var eqlCommand = @"SELECT *, $user_approval_history.username, $user_approval_history.email 
                                   FROM approval_history 
                                   WHERE request_id = @requestId 
                                   ORDER BY performed_on DESC 
                                   PAGE 1 PAGESIZE 1";

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("requestId", requestId)
                };

                var result = new EqlCommand(eqlCommand, eqlParams).Execute();
                return result.FirstOrDefault();
            }
            catch (Exception ex)
            {
                throw new ValidationException($"Failed to retrieve latest history entry: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the total count of history entries for a specific approval request.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request.</param>
        /// <returns>The count of history entries for the request.</returns>
        /// <exception cref="ArgumentException">Thrown when requestId is empty.</exception>
        public int GetRequestHistoryCount(Guid requestId)
        {
            if (requestId == Guid.Empty)
            {
                throw new ArgumentException("Request ID cannot be empty.", nameof(requestId));
            }

            try
            {
                var eqlCommand = @"SELECT id FROM approval_history WHERE request_id = @requestId";

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("requestId", requestId)
                };

                var result = new EqlCommand(eqlCommand, eqlParams).Execute();
                return result.Count();
            }
            catch (Exception ex)
            {
                throw new ValidationException($"Failed to get request history count: {ex.Message}");
            }
        }
    }
}
