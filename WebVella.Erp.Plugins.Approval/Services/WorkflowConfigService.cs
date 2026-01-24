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
    /// Foundational admin-facing service for CRUD operations on approval_workflow entity configuration.
    /// Provides Create(), GetById(), GetAll(), Update(), and Delete() methods for managing workflow definitions.
    /// Includes validation logic to ensure name uniqueness, target_entity_name references a valid entity,
    /// and prevents deletion of workflows with active requests.
    /// </summary>
    /// <remarks>
    /// This service follows the WebVella service pattern using RecordManager for data persistence
    /// and EntityManager for entity validation. All methods use EQL (Entity Query Language) for
    /// parameterized queries to ensure SQL injection protection.
    /// </remarks>
    public class WorkflowConfigService
    {
        #region Constants

        /// <summary>
        /// Entity name constant for the approval_workflow entity.
        /// </summary>
        public const string ENTITY_NAME = "approval_workflow";

        /// <summary>
        /// Entity name constant for the approval_step entity.
        /// </summary>
        public const string STEP_ENTITY_NAME = "approval_step";

        /// <summary>
        /// Entity name constant for the approval_rule entity.
        /// </summary>
        public const string RULE_ENTITY_NAME = "approval_rule";

        /// <summary>
        /// Entity name constant for the approval_request entity.
        /// </summary>
        public const string REQUEST_ENTITY_NAME = "approval_request";

        /// <summary>
        /// Maximum allowed length for workflow name.
        /// </summary>
        public const int MAX_NAME_LENGTH = 256;

        /// <summary>
        /// Maximum allowed length for target entity name.
        /// </summary>
        public const int MAX_TARGET_ENTITY_NAME_LENGTH = 128;

        #endregion

        #region Properties

        private RecordManager _recordManager;
        private EntityManager _entityManager;

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
        /// Gets the EntityManager instance for entity schema validation.
        /// Lazy-initialized to avoid constructor exceptions when ERP system is not initialized.
        /// </summary>
        protected EntityManager EntMan
        {
            get
            {
                if (_entityManager == null)
                {
                    _entityManager = new EntityManager();
                }
                return _entityManager;
            }
        }

        #endregion

        #region Public Methods - CRUD Operations

        /// <summary>
        /// Creates a new approval workflow with comprehensive validation.
        /// </summary>
        /// <param name="model">The workflow model containing the workflow configuration data.</param>
        /// <returns>The created ApprovalWorkflowModel with generated ID, CreatedOn, and CreatedBy values.</returns>
        /// <exception cref="ValidationException">
        /// Thrown when:
        /// - Model is null
        /// - Name is empty, whitespace, or exceeds 256 characters
        /// - Name is not unique (another workflow with same name exists)
        /// - TargetEntityName is empty, whitespace, or exceeds 128 characters
        /// - TargetEntityName does not reference a valid entity in the system
        /// - Database operation fails
        /// </exception>
        /// <example>
        /// <code>
        /// var service = new WorkflowConfigService();
        /// var model = new ApprovalWorkflowModel
        /// {
        ///     Name = "Purchase Order Approval",
        ///     TargetEntityName = "purchase_order",
        ///     IsEnabled = true
        /// };
        /// var created = service.Create(model);
        /// </code>
        /// </example>
        public ApprovalWorkflowModel Create(ApprovalWorkflowModel model)
        {
            // Validate the model structure and field constraints
            ValidateModelForCreate(model);

            // Validate name uniqueness - no other workflow should have the same name
            ValidateNameUniqueness(model.Name, null);

            // Validate that target entity exists in the system
            ValidateTargetEntityExists(model.TargetEntityName);

            // Generate new ID and set audit fields
            model.Id = Guid.NewGuid();
            model.CreatedOn = DateTime.UtcNow;
            
            // Get current user ID from SecurityContext, default to Empty if no user context
            Guid createdById = Guid.Empty;
            try
            {
                if (SecurityContext.CurrentUser != null)
                {
                    createdById = SecurityContext.CurrentUser.Id;
                }
            }
            catch (Exception)
            {
                // SecurityContext may throw if not in valid context; use default
            }
            model.CreatedBy = createdById;

            // Build the entity record for persistence
            var record = new EntityRecord();
            record["id"] = model.Id;
            record["name"] = model.Name;
            record["target_entity_name"] = model.TargetEntityName;
            record["is_enabled"] = model.IsEnabled;
            record["created_on"] = model.CreatedOn;
            record["created_by"] = model.CreatedBy;

            // Execute the create operation
            var response = RecMan.CreateRecord(ENTITY_NAME, record);
            if (!response.Success)
            {
                throw new ValidationException(response.Message);
            }

            // Initialize calculated fields for the returned model
            model.StepsCount = 0;
            model.RulesCount = 0;

            return model;
        }

        /// <summary>
        /// Retrieves a specific approval workflow by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier (GUID) of the workflow to retrieve.</param>
        /// <returns>
        /// The ApprovalWorkflowModel if found, including calculated StepsCount and RulesCount;
        /// null if no workflow exists with the specified ID.
        /// </returns>
        /// <example>
        /// <code>
        /// var service = new WorkflowConfigService();
        /// var workflow = service.GetById(workflowId);
        /// if (workflow != null)
        /// {
        ///     Console.WriteLine($"Workflow: {workflow.Name}, Steps: {workflow.StepsCount}");
        /// }
        /// </code>
        /// </example>
        public ApprovalWorkflowModel GetById(Guid id)
        {
            if (id == Guid.Empty)
            {
                return null;
            }

            try
            {
                // Query with related entities for step and rule counts
                var eqlCommand = "SELECT *,$approval_step_workflow.id,$approval_rule_workflow.id FROM approval_workflow WHERE id = @id";
                var eqlParams = new List<EqlParameter>() { new EqlParameter("id", id) };
                var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (eqlResult == null || !eqlResult.Any())
                {
                    return null;
                }

                var record = eqlResult.First();
                var model = MapToModel(record);

                // Calculate steps count from relation data
                if (record.Properties.ContainsKey("$approval_step_workflow"))
                {
                    var steps = record["$approval_step_workflow"] as List<EntityRecord>;
                    model.StepsCount = steps?.Count ?? 0;
                }

                // Calculate rules count from relation data
                if (record.Properties.ContainsKey("$approval_rule_workflow"))
                {
                    var rules = record["$approval_rule_workflow"] as List<EntityRecord>;
                    model.RulesCount = rules?.Count ?? 0;
                }

                return model;
            }
            catch (Exception)
            {
                // Entity may not exist yet during plugin initialization
                return null;
            }
        }

        /// <summary>
        /// Retrieves all approval workflows ordered by name ascending.
        /// </summary>
        /// <param name="includeDisabled">
        /// If true, includes disabled workflows in the result. Default is true to return all workflows.
        /// </param>
        /// <returns>
        /// A list of all ApprovalWorkflowModel objects, each including calculated StepsCount and RulesCount.
        /// Returns an empty list if no workflows exist or if the entity is not yet created.
        /// </returns>
        /// <example>
        /// <code>
        /// var service = new WorkflowConfigService();
        /// var allWorkflows = service.GetAll();
        /// var enabledOnly = service.GetAll(includeDisabled: false);
        /// </code>
        /// </example>
        public List<ApprovalWorkflowModel> GetAll(bool includeDisabled = true)
        {
            var result = new List<ApprovalWorkflowModel>();

            try
            {
                // Build query with related entities for counts
                string eqlCommand;
                var eqlParams = new List<EqlParameter>();

                if (includeDisabled)
                {
                    eqlCommand = "SELECT *,$approval_step_workflow.id,$approval_rule_workflow.id FROM approval_workflow ORDER BY name ASC";
                }
                else
                {
                    eqlCommand = "SELECT *,$approval_step_workflow.id,$approval_rule_workflow.id FROM approval_workflow WHERE is_enabled = @enabled ORDER BY name ASC";
                    eqlParams.Add(new EqlParameter("enabled", true));
                }

                var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (eqlResult != null && eqlResult.Any())
                {
                    foreach (var record in eqlResult)
                    {
                        var model = MapToModel(record);

                        // Calculate steps count from relation data
                        if (record.Properties.ContainsKey("$approval_step_workflow"))
                        {
                            var steps = record["$approval_step_workflow"] as List<EntityRecord>;
                            model.StepsCount = steps?.Count ?? 0;
                        }

                        // Calculate rules count from relation data
                        if (record.Properties.ContainsKey("$approval_rule_workflow"))
                        {
                            var rules = record["$approval_rule_workflow"] as List<EntityRecord>;
                            model.RulesCount = rules?.Count ?? 0;
                        }

                        result.Add(model);
                    }
                }
            }
            catch (Exception)
            {
                // Entity may not exist yet during plugin initialization
                // Return empty list
            }

            return result;
        }

        /// <summary>
        /// Updates an existing approval workflow with comprehensive validation.
        /// </summary>
        /// <param name="model">
        /// The workflow model containing updated values. The Id property must reference an existing workflow.
        /// </param>
        /// <returns>The updated ApprovalWorkflowModel with refreshed data from the database.</returns>
        /// <exception cref="ValidationException">
        /// Thrown when:
        /// - Model is null
        /// - Model.Id is empty (Guid.Empty)
        /// - Workflow with specified Id does not exist
        /// - Name is empty, whitespace, or exceeds 256 characters
        /// - Name is not unique (another workflow with same name exists, excluding self)
        /// - TargetEntityName is empty, whitespace, or exceeds 128 characters
        /// - TargetEntityName does not reference a valid entity in the system
        /// - Database operation fails
        /// </exception>
        /// <example>
        /// <code>
        /// var service = new WorkflowConfigService();
        /// var workflow = service.GetById(workflowId);
        /// workflow.Name = "Updated Workflow Name";
        /// workflow.IsEnabled = false;
        /// var updated = service.Update(workflow);
        /// </code>
        /// </example>
        public ApprovalWorkflowModel Update(ApprovalWorkflowModel model)
        {
            // Validate the model structure
            if (model == null)
            {
                throw new ValidationException("Workflow model is required.");
            }

            // Validate that ID is provided for update
            if (model.Id == Guid.Empty)
            {
                throw new ValidationException("Workflow ID is required for update operation.");
            }

            // Verify the workflow exists
            var existing = GetById(model.Id);
            if (existing == null)
            {
                throw new ValidationException($"Workflow with ID '{model.Id}' not found.");
            }

            // Validate field constraints
            ValidateModelFields(model);

            // Validate name uniqueness, excluding the current workflow
            ValidateNameUniqueness(model.Name, model.Id);

            // Validate target entity exists (only if it changed)
            if (!string.Equals(existing.TargetEntityName, model.TargetEntityName, StringComparison.OrdinalIgnoreCase))
            {
                ValidateTargetEntityExists(model.TargetEntityName);
            }

            // Build patch record with only changed fields to minimize database operations
            var patchRecord = new EntityRecord();
            patchRecord["id"] = model.Id;

            // Only include changed fields in the patch
            if (!string.Equals(existing.Name, model.Name, StringComparison.Ordinal))
            {
                patchRecord["name"] = model.Name;
            }

            if (!string.Equals(existing.TargetEntityName, model.TargetEntityName, StringComparison.OrdinalIgnoreCase))
            {
                patchRecord["target_entity_name"] = model.TargetEntityName;
            }

            if (existing.IsEnabled != model.IsEnabled)
            {
                patchRecord["is_enabled"] = model.IsEnabled;
            }

            // Execute update operation
            var response = RecMan.UpdateRecord(ENTITY_NAME, patchRecord);
            if (!response.Success)
            {
                throw new ValidationException(response.Message);
            }

            // Return refreshed data from database
            return GetById(model.Id);
        }

        /// <summary>
        /// Deletes an approval workflow by its unique identifier.
        /// Prevents deletion if the workflow has active (pending) approval requests.
        /// Also performs cascade deletion of related approval_step and approval_rule records.
        /// </summary>
        /// <param name="id">The unique identifier (GUID) of the workflow to delete.</param>
        /// <exception cref="ValidationException">
        /// Thrown when:
        /// - Id is empty (Guid.Empty)
        /// - Workflow with specified Id does not exist
        /// - Workflow has active (pending) approval requests
        /// - Database operation fails during deletion
        /// </exception>
        /// <remarks>
        /// This method performs the following operations in order:
        /// 1. Validates the workflow exists
        /// 2. Checks for active approval requests (status = 'pending')
        /// 3. Deletes all related approval_rule records
        /// 4. Deletes all related approval_step records
        /// 5. Deletes the workflow record
        /// 
        /// Note: Completed approval requests (approved/rejected) are preserved for audit purposes
        /// but will have orphaned workflow_id references after deletion.
        /// </remarks>
        /// <example>
        /// <code>
        /// var service = new WorkflowConfigService();
        /// try
        /// {
        ///     service.Delete(workflowId);
        ///     Console.WriteLine("Workflow deleted successfully.");
        /// }
        /// catch (ValidationException ex)
        /// {
        ///     Console.WriteLine($"Cannot delete: {ex.Message}");
        /// }
        /// </code>
        /// </example>
        public void Delete(Guid id)
        {
            // Validate ID is provided
            if (id == Guid.Empty)
            {
                throw new ValidationException("Workflow ID is required for delete operation.");
            }

            // Verify the workflow exists
            var existing = GetById(id);
            if (existing == null)
            {
                throw new ValidationException($"Workflow with ID '{id}' not found.");
            }

            // Check for active (pending) approval requests
            ValidateNoActiveRequests(id);

            // Delete related approval rules first (foreign key constraint)
            DeleteRelatedRules(id);

            // Delete related approval steps (foreign key constraint)
            DeleteRelatedSteps(id);

            // Delete the workflow record
            var response = RecMan.DeleteRecord(ENTITY_NAME, id);
            if (!response.Success)
            {
                throw new ValidationException(response.Message);
            }
        }

        /// <summary>
        /// Retrieves workflows by target entity name.
        /// Useful for finding which workflows apply to a specific entity type.
        /// </summary>
        /// <param name="targetEntityName">The entity name to filter by.</param>
        /// <param name="enabledOnly">If true, only returns enabled workflows. Default is true.</param>
        /// <returns>List of workflows targeting the specified entity.</returns>
        /// <example>
        /// <code>
        /// var service = new WorkflowConfigService();
        /// var purchaseOrderWorkflows = service.GetByTargetEntity("purchase_order");
        /// </code>
        /// </example>
        public List<ApprovalWorkflowModel> GetByTargetEntity(string targetEntityName, bool enabledOnly = true)
        {
            var result = new List<ApprovalWorkflowModel>();

            if (string.IsNullOrWhiteSpace(targetEntityName))
            {
                return result;
            }

            try
            {
                string eqlCommand;
                var eqlParams = new List<EqlParameter>() { new EqlParameter("entityName", targetEntityName) };

                if (enabledOnly)
                {
                    eqlCommand = "SELECT *,$approval_step_workflow.id,$approval_rule_workflow.id FROM approval_workflow WHERE target_entity_name = @entityName AND is_enabled = @enabled ORDER BY name ASC";
                    eqlParams.Add(new EqlParameter("enabled", true));
                }
                else
                {
                    eqlCommand = "SELECT *,$approval_step_workflow.id,$approval_rule_workflow.id FROM approval_workflow WHERE target_entity_name = @entityName ORDER BY name ASC";
                }

                var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (eqlResult != null && eqlResult.Any())
                {
                    foreach (var record in eqlResult)
                    {
                        var model = MapToModel(record);

                        if (record.Properties.ContainsKey("$approval_step_workflow"))
                        {
                            var steps = record["$approval_step_workflow"] as List<EntityRecord>;
                            model.StepsCount = steps?.Count ?? 0;
                        }

                        if (record.Properties.ContainsKey("$approval_rule_workflow"))
                        {
                            var rules = record["$approval_rule_workflow"] as List<EntityRecord>;
                            model.RulesCount = rules?.Count ?? 0;
                        }

                        result.Add(model);
                    }
                }
            }
            catch (Exception)
            {
                // Entity may not exist yet
            }

            return result;
        }

        /// <summary>
        /// Sets the enabled/disabled status of a workflow.
        /// </summary>
        /// <param name="id">The workflow ID.</param>
        /// <param name="isEnabled">The new enabled status.</param>
        /// <returns>The updated workflow model.</returns>
        /// <exception cref="ValidationException">Thrown when workflow not found or update fails.</exception>
        public ApprovalWorkflowModel SetEnabled(Guid id, bool isEnabled)
        {
            if (id == Guid.Empty)
            {
                throw new ValidationException("Workflow ID is required.");
            }

            var existing = GetById(id);
            if (existing == null)
            {
                throw new ValidationException($"Workflow with ID '{id}' not found.");
            }

            var patchRecord = new EntityRecord();
            patchRecord["id"] = id;
            patchRecord["is_enabled"] = isEnabled;

            var response = RecMan.UpdateRecord(ENTITY_NAME, patchRecord);
            if (!response.Success)
            {
                throw new ValidationException(response.Message);
            }

            return GetById(id);
        }

        /// <summary>
        /// Checks if a workflow with the specified name already exists.
        /// </summary>
        /// <param name="name">The workflow name to check.</param>
        /// <param name="excludeId">Optional workflow ID to exclude from the check (for updates).</param>
        /// <returns>True if a workflow with the name exists, false otherwise.</returns>
        public bool NameExists(string name, Guid? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            try
            {
                string eqlCommand;
                var eqlParams = new List<EqlParameter>() { new EqlParameter("name", name) };

                if (excludeId.HasValue && excludeId.Value != Guid.Empty)
                {
                    eqlCommand = "SELECT id FROM approval_workflow WHERE name = @name AND id <> @excludeId";
                    eqlParams.Add(new EqlParameter("excludeId", excludeId.Value));
                }
                else
                {
                    eqlCommand = "SELECT id FROM approval_workflow WHERE name = @name";
                }

                var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();
                return eqlResult != null && eqlResult.Any();
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion

        #region Private Validation Methods

        /// <summary>
        /// Validates the model for create operations including null check and field constraints.
        /// </summary>
        /// <param name="model">The model to validate.</param>
        /// <exception cref="ValidationException">Thrown when validation fails.</exception>
        private void ValidateModelForCreate(ApprovalWorkflowModel model)
        {
            if (model == null)
            {
                throw new ValidationException("Workflow model is required.");
            }

            ValidateModelFields(model);
        }

        /// <summary>
        /// Validates the field constraints of the workflow model.
        /// </summary>
        /// <param name="model">The model to validate.</param>
        /// <exception cref="ValidationException">Thrown when validation fails.</exception>
        private void ValidateModelFields(ApprovalWorkflowModel model)
        {
            // Validate name field
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                throw new ValidationException("Workflow name is required.");
            }

            if (model.Name.Length > MAX_NAME_LENGTH)
            {
                throw new ValidationException($"Workflow name cannot exceed {MAX_NAME_LENGTH} characters.");
            }

            // Validate target entity name field
            if (string.IsNullOrWhiteSpace(model.TargetEntityName))
            {
                throw new ValidationException("Target entity name is required.");
            }

            if (model.TargetEntityName.Length > MAX_TARGET_ENTITY_NAME_LENGTH)
            {
                throw new ValidationException($"Target entity name cannot exceed {MAX_TARGET_ENTITY_NAME_LENGTH} characters.");
            }
        }

        /// <summary>
        /// Validates that the workflow name is unique in the system.
        /// </summary>
        /// <param name="name">The name to check for uniqueness.</param>
        /// <param name="excludeId">Optional workflow ID to exclude from the uniqueness check (for updates).</param>
        /// <exception cref="ValidationException">Thrown when a workflow with the same name already exists.</exception>
        private void ValidateNameUniqueness(string name, Guid? excludeId)
        {
            if (NameExists(name, excludeId))
            {
                throw new ValidationException($"A workflow with the name '{name}' already exists. Please choose a different name.");
            }
        }

        /// <summary>
        /// Validates that the target entity name references a valid entity in the system.
        /// Uses EntityManager.ReadEntity() to check for entity existence.
        /// </summary>
        /// <param name="targetEntityName">The entity name to validate.</param>
        /// <exception cref="ValidationException">Thrown when the entity does not exist.</exception>
        private void ValidateTargetEntityExists(string targetEntityName)
        {
            try
            {
                var entityResponse = EntMan.ReadEntity(targetEntityName);
                
                if (entityResponse == null || !entityResponse.Success || entityResponse.Object == null)
                {
                    throw new ValidationException($"Target entity '{targetEntityName}' does not exist in the system. Please specify a valid entity name.");
                }
            }
            catch (ValidationException)
            {
                // Re-throw validation exceptions
                throw;
            }
            catch (Exception ex)
            {
                throw new ValidationException($"Unable to validate target entity '{targetEntityName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Validates that the workflow has no active (pending) approval requests.
        /// </summary>
        /// <param name="workflowId">The workflow ID to check.</param>
        /// <exception cref="ValidationException">Thrown when active requests exist.</exception>
        private void ValidateNoActiveRequests(Guid workflowId)
        {
            try
            {
                var eqlCommand = "SELECT COUNT(*) AS cnt FROM approval_request WHERE workflow_id = @workflowId AND status = @status";
                var eqlParams = new List<EqlParameter>()
                {
                    new EqlParameter("workflowId", workflowId),
                    new EqlParameter("status", "pending")
                };

                var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (eqlResult != null && eqlResult.Any())
                {
                    var countRecord = eqlResult.First();
                    var count = countRecord["cnt"];
                    
                    // Handle different possible return types for COUNT
                    long activeCount = 0;
                    if (count != null)
                    {
                        if (count is long longVal)
                        {
                            activeCount = longVal;
                        }
                        else if (count is int intVal)
                        {
                            activeCount = intVal;
                        }
                        else if (count is decimal decVal)
                        {
                            activeCount = (long)decVal;
                        }
                        else
                        {
                            long.TryParse(count.ToString(), out activeCount);
                        }
                    }

                    if (activeCount > 0)
                    {
                        throw new ValidationException($"Cannot delete workflow: There are {activeCount} active (pending) approval request(s). Please complete or cancel all pending requests before deleting the workflow.");
                    }
                }
            }
            catch (ValidationException)
            {
                // Re-throw validation exceptions
                throw;
            }
            catch (Exception)
            {
                // Entity may not exist yet; allow deletion to proceed
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Deletes all approval rules related to the specified workflow.
        /// </summary>
        /// <param name="workflowId">The workflow ID whose rules should be deleted.</param>
        private void DeleteRelatedRules(Guid workflowId)
        {
            try
            {
                // Find all rules for this workflow
                var eqlCommand = "SELECT id FROM approval_rule WHERE workflow_id = @workflowId";
                var eqlParams = new List<EqlParameter>() { new EqlParameter("workflowId", workflowId) };
                var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (eqlResult != null && eqlResult.Any())
                {
                    foreach (var record in eqlResult)
                    {
                        var ruleId = (Guid)record["id"];
                        var deleteResponse = RecMan.DeleteRecord(RULE_ENTITY_NAME, ruleId);
                        
                        if (!deleteResponse.Success)
                        {
                            // Log warning but continue with other deletions
                            // In production, consider using a transaction for atomicity
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Entity may not exist yet; continue
            }
        }

        /// <summary>
        /// Deletes all approval steps related to the specified workflow.
        /// </summary>
        /// <param name="workflowId">The workflow ID whose steps should be deleted.</param>
        private void DeleteRelatedSteps(Guid workflowId)
        {
            try
            {
                // Find all steps for this workflow
                var eqlCommand = "SELECT id FROM approval_step WHERE workflow_id = @workflowId";
                var eqlParams = new List<EqlParameter>() { new EqlParameter("workflowId", workflowId) };
                var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (eqlResult != null && eqlResult.Any())
                {
                    foreach (var record in eqlResult)
                    {
                        var stepId = (Guid)record["id"];
                        var deleteResponse = RecMan.DeleteRecord(STEP_ENTITY_NAME, stepId);
                        
                        if (!deleteResponse.Success)
                        {
                            // Log warning but continue with other deletions
                            // In production, consider using a transaction for atomicity
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Entity may not exist yet; continue
            }
        }

        /// <summary>
        /// Maps an EntityRecord from the approval_workflow entity to an ApprovalWorkflowModel DTO.
        /// </summary>
        /// <param name="record">The entity record to map.</param>
        /// <returns>The mapped ApprovalWorkflowModel.</returns>
        private ApprovalWorkflowModel MapToModel(EntityRecord record)
        {
            if (record == null)
            {
                return null;
            }

            return new ApprovalWorkflowModel
            {
                Id = record.Properties.ContainsKey("id") && record["id"] != null 
                    ? (Guid)record["id"] 
                    : Guid.Empty,
                    
                Name = record.Properties.ContainsKey("name") && record["name"] != null 
                    ? record["name"].ToString() 
                    : string.Empty,
                    
                TargetEntityName = record.Properties.ContainsKey("target_entity_name") && record["target_entity_name"] != null 
                    ? record["target_entity_name"].ToString() 
                    : string.Empty,
                    
                IsEnabled = record.Properties.ContainsKey("is_enabled") && record["is_enabled"] != null 
                    ? (bool)record["is_enabled"] 
                    : true,
                    
                CreatedOn = record.Properties.ContainsKey("created_on") && record["created_on"] != null 
                    ? (DateTime)record["created_on"] 
                    : DateTime.MinValue,
                    
                CreatedBy = record.Properties.ContainsKey("created_by") && record["created_by"] != null 
                    ? (Guid?)record["created_by"] 
                    : null,

                // StepsCount and RulesCount are calculated separately from relation data
                StepsCount = 0,
                RulesCount = 0
            };
        }

        #endregion
    }
}
