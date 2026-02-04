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
    /// Admin-facing service for CRUD operations on approval_step entity configuration.
    /// Provides Create(), GetById(), GetByWorkflowId(), Update(), and Delete() methods
    /// for managing workflow steps. Includes step order management with ReorderSteps()
    /// for adjusting step sequence and automatic step_order assignment on create.
    /// </summary>
    /// <remarks>
    /// This service follows the WebVella service pattern using RecordManager for data persistence.
    /// All methods use EQL (Entity Query Language) for parameterized queries to ensure SQL injection protection.
    /// Validates approver_type against supported values (role, user, department_head).
    /// </remarks>
    public class StepConfigService
    {
        #region Constants

        /// <summary>
        /// Entity name constant for the approval_step entity.
        /// </summary>
        public const string ENTITY_NAME = "approval_step";

        /// <summary>
        /// Entity name constant for the approval_request entity.
        /// Used for checking active references before deletion.
        /// </summary>
        public const string REQUEST_ENTITY_NAME = "approval_request";

        /// <summary>
        /// Array of valid approver type values.
        /// Approvers can be assigned by role, specific user, or department head.
        /// </summary>
        public static readonly string[] VALID_APPROVER_TYPES = { "role", "user", "department_head" };

        /// <summary>
        /// Maximum allowed length for step name.
        /// </summary>
        public const int MAX_NAME_LENGTH = 256;

        #endregion

        #region Properties

        private RecordManager _recordManager;
        private WorkflowConfigService _workflowConfigService;

        /// <summary>
        /// Gets the RecordManager instance for database record operations.
        /// Lazy-initialized to avoid constructor exceptions when ERP system is not initialized.
        /// </summary>
        protected RecordManager RecMan
        {
            get
            {
                if (_recordManager == null)
                {
                    _recordManager = new RecordManager();
                }
                return _recordManager;
            }
        }

        /// <summary>
        /// Gets the WorkflowConfigService instance for workflow validation.
        /// Lazy-initialized to avoid constructor exceptions when ERP system is not initialized.
        /// </summary>
        protected WorkflowConfigService WorkflowService
        {
            get
            {
                if (_workflowConfigService == null)
                {
                    _workflowConfigService = new WorkflowConfigService();
                }
                return _workflowConfigService;
            }
        }

        #endregion

        #region Public Methods - CRUD Operations

        /// <summary>
        /// Creates a new approval step with comprehensive validation.
        /// Automatically assigns step_order as max(existing) + 1 if not provided or if provided as 0.
        /// </summary>
        /// <param name="model">The step model containing the step configuration data.</param>
        /// <returns>The created ApprovalStepModel with generated ID and assigned step_order.</returns>
        /// <exception cref="ValidationException">
        /// Thrown when:
        /// - Model is null
        /// - WorkflowId is empty or references a non-existent workflow
        /// - Name is empty, whitespace, or exceeds 256 characters
        /// - ApproverType is not one of: "role", "user", "department_head"
        /// - ApproverType is "role" or "user" but ApproverId is not set
        /// - Database operation fails
        /// </exception>
        /// <example>
        /// <code>
        /// var service = new StepConfigService();
        /// var model = new ApprovalStepModel
        /// {
        ///     WorkflowId = workflowId,
        ///     Name = "Manager Approval",
        ///     ApproverType = "role",
        ///     ApproverId = managerRoleId,
        ///     TimeoutHours = 48,
        ///     IsFinal = false
        /// };
        /// var created = service.Create(model);
        /// </code>
        /// </example>
        public ApprovalStepModel Create(ApprovalStepModel model)
        {
            // Validate the model structure and field constraints
            ValidateModelForCreate(model);

            // Validate that workflow exists
            ValidateWorkflowExists(model.WorkflowId);

            // Validate approver_type against valid values
            ValidateApproverType(model.ApproverType);

            // Validate ApproverId is set when required by ApproverType
            ValidateApproverId(model.ApproverType, model.ApproverId);

            // Generate new ID
            model.Id = Guid.NewGuid();

            // Auto-assign step_order as max(existing) + 1 if not provided
            if (model.StepOrder <= 0)
            {
                model.StepOrder = GetMaxStepOrder(model.WorkflowId) + 1;
            }

            // Build the entity record for persistence
            var record = new EntityRecord();
            record["id"] = model.Id;
            record["workflow_id"] = model.WorkflowId;
            record["step_order"] = model.StepOrder;
            record["name"] = model.Name;
            record["approver_type"] = model.ApproverType;
            record["approver_id"] = model.ApproverId;
            record["timeout_hours"] = model.TimeoutHours;
            record["is_final"] = model.IsFinal;

            // Execute the create operation
            var response = RecMan.CreateRecord(ENTITY_NAME, record);
            if (!response.Success)
            {
                throw new ValidationException(response.Message);
            }

            return model;
        }

        /// <summary>
        /// Retrieves a specific approval step by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier (GUID) of the step to retrieve.</param>
        /// <returns>
        /// The ApprovalStepModel if found; null if no step exists with the specified ID.
        /// </returns>
        /// <example>
        /// <code>
        /// var service = new StepConfigService();
        /// var step = service.GetById(stepId);
        /// if (step != null)
        /// {
        ///     Console.WriteLine($"Step: {step.Name}, Order: {step.StepOrder}");
        /// }
        /// </code>
        /// </example>
        public ApprovalStepModel GetById(Guid id)
        {
            if (id == Guid.Empty)
            {
                return null;
            }

            try
            {
                var eqlCommand = "SELECT * FROM approval_step WHERE id = @id";
                var eqlParams = new List<EqlParameter>() { new EqlParameter("id", id) };
                var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (eqlResult == null || !eqlResult.Any())
                {
                    return null;
                }

                return MapToModel(eqlResult.First());
            }
            catch (Exception)
            {
                // Entity may not exist yet during plugin initialization
                return null;
            }
        }

        /// <summary>
        /// Retrieves all approval steps for a specific workflow ordered by step_order ascending.
        /// </summary>
        /// <param name="workflowId">The unique identifier of the workflow to get steps for.</param>
        /// <returns>
        /// A list of ApprovalStepModel objects ordered by step_order ascending.
        /// Returns an empty list if no steps exist for the workflow.
        /// </returns>
        /// <example>
        /// <code>
        /// var service = new StepConfigService();
        /// var steps = service.GetByWorkflowId(workflowId);
        /// foreach (var step in steps)
        /// {
        ///     Console.WriteLine($"Step {step.StepOrder}: {step.Name}");
        /// }
        /// </code>
        /// </example>
        public List<ApprovalStepModel> GetByWorkflowId(Guid workflowId)
        {
            var result = new List<ApprovalStepModel>();

            if (workflowId == Guid.Empty)
            {
                return result;
            }

            try
            {
                var eqlCommand = "SELECT * FROM approval_step WHERE workflow_id = @workflowId ORDER BY step_order ASC";
                var eqlParams = new List<EqlParameter>() { new EqlParameter("workflowId", workflowId) };
                var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (eqlResult == null || !eqlResult.Any())
                {
                    return result;
                }

                foreach (var record in eqlResult)
                {
                    result.Add(MapToModel(record));
                }

                return result;
            }
            catch (Exception)
            {
                // Entity may not exist yet during plugin initialization
                return result;
            }
        }

        /// <summary>
        /// Updates an existing approval step with comprehensive validation.
        /// </summary>
        /// <param name="model">The step model containing updated configuration data.</param>
        /// <returns>The updated ApprovalStepModel.</returns>
        /// <exception cref="ValidationException">
        /// Thrown when:
        /// - Model is null
        /// - Id is empty or references a non-existent step
        /// - WorkflowId is empty or references a non-existent workflow
        /// - Name is empty, whitespace, or exceeds 256 characters
        /// - ApproverType is not one of: "role", "user", "department_head"
        /// - ApproverType is "role" or "user" but ApproverId is not set
        /// - Database operation fails
        /// </exception>
        /// <example>
        /// <code>
        /// var service = new StepConfigService();
        /// var step = service.GetById(stepId);
        /// step.Name = "Updated Step Name";
        /// step.TimeoutHours = 72;
        /// var updated = service.Update(step);
        /// </code>
        /// </example>
        public ApprovalStepModel Update(ApprovalStepModel model)
        {
            // Validate the model structure and field constraints
            ValidateModelForUpdate(model);

            // Validate that step exists
            var existingStep = GetById(model.Id);
            if (existingStep == null)
            {
                throw new ValidationException("Step not found. The specified step ID does not exist.");
            }

            // Validate that workflow exists
            ValidateWorkflowExists(model.WorkflowId);

            // Validate approver_type against valid values
            ValidateApproverType(model.ApproverType);

            // Validate ApproverId is set when required by ApproverType
            ValidateApproverId(model.ApproverType, model.ApproverId);

            // Build the patch record for update
            var patchRecord = new EntityRecord();
            patchRecord["id"] = model.Id;
            patchRecord["workflow_id"] = model.WorkflowId;
            patchRecord["step_order"] = model.StepOrder;
            patchRecord["name"] = model.Name;
            patchRecord["approver_type"] = model.ApproverType;
            patchRecord["approver_id"] = model.ApproverId;
            patchRecord["timeout_hours"] = model.TimeoutHours;
            patchRecord["is_final"] = model.IsFinal;

            // Execute the update operation
            var response = RecMan.UpdateRecord(ENTITY_NAME, patchRecord);
            if (!response.Success)
            {
                throw new ValidationException(response.Message);
            }

            return model;
        }

        /// <summary>
        /// Deletes an approval step by its unique identifier.
        /// Validates that no active approval requests reference this step before deletion.
        /// </summary>
        /// <param name="id">The unique identifier (GUID) of the step to delete.</param>
        /// <exception cref="ValidationException">
        /// Thrown when:
        /// - Id is empty
        /// - Step does not exist
        /// - Active approval requests reference this step (status is 'pending' or 'escalated')
        /// - Database operation fails
        /// </exception>
        /// <example>
        /// <code>
        /// var service = new StepConfigService();
        /// service.Delete(stepId);
        /// </code>
        /// </example>
        public void Delete(Guid id)
        {
            if (id == Guid.Empty)
            {
                throw new ValidationException("Step ID is required for deletion.");
            }

            // Validate that step exists
            var existingStep = GetById(id);
            if (existingStep == null)
            {
                throw new ValidationException("Step not found. The specified step ID does not exist.");
            }

            // Check if any active approval requests reference this step
            ValidateNoActiveRequestsReferenceStep(id);

            // Execute the delete operation
            var response = RecMan.DeleteRecord(ENTITY_NAME, id);
            if (!response.Success)
            {
                throw new ValidationException(response.Message);
            }
        }

        /// <summary>
        /// Reorders the steps of a workflow based on the provided ordered list of step IDs.
        /// Updates the step_order field for each step based on its position in the list.
        /// </summary>
        /// <param name="workflowId">The unique identifier of the workflow whose steps are being reordered.</param>
        /// <param name="stepIdsInOrder">
        /// A list of step GUIDs in the desired order. The first ID will have step_order=1,
        /// the second will have step_order=2, and so on.
        /// </param>
        /// <exception cref="ValidationException">
        /// Thrown when:
        /// - WorkflowId is empty or references a non-existent workflow
        /// - stepIdsInOrder is null or empty
        /// - Any step ID in the list does not belong to the specified workflow
        /// - Database operation fails
        /// </exception>
        /// <example>
        /// <code>
        /// var service = new StepConfigService();
        /// // Reorder steps: step2 first, then step1, then step3
        /// var newOrder = new List&lt;Guid&gt; { step2Id, step1Id, step3Id };
        /// service.ReorderSteps(workflowId, newOrder);
        /// </code>
        /// </example>
        public void ReorderSteps(Guid workflowId, List<Guid> stepIdsInOrder)
        {
            if (workflowId == Guid.Empty)
            {
                throw new ValidationException("Workflow ID is required for reordering steps.");
            }

            if (stepIdsInOrder == null || !stepIdsInOrder.Any())
            {
                throw new ValidationException("Step IDs list is required and cannot be empty.");
            }

            // Validate that workflow exists
            ValidateWorkflowExists(workflowId);

            // Get existing steps for the workflow to validate all provided IDs belong to this workflow
            var existingSteps = GetByWorkflowId(workflowId);
            var existingStepIds = existingSteps.Select(s => s.Id).ToList();

            // Validate all provided step IDs belong to this workflow
            foreach (var stepId in stepIdsInOrder)
            {
                if (!existingStepIds.Contains(stepId))
                {
                    throw new ValidationException($"Step ID '{stepId}' does not belong to workflow '{workflowId}'.");
                }
            }

            // Use transaction to ensure atomic operation per STORY-003 AC5
            // Ensures contiguous step_order numbering (1, 2, 3...) on success
            // Rolls back all changes if any update fails
            using (var connection = DbContext.Current.CreateConnection())
            {
                try
                {
                    connection.BeginTransaction();

                    // Update step_order for each step based on list position
                    int newOrder = 1;
                    foreach (var stepId in stepIdsInOrder)
                    {
                        var patchRecord = new EntityRecord();
                        patchRecord["id"] = stepId;
                        patchRecord["step_order"] = newOrder;

                        var response = RecMan.UpdateRecord(ENTITY_NAME, patchRecord);
                        if (!response.Success)
                        {
                            throw new ValidationException($"Failed to update step order for step '{stepId}': {response.Message}");
                        }

                        newOrder++;
                    }

                    connection.CommitTransaction();
                }
                catch (ValidationException)
                {
                    connection.RollbackTransaction();
                    throw;
                }
                catch (Exception ex)
                {
                    connection.RollbackTransaction();
                    throw new ValidationException($"Failed to reorder steps: {ex.Message}");
                }
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Gets the maximum step_order value for steps in a given workflow.
        /// Returns 0 if no steps exist for the workflow.
        /// </summary>
        /// <param name="workflowId">The workflow ID to query.</param>
        /// <returns>The maximum step_order value, or 0 if no steps exist.</returns>
        private int GetMaxStepOrder(Guid workflowId)
        {
            try
            {
                var eqlCommand = "SELECT step_order FROM approval_step WHERE workflow_id = @workflowId ORDER BY step_order DESC";
                var eqlParams = new List<EqlParameter>() { new EqlParameter("workflowId", workflowId) };
                var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (eqlResult == null || !eqlResult.Any())
                {
                    return 0;
                }

                var maxOrderValue = eqlResult.First()["step_order"];
                if (maxOrderValue == null)
                {
                    return 0;
                }

                return Convert.ToInt32(maxOrderValue);
            }
            catch (Exception)
            {
                // Entity may not exist yet during plugin initialization
                return 0;
            }
        }

        /// <summary>
        /// Validates the model structure and field constraints for create operation.
        /// </summary>
        /// <param name="model">The model to validate.</param>
        /// <exception cref="ValidationException">Thrown when validation fails.</exception>
        private void ValidateModelForCreate(ApprovalStepModel model)
        {
            if (model == null)
            {
                throw new ValidationException("Step model cannot be null.");
            }

            if (model.WorkflowId == Guid.Empty)
            {
                throw new ValidationException("Workflow ID is required.");
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                throw new ValidationException("Step name is required.");
            }

            if (model.Name.Length > MAX_NAME_LENGTH)
            {
                throw new ValidationException($"Step name cannot exceed {MAX_NAME_LENGTH} characters.");
            }

            if (string.IsNullOrWhiteSpace(model.ApproverType))
            {
                throw new ValidationException("Approver type is required.");
            }
        }

        /// <summary>
        /// Validates the model structure and field constraints for update operation.
        /// </summary>
        /// <param name="model">The model to validate.</param>
        /// <exception cref="ValidationException">Thrown when validation fails.</exception>
        private void ValidateModelForUpdate(ApprovalStepModel model)
        {
            if (model == null)
            {
                throw new ValidationException("Step model cannot be null.");
            }

            if (model.Id == Guid.Empty)
            {
                throw new ValidationException("Step ID is required for update.");
            }

            if (model.WorkflowId == Guid.Empty)
            {
                throw new ValidationException("Workflow ID is required.");
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                throw new ValidationException("Step name is required.");
            }

            if (model.Name.Length > MAX_NAME_LENGTH)
            {
                throw new ValidationException($"Step name cannot exceed {MAX_NAME_LENGTH} characters.");
            }

            if (string.IsNullOrWhiteSpace(model.ApproverType))
            {
                throw new ValidationException("Approver type is required.");
            }

            if (model.StepOrder <= 0)
            {
                throw new ValidationException("Step order must be greater than 0.");
            }
        }

        /// <summary>
        /// Validates that the specified workflow exists.
        /// </summary>
        /// <param name="workflowId">The workflow ID to validate.</param>
        /// <exception cref="ValidationException">Thrown when workflow does not exist.</exception>
        private void ValidateWorkflowExists(Guid workflowId)
        {
            var workflow = WorkflowService.GetById(workflowId);
            if (workflow == null)
            {
                throw new ValidationException($"Workflow with ID '{workflowId}' does not exist.");
            }
        }

        /// <summary>
        /// Validates that the approver_type is one of the valid values.
        /// </summary>
        /// <param name="approverType">The approver type to validate.</param>
        /// <exception cref="ValidationException">Thrown when approver type is invalid.</exception>
        private void ValidateApproverType(string approverType)
        {
            if (!VALID_APPROVER_TYPES.Contains(approverType.ToLowerInvariant()))
            {
                throw new ValidationException(
                    $"Invalid approver type '{approverType}'. Valid values are: {string.Join(", ", VALID_APPROVER_TYPES)}");
            }
        }

        /// <summary>
        /// Validates that ApproverId is set when required by the ApproverType.
        /// ApproverId is required for "role" and "user" approver types.
        /// ApproverId is optional for "department_head" as it's resolved dynamically.
        /// </summary>
        /// <param name="approverType">The approver type.</param>
        /// <param name="approverId">The approver ID to validate.</param>
        /// <exception cref="ValidationException">Thrown when ApproverId is required but not set.</exception>
        private void ValidateApproverId(string approverType, Guid? approverId)
        {
            var normalizedType = approverType.ToLowerInvariant();

            // For role and user approver types, ApproverId is required
            if ((normalizedType == "role" || normalizedType == "user") && 
                (!approverId.HasValue || approverId.Value == Guid.Empty))
            {
                throw new ValidationException(
                    $"Approver ID is required when approver type is '{approverType}'.");
            }
        }

        /// <summary>
        /// Validates that no active approval requests reference the specified step.
        /// Active requests are those with status 'pending' or 'escalated'.
        /// </summary>
        /// <param name="stepId">The step ID to check for references.</param>
        /// <exception cref="ValidationException">Thrown when active requests reference the step.</exception>
        private void ValidateNoActiveRequestsReferenceStep(Guid stepId)
        {
            try
            {
                var eqlCommand = @"SELECT id FROM approval_request 
                                   WHERE current_step_id = @stepId 
                                   AND (status = @pendingStatus OR status = @escalatedStatus)";
                var eqlParams = new List<EqlParameter>()
                {
                    new EqlParameter("stepId", stepId),
                    new EqlParameter("pendingStatus", "pending"),
                    new EqlParameter("escalatedStatus", "escalated")
                };
                var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (eqlResult != null && eqlResult.Any())
                {
                    throw new ValidationException(
                        "Cannot delete step. There are active approval requests currently at this step. " +
                        "Please complete or cancel all pending requests before deleting this step.");
                }
            }
            catch (ValidationException)
            {
                // Re-throw validation exceptions
                throw;
            }
            catch (Exception)
            {
                // Entity may not exist yet during plugin initialization - allow deletion
            }
        }

        /// <summary>
        /// Maps an EntityRecord from the approval_step entity to an ApprovalStepModel DTO.
        /// </summary>
        /// <param name="record">The entity record to map.</param>
        /// <returns>The mapped ApprovalStepModel.</returns>
        private ApprovalStepModel MapToModel(EntityRecord record)
        {
            var model = new ApprovalStepModel();

            model.Id = (Guid)record["id"];
            model.WorkflowId = (Guid)record["workflow_id"];
            model.StepOrder = record["step_order"] != null ? Convert.ToInt32(record["step_order"]) : 0;
            model.Name = record["name"]?.ToString() ?? string.Empty;
            model.ApproverType = record["approver_type"]?.ToString() ?? string.Empty;
            model.ApproverId = record["approver_id"] as Guid?;
            model.TimeoutHours = record["timeout_hours"] != null ? Convert.ToInt32(record["timeout_hours"]) : (int?)null;
            model.IsFinal = record["is_final"] != null && (bool)record["is_final"];
            model.ThresholdConfig = record.Properties.ContainsKey("threshold_config") && record["threshold_config"] != null
                ? record["threshold_config"].ToString()
                : null;

            return model;
        }

        #endregion
    }
}
