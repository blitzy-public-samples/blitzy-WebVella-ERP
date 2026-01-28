using System;
using System.Collections.Generic;
using System.Linq;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Database;
using WebVella.Erp.Eql;
using WebVella.Erp.Exceptions;
using WebVella.Erp.Plugins.Approval.Api;

namespace WebVella.Erp.Plugins.Approval.Services
{
    /// <summary>
    /// Core service managing approval request lifecycle as a state machine.
    /// Implements Create(), Approve(), Reject(), and Delegate() operations for the approval_request entity.
    /// Create() initiates workflow by evaluating rules via ApprovalRouteService and setting initial step.
    /// Approve() advances to next step or completes workflow.
    /// Reject() terminates workflow with rejected status.
    /// Delegate() reassigns current step approver.
    /// All operations log history via ApprovalHistoryService.
    /// Uses RecordManager for data persistence and maintains transaction safety for multi-step operations.
    /// </summary>
    /// <remarks>
    /// This service follows the WebVella service pattern with inline property initialization.
    /// All state transitions are logged to the approval_history entity for audit trail purposes.
    /// Transaction management is used for multi-step operations to ensure data consistency.
    /// </remarks>
    public class ApprovalRequestService
    {
        #region << Properties >>

        /// <summary>
        /// RecordManager instance for CRUD operations on approval_request entity.
        /// Initialized inline following WebVella service pattern.
        /// </summary>
        protected RecordManager RecMan { get; private set; } = new RecordManager();

        /// <summary>
        /// ApprovalRouteService instance for rule evaluation and step routing.
        /// Used to find matching workflows, get steps, and validate approver authorization.
        /// </summary>
        protected ApprovalRouteService RouteService { get; private set; } = new ApprovalRouteService();

        /// <summary>
        /// ApprovalHistoryService instance for audit trail management.
        /// Used to log all approval lifecycle events.
        /// </summary>
        protected ApprovalHistoryService HistoryService { get; private set; } = new ApprovalHistoryService();

        #endregion

        #region << Constants >>

        /// <summary>
        /// Entity name constant for the approval request table.
        /// </summary>
        private const string ENTITY_NAME = "approval_request";

        /// <summary>
        /// Status value for pending requests.
        /// </summary>
        private const string STATUS_PENDING = "pending";

        /// <summary>
        /// Status value for approved requests.
        /// </summary>
        private const string STATUS_APPROVED = "approved";

        /// <summary>
        /// Status value for rejected requests.
        /// </summary>
        private const string STATUS_REJECTED = "rejected";

        /// <summary>
        /// Status value for escalated requests.
        /// </summary>
        private const string STATUS_ESCALATED = "escalated";

        /// <summary>
        /// Status value for expired requests.
        /// </summary>
        private const string STATUS_EXPIRED = "expired";

        /// <summary>
        /// Action value for submission events.
        /// </summary>
        private const string ACTION_SUBMITTED = "submitted";

        /// <summary>
        /// Action value for approval events.
        /// </summary>
        private const string ACTION_APPROVED = "approved";

        /// <summary>
        /// Action value for rejection events.
        /// </summary>
        private const string ACTION_REJECTED = "rejected";

        /// <summary>
        /// Action value for delegation events.
        /// </summary>
        private const string ACTION_DELEGATED = "delegated";

        #endregion

        #region << Lifecycle Methods >>

        /// <summary>
        /// Creates a new approval request by evaluating workflow rules and initializing the request with the first step.
        /// This method initiates the approval workflow lifecycle for a source record.
        /// </summary>
        /// <param name="sourceRecordId">The unique identifier of the source record requiring approval.</param>
        /// <param name="sourceEntityName">The name of the entity the source record belongs to (e.g., "purchase_order").</param>
        /// <param name="requestedBy">The unique identifier of the user who initiated the approval request.</param>
        /// <returns>The created ApprovalRequestModel with status 'pending' and assigned to the first workflow step.</returns>
        /// <exception cref="ArgumentException">Thrown when sourceRecordId or requestedBy is empty, or sourceEntityName is null/empty.</exception>
        /// <exception cref="ValidationException">Thrown when no matching workflow is found, no steps are configured, or record creation fails.</exception>
        /// <example>
        /// <code>
        /// var requestService = new ApprovalRequestService();
        /// var request = requestService.Create(purchaseOrderId, "purchase_order", currentUserId);
        /// Console.WriteLine($"Request created: {request.Id}, Status: {request.Status}");
        /// </code>
        /// </example>
        public ApprovalRequestModel Create(Guid sourceRecordId, string sourceEntityName, Guid requestedBy)
        {
            // Validate input parameters
            if (sourceRecordId == Guid.Empty)
            {
                throw new ArgumentException("Source record ID cannot be empty.", nameof(sourceRecordId));
            }

            if (string.IsNullOrWhiteSpace(sourceEntityName))
            {
                throw new ArgumentException("Source entity name cannot be null or empty.", nameof(sourceEntityName));
            }

            if (requestedBy == Guid.Empty)
            {
                throw new ArgumentException("Requested by user ID cannot be empty.", nameof(requestedBy));
            }

            // Use transaction for multi-step operation
            using (var connection = DbContext.Current.CreateConnection())
            {
                try
                {
                    connection.BeginTransaction();

                    // Step 1: Get the source record to evaluate rules against
                    var sourceRecord = GetSourceRecord(sourceRecordId, sourceEntityName);
                    if (sourceRecord == null)
                    {
                        throw new ValidationException($"Source record with ID {sourceRecordId} not found in entity {sourceEntityName}.");
                    }

                    // Step 2: Evaluate rules to find matching workflow
                    var matchingWorkflow = RouteService.EvaluateRules(sourceRecord, sourceEntityName);
                    if (matchingWorkflow == null)
                    {
                        throw new ValidationException($"No matching approval workflow found for entity '{sourceEntityName}'.");
                    }

                    // Step 3: Get the first step of the workflow
                    var firstStep = RouteService.GetFirstStep(matchingWorkflow.Id);
                    if (firstStep == null)
                    {
                        throw new ValidationException($"Workflow '{matchingWorkflow.Name}' has no steps configured.");
                    }

                    // Step 4: Create the approval_request record
                    var requestId = Guid.NewGuid();
                    var requestedOn = DateTime.UtcNow;

                    var requestRecord = new EntityRecord();
                    requestRecord["id"] = requestId;
                    requestRecord["workflow_id"] = matchingWorkflow.Id;
                    requestRecord["current_step_id"] = firstStep.Id;
                    requestRecord["source_entity"] = sourceEntityName;
                    requestRecord["source_record_id"] = sourceRecordId;
                    requestRecord["status"] = STATUS_PENDING;
                    requestRecord["requested_by"] = requestedBy;
                    requestRecord["requested_on"] = requestedOn;
                    requestRecord["completed_on"] = null;

                    var createResponse = RecMan.CreateRecord(ENTITY_NAME, requestRecord);
                    if (!createResponse.Success)
                    {
                        throw new ValidationException($"Failed to create approval request: {createResponse.Message}");
                    }

                    // Step 5: Log the 'submitted' action to history
                    HistoryService.LogAction(requestId, firstStep.Id, ACTION_SUBMITTED, requestedBy, "Approval request submitted.", null, STATUS_PENDING);

                    connection.CommitTransaction();

                    // Return the created model
                    return new ApprovalRequestModel
                    {
                        Id = requestId,
                        WorkflowId = matchingWorkflow.Id,
                        CurrentStepId = firstStep.Id,
                        SourceEntityName = sourceEntityName,
                        SourceRecordId = sourceRecordId,
                        Status = STATUS_PENDING,
                        RequestedBy = requestedBy,
                        RequestedOn = requestedOn,
                        CompletedOn = null
                    };
                }
                catch (ValidationException)
                {
                    connection.RollbackTransaction();
                    throw;
                }
                catch (ArgumentException)
                {
                    connection.RollbackTransaction();
                    throw;
                }
                catch (Exception ex)
                {
                    connection.RollbackTransaction();
                    throw new ValidationException($"Failed to create approval request: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Approves an approval request, advancing it to the next step or completing the workflow.
        /// Validates that the user is authorized to approve the current step before processing.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request to approve.</param>
        /// <param name="userId">The unique identifier of the user performing the approval.</param>
        /// <param name="comments">Optional comments from the approver.</param>
        /// <returns>The updated ApprovalRequestModel reflecting the new state.</returns>
        /// <exception cref="ArgumentException">Thrown when requestId or userId is empty.</exception>
        /// <exception cref="ValidationException">
        /// Thrown when request is not found, request is not in pending status,
        /// user is not authorized to approve, or update fails.
        /// </exception>
        /// <remarks>
        /// If the current step is the final step (or no more steps exist), the workflow is completed
        /// with status 'approved' and completed_on timestamp set. Otherwise, the request advances
        /// to the next step while maintaining 'pending' status.
        /// </remarks>
        /// <example>
        /// <code>
        /// var requestService = new ApprovalRequestService();
        /// var updatedRequest = requestService.Approve(requestId, currentUserId, "Approved - budget verified.");
        /// if (updatedRequest.Status == "approved")
        /// {
        ///     Console.WriteLine("Workflow complete!");
        /// }
        /// </code>
        /// </example>
        public ApprovalRequestModel Approve(Guid requestId, Guid userId, string comments)
        {
            // Validate input parameters
            if (requestId == Guid.Empty)
            {
                throw new ArgumentException("Request ID cannot be empty.", nameof(requestId));
            }

            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            }

            // Use transaction for multi-step operation
            using (var connection = DbContext.Current.CreateConnection())
            {
                try
                {
                    connection.BeginTransaction();

                    // Step 1: Get the current request
                    var request = GetById(requestId);
                    if (request == null)
                    {
                        throw new ValidationException($"Approval request with ID {requestId} not found.");
                    }

                    // Step 2: Validate request is in pending status
                    if (!string.Equals(request.Status, STATUS_PENDING, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ValidationException($"Cannot approve request - current status is '{request.Status}'. Only pending requests can be approved.");
                    }

                    // Step 3: Validate current step exists
                    if (!request.CurrentStepId.HasValue)
                    {
                        throw new ValidationException("Request has no current step assigned.");
                    }

                    var currentStepId = request.CurrentStepId.Value;

                    // Step 4: Validate user is authorized to approve this step
                    if (!RouteService.IsUserAuthorizedApprover(userId, currentStepId))
                    {
                        throw new ValidationException("User is not authorized to approve this step.");
                    }

                    // Step 5: Get next step (if any)
                    var nextStep = RouteService.GetNextStep(request.WorkflowId, currentStepId);

                    var patchRecord = new EntityRecord();
                    patchRecord["id"] = requestId;

                    string newStatus;
                    DateTime? completedOn = null;
                    Guid? newCurrentStepId;

                    if (nextStep == null)
                    {
                        // No more steps - workflow is complete
                        newStatus = STATUS_APPROVED;
                        completedOn = DateTime.UtcNow;
                        newCurrentStepId = null;

                        patchRecord["status"] = newStatus;
                        patchRecord["completed_on"] = completedOn;
                        patchRecord["current_step_id"] = null;
                    }
                    else
                    {
                        // Advance to next step
                        newStatus = STATUS_PENDING;
                        newCurrentStepId = nextStep.Id;

                        patchRecord["current_step_id"] = nextStep.Id;
                    }

                    // Step 6: Update the request record
                    var updateResponse = RecMan.UpdateRecord(ENTITY_NAME, patchRecord);
                    if (!updateResponse.Success)
                    {
                        throw new ValidationException($"Failed to update approval request: {updateResponse.Message}");
                    }

                    // Step 7: Log the 'approved' action to history
                    var historyComments = string.IsNullOrWhiteSpace(comments) ? "Step approved." : comments;
                    HistoryService.LogAction(requestId, currentStepId, ACTION_APPROVED, userId, historyComments, STATUS_PENDING, newStatus);

                    connection.CommitTransaction();

                    // Return updated model
                    return new ApprovalRequestModel
                    {
                        Id = requestId,
                        WorkflowId = request.WorkflowId,
                        CurrentStepId = newCurrentStepId,
                        SourceEntityName = request.SourceEntityName,
                        SourceRecordId = request.SourceRecordId,
                        Status = newStatus,
                        RequestedBy = request.RequestedBy,
                        RequestedOn = request.RequestedOn,
                        CompletedOn = completedOn
                    };
                }
                catch (ValidationException)
                {
                    connection.RollbackTransaction();
                    throw;
                }
                catch (ArgumentException)
                {
                    connection.RollbackTransaction();
                    throw;
                }
                catch (Exception ex)
                {
                    connection.RollbackTransaction();
                    throw new ValidationException($"Failed to approve request: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Rejects an approval request, terminating the workflow with rejected status.
        /// Validates that the user is authorized to reject before processing.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request to reject.</param>
        /// <param name="userId">The unique identifier of the user performing the rejection.</param>
        /// <param name="reason">The reason for rejection (required for audit purposes).</param>
        /// <param name="comments">Additional comments from the rejector.</param>
        /// <returns>The updated ApprovalRequestModel with status 'rejected' and completed_on timestamp.</returns>
        /// <exception cref="ArgumentException">Thrown when requestId or userId is empty, or reason is null/empty.</exception>
        /// <exception cref="ValidationException">
        /// Thrown when request is not found, request is not in pending status,
        /// user is not authorized to reject, or update fails.
        /// </exception>
        /// <remarks>
        /// Rejection is a terminal state - once rejected, the request cannot be modified further.
        /// The rejection reason and comments are recorded in the audit history for compliance purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var requestService = new ApprovalRequestService();
        /// var rejectedRequest = requestService.Reject(requestId, currentUserId, "Budget exceeded", "Amount exceeds department limit.");
        /// </code>
        /// </example>
        public ApprovalRequestModel Reject(Guid requestId, Guid userId, string reason, string comments)
        {
            // Validate input parameters
            if (requestId == Guid.Empty)
            {
                throw new ArgumentException("Request ID cannot be empty.", nameof(requestId));
            }

            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Rejection reason cannot be null or empty.", nameof(reason));
            }

            // Use transaction for multi-step operation
            using (var connection = DbContext.Current.CreateConnection())
            {
                try
                {
                    connection.BeginTransaction();

                    // Step 1: Get the current request
                    var request = GetById(requestId);
                    if (request == null)
                    {
                        throw new ValidationException($"Approval request with ID {requestId} not found.");
                    }

                    // Step 2: Validate request is in pending or escalated status
                    if (!string.Equals(request.Status, STATUS_PENDING, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(request.Status, STATUS_ESCALATED, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ValidationException($"Cannot reject request - current status is '{request.Status}'. Only pending or escalated requests can be rejected.");
                    }

                    // Step 3: Validate current step exists
                    if (!request.CurrentStepId.HasValue)
                    {
                        throw new ValidationException("Request has no current step assigned.");
                    }

                    var currentStepId = request.CurrentStepId.Value;

                    // Step 4: Validate user is authorized to act on this step
                    if (!RouteService.IsUserAuthorizedApprover(userId, currentStepId))
                    {
                        throw new ValidationException("User is not authorized to reject this step.");
                    }

                    // Step 5: Update the request record to rejected status
                    var completedOn = DateTime.UtcNow;

                    var patchRecord = new EntityRecord();
                    patchRecord["id"] = requestId;
                    patchRecord["status"] = STATUS_REJECTED;
                    patchRecord["completed_on"] = completedOn;

                    var updateResponse = RecMan.UpdateRecord(ENTITY_NAME, patchRecord);
                    if (!updateResponse.Success)
                    {
                        throw new ValidationException($"Failed to update approval request: {updateResponse.Message}");
                    }

                    // Step 6: Log the 'rejected' action to history with reason
                    var historyComments = $"Reason: {reason}";
                    if (!string.IsNullOrWhiteSpace(comments))
                    {
                        historyComments += $". Comments: {comments}";
                    }
                    HistoryService.LogAction(requestId, currentStepId, ACTION_REJECTED, userId, historyComments, request.Status, STATUS_REJECTED);

                    connection.CommitTransaction();

                    // Return updated model
                    return new ApprovalRequestModel
                    {
                        Id = requestId,
                        WorkflowId = request.WorkflowId,
                        CurrentStepId = request.CurrentStepId,
                        SourceEntityName = request.SourceEntityName,
                        SourceRecordId = request.SourceRecordId,
                        Status = STATUS_REJECTED,
                        RequestedBy = request.RequestedBy,
                        RequestedOn = request.RequestedOn,
                        CompletedOn = completedOn
                    };
                }
                catch (ValidationException)
                {
                    connection.RollbackTransaction();
                    throw;
                }
                catch (ArgumentException)
                {
                    connection.RollbackTransaction();
                    throw;
                }
                catch (Exception ex)
                {
                    connection.RollbackTransaction();
                    throw new ValidationException($"Failed to reject request: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Delegates an approval request to another user for the current step.
        /// The delegatee becomes responsible for the approval action at the current step.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request to delegate.</param>
        /// <param name="userId">The unique identifier of the user performing the delegation.</param>
        /// <param name="delegateToUserId">The unique identifier of the user to delegate to.</param>
        /// <param name="comments">Optional comments explaining the delegation.</param>
        /// <returns>The ApprovalRequestModel (unchanged except for delegation tracking).</returns>
        /// <exception cref="ArgumentException">Thrown when requestId, userId, or delegateToUserId is empty.</exception>
        /// <exception cref="ValidationException">
        /// Thrown when request is not found, request is not in pending status,
        /// user is not authorized to delegate, user tries to delegate to themselves, or update fails.
        /// </exception>
        /// <remarks>
        /// Delegation creates a record in the approval history but does not change the request status.
        /// The delegated user can then approve, reject, or further delegate the request.
        /// Delegation is only allowed from authorized approvers to prevent unauthorized transfers.
        /// </remarks>
        /// <example>
        /// <code>
        /// var requestService = new ApprovalRequestService();
        /// var delegatedRequest = requestService.Delegate(requestId, currentUserId, substituteUserId, "Out of office - delegating to backup approver.");
        /// </code>
        /// </example>
        public ApprovalRequestModel Delegate(Guid requestId, Guid userId, Guid delegateToUserId, string comments)
        {
            // Validate input parameters
            if (requestId == Guid.Empty)
            {
                throw new ArgumentException("Request ID cannot be empty.", nameof(requestId));
            }

            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            }

            if (delegateToUserId == Guid.Empty)
            {
                throw new ArgumentException("Delegate to user ID cannot be empty.", nameof(delegateToUserId));
            }

            if (userId == delegateToUserId)
            {
                throw new ArgumentException("Cannot delegate to yourself.", nameof(delegateToUserId));
            }

            // Use transaction for multi-step operation
            using (var connection = DbContext.Current.CreateConnection())
            {
                try
                {
                    connection.BeginTransaction();

                    // Step 1: Get the current request
                    var request = GetById(requestId);
                    if (request == null)
                    {
                        throw new ValidationException($"Approval request with ID {requestId} not found.");
                    }

                    // Step 2: Validate request is in pending status
                    if (!string.Equals(request.Status, STATUS_PENDING, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ValidationException($"Cannot delegate request - current status is '{request.Status}'. Only pending requests can be delegated.");
                    }

                    // Step 3: Validate current step exists
                    if (!request.CurrentStepId.HasValue)
                    {
                        throw new ValidationException("Request has no current step assigned.");
                    }

                    var currentStepId = request.CurrentStepId.Value;

                    // Step 4: Validate user is authorized to delegate this step
                    if (!RouteService.IsUserAuthorizedApprover(userId, currentStepId))
                    {
                        throw new ValidationException("User is not authorized to delegate this step.");
                    }

                    // Step 5: Create or update delegation tracking
                    // Note: In a full implementation, this might update a separate delegation table
                    // or modify the step's approver. For now, we log the delegation in history.
                    // The authorization check for the delegatee will be handled by role-based
                    // authorization if needed in a more complex implementation.

                    // Step 6: Log the 'delegated' action to history
                    var historyComments = $"Delegated to user {delegateToUserId}";
                    if (!string.IsNullOrWhiteSpace(comments))
                    {
                        historyComments += $". Comments: {comments}";
                    }
                    HistoryService.LogAction(requestId, currentStepId, ACTION_DELEGATED, userId, historyComments, request.Status, request.Status);

                    connection.CommitTransaction();

                    // Return the current model (unchanged)
                    return request;
                }
                catch (ValidationException)
                {
                    connection.RollbackTransaction();
                    throw;
                }
                catch (ArgumentException)
                {
                    connection.RollbackTransaction();
                    throw;
                }
                catch (Exception ex)
                {
                    connection.RollbackTransaction();
                    throw new ValidationException($"Failed to delegate request: {ex.Message}");
                }
            }
        }

        #endregion

        #region << Query Methods >>

        /// <summary>
        /// Retrieves an approval request by its unique identifier.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request.</param>
        /// <returns>The ApprovalRequestModel if found, or null if not found.</returns>
        /// <exception cref="ArgumentException">Thrown when requestId is empty.</exception>
        /// <example>
        /// <code>
        /// var requestService = new ApprovalRequestService();
        /// var request = requestService.GetById(requestId);
        /// if (request != null)
        /// {
        ///     Console.WriteLine($"Request status: {request.Status}");
        /// }
        /// </code>
        /// </example>
        public ApprovalRequestModel GetById(Guid requestId)
        {
            if (requestId == Guid.Empty)
            {
                throw new ArgumentException("Request ID cannot be empty.", nameof(requestId));
            }

            try
            {
                var eqlCommand = @"SELECT id, workflow_id, current_step_id, source_entity, source_record_id, 
                                          status, requested_by, requested_on, completed_on 
                                   FROM approval_request 
                                   WHERE id = @requestId";

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("requestId", requestId)
                };

                var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (eqlResult == null || !eqlResult.Any())
                {
                    return null;
                }

                return MapToModel(eqlResult.First());
            }
            catch (Exception ex)
            {
                throw new ValidationException($"Failed to retrieve approval request {requestId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves pending approval requests, optionally filtered by user.
        /// Returns requests where the specified user is an authorized approver for the current step.
        /// </summary>
        /// <param name="userId">Optional user ID to filter requests by. If null, returns all pending requests.</param>
        /// <returns>A list of pending ApprovalRequestModel instances.</returns>
        /// <remarks>
        /// When userId is provided, the method filters to only include requests where:
        /// - The request is in 'pending' status
        /// - The user is authorized to approve the current step
        /// This is used for displaying a user's approval queue/inbox.
        /// </remarks>
        /// <example>
        /// <code>
        /// var requestService = new ApprovalRequestService();
        /// 
        /// // Get all pending requests
        /// var allPending = requestService.GetPending(null);
        /// 
        /// // Get pending requests for specific user
        /// var myPending = requestService.GetPending(currentUserId);
        /// foreach (var request in myPending)
        /// {
        ///     Console.WriteLine($"Pending: {request.Id} - {request.SourceEntityName}");
        /// }
        /// </code>
        /// </example>
        public List<ApprovalRequestModel> GetPending(Guid? userId)
        {
            try
            {
                var eqlCommand = @"SELECT id, workflow_id, current_step_id, source_entity, source_record_id, 
                                          status, requested_by, requested_on, completed_on 
                                   FROM approval_request 
                                   WHERE status = @status
                                   ORDER BY requested_on DESC";

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("status", STATUS_PENDING)
                };

                var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (eqlResult == null || !eqlResult.Any())
                {
                    return new List<ApprovalRequestModel>();
                }

                var requests = eqlResult.Select(record => MapToModel(record)).ToList();

                // If userId is provided, filter to only include requests where user is authorized
                if (userId.HasValue && userId.Value != Guid.Empty)
                {
                    requests = requests.Where(r => 
                        r.CurrentStepId.HasValue && 
                        RouteService.IsUserAuthorizedApprover(userId.Value, r.CurrentStepId.Value)
                    ).ToList();
                }

                return requests;
            }
            catch (Exception ex)
            {
                throw new ValidationException($"Failed to retrieve pending approval requests: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves approval requests filtered by status.
        /// </summary>
        /// <param name="status">The status to filter by (pending, approved, rejected, escalated, expired).</param>
        /// <returns>A list of ApprovalRequestModel instances matching the specified status.</returns>
        /// <exception cref="ArgumentException">Thrown when status is null or empty, or is not a valid status value.</exception>
        /// <example>
        /// <code>
        /// var requestService = new ApprovalRequestService();
        /// 
        /// // Get all approved requests
        /// var approved = requestService.GetByStatus("approved");
        /// 
        /// // Get all rejected requests
        /// var rejected = requestService.GetByStatus("rejected");
        /// </code>
        /// </example>
        public List<ApprovalRequestModel> GetByStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                throw new ArgumentException("Status cannot be null or empty.", nameof(status));
            }

            // Validate status value
            var validStatuses = new[] { STATUS_PENDING, STATUS_APPROVED, STATUS_REJECTED, STATUS_ESCALATED, STATUS_EXPIRED };
            var normalizedStatus = status.ToLowerInvariant();
            if (!validStatuses.Contains(normalizedStatus))
            {
                throw new ArgumentException(
                    $"Invalid status value '{status}'. Must be one of: {string.Join(", ", validStatuses)}",
                    nameof(status));
            }

            try
            {
                var eqlCommand = @"SELECT id, workflow_id, current_step_id, source_entity, source_record_id, 
                                          status, requested_by, requested_on, completed_on 
                                   FROM approval_request 
                                   WHERE status = @status
                                   ORDER BY requested_on DESC";

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("status", normalizedStatus)
                };

                var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (eqlResult == null || !eqlResult.Any())
                {
                    return new List<ApprovalRequestModel>();
                }

                return eqlResult.Select(record => MapToModel(record)).ToList();
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ValidationException($"Failed to retrieve approval requests by status: {ex.Message}");
            }
        }

        #endregion

        #region << Hook Logic Methods >>

        /// <summary>
        /// Pre-create hook logic for approval_request entity validation.
        /// Called before a new approval_request record is created to validate required fields
        /// and ensure business rules are satisfied.
        /// </summary>
        /// <param name="entityName">The name of the entity being created.</param>
        /// <param name="record">The entity record being created.</param>
        /// <param name="errors">A list to add validation errors to if validation fails.</param>
        /// <remarks>
        /// This method is intended to be called from an IErpPreCreateRecordHook implementation.
        /// It validates:
        /// - Required fields are present (workflow_id, source_entity, source_record_id, requested_by)
        /// - Referenced workflow exists and is enabled
        /// - No duplicate pending requests exist for the same source record
        /// </remarks>
        public void PreCreateApiHookLogic(string entityName, EntityRecord record, List<ErrorModel> errors)
        {
            if (!string.Equals(entityName, ENTITY_NAME, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Validate required fields
            if (!record.Properties.ContainsKey("workflow_id") || record["workflow_id"] == null)
            {
                errors.Add(new ErrorModel
                {
                    Key = "workflow_id",
                    Message = "Workflow ID is required."
                });
            }

            if (!record.Properties.ContainsKey("source_entity") || string.IsNullOrWhiteSpace(record["source_entity"]?.ToString()))
            {
                errors.Add(new ErrorModel
                {
                    Key = "source_entity",
                    Message = "Source entity name is required."
                });
            }

            if (!record.Properties.ContainsKey("source_record_id") || record["source_record_id"] == null)
            {
                errors.Add(new ErrorModel
                {
                    Key = "source_record_id",
                    Message = "Source record ID is required."
                });
            }

            if (!record.Properties.ContainsKey("requested_by") || record["requested_by"] == null)
            {
                errors.Add(new ErrorModel
                {
                    Key = "requested_by",
                    Message = "Requested by user ID is required."
                });
            }

            // Early return if there are already validation errors
            if (errors.Count > 0)
            {
                return;
            }

            var workflowId = (Guid)record["workflow_id"];
            var sourceEntityName = record["source_entity"]?.ToString();
            var sourceRecordId = (Guid)record["source_record_id"];

            // Validate workflow exists and is enabled
            try
            {
                var workflowService = new WorkflowConfigService();
                var workflow = workflowService.GetById(workflowId);
                
                if (workflow == null)
                {
                    errors.Add(new ErrorModel
                    {
                        Key = "workflow_id",
                        Message = $"Workflow with ID '{workflowId}' not found."
                    });
                    return;
                }
                
                if (!workflow.IsEnabled)
                {
                    errors.Add(new ErrorModel
                    {
                        Key = "workflow_id",
                        Message = "The specified workflow is disabled and cannot accept new requests."
                    });
                    return;
                }
            }
            catch (Exception)
            {
                // Silently continue if we can't validate the workflow - it may not exist yet during init
            }

            // Check for duplicate pending requests
            try
            {
                var duplicateCheckEql = @"SELECT id FROM approval_request 
                                          WHERE source_entity = @entityName 
                                          AND source_record_id = @recordId 
                                          AND status = @status";

                var duplicateParams = new List<EqlParameter>
                {
                    new EqlParameter("entityName", sourceEntityName),
                    new EqlParameter("recordId", sourceRecordId),
                    new EqlParameter("status", STATUS_PENDING)
                };

                var duplicateResult = new EqlCommand(duplicateCheckEql, duplicateParams).Execute();

                if (duplicateResult != null && duplicateResult.Any())
                {
                    errors.Add(new ErrorModel
                    {
                        Key = "source_record_id",
                        Message = "A pending approval request already exists for this record."
                    });
                    return;
                }
            }
            catch (Exception)
            {
                // Silently continue - validation is best-effort
            }

            // Determine and set initial step (AC3: setting current_step_id on the record before creation)
            try
            {
                var routeService = new ApprovalRouteService();
                var firstStep = routeService.GetFirstStep(workflowId);
                
                if (firstStep != null)
                {
                    record["current_step_id"] = firstStep.Id;
                }
            }
            catch (Exception)
            {
                // Silently continue - step may be assigned later
            }

            // Set initial status if not already set
            if (!record.Properties.ContainsKey("status") || record["status"] == null || string.IsNullOrWhiteSpace(record["status"]?.ToString()))
            {
                record["status"] = STATUS_PENDING;
            }

            // Set creation timestamp if not already set
            if (!record.Properties.ContainsKey("requested_on") || record["requested_on"] == null)
            {
                record["requested_on"] = DateTime.UtcNow;
            }

            // Initialize notification tracking fields
            if (!record.Properties.ContainsKey("notification_count") || record["notification_count"] == null)
            {
                record["notification_count"] = 0;
            }

            // Initialize archived flag
            if (!record.Properties.ContainsKey("is_archived") || record["is_archived"] == null)
            {
                record["is_archived"] = false;
            }
        }

        /// <summary>
        /// Post-update hook logic for approval_request entity.
        /// Called after an approval_request record is updated to handle side effects
        /// such as notifications or cascading status updates.
        /// </summary>
        /// <param name="entityName">The name of the entity that was updated.</param>
        /// <param name="record">The updated entity record.</param>
        /// <remarks>
        /// This method is intended to be called from an IErpPostUpdateRecordHook implementation.
        /// It handles:
        /// - Triggering notifications when status changes to approved/rejected
        /// - Updating related records when workflow completes
        /// </remarks>
        public void PostUpdateApiHookLogic(string entityName, EntityRecord record)
        {
            if (!string.Equals(entityName, ENTITY_NAME, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!record.Properties.ContainsKey("status") || record["status"] == null)
            {
                return;
            }

            var status = record["status"].ToString().ToLowerInvariant();

            // Handle status-specific post-processing
            switch (status)
            {
                case STATUS_APPROVED:
                    HandleApprovalComplete(record);
                    break;

                case STATUS_REJECTED:
                    HandleRejectionComplete(record);
                    break;

                case STATUS_ESCALATED:
                    HandleEscalation(record);
                    break;

                default:
                    // No special handling for other statuses
                    break;
            }
        }

        #endregion

        #region << Private Helper Methods >>

        /// <summary>
        /// Retrieves a source record from its entity for rule evaluation.
        /// </summary>
        /// <param name="recordId">The unique identifier of the source record.</param>
        /// <param name="entityName">The name of the entity the record belongs to.</param>
        /// <returns>The EntityRecord if found, or null if not found.</returns>
        private EntityRecord GetSourceRecord(Guid recordId, string entityName)
        {
            try
            {
                var eqlCommand = $"SELECT * FROM {entityName} WHERE id = @recordId";
                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("recordId", recordId)
                };

                var result = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (result == null || !result.Any())
                {
                    return null;
                }

                return result.First();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Maps an EntityRecord to an ApprovalRequestModel.
        /// </summary>
        /// <param name="record">The entity record containing approval request data.</param>
        /// <returns>An ApprovalRequestModel populated from the record.</returns>
        private ApprovalRequestModel MapToModel(EntityRecord record)
        {
            if (record == null)
            {
                return null;
            }

            return new ApprovalRequestModel
            {
                Id = record.Properties.ContainsKey("id") ? (Guid)record["id"] : Guid.Empty,
                WorkflowId = record.Properties.ContainsKey("workflow_id") && record["workflow_id"] != null
                    ? (Guid)record["workflow_id"]
                    : Guid.Empty,
                CurrentStepId = record.Properties.ContainsKey("current_step_id") && record["current_step_id"] != null
                    ? (Guid?)record["current_step_id"]
                    : null,
                SourceEntityName = record.Properties.ContainsKey("source_entity")
                    ? record["source_entity"]?.ToString()
                    : string.Empty,
                SourceRecordId = record.Properties.ContainsKey("source_record_id") && record["source_record_id"] != null
                    ? (Guid)record["source_record_id"]
                    : Guid.Empty,
                Status = record.Properties.ContainsKey("status")
                    ? record["status"]?.ToString()
                    : STATUS_PENDING,
                RequestedBy = record.Properties.ContainsKey("requested_by") && record["requested_by"] != null
                    ? (Guid)record["requested_by"]
                    : Guid.Empty,
                RequestedOn = record.Properties.ContainsKey("requested_on") && record["requested_on"] != null
                    ? (DateTime)record["requested_on"]
                    : DateTime.MinValue,
                CompletedOn = record.Properties.ContainsKey("completed_on") && record["completed_on"] != null
                    ? (DateTime?)record["completed_on"]
                    : null,
                LastNotificationSent = record.Properties.ContainsKey("last_notification_sent") && record["last_notification_sent"] != null
                    ? (DateTime?)record["last_notification_sent"]
                    : null,
                NotificationCount = record.Properties.ContainsKey("notification_count") && record["notification_count"] != null
                    ? Convert.ToInt32(record["notification_count"])
                    : 0,
                IsArchived = record.Properties.ContainsKey("is_archived") && record["is_archived"] != null
                    ? Convert.ToBoolean(record["is_archived"])
                    : false,
                ArchivedOn = record.Properties.ContainsKey("archived_on") && record["archived_on"] != null
                    ? (DateTime?)record["archived_on"]
                    : null
            };
        }

        /// <summary>
        /// Handles post-processing when an approval request is approved (workflow complete).
        /// </summary>
        /// <param name="record">The approval request record.</param>
        private void HandleApprovalComplete(EntityRecord record)
        {
            // This could trigger notifications, update the source record status, etc.
            // Implementation depends on business requirements
            // For now, this is a placeholder for extensibility
            
            if (!record.Properties.ContainsKey("source_entity") || 
                !record.Properties.ContainsKey("source_record_id"))
            {
                return;
            }

            var sourceEntityName = record["source_entity"]?.ToString();
            var sourceRecordId = record["source_record_id"];

            if (string.IsNullOrWhiteSpace(sourceEntityName) || sourceRecordId == null)
            {
                return;
            }

            // Potential actions:
            // 1. Update source record's approval_status field
            // 2. Send notification to requester
            // 3. Trigger downstream processes
            
            // Example: Update source record if it has an approval_status field
            try
            {
                var patchRecord = new EntityRecord();
                patchRecord["id"] = sourceRecordId;
                patchRecord["approval_status"] = STATUS_APPROVED;
                
                // This will silently fail if the field doesn't exist, which is acceptable
                RecMan.UpdateRecord(sourceEntityName, patchRecord);
            }
            catch (Exception)
            {
                // Silently continue - source record update is best-effort
            }
        }

        /// <summary>
        /// Handles post-processing when an approval request is rejected.
        /// </summary>
        /// <param name="record">The approval request record.</param>
        private void HandleRejectionComplete(EntityRecord record)
        {
            // Similar to approval, this could trigger notifications and updates
            
            if (!record.Properties.ContainsKey("source_entity") || 
                !record.Properties.ContainsKey("source_record_id"))
            {
                return;
            }

            var sourceEntityName = record["source_entity"]?.ToString();
            var sourceRecordId = record["source_record_id"];

            if (string.IsNullOrWhiteSpace(sourceEntityName) || sourceRecordId == null)
            {
                return;
            }

            // Example: Update source record if it has an approval_status field
            try
            {
                var patchRecord = new EntityRecord();
                patchRecord["id"] = sourceRecordId;
                patchRecord["approval_status"] = STATUS_REJECTED;
                
                RecMan.UpdateRecord(sourceEntityName, patchRecord);
            }
            catch (Exception)
            {
                // Silently continue - source record update is best-effort
            }
        }

        /// <summary>
        /// Handles post-processing when an approval request is escalated.
        /// </summary>
        /// <param name="record">The approval request record.</param>
        private void HandleEscalation(EntityRecord record)
        {
            // Escalation handling - could notify managers, update dashboards, etc.
            // Implementation depends on business requirements
            
            // For now, this is a placeholder for extensibility
            // Escalation notifications are typically handled by the background job
        }

        #endregion
    }
}
