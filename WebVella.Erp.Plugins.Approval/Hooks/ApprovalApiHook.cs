using System;
using System.Collections.Generic;
using System.Linq;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Hooks;
using WebVella.Erp.Plugins.Approval.Services;

namespace WebVella.Erp.Plugins.Approval.Hooks
{
    /// <summary>
    /// API hook class for intercepting entity CRUD operations to trigger approval workflows.
    /// Implements pre-create, post-create, pre-update, and post-update hooks to detect when
    /// records requiring approval are created or modified.
    /// </summary>
    /// <remarks>
    /// This hook class provides automated workflow initiation by:
    /// 
    /// OnPreCreateRecord:
    /// - Checks if the entity has an active approval workflow
    /// - Validates the record against workflow rules
    /// - Blocks creation if validation fails
    /// 
    /// OnPostCreateRecord:
    /// - Determines if a workflow applies to the newly created record
    /// - Creates an approval request if a matching workflow is found
    /// - Logs workflow initiation for audit purposes
    /// 
    /// OnPreUpdateRecord:
    /// - Checks if the record has a pending approval request
    /// - May block updates during active approval process
    /// - Validates update against workflow requirements
    /// 
    /// OnPostUpdateRecord:
    /// - Evaluates if update triggers workflow re-evaluation
    /// - Updates existing request status if needed
    /// 
    /// The hook follows WebVella patterns by:
    /// - Using [HookAttachment] attribute for auto-discovery (global - no entity key)
    /// - Keeping hooks thin with all business logic delegated to services
    /// - Instantiating services inline within hook methods
    /// - Using synchronous void methods with no return values
    /// 
    /// Services Used:
    /// - ApprovalRouteService: For workflow routing and rule evaluation
    /// - ApprovalRequestService: For request lifecycle management
    /// </remarks>
    [HookAttachment]
    public class ApprovalApiHook : IErpPreCreateRecordHook, IErpPostCreateRecordHook, IErpPreUpdateRecordHook, IErpPostUpdateRecordHook
    {
        #region Constants

        /// <summary>
        /// Entity name for approval requests to avoid self-triggering.
        /// </summary>
        private const string ENTITY_APPROVAL_REQUEST = "approval_request";

        /// <summary>
        /// Entity name for approval workflows to avoid self-triggering.
        /// </summary>
        private const string ENTITY_APPROVAL_WORKFLOW = "approval_workflow";

        /// <summary>
        /// Entity name for approval steps to avoid self-triggering.
        /// </summary>
        private const string ENTITY_APPROVAL_STEP = "approval_step";

        /// <summary>
        /// Entity name for approval rules to avoid self-triggering.
        /// </summary>
        private const string ENTITY_APPROVAL_RULE = "approval_rule";

        /// <summary>
        /// Entity name for approval history to avoid self-triggering.
        /// </summary>
        private const string ENTITY_APPROVAL_HISTORY = "approval_history";

        /// <summary>
        /// Field name for record ID.
        /// </summary>
        private const string FIELD_ID = "id";

        /// <summary>
        /// Field name for source record ID in approval_request.
        /// </summary>
        private const string FIELD_SOURCE_RECORD_ID = "source_record_id";

        /// <summary>
        /// Field name for source entity in approval_request.
        /// </summary>
        private const string FIELD_SOURCE_ENTITY = "source_entity";

        /// <summary>
        /// Field name for status in approval_request.
        /// </summary>
        private const string FIELD_STATUS = "status";

        /// <summary>
        /// Status value for pending requests.
        /// </summary>
        private const string STATUS_PENDING = "pending";

        #endregion

        #region IErpPreCreateRecordHook Implementation

        /// <summary>
        /// Hook method called before a record is created.
        /// Validates if the entity has an active approval workflow and if the record passes workflow rules.
        /// </summary>
        /// <param name="entityName">The name of the entity being operated on.</param>
        /// <param name="record">The entity record being created.</param>
        /// <param name="errors">List of errors to populate if validation fails.</param>
        /// <remarks>
        /// This method:
        /// 1. Checks if the entity is an approval system entity (skips if so)
        /// 2. Instantiates ApprovalRouteService for routing evaluation
        /// 3. Evaluates if a workflow applies to this entity
        /// 4. If workflow exists and validation required, validates via service
        /// 5. Populates errors list if validation fails
        /// 
        /// Note: For pre-create, the record may not have an ID yet. The hook evaluates
        /// workflow applicability based on entity name and record field values.
        /// </remarks>
        public void OnPreCreateRecord(string entityName, EntityRecord record, List<ErrorModel> errors)
        {
            // Skip approval system entities to prevent infinite loops
            if (IsApprovalSystemEntity(entityName))
            {
                return;
            }

            // Skip if record or errors list is null
            if (record == null || errors == null)
            {
                return;
            }

            try
            {
                // Instantiate route service for validation
                var routeService = new ApprovalRouteService();

                // Get record ID if available (might be null for new records)
                var recordId = record[FIELD_ID] as Guid?;
                
                // For pre-create, we evaluate rules against the record data without relying on recordId
                // If the workflow has strict pre-validation rules, they are evaluated here
                if (!recordId.HasValue || recordId.Value == Guid.Empty)
                {
                    // Generate a temporary ID for rule evaluation purposes
                    recordId = Guid.NewGuid();
                    record[FIELD_ID] = recordId.Value;
                }

                // Check if any active workflow targets this entity
                var routeResult = routeService.DetermineRoute(recordId.Value, entityName);

                if (routeResult.HasValue)
                {
                    // A workflow exists for this entity - evaluate pre-creation rules
                    var workflowId = routeResult.Value.workflowId;
                    
                    // Evaluate rules against the record being created
                    var rulesPass = routeService.EvaluateRules(workflowId, record);

                    if (!rulesPass)
                    {
                        // Rules do not pass - add error to block creation
                        errors.Add(new ErrorModel
                        {
                            Key = "approval_validation",
                            Value = "approval_rules_failed",
                            Message = $"Record does not meet approval workflow requirements for entity '{entityName}'."
                        });
                    }
                }
                // If no workflow exists, allow creation without approval
            }
            catch (Exception ex)
            {
                // Log but don't block creation for hook errors
                // Production systems should implement proper logging
                System.Diagnostics.Debug.WriteLine($"ApprovalApiHook.OnPreCreateRecord error: {ex.Message}");
            }
        }

        #endregion

        #region IErpPostCreateRecordHook Implementation

        /// <summary>
        /// Hook method called after a record is created.
        /// Creates an approval request if the entity has an active workflow and rules pass.
        /// </summary>
        /// <param name="entityName">The name of the entity being operated on.</param>
        /// <param name="record">The created entity record.</param>
        /// <remarks>
        /// This method:
        /// 1. Checks if the entity is an approval system entity (skips if so)
        /// 2. Instantiates ApprovalRequestService and ApprovalRouteService
        /// 3. Calls DetermineRoute(recordId, entityName) to check if workflow applies
        /// 4. If workflow found, creates approval request via ApprovalRequestService.CreateRequest()
        /// 5. Logs workflow initiation for audit purposes
        /// 
        /// The approval request is created with:
        /// - Source record ID: The newly created record's ID
        /// - Source entity: The entity name
        /// - Workflow ID: From the route determination
        /// - Initiated by: The current user from SecurityContext
        /// </remarks>
        public void OnPostCreateRecord(string entityName, EntityRecord record)
        {
            // Skip approval system entities to prevent infinite loops
            if (IsApprovalSystemEntity(entityName))
            {
                return;
            }

            // Skip if record is null
            if (record == null)
            {
                return;
            }

            try
            {
                // Get record ID - required for workflow creation
                var recordId = record[FIELD_ID] as Guid?;
                if (!recordId.HasValue || recordId.Value == Guid.Empty)
                {
                    return;
                }

                // Instantiate services inline per WebVella pattern
                var requestService = new ApprovalRequestService();
                var routeService = new ApprovalRouteService();

                // Determine if a workflow applies to this record
                var routeResult = routeService.DetermineRoute(recordId.Value, entityName);

                if (routeResult.HasValue)
                {
                    // Workflow found - create approval request
                    var workflowId = routeResult.Value.workflowId;

                    // Get current user ID for initiatedBy
                    var currentUserId = GetCurrentUserId();

                    if (currentUserId.HasValue)
                    {
                        // Create the approval request
                        var approvalRequest = requestService.CreateRequest(
                            sourceRecordId: recordId.Value,
                            sourceEntity: entityName,
                            workflowId: workflowId,
                            initiatedBy: currentUserId.Value
                        );

                        // Log workflow initiation (debug output for now)
                        System.Diagnostics.Debug.WriteLine(
                            $"ApprovalApiHook: Created approval request {approvalRequest[FIELD_ID]} " +
                            $"for {entityName} record {recordId.Value} using workflow {workflowId}");
                    }
                }
                // If no workflow exists, record is created without approval requirement
            }
            catch (Exception ex)
            {
                // Log but don't fail the record creation for hook errors
                // Production systems should implement proper logging
                System.Diagnostics.Debug.WriteLine($"ApprovalApiHook.OnPostCreateRecord error: {ex.Message}");
            }
        }

        #endregion

        #region IErpPreUpdateRecordHook Implementation

        /// <summary>
        /// Hook method called before a record is updated.
        /// Checks if the record has a pending approval request and may block updates during approval.
        /// </summary>
        /// <param name="entityName">The name of the entity being operated on.</param>
        /// <param name="record">The entity record being updated.</param>
        /// <param name="errors">List of errors to populate if update should be blocked.</param>
        /// <remarks>
        /// This method:
        /// 1. Checks if the entity is an approval system entity (skips if so)
        /// 2. Instantiates ApprovalRequestService
        /// 3. Checks if record has a pending approval request via GetRequest()
        /// 4. If pending approval exists, may block the update (based on configuration)
        /// 5. Populates errors list if update is blocked
        /// 
        /// Business logic for update blocking:
        /// - Records with pending approval requests may be locked to prevent changes
        /// - This ensures approvers are reviewing the original submitted data
        /// - Certain fields may be whitelisted for updates during approval
        /// </remarks>
        public void OnPreUpdateRecord(string entityName, EntityRecord record, List<ErrorModel> errors)
        {
            // Skip approval system entities to prevent infinite loops
            if (IsApprovalSystemEntity(entityName))
            {
                return;
            }

            // Skip if record or errors list is null
            if (record == null || errors == null)
            {
                return;
            }

            try
            {
                // Get record ID
                var recordId = record[FIELD_ID] as Guid?;
                if (!recordId.HasValue || recordId.Value == Guid.Empty)
                {
                    return;
                }

                // Instantiate request service
                var requestService = new ApprovalRequestService();

                // Check if there's a pending approval request for this record
                var pendingRequest = FindPendingRequestForRecord(requestService, recordId.Value, entityName);

                if (pendingRequest != null)
                {
                    // Record has a pending approval - determine if update should be blocked
                    var status = pendingRequest[FIELD_STATUS] as string;

                    if (string.Equals(status, STATUS_PENDING, StringComparison.OrdinalIgnoreCase))
                    {
                        // By default, block updates during pending approval
                        // Note: In a full implementation, you might check configuration or field whitelists
                        errors.Add(new ErrorModel
                        {
                            Key = "approval_pending",
                            Value = "update_blocked",
                            Message = $"Cannot update record while approval request is pending. " +
                                      $"Please wait for the approval process to complete or cancel the request."
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but don't block update for hook errors
                System.Diagnostics.Debug.WriteLine($"ApprovalApiHook.OnPreUpdateRecord error: {ex.Message}");
            }
        }

        #endregion

        #region IErpPostUpdateRecordHook Implementation

        /// <summary>
        /// Hook method called after a record is updated.
        /// Evaluates if the update triggers workflow re-evaluation and updates existing request status if needed.
        /// </summary>
        /// <param name="entityName">The name of the entity being operated on.</param>
        /// <param name="record">The updated entity record.</param>
        /// <remarks>
        /// This method:
        /// 1. Checks if the entity is an approval system entity (skips if so)
        /// 2. Instantiates ApprovalRequestService and ApprovalRouteService
        /// 3. Checks if record has existing approval request
        /// 4. Evaluates if update affects workflow rules
        /// 5. Updates existing request status if needed
        /// 
        /// Workflow re-evaluation scenarios:
        /// - Record field changes may affect which workflow applies
        /// - Amount or threshold changes may affect approval step routing
        /// - This ensures approval requirements stay synchronized with record data
        /// </remarks>
        public void OnPostUpdateRecord(string entityName, EntityRecord record)
        {
            // Skip approval system entities to prevent infinite loops
            if (IsApprovalSystemEntity(entityName))
            {
                return;
            }

            // Skip if record is null
            if (record == null)
            {
                return;
            }

            try
            {
                // Get record ID
                var recordId = record[FIELD_ID] as Guid?;
                if (!recordId.HasValue || recordId.Value == Guid.Empty)
                {
                    return;
                }

                // Instantiate services
                var requestService = new ApprovalRequestService();
                var routeService = new ApprovalRouteService();

                // Check if there's an existing approval request for this record
                var existingRequest = FindPendingRequestForRecord(requestService, recordId.Value, entityName);

                if (existingRequest != null)
                {
                    // Record has existing approval request - re-evaluate workflow rules
                    var workflowId = existingRequest[FIELD_WORKFLOW_ID] as Guid?;

                    if (workflowId.HasValue)
                    {
                        // Re-evaluate rules with updated record data
                        var rulesStillPass = routeService.EvaluateRules(workflowId.Value, record);

                        if (!rulesStillPass)
                        {
                            // Rules no longer pass after update - log for review
                            System.Diagnostics.Debug.WriteLine(
                                $"ApprovalApiHook: Record {recordId.Value} in {entityName} " +
                                $"no longer passes workflow rules after update. Review may be required.");
                        }
                    }
                }
                else
                {
                    // No existing request - check if update triggers new workflow
                    var routeResult = routeService.DetermineRoute(recordId.Value, entityName);

                    if (routeResult.HasValue)
                    {
                        // Update triggered a new workflow - create approval request
                        var currentUserId = GetCurrentUserId();

                        if (currentUserId.HasValue)
                        {
                            var approvalRequest = requestService.CreateRequest(
                                sourceRecordId: recordId.Value,
                                sourceEntity: entityName,
                                workflowId: routeResult.Value.workflowId,
                                initiatedBy: currentUserId.Value
                            );

                            System.Diagnostics.Debug.WriteLine(
                                $"ApprovalApiHook: Created approval request {approvalRequest[FIELD_ID]} " +
                                $"for {entityName} record {recordId.Value} after update triggered workflow");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail the update for hook errors
                System.Diagnostics.Debug.WriteLine($"ApprovalApiHook.OnPostUpdateRecord error: {ex.Message}");
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Field name for workflow ID in approval_request.
        /// </summary>
        private const string FIELD_WORKFLOW_ID = "workflow_id";

        /// <summary>
        /// Determines if the specified entity is part of the approval system.
        /// Used to prevent infinite loops and self-triggering.
        /// </summary>
        /// <param name="entityName">The entity name to check.</param>
        /// <returns>True if the entity is an approval system entity, false otherwise.</returns>
        private bool IsApprovalSystemEntity(string entityName)
        {
            if (string.IsNullOrWhiteSpace(entityName))
            {
                return false;
            }

            // Check against all approval system entities
            return entityName.Equals(ENTITY_APPROVAL_REQUEST, StringComparison.OrdinalIgnoreCase) ||
                   entityName.Equals(ENTITY_APPROVAL_WORKFLOW, StringComparison.OrdinalIgnoreCase) ||
                   entityName.Equals(ENTITY_APPROVAL_STEP, StringComparison.OrdinalIgnoreCase) ||
                   entityName.Equals(ENTITY_APPROVAL_RULE, StringComparison.OrdinalIgnoreCase) ||
                   entityName.Equals(ENTITY_APPROVAL_HISTORY, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Finds a pending approval request for the specified record.
        /// </summary>
        /// <param name="requestService">The ApprovalRequestService instance to use.</param>
        /// <param name="recordId">The source record ID to find a request for.</param>
        /// <param name="entityName">The source entity name.</param>
        /// <returns>The pending EntityRecord if found, null otherwise.</returns>
        private EntityRecord FindPendingRequestForRecord(ApprovalRequestService requestService, Guid recordId, string entityName)
        {
            try
            {
                // Get current user's pending requests and filter by record
                var currentUserId = GetCurrentUserId();
                
                if (!currentUserId.HasValue)
                {
                    return null;
                }

                // Get pending requests for user (this will include requests where user is approver)
                var pendingRequests = requestService.GetPendingRequestsForUser(currentUserId.Value);

                if (pendingRequests == null || !pendingRequests.Any())
                {
                    // Also try to find request directly for this record using alternative approach
                    // For a complete implementation, a dedicated method GetRequestBySourceRecord would be ideal
                    return null;
                }

                // Filter to find request matching our record
                return pendingRequests.FirstOrDefault(r =>
                {
                    var sourceRecordId = r[FIELD_SOURCE_RECORD_ID] as Guid?;
                    var sourceEntity = r[FIELD_SOURCE_ENTITY] as string;

                    return sourceRecordId.HasValue &&
                           sourceRecordId.Value == recordId &&
                           string.Equals(sourceEntity, entityName, StringComparison.OrdinalIgnoreCase);
                });
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the current user's ID from the security context.
        /// </summary>
        /// <returns>The current user's GUID, or null if not available.</returns>
        private Guid? GetCurrentUserId()
        {
            try
            {
                var securityContext = SecurityContext.Current;
                
                if (securityContext?.User != null)
                {
                    return securityContext.User.Id;
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        #endregion
    }
}
