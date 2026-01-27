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
    /// Admin-facing service for CRUD operations on approval_rule entity configuration.
    /// Provides Create(), GetById(), GetByWorkflowId(), Update(), and Delete() methods
    /// for managing routing rules. Includes priority management with ReorderPriorities()
    /// for adjusting rule evaluation order.
    /// </summary>
    /// <remarks>
    /// This service follows the WebVella service pattern using RecordManager for data persistence
    /// and EntityManager for entity validation. All methods use EQL (Entity Query Language) for
    /// parameterized queries to ensure SQL injection protection.
    /// 
    /// Rules define conditional logic that determines which workflow should be applied
    /// to a record based on field values and comparison operators.
    /// </remarks>
    public class RuleConfigService
    {
        #region Constants

        /// <summary>
        /// Entity name constant for the approval_rule entity.
        /// </summary>
        public const string ENTITY_NAME = "approval_rule";

        /// <summary>
        /// Entity name constant for the approval_workflow entity.
        /// </summary>
        public const string WORKFLOW_ENTITY_NAME = "approval_workflow";

        /// <summary>
        /// Array of valid comparison operators for approval rules.
        /// </summary>
        /// <remarks>
        /// Valid operators include:
        /// - eq: Equal comparison
        /// - neq: Not equal comparison
        /// - gt: Greater than comparison
        /// - gte: Greater than or equal comparison
        /// - lt: Less than comparison
        /// - lte: Less than or equal comparison
        /// - contains: Substring contains comparison
        /// </remarks>
        public static readonly string[] VALID_OPERATORS = new string[] 
        { 
            "eq", "neq", "gt", "gte", "lt", "lte", "contains" 
        };

        /// <summary>
        /// Maximum allowed length for rule name.
        /// </summary>
        public const int MAX_NAME_LENGTH = 256;

        /// <summary>
        /// Maximum allowed length for field name.
        /// </summary>
        public const int MAX_FIELD_NAME_LENGTH = 128;

        /// <summary>
        /// Maximum allowed length for value.
        /// </summary>
        public const int MAX_VALUE_LENGTH = 1024;

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
        /// Creates a new approval rule with comprehensive validation.
        /// </summary>
        /// <param name="model">The rule model containing the rule configuration data.</param>
        /// <returns>The created ApprovalRuleModel with generated ID.</returns>
        /// <exception cref="ValidationException">
        /// Thrown when:
        /// - Model is null
        /// - WorkflowId does not reference an existing workflow
        /// - Name is empty, whitespace, or exceeds 256 characters
        /// - FieldName is empty, whitespace, or exceeds 128 characters
        /// - FieldName does not exist on the target entity of the parent workflow
        /// - Operator is not in the list of valid operators
        /// - Value is null or exceeds 1024 characters
        /// - Database operation fails
        /// </exception>
        /// <example>
        /// <code>
        /// var service = new RuleConfigService();
        /// var model = new ApprovalRuleModel
        /// {
        ///     WorkflowId = workflowId,
        ///     Name = "High Value Rule",
        ///     FieldName = "total_amount",
        ///     Operator = "gt",
        ///     Value = "10000",
        ///     Priority = 10
        /// };
        /// var created = service.Create(model);
        /// </code>
        /// </example>
        public ApprovalRuleModel Create(ApprovalRuleModel model)
        {
            // Validate the model structure and field constraints
            ValidateModelForCreate(model);

            // Validate workflow exists and get its target entity
            var workflowService = new WorkflowConfigService();
            var workflow = workflowService.GetById(model.WorkflowId);
            if (workflow == null)
            {
                throw new ValidationException($"Workflow with ID '{model.WorkflowId}' not found. Cannot create rule for non-existent workflow.");
            }

            // Validate operator is in the list of valid operators
            ValidateOperator(model.Operator);

            // Validate field_name exists on target entity
            ValidateFieldExists(workflow.TargetEntityName, model.FieldName);

            // Generate new ID
            model.Id = Guid.NewGuid();

            // Build the entity record for persistence
            var record = new EntityRecord();
            record["id"] = model.Id;
            record["workflow_id"] = model.WorkflowId;
            record["name"] = model.Name;
            record["field_name"] = model.FieldName;
            record["operator"] = model.Operator;
            record["value"] = model.Value;
            record["priority"] = model.Priority;

            // Execute the create operation
            var response = RecMan.CreateRecord(ENTITY_NAME, record);
            if (!response.Success)
            {
                throw new ValidationException(response.Message);
            }

            return model;
        }

        /// <summary>
        /// Retrieves a specific approval rule by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier (GUID) of the rule to retrieve.</param>
        /// <returns>
        /// The ApprovalRuleModel if found; null if no rule exists with the specified ID.
        /// </returns>
        /// <example>
        /// <code>
        /// var service = new RuleConfigService();
        /// var rule = service.GetById(ruleId);
        /// if (rule != null)
        /// {
        ///     Console.WriteLine($"Rule: {rule.Name}, Field: {rule.FieldName}, Op: {rule.Operator}");
        /// }
        /// </code>
        /// </example>
        public ApprovalRuleModel GetById(Guid id)
        {
            if (id == Guid.Empty)
            {
                return null;
            }

            try
            {
                var eqlCommand = "SELECT * FROM approval_rule WHERE id = @id";
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
        /// Retrieves all approval rules for a specific workflow, ordered by priority descending.
        /// Higher priority rules are returned first.
        /// </summary>
        /// <param name="workflowId">The unique identifier of the parent workflow.</param>
        /// <returns>
        /// A list of ApprovalRuleModel objects ordered by priority descending.
        /// Returns an empty list if no rules exist for the workflow or if the entity is not yet created.
        /// </returns>
        /// <example>
        /// <code>
        /// var service = new RuleConfigService();
        /// var rules = service.GetByWorkflowId(workflowId);
        /// foreach (var rule in rules)
        /// {
        ///     Console.WriteLine($"Priority {rule.Priority}: {rule.Name}");
        /// }
        /// </code>
        /// </example>
        public List<ApprovalRuleModel> GetByWorkflowId(Guid workflowId)
        {
            var result = new List<ApprovalRuleModel>();

            if (workflowId == Guid.Empty)
            {
                return result;
            }

            try
            {
                var eqlCommand = "SELECT * FROM approval_rule WHERE workflow_id = @workflowId ORDER BY priority DESC";
                var eqlParams = new List<EqlParameter>() { new EqlParameter("workflowId", workflowId) };
                var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (eqlResult != null && eqlResult.Any())
                {
                    foreach (var record in eqlResult)
                    {
                        result.Add(MapToModel(record));
                    }
                }
            }
            catch (Exception)
            {
                // Entity may not exist yet during plugin initialization
            }

            return result;
        }

        /// <summary>
        /// Updates an existing approval rule with validation.
        /// </summary>
        /// <param name="model">The rule model containing the updated configuration data.</param>
        /// <returns>The updated ApprovalRuleModel refreshed from the database.</returns>
        /// <exception cref="ValidationException">
        /// Thrown when:
        /// - Model is null
        /// - Id is empty (Guid.Empty)
        /// - Rule with specified Id does not exist
        /// - WorkflowId does not reference an existing workflow
        /// - Name is empty, whitespace, or exceeds 256 characters
        /// - FieldName is empty, whitespace, or exceeds 128 characters
        /// - FieldName does not exist on the target entity of the parent workflow
        /// - Operator is not in the list of valid operators
        /// - Value is null or exceeds 1024 characters
        /// - Database operation fails
        /// </exception>
        /// <example>
        /// <code>
        /// var service = new RuleConfigService();
        /// var rule = service.GetById(ruleId);
        /// rule.Value = "20000";
        /// rule.Priority = 20;
        /// var updated = service.Update(rule);
        /// </code>
        /// </example>
        public ApprovalRuleModel Update(ApprovalRuleModel model)
        {
            // Validate the model structure and field constraints
            ValidateModelForUpdate(model);

            // Verify the rule exists
            var existing = GetById(model.Id);
            if (existing == null)
            {
                throw new ValidationException($"Rule with ID '{model.Id}' not found.");
            }

            // Validate workflow exists and get its target entity
            var workflowService = new WorkflowConfigService();
            var workflow = workflowService.GetById(model.WorkflowId);
            if (workflow == null)
            {
                throw new ValidationException($"Workflow with ID '{model.WorkflowId}' not found. Cannot update rule with non-existent workflow reference.");
            }

            // Validate operator is in the list of valid operators
            ValidateOperator(model.Operator);

            // Validate field_name exists on target entity
            ValidateFieldExists(workflow.TargetEntityName, model.FieldName);

            // Build the patch record for update
            var patchRecord = new EntityRecord();
            patchRecord["id"] = model.Id;
            patchRecord["workflow_id"] = model.WorkflowId;
            patchRecord["name"] = model.Name;
            patchRecord["field_name"] = model.FieldName;
            patchRecord["operator"] = model.Operator;
            patchRecord["value"] = model.Value;
            patchRecord["priority"] = model.Priority;

            // Execute the update operation
            var response = RecMan.UpdateRecord(ENTITY_NAME, patchRecord);
            if (!response.Success)
            {
                throw new ValidationException(response.Message);
            }

            // Return refreshed data from database
            return GetById(model.Id);
        }

        /// <summary>
        /// Deletes an approval rule by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier (GUID) of the rule to delete.</param>
        /// <exception cref="ValidationException">
        /// Thrown when:
        /// - Id is empty (Guid.Empty)
        /// - Rule with specified Id does not exist
        /// - Database operation fails during deletion
        /// </exception>
        /// <example>
        /// <code>
        /// var service = new RuleConfigService();
        /// try
        /// {
        ///     service.Delete(ruleId);
        ///     Console.WriteLine("Rule deleted successfully.");
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
                throw new ValidationException("Rule ID is required for delete operation.");
            }

            // Verify the rule exists
            var existing = GetById(id);
            if (existing == null)
            {
                throw new ValidationException($"Rule with ID '{id}' not found.");
            }

            // Delete the rule record
            var response = RecMan.DeleteRecord(ENTITY_NAME, id);
            if (!response.Success)
            {
                throw new ValidationException(response.Message);
            }
        }

        /// <summary>
        /// Reorders the priority of rules for a specific workflow based on the provided list of rule IDs.
        /// The first rule in the list gets the highest priority, with each subsequent rule having a lower priority.
        /// </summary>
        /// <param name="workflowId">The unique identifier of the parent workflow.</param>
        /// <param name="ruleIdsInOrder">
        /// List of rule IDs in the desired order. The first ID will have the highest priority.
        /// All IDs must belong to rules in the specified workflow.
        /// </param>
        /// <exception cref="ValidationException">
        /// Thrown when:
        /// - WorkflowId is empty (Guid.Empty)
        /// - Workflow with specified Id does not exist
        /// - ruleIdsInOrder is null or empty
        /// - Any rule ID in the list does not belong to the specified workflow
        /// - Database operation fails during update
        /// </exception>
        /// <remarks>
        /// Priority values are assigned in descending order, with the first rule receiving
        /// priority = count, second rule priority = count - 1, and so on.
        /// This ensures higher priority rules are evaluated first during workflow routing.
        /// </remarks>
        /// <example>
        /// <code>
        /// var service = new RuleConfigService();
        /// var newOrder = new List&lt;Guid&gt; { highPriorityRuleId, mediumPriorityRuleId, lowPriorityRuleId };
        /// service.ReorderPriorities(workflowId, newOrder);
        /// </code>
        /// </example>
        public void ReorderPriorities(Guid workflowId, List<Guid> ruleIdsInOrder)
        {
            // Validate workflowId
            if (workflowId == Guid.Empty)
            {
                throw new ValidationException("Workflow ID is required for reorder operation.");
            }

            // Validate workflow exists
            var workflowService = new WorkflowConfigService();
            var workflow = workflowService.GetById(workflowId);
            if (workflow == null)
            {
                throw new ValidationException($"Workflow with ID '{workflowId}' not found.");
            }

            // Validate ruleIdsInOrder
            if (ruleIdsInOrder == null || !ruleIdsInOrder.Any())
            {
                throw new ValidationException("Rule IDs list is required and cannot be empty.");
            }

            // Get all existing rules for the workflow to validate ownership
            var existingRules = GetByWorkflowId(workflowId);
            var existingRuleIds = existingRules.Select(r => r.Id).ToList();

            // Validate all provided rule IDs belong to this workflow
            foreach (var ruleId in ruleIdsInOrder)
            {
                if (!existingRuleIds.Contains(ruleId))
                {
                    throw new ValidationException($"Rule with ID '{ruleId}' does not belong to workflow '{workflowId}' or does not exist.");
                }
            }

            // Assign priorities in descending order
            // First rule gets highest priority (count), last gets lowest (1)
            int priority = ruleIdsInOrder.Count;
            
            foreach (var ruleId in ruleIdsInOrder)
            {
                var patchRecord = new EntityRecord();
                patchRecord["id"] = ruleId;
                patchRecord["priority"] = priority;

                var response = RecMan.UpdateRecord(ENTITY_NAME, patchRecord);
                if (!response.Success)
                {
                    throw new ValidationException($"Failed to update priority for rule '{ruleId}': {response.Message}");
                }

                priority--;
            }
        }

        /// <summary>
        /// Retrieves all rules for a specific field within a workflow.
        /// This is useful for evaluating which rules apply to a particular field's value.
        /// </summary>
        /// <param name="workflowId">The workflow ID to filter by.</param>
        /// <param name="fieldName">The field name to filter by.</param>
        /// <returns>
        /// A list of ApprovalRuleModel objects that match the workflow and field criteria,
        /// ordered by threshold value in ascending order. Returns empty list if no matches found.
        /// </returns>
        /// <example>
        /// <code>
        /// var service = new RuleConfigService();
        /// // Get all rules that evaluate the "amount" field for a workflow
        /// var rules = service.GetRulesForField(workflowId, "amount");
        /// foreach (var rule in rules)
        /// {
        ///     Console.WriteLine($"Rule: {rule.Name}, Threshold: {rule.ThresholdValue}");
        /// }
        /// </code>
        /// </example>
        public List<ApprovalRuleModel> GetRulesForField(Guid workflowId, string fieldName)
        {
            var result = new List<ApprovalRuleModel>();

            if (workflowId == Guid.Empty || string.IsNullOrWhiteSpace(fieldName))
            {
                return result;
            }

            try
            {
                var eqlCommand = @"SELECT * FROM approval_rule 
                                   WHERE workflow_id = @workflowId AND field_name = @fieldName 
                                   ORDER BY threshold_value ASC";
                var eqlParams = new List<EqlParameter>() 
                { 
                    new EqlParameter("workflowId", workflowId),
                    new EqlParameter("fieldName", fieldName)
                };
                var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (eqlResult != null && eqlResult.Any())
                {
                    foreach (var record in eqlResult)
                    {
                        result.Add(MapToModel(record));
                    }
                }
            }
            catch (Exception)
            {
                // Entity may not exist yet during plugin initialization
            }

            return result;
        }

        #endregion

        #region Private Validation Methods

        /// <summary>
        /// Validates the model structure and field constraints for create operations.
        /// </summary>
        /// <param name="model">The model to validate.</param>
        /// <exception cref="ValidationException">Thrown when validation fails.</exception>
        private void ValidateModelForCreate(ApprovalRuleModel model)
        {
            if (model == null)
            {
                throw new ValidationException("Rule model cannot be null.");
            }

            // Validate WorkflowId
            if (model.WorkflowId == Guid.Empty)
            {
                throw new ValidationException("Workflow ID is required.");
            }

            // Validate Name
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                throw new ValidationException("Rule name is required and cannot be empty or whitespace.");
            }

            if (model.Name.Length > MAX_NAME_LENGTH)
            {
                throw new ValidationException($"Rule name cannot exceed {MAX_NAME_LENGTH} characters. Current length: {model.Name.Length}.");
            }

            // Validate FieldName
            if (string.IsNullOrWhiteSpace(model.FieldName))
            {
                throw new ValidationException("Field name is required and cannot be empty or whitespace.");
            }

            if (model.FieldName.Length > MAX_FIELD_NAME_LENGTH)
            {
                throw new ValidationException($"Field name cannot exceed {MAX_FIELD_NAME_LENGTH} characters. Current length: {model.FieldName.Length}.");
            }

            // Validate Operator
            if (string.IsNullOrWhiteSpace(model.Operator))
            {
                throw new ValidationException("Operator is required and cannot be empty or whitespace.");
            }

            // Validate Value
            if (model.Value == null)
            {
                throw new ValidationException("Value is required and cannot be null.");
            }

            if (model.Value.Length > MAX_VALUE_LENGTH)
            {
                throw new ValidationException($"Value cannot exceed {MAX_VALUE_LENGTH} characters. Current length: {model.Value.Length}.");
            }
        }

        /// <summary>
        /// Validates the model structure and field constraints for update operations.
        /// </summary>
        /// <param name="model">The model to validate.</param>
        /// <exception cref="ValidationException">Thrown when validation fails.</exception>
        private void ValidateModelForUpdate(ApprovalRuleModel model)
        {
            // First perform create validations
            ValidateModelForCreate(model);

            // Then validate ID for update
            if (model.Id == Guid.Empty)
            {
                throw new ValidationException("Rule ID is required for update operation.");
            }
        }

        /// <summary>
        /// Validates that the operator is in the list of valid operators.
        /// </summary>
        /// <param name="operatorValue">The operator string to validate.</param>
        /// <exception cref="ValidationException">Thrown when operator is invalid.</exception>
        private void ValidateOperator(string operatorValue)
        {
            if (!VALID_OPERATORS.Contains(operatorValue.ToLowerInvariant()))
            {
                throw new ValidationException(
                    $"Invalid operator '{operatorValue}'. Valid operators are: {string.Join(", ", VALID_OPERATORS)}.");
            }
        }

        /// <summary>
        /// Validates that the specified field exists on the target entity.
        /// Uses EntityManager.ReadEntity() to check the entity schema.
        /// </summary>
        /// <param name="entityName">The name of the entity to check.</param>
        /// <param name="fieldName">The name of the field to validate.</param>
        /// <exception cref="ValidationException">Thrown when entity or field does not exist.</exception>
        private void ValidateFieldExists(string entityName, string fieldName)
        {
            try
            {
                var entityResponse = EntMan.ReadEntity(entityName);
                
                if (!entityResponse.Success || entityResponse.Object == null)
                {
                    throw new ValidationException(
                        $"Target entity '{entityName}' not found. Cannot validate field '{fieldName}'.");
                }

                var entity = entityResponse.Object;
                
                // Check if the field exists in the entity's fields collection
                var fieldExists = entity.Fields != null && 
                    entity.Fields.Any(f => f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));

                if (!fieldExists)
                {
                    throw new ValidationException(
                        $"Field '{fieldName}' does not exist on entity '{entityName}'. Please specify a valid field name.");
                }
            }
            catch (ValidationException)
            {
                // Re-throw validation exceptions
                throw;
            }
            catch (Exception ex)
            {
                throw new ValidationException(
                    $"Unable to validate field '{fieldName}' on entity '{entityName}': {ex.Message}");
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Maps an EntityRecord from the approval_rule entity to an ApprovalRuleModel DTO.
        /// </summary>
        /// <param name="record">The entity record to map.</param>
        /// <returns>The mapped ApprovalRuleModel.</returns>
        private ApprovalRuleModel MapToModel(EntityRecord record)
        {
            if (record == null)
            {
                return null;
            }

            return new ApprovalRuleModel
            {
                Id = record.Properties.ContainsKey("id") && record["id"] != null
                    ? (Guid)record["id"]
                    : Guid.Empty,

                WorkflowId = record.Properties.ContainsKey("workflow_id") && record["workflow_id"] != null
                    ? (Guid)record["workflow_id"]
                    : Guid.Empty,

                Name = record.Properties.ContainsKey("name") && record["name"] != null
                    ? record["name"].ToString()
                    : string.Empty,

                FieldName = record.Properties.ContainsKey("field_name") && record["field_name"] != null
                    ? record["field_name"].ToString()
                    : string.Empty,

                Operator = record.Properties.ContainsKey("operator") && record["operator"] != null
                    ? record["operator"].ToString()
                    : string.Empty,

                Value = record.Properties.ContainsKey("value") && record["value"] != null
                    ? record["value"].ToString()
                    : string.Empty,

                Priority = record.Properties.ContainsKey("priority") && record["priority"] != null
                    ? Convert.ToInt32(record["priority"])
                    : 0
            };
        }

        #endregion
    }
}
