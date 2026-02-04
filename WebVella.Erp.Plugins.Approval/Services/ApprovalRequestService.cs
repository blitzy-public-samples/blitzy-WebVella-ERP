using System;
using System.Collections.Generic;
using System.Linq;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Database;
using WebVella.Erp.Eql;
using WebVella.Erp.Exceptions;
using WebVella.Erp.Plugins.Approval.Model;

namespace WebVella.Erp.Plugins.Approval.Services
{
    /// <summary>
    /// Service class for managing the complete lifecycle of approval requests from creation through final disposition.
    /// Handles request CRUD operations and state transitions (Pending → Approved/Rejected/Delegated/Escalated/Cancelled).
    /// Integrates with ApprovalRouteService for step determination and ApprovalHistoryService for audit logging.
    /// All state-changing operations execute within transaction scope for data integrity.
    /// </summary>
    /// <remarks>
    /// This service is the primary orchestrator for all approval request operations and follows these patterns:
    /// 
    /// Transaction Management:
    /// - All state-changing operations (Create, Approve, Reject, Delegate, Escalate) are wrapped in database transactions
    /// - Transactions are created using DbContext.Current.CreateConnection() for atomicity
    /// - History logging occurs within the same transaction to ensure audit trail consistency
    /// 
    /// Authorization:
    /// - Each action validates that the performing user is authorized for the current step
    /// - Authorization is determined via ApprovalRouteService.GetApproversForStep()
    /// - Unauthorized attempts throw UnauthorizedAccessException
    /// 
    /// Status Transitions:
    /// - Pending → Approved: When all steps are completed successfully
    /// - Pending → Rejected: When any approver rejects the request
    /// - Pending → Escalated: When request is escalated due to SLA breach or manual escalation
    /// - Pending → Pending: When approved but more steps remain (step transition)
    /// - Delegated: Request remains pending but assigned to new approver
    /// 
    /// Entity: approval_request
    /// Related Entities: approval_workflow, approval_step, approval_history
    /// </remarks>
    public class ApprovalRequestService : BaseService
    {
        #region Constants

        /// <summary>
        /// Entity name for approval requests.
        /// </summary>
        private const string ENTITY_REQUEST = "approval_request";

        /// <summary>
        /// Entity name for approval workflows.
        /// </summary>
        private const string ENTITY_WORKFLOW = "approval_workflow";

        /// <summary>
        /// Entity name for approval steps.
        /// </summary>
        private const string ENTITY_STEP = "approval_step";

        /// <summary>
        /// Field name for record ID.
        /// </summary>
        private const string FIELD_ID = "id";

        /// <summary>
        /// Field name for source record ID.
        /// </summary>
        private const string FIELD_SOURCE_RECORD_ID = "source_record_id";

        /// <summary>
        /// Field name for source entity name.
        /// </summary>
        private const string FIELD_SOURCE_ENTITY = "source_entity";

        /// <summary>
        /// Field name for workflow ID.
        /// </summary>
        private const string FIELD_WORKFLOW_ID = "workflow_id";

        /// <summary>
        /// Field name for current step ID.
        /// </summary>
        private const string FIELD_CURRENT_STEP_ID = "current_step_id";

        /// <summary>
        /// Field name for status.
        /// </summary>
        private const string FIELD_STATUS = "status";

        /// <summary>
        /// Field name for created on timestamp.
        /// </summary>
        private const string FIELD_CREATED_ON = "created_on";

        /// <summary>
        /// Field name for created by user ID.
        /// </summary>
        private const string FIELD_CREATED_BY = "created_by";

        /// <summary>
        /// Field name for delegated to user ID.
        /// </summary>
        private const string FIELD_DELEGATED_TO = "delegated_to";

        /// <summary>
        /// Field name for due date.
        /// </summary>
        private const string FIELD_DUE_DATE = "due_date";

        /// <summary>
        /// Field name for is_active flag on workflows.
        /// </summary>
        private const string FIELD_IS_ACTIVE = "is_active";

        /// <summary>
        /// Field name for SLA hours on steps.
        /// </summary>
        private const string FIELD_SLA_HOURS = "sla_hours";

        /// <summary>
        /// Action type string for submitted action.
        /// </summary>
        private const string ACTION_SUBMITTED = "submitted";

        /// <summary>
        /// Action type string for approved action.
        /// </summary>
        private const string ACTION_APPROVED = "approved";

        /// <summary>
        /// Action type string for rejected action.
        /// </summary>
        private const string ACTION_REJECTED = "rejected";

        /// <summary>
        /// Action type string for delegated action.
        /// </summary>
        private const string ACTION_DELEGATED = "delegated";

        /// <summary>
        /// Action type string for escalated action.
        /// </summary>
        private const string ACTION_ESCALATED = "escalated";

        #endregion

        #region Private Fields

        /// <summary>
        /// Service for workflow routing and step determination.
        /// </summary>
        private readonly ApprovalRouteService _routeService;

        /// <summary>
        /// Service for audit trail logging.
        /// </summary>
        private readonly ApprovalHistoryService _historyService;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="ApprovalRequestService"/> class.
        /// Creates instances of required dependent services for routing and history logging.
        /// </summary>
        public ApprovalRequestService()
        {
            _routeService = new ApprovalRouteService();
            _historyService = new ApprovalHistoryService();
        }

        #endregion

        #region Public Methods - Request Creation

        /// <summary>
        /// Creates a new approval request for a source record and initiates the approval workflow.
        /// Determines the initial step via routing service and logs the submission action.
        /// </summary>
        /// <param name="sourceRecordId">The unique identifier of the source record requiring approval.</param>
        /// <param name="sourceEntity">The entity name of the source record (e.g., "purchase_order", "expense_report").</param>
        /// <param name="workflowId">The unique identifier of the workflow to use for this request.</param>
        /// <param name="initiatedBy">The unique identifier of the user initiating the approval request.</param>
        /// <returns>The created EntityRecord containing the new approval request.</returns>
        /// <exception cref="ValidationException">
        /// Thrown when:
        /// - The specified workflow does not exist
        /// - The workflow is not active
        /// - No steps are defined for the workflow
        /// - Record creation fails
        /// </exception>
        /// <exception cref="ArgumentException">Thrown when sourceEntity is null or empty.</exception>
        /// <remarks>
        /// This method executes within a database transaction to ensure atomicity.
        /// The following operations are performed:
        /// 1. Validate workflow exists and is active
        /// 2. Determine initial step using DetermineRoute()
        /// 3. Calculate due date based on step SLA
        /// 4. Create approval_request record with status='pending'
        /// 5. Log 'submitted' action to approval_history
        /// </remarks>
        public EntityRecord CreateRequest(Guid sourceRecordId, string sourceEntity, Guid workflowId, Guid initiatedBy)
        {
            // Validate input parameters
            if (sourceRecordId == Guid.Empty)
            {
                throw new ArgumentException("Source record ID cannot be empty.", nameof(sourceRecordId));
            }

            if (string.IsNullOrWhiteSpace(sourceEntity))
            {
                throw new ArgumentException("Source entity name cannot be null or empty.", nameof(sourceEntity));
            }

            if (workflowId == Guid.Empty)
            {
                throw new ArgumentException("Workflow ID cannot be empty.", nameof(workflowId));
            }

            if (initiatedBy == Guid.Empty)
            {
                throw new ArgumentException("Initiated by user ID cannot be empty.", nameof(initiatedBy));
            }

            try
            {
                using (var connection = DbContext.Current.CreateConnection())
                {
                    try
                    {
                        connection.BeginTransaction();

                        // Validate workflow exists and is enabled
                        var workflow = GetWorkflow(workflowId);
                        if (workflow == null)
                        {
                            throw new ValidationException($"Workflow with ID {workflowId} not found.");
                        }

                        var isActive = workflow[FIELD_IS_ACTIVE] as bool? ?? false;
                        if (!isActive)
                        {
                            throw new ValidationException($"Workflow with ID {workflowId} is not active.");
                        }

                        // Determine initial step via routing service
                        var routeResult = _routeService.DetermineRoute(sourceRecordId, sourceEntity);
                        Guid? initialStepId = null;

                        if (routeResult.HasValue)
                        {
                            // Use the step from the route if workflow matches
                            if (routeResult.Value.workflowId == workflowId)
                            {
                                initialStepId = routeResult.Value.stepId;
                            }
                            else
                            {
                                // Route returned different workflow, get first step of specified workflow
                                initialStepId = GetFirstStepForWorkflow(workflowId);
                            }
                        }
                        else
                        {
                            // No route found, get first step of workflow
                            initialStepId = GetFirstStepForWorkflow(workflowId);
                        }

                        if (!initialStepId.HasValue)
                        {
                            throw new ValidationException($"No steps defined for workflow {workflowId}.");
                        }

                        // Calculate due date based on step SLA
                        DateTime? dueDate = CalculateDueDate(initialStepId.Value);

                        // Create approval_request record
                        var requestId = Guid.NewGuid();
                        var createdOn = DateTime.UtcNow;
                        var status = ApprovalStatus.Pending.ToString().ToLowerInvariant();

                        var record = new EntityRecord();
                        record[FIELD_ID] = requestId;
                        record[FIELD_SOURCE_RECORD_ID] = sourceRecordId;
                        record[FIELD_SOURCE_ENTITY] = sourceEntity;
                        record[FIELD_WORKFLOW_ID] = workflowId;
                        record[FIELD_CURRENT_STEP_ID] = initialStepId.Value;
                        record[FIELD_STATUS] = status;
                        record[FIELD_CREATED_ON] = createdOn;
                        record[FIELD_CREATED_BY] = initiatedBy;
                        record[FIELD_DUE_DATE] = dueDate;

                        var createResponse = RecMan.CreateRecord(ENTITY_REQUEST, record);
                        if (!createResponse.Success)
                        {
                            throw new ValidationException($"Failed to create approval request: {createResponse.Message}");
                        }

                        // Log 'submitted' action via history service
                        _historyService.LogApprovalAction(
                            requestId: requestId,
                            actionType: ACTION_SUBMITTED,
                            performedBy: initiatedBy,
                            comments: $"Approval request submitted for {sourceEntity} record.",
                            previousStatus: string.Empty,
                            newStatus: status
                        );

                        connection.CommitTransaction();

                        return record;
                    }
                    catch
                    {
                        connection.RollbackTransaction();
                        throw;
                    }
                }
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ValidationException($"Error creating approval request: {ex.Message}");
            }
        }

        #endregion

        #region Public Methods - Request Actions

        /// <summary>
        /// Approves an approval request at the current step and advances the workflow.
        /// If this is the final step, the request status becomes 'approved'.
        /// If more steps remain, the request advances to the next step.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request to approve.</param>
        /// <param name="approverId">The unique identifier of the user performing the approval.</param>
        /// <param name="comments">Optional comments about the approval decision.</param>
        /// <returns>The updated EntityRecord after approval processing.</returns>
        /// <exception cref="ValidationException">
        /// Thrown when:
        /// - The request does not exist
        /// - The request status is not 'pending'
        /// - The update operation fails
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">
        /// Thrown when the approverId is not authorized to approve at the current step.
        /// </exception>
        /// <remarks>
        /// Workflow progression logic:
        /// 1. Validate request exists and is pending
        /// 2. Validate approver authorization
        /// 3. Evaluate next step using routing service
        /// 4. If next step exists: Update current_step_id, calculate new due date, stay 'pending'
        /// 5. If no next step: Update status to 'approved' (workflow complete)
        /// 6. Log 'approved' action with step transition details
        /// </remarks>
        public EntityRecord ApproveRequest(Guid requestId, Guid approverId, string comments = null)
        {
            if (requestId == Guid.Empty)
            {
                throw new ArgumentException("Request ID cannot be empty.", nameof(requestId));
            }

            if (approverId == Guid.Empty)
            {
                throw new ArgumentException("Approver ID cannot be empty.", nameof(approverId));
            }

            try
            {
                using (var connection = DbContext.Current.CreateConnection())
                {
                    try
                    {
                        connection.BeginTransaction();

                        // Validate request exists and status is 'pending'
                        var request = GetRequest(requestId);
                        if (request == null)
                        {
                            throw new ValidationException($"Approval request with ID {requestId} not found.");
                        }

                        var currentStatus = request[FIELD_STATUS] as string;
                        var pendingStatus = ApprovalStatus.Pending.ToString().ToLowerInvariant();

                        if (!string.Equals(currentStatus, pendingStatus, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new ValidationException($"Cannot approve request with status '{currentStatus}'. Request must be in 'pending' status.");
                        }

                        // Validate approver authorization
                        ValidateApproverAuthorization(request, approverId);

                        var workflowId = (Guid)request[FIELD_WORKFLOW_ID];
                        var currentStepId = request[FIELD_CURRENT_STEP_ID] as Guid?;

                        // Evaluate next step using routing service
                        var nextStepId = _routeService.EvaluateNextStep(requestId);

                        var updateRecord = new EntityRecord();
                        updateRecord[FIELD_ID] = requestId;

                        string newStatus;
                        string actionComments;

                        if (nextStepId.HasValue)
                        {
                            // More steps remain - advance to next step
                            newStatus = pendingStatus;
                            updateRecord[FIELD_CURRENT_STEP_ID] = nextStepId.Value;
                            updateRecord[FIELD_STATUS] = newStatus;

                            // Calculate new due date based on next step's SLA
                            var newDueDate = CalculateDueDate(nextStepId.Value);
                            updateRecord[FIELD_DUE_DATE] = newDueDate;

                            actionComments = string.IsNullOrWhiteSpace(comments)
                                ? $"Approved at step. Advanced to next step."
                                : comments;
                        }
                        else
                        {
                            // No more steps - workflow complete
                            newStatus = ApprovalStatus.Approved.ToString().ToLowerInvariant();
                            updateRecord[FIELD_STATUS] = newStatus;

                            actionComments = string.IsNullOrWhiteSpace(comments)
                                ? "Final approval granted. Workflow complete."
                                : comments;
                        }

                        var updateResponse = RecMan.UpdateRecord(ENTITY_REQUEST, updateRecord);
                        if (!updateResponse.Success)
                        {
                            throw new ValidationException($"Failed to update approval request: {updateResponse.Message}");
                        }

                        // Log 'approved' action via history service
                        _historyService.LogApprovalAction(
                            requestId: requestId,
                            actionType: ACTION_APPROVED,
                            performedBy: approverId,
                            comments: actionComments,
                            previousStatus: currentStatus,
                            newStatus: newStatus
                        );

                        connection.CommitTransaction();

                        // Return updated record
                        return GetRequest(requestId);
                    }
                    catch
                    {
                        connection.RollbackTransaction();
                        throw;
                    }
                }
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ValidationException($"Error approving request: {ex.Message}");
            }
        }

        /// <summary>
        /// Rejects an approval request and terminates the workflow.
        /// The request status becomes 'rejected' and no further steps are processed.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request to reject.</param>
        /// <param name="approverId">The unique identifier of the user performing the rejection.</param>
        /// <param name="comments">Required comments explaining the rejection reason.</param>
        /// <returns>The updated EntityRecord after rejection.</returns>
        /// <exception cref="ValidationException">
        /// Thrown when:
        /// - Comments are null or empty (required for rejections)
        /// - The request does not exist
        /// - The request status is not 'pending'
        /// - The update operation fails
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">
        /// Thrown when the approverId is not authorized to reject at the current step.
        /// </exception>
        /// <remarks>
        /// Rejection is a terminal action - once rejected, the request cannot be
        /// reprocessed through the workflow. A new request must be created.
        /// Comments are required to document the rejection reason for audit purposes.
        /// </remarks>
        public EntityRecord RejectRequest(Guid requestId, Guid approverId, string comments)
        {
            if (requestId == Guid.Empty)
            {
                throw new ArgumentException("Request ID cannot be empty.", nameof(requestId));
            }

            if (approverId == Guid.Empty)
            {
                throw new ArgumentException("Approver ID cannot be empty.", nameof(approverId));
            }

            // Comments are required for rejection
            if (string.IsNullOrWhiteSpace(comments))
            {
                throw new ValidationException("Comments are required when rejecting an approval request.");
            }

            try
            {
                using (var connection = DbContext.Current.CreateConnection())
                {
                    try
                    {
                        connection.BeginTransaction();

                        // Validate request exists and status is 'pending'
                        var request = GetRequest(requestId);
                        if (request == null)
                        {
                            throw new ValidationException($"Approval request with ID {requestId} not found.");
                        }

                        var currentStatus = request[FIELD_STATUS] as string;
                        var pendingStatus = ApprovalStatus.Pending.ToString().ToLowerInvariant();

                        if (!string.Equals(currentStatus, pendingStatus, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new ValidationException($"Cannot reject request with status '{currentStatus}'. Request must be in 'pending' status.");
                        }

                        // Validate approver authorization
                        ValidateApproverAuthorization(request, approverId);

                        // Update status to 'rejected'
                        var newStatus = ApprovalStatus.Rejected.ToString().ToLowerInvariant();

                        var updateRecord = new EntityRecord();
                        updateRecord[FIELD_ID] = requestId;
                        updateRecord[FIELD_STATUS] = newStatus;

                        var updateResponse = RecMan.UpdateRecord(ENTITY_REQUEST, updateRecord);
                        if (!updateResponse.Success)
                        {
                            throw new ValidationException($"Failed to update approval request: {updateResponse.Message}");
                        }

                        // Log 'rejected' action via history service
                        _historyService.LogApprovalAction(
                            requestId: requestId,
                            actionType: ACTION_REJECTED,
                            performedBy: approverId,
                            comments: comments,
                            previousStatus: currentStatus,
                            newStatus: newStatus
                        );

                        connection.CommitTransaction();

                        // Return updated record
                        return GetRequest(requestId);
                    }
                    catch
                    {
                        connection.RollbackTransaction();
                        throw;
                    }
                }
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ValidationException($"Error rejecting request: {ex.Message}");
            }
        }

        /// <summary>
        /// Delegates an approval request to another user.
        /// The request remains in 'pending' status but the specified user becomes the new approver.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request to delegate.</param>
        /// <param name="delegatorId">The unique identifier of the user performing the delegation.</param>
        /// <param name="delegateToUserId">The unique identifier of the user to delegate to.</param>
        /// <param name="comments">Optional comments explaining the delegation reason.</param>
        /// <returns>The updated EntityRecord after delegation.</returns>
        /// <exception cref="ValidationException">
        /// Thrown when:
        /// - The request does not exist
        /// - The request status is not 'pending'
        /// - The delegate-to user does not exist
        /// - The update operation fails
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">
        /// Thrown when the delegatorId is not authorized to delegate at the current step.
        /// </exception>
        /// <remarks>
        /// Delegation transfers approval responsibility to another user.
        /// The delegated user will be recorded in the 'delegated_to' field.
        /// The original step and SLA remain unchanged.
        /// Delegation is logged in the audit trail with both the delegator and delegate information.
        /// </remarks>
        public EntityRecord DelegateRequest(Guid requestId, Guid delegatorId, Guid delegateToUserId, string comments = null)
        {
            if (requestId == Guid.Empty)
            {
                throw new ArgumentException("Request ID cannot be empty.", nameof(requestId));
            }

            if (delegatorId == Guid.Empty)
            {
                throw new ArgumentException("Delegator ID cannot be empty.", nameof(delegatorId));
            }

            if (delegateToUserId == Guid.Empty)
            {
                throw new ArgumentException("Delegate-to user ID cannot be empty.", nameof(delegateToUserId));
            }

            try
            {
                using (var connection = DbContext.Current.CreateConnection())
                {
                    try
                    {
                        connection.BeginTransaction();

                        // Validate request exists and status is 'pending'
                        var request = GetRequest(requestId);
                        if (request == null)
                        {
                            throw new ValidationException($"Approval request with ID {requestId} not found.");
                        }

                        var currentStatus = request[FIELD_STATUS] as string;
                        var pendingStatus = ApprovalStatus.Pending.ToString().ToLowerInvariant();

                        if (!string.Equals(currentStatus, pendingStatus, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new ValidationException($"Cannot delegate request with status '{currentStatus}'. Request must be in 'pending' status.");
                        }

                        // Validate delegator authorization
                        ValidateApproverAuthorization(request, delegatorId);

                        // Validate delegate-to user exists
                        var delegateUser = SecMan.GetUser(delegateToUserId);
                        if (delegateUser == null)
                        {
                            throw new ValidationException($"Delegate-to user with ID {delegateToUserId} not found.");
                        }

                        // Update request with delegated_to field
                        var updateRecord = new EntityRecord();
                        updateRecord[FIELD_ID] = requestId;
                        updateRecord[FIELD_DELEGATED_TO] = delegateToUserId;

                        var updateResponse = RecMan.UpdateRecord(ENTITY_REQUEST, updateRecord);
                        if (!updateResponse.Success)
                        {
                            throw new ValidationException($"Failed to update approval request: {updateResponse.Message}");
                        }

                        // Build delegation comments with user info
                        var delegateUsername = delegateUser.Username ?? delegateToUserId.ToString();
                        var actionComments = string.IsNullOrWhiteSpace(comments)
                            ? $"Delegated to user: {delegateUsername}"
                            : $"{comments} (Delegated to: {delegateUsername})";

                        // Log 'delegated' action via history service
                        _historyService.LogApprovalAction(
                            requestId: requestId,
                            actionType: ACTION_DELEGATED,
                            performedBy: delegatorId,
                            comments: actionComments,
                            previousStatus: currentStatus,
                            newStatus: currentStatus // Status remains pending
                        );

                        connection.CommitTransaction();

                        // Return updated record
                        return GetRequest(requestId);
                    }
                    catch
                    {
                        connection.RollbackTransaction();
                        throw;
                    }
                }
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ValidationException($"Error delegating request: {ex.Message}");
            }
        }

        /// <summary>
        /// Escalates an approval request to a higher authority.
        /// The request status becomes 'escalated' indicating special handling is required.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request to escalate.</param>
        /// <param name="escalatedBy">The unique identifier of the user or system performing the escalation.</param>
        /// <param name="reason">Required reason for the escalation.</param>
        /// <returns>The updated EntityRecord after escalation.</returns>
        /// <exception cref="ValidationException">
        /// Thrown when:
        /// - Reason is null or empty (required for escalations)
        /// - The request does not exist
        /// - The request status is not 'pending'
        /// - The update operation fails
        /// </exception>
        /// <remarks>
        /// Escalation is typically triggered by:
        /// - SLA breach (automatic via ApprovalEscalationJob)
        /// - Manual escalation by an approver who cannot make a decision
        /// - System escalation due to business rules
        /// 
        /// Escalated requests require special handling - they may be routed to
        /// a manager, admin, or designated escalation handler.
        /// A reason is required to document why the escalation occurred.
        /// </remarks>
        public EntityRecord EscalateRequest(Guid requestId, Guid escalatedBy, string reason)
        {
            if (requestId == Guid.Empty)
            {
                throw new ArgumentException("Request ID cannot be empty.", nameof(requestId));
            }

            if (escalatedBy == Guid.Empty)
            {
                throw new ArgumentException("Escalated by ID cannot be empty.", nameof(escalatedBy));
            }

            // Reason is required for escalation
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ValidationException("Reason is required when escalating an approval request.");
            }

            try
            {
                using (var connection = DbContext.Current.CreateConnection())
                {
                    try
                    {
                        connection.BeginTransaction();

                        // Validate request exists and status is 'pending'
                        var request = GetRequest(requestId);
                        if (request == null)
                        {
                            throw new ValidationException($"Approval request with ID {requestId} not found.");
                        }

                        var currentStatus = request[FIELD_STATUS] as string;
                        var pendingStatus = ApprovalStatus.Pending.ToString().ToLowerInvariant();

                        if (!string.Equals(currentStatus, pendingStatus, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new ValidationException($"Cannot escalate request with status '{currentStatus}'. Request must be in 'pending' status.");
                        }

                        // Update status to 'escalated'
                        var newStatus = ApprovalStatus.Escalated.ToString().ToLowerInvariant();

                        var updateRecord = new EntityRecord();
                        updateRecord[FIELD_ID] = requestId;
                        updateRecord[FIELD_STATUS] = newStatus;

                        var updateResponse = RecMan.UpdateRecord(ENTITY_REQUEST, updateRecord);
                        if (!updateResponse.Success)
                        {
                            throw new ValidationException($"Failed to update approval request: {updateResponse.Message}");
                        }

                        // Log 'escalated' action via history service
                        _historyService.LogApprovalAction(
                            requestId: requestId,
                            actionType: ACTION_ESCALATED,
                            performedBy: escalatedBy,
                            comments: reason,
                            previousStatus: currentStatus,
                            newStatus: newStatus
                        );

                        connection.CommitTransaction();

                        // Return updated record
                        return GetRequest(requestId);
                    }
                    catch
                    {
                        connection.RollbackTransaction();
                        throw;
                    }
                }
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ValidationException($"Error escalating request: {ex.Message}");
            }
        }

        #endregion

        #region Public Methods - Request Retrieval

        /// <summary>
        /// Retrieves an approval request by its unique identifier.
        /// </summary>
        /// <param name="requestId">The unique identifier of the approval request to retrieve.</param>
        /// <returns>The EntityRecord for the request, or null if not found.</returns>
        /// <exception cref="ArgumentException">Thrown when requestId is empty.</exception>
        /// <remarks>
        /// This method retrieves all fields from the approval_request entity.
        /// For related data (workflow, step details), use the appropriate service methods.
        /// </remarks>
        public EntityRecord GetRequest(Guid requestId)
        {
            if (requestId == Guid.Empty)
            {
                throw new ArgumentException("Request ID cannot be empty.", nameof(requestId));
            }

            try
            {
                var eqlCommand = $"SELECT * FROM {ENTITY_REQUEST} WHERE {FIELD_ID} = @requestId";
                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("requestId", requestId)
                };

                var result = new EqlCommand(eqlCommand, eqlParams).Execute();
                return result?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                throw new ValidationException($"Error retrieving approval request: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves all pending approval requests where the specified user is an authorized approver.
        /// </summary>
        /// <param name="userId">The unique identifier of the user to find pending requests for.</param>
        /// <returns>
        /// An EntityRecordList containing all pending requests where the user can approve,
        /// ordered by due_date ascending (most urgent first).
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when userId is empty.</exception>
        /// <remarks>
        /// This method finds requests where:
        /// - Status is 'pending'
        /// - The user is in the approvers list for the current step (via ApprovalRouteService)
        /// - OR the request was delegated to this user
        /// 
        /// The returned list is sorted by due_date to prioritize requests requiring urgent attention.
        /// </remarks>
        public EntityRecordList GetPendingRequestsForUser(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            }

            try
            {
                var pendingStatus = ApprovalStatus.Pending.ToString().ToLowerInvariant();

                // First, get all pending requests
                var eqlCommand = $@"SELECT * FROM {ENTITY_REQUEST} 
                                    WHERE {FIELD_STATUS} = @status 
                                    ORDER BY {FIELD_DUE_DATE} ASC";

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("status", pendingStatus)
                };

                var allPendingRequests = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (allPendingRequests == null || !allPendingRequests.Any())
                {
                    return new EntityRecordList();
                }

                // Filter to requests where user is authorized approver or delegate
                var userRequests = new EntityRecordList();

                foreach (var request in allPendingRequests)
                {
                    var currentStepId = request[FIELD_CURRENT_STEP_ID] as Guid?;
                    var delegatedTo = request[FIELD_DELEGATED_TO] as Guid?;

                    // Check if request was delegated to this user
                    if (delegatedTo.HasValue && delegatedTo.Value == userId)
                    {
                        userRequests.Add(request);
                        continue;
                    }

                    // Check if user is in approvers list for current step
                    if (currentStepId.HasValue)
                    {
                        var approvers = _routeService.GetApproversForStep(currentStepId.Value);
                        if (approvers != null && approvers.Contains(userId))
                        {
                            userRequests.Add(request);
                        }
                    }
                }

                return userRequests;
            }
            catch (Exception ex)
            {
                throw new ValidationException($"Error retrieving pending requests for user: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves all approval requests with the specified status.
        /// </summary>
        /// <param name="status">The status to filter by (e.g., "pending", "approved", "rejected").</param>
        /// <returns>
        /// An EntityRecordList containing all requests with the specified status,
        /// ordered by created_on descending (most recent first).
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when status is null or empty.</exception>
        /// <remarks>
        /// Valid status values are defined in the ApprovalStatus enum:
        /// - pending: Awaiting approval
        /// - approved: Successfully approved
        /// - rejected: Rejected by approver
        /// - delegated: Delegated to another user (note: status remains 'pending')
        /// - escalated: Escalated to higher authority
        /// - cancelled: Cancelled before completion
        /// </remarks>
        public EntityRecordList GetRequestsByStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                throw new ArgumentException("Status cannot be null or empty.", nameof(status));
            }

            try
            {
                var eqlCommand = $@"SELECT * FROM {ENTITY_REQUEST} 
                                    WHERE {FIELD_STATUS} = @status 
                                    ORDER BY {FIELD_CREATED_ON} DESC";

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("status", status.ToLowerInvariant())
                };

                var result = new EqlCommand(eqlCommand, eqlParams).Execute();
                return result ?? new EntityRecordList();
            }
            catch (Exception ex)
            {
                throw new ValidationException($"Error retrieving requests by status: {ex.Message}");
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Validates that the specified user is authorized to approve at the current step of the request.
        /// </summary>
        /// <param name="request">The approval request EntityRecord to validate against.</param>
        /// <param name="userId">The user ID to check authorization for.</param>
        /// <exception cref="UnauthorizedAccessException">
        /// Thrown when the user is not authorized to approve at the current step.
        /// </exception>
        /// <remarks>
        /// Authorization is determined by:
        /// 1. Checking if the request was delegated to this user
        /// 2. Getting the approvers list for the current step via ApprovalRouteService
        /// 3. Verifying the user is in the approvers list
        /// </remarks>
        private void ValidateApproverAuthorization(EntityRecord request, Guid userId)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request), "Request cannot be null.");
            }

            if (userId == Guid.Empty)
            {
                throw new UnauthorizedAccessException("User ID cannot be empty for authorization check.");
            }

            // Check if request was delegated to this user
            var delegatedTo = request[FIELD_DELEGATED_TO] as Guid?;
            if (delegatedTo.HasValue && delegatedTo.Value == userId)
            {
                // User is the delegate - authorized
                return;
            }

            // Get current step ID from request
            var currentStepId = request[FIELD_CURRENT_STEP_ID] as Guid?;
            if (!currentStepId.HasValue)
            {
                throw new UnauthorizedAccessException("Request does not have a current step assigned.");
            }

            // Get authorized approvers for the step
            var approvers = _routeService.GetApproversForStep(currentStepId.Value);

            if (approvers == null || !approvers.Any())
            {
                throw new UnauthorizedAccessException("No approvers configured for the current step.");
            }

            // Check if user is in the approvers list
            if (!approvers.Contains(userId))
            {
                throw new UnauthorizedAccessException(
                    $"User {userId} is not authorized to approve at the current step.");
            }
        }

        /// <summary>
        /// Retrieves a workflow record by its unique identifier.
        /// </summary>
        /// <param name="workflowId">The workflow ID to retrieve.</param>
        /// <returns>The EntityRecord for the workflow, or null if not found.</returns>
        private EntityRecord GetWorkflow(Guid workflowId)
        {
            var eqlCommand = $"SELECT * FROM {ENTITY_WORKFLOW} WHERE {FIELD_ID} = @workflowId";
            var eqlParams = new List<EqlParameter>
            {
                new EqlParameter("workflowId", workflowId)
            };

            var result = new EqlCommand(eqlCommand, eqlParams).Execute();
            return result?.FirstOrDefault();
        }

        /// <summary>
        /// Gets the first step for a workflow (step with lowest step_order).
        /// </summary>
        /// <param name="workflowId">The workflow ID to find the first step for.</param>
        /// <returns>The ID of the first step, or null if no steps exist.</returns>
        private Guid? GetFirstStepForWorkflow(Guid workflowId)
        {
            var eqlCommand = $@"SELECT * FROM {ENTITY_STEP} 
                                WHERE {FIELD_WORKFLOW_ID} = @workflowId 
                                ORDER BY step_order ASC 
                                PAGE 1 PAGESIZE 1";

            var eqlParams = new List<EqlParameter>
            {
                new EqlParameter("workflowId", workflowId)
            };

            var result = new EqlCommand(eqlCommand, eqlParams).Execute();

            if (result != null && result.Any())
            {
                return (Guid)result[0][FIELD_ID];
            }

            return null;
        }

        /// <summary>
        /// Retrieves a step record by its unique identifier.
        /// </summary>
        /// <param name="stepId">The step ID to retrieve.</param>
        /// <returns>The EntityRecord for the step, or null if not found.</returns>
        private EntityRecord GetStep(Guid stepId)
        {
            var eqlCommand = $"SELECT * FROM {ENTITY_STEP} WHERE {FIELD_ID} = @stepId";
            var eqlParams = new List<EqlParameter>
            {
                new EqlParameter("stepId", stepId)
            };

            var result = new EqlCommand(eqlCommand, eqlParams).Execute();
            return result?.FirstOrDefault();
        }

        /// <summary>
        /// Calculates the due date for a request based on the step's SLA hours.
        /// </summary>
        /// <param name="stepId">The step ID to calculate due date for.</param>
        /// <returns>
        /// The calculated due date (current UTC time + SLA hours), 
        /// or null if step not found or SLA not defined.
        /// </returns>
        private DateTime? CalculateDueDate(Guid stepId)
        {
            var step = GetStep(stepId);
            if (step == null)
            {
                return null;
            }

            var slaHours = step[FIELD_SLA_HOURS] as int?;
            if (!slaHours.HasValue || slaHours.Value <= 0)
            {
                // No SLA defined - return null (no due date)
                return null;
            }

            return DateTime.UtcNow.AddHours(slaHours.Value);
        }

        #endregion
    }
}
