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
    /// Service for workflow runtime operations including lifecycle management.
    /// Provides Enable() and Disable() methods to activate/deactivate workflows,
    /// GetActiveWorkflows() to list enabled workflows, GetWorkflowByEntityName() 
    /// to find workflow for an entity type, ValidateWorkflowConfiguration() to 
    /// ensure workflow has valid steps before enabling, and GetWorkflowSteps() 
    /// and GetWorkflowRules() for loading related configuration.
    /// Uses RecordManager for data access.
    /// </summary>
    public class ApprovalWorkflowService
    {
        #region Constants

        /// <summary>
        /// Entity name for approval workflow records.
        /// </summary>
        private const string WORKFLOW_ENTITY = "approval_workflow";

        /// <summary>
        /// Entity name for approval step records.
        /// </summary>
        private const string STEP_ENTITY = "approval_step";

        /// <summary>
        /// Entity name for approval rule records.
        /// </summary>
        private const string RULE_ENTITY = "approval_rule";

        #endregion

        #region Properties

        /// <summary>
        /// Gets the RecordManager instance for data access operations.
        /// Initialized inline following WebVella service pattern.
        /// </summary>
        protected RecordManager RecMan { get; private set; } = new RecordManager();

        #endregion

        #region Public Methods

        /// <summary>
        /// Enables an approval workflow by setting is_enabled to true.
        /// Validates the workflow configuration before enabling to ensure it has
        /// at least one step and at least one final step.
        /// </summary>
        /// <param name="workflowId">The unique identifier of the workflow to enable.</param>
        /// <exception cref="ValidationException">
        /// Thrown when the workflow does not exist or when the workflow configuration
        /// is invalid (no steps or no final step configured).
        /// </exception>
        /// <exception cref="Exception">
        /// Thrown when the database update operation fails.
        /// </exception>
        public void Enable(Guid workflowId)
        {
            // Validate workflow exists
            var workflow = GetWorkflowById(workflowId);
            if (workflow == null)
            {
                var ex = new ValidationException("Workflow not found.");
                ex.AddError("id", "Workflow not found.");
                throw ex;
            }

            // Validate workflow configuration before enabling
            ValidateWorkflowConfiguration(workflowId);

            // Update is_enabled to true
            var patchRecord = new EntityRecord();
            patchRecord["id"] = workflowId;
            patchRecord["is_enabled"] = true;

            var updateResponse = RecMan.UpdateRecord(WORKFLOW_ENTITY, patchRecord);
            if (!updateResponse.Success)
            {
                throw new Exception($"Failed to enable workflow: {updateResponse.Message}");
            }
        }

        /// <summary>
        /// Disables an approval workflow by setting is_enabled to false.
        /// Disabled workflows will not be triggered for new records.
        /// </summary>
        /// <param name="workflowId">The unique identifier of the workflow to disable.</param>
        /// <exception cref="ValidationException">
        /// Thrown when the workflow does not exist.
        /// </exception>
        /// <exception cref="Exception">
        /// Thrown when the database update operation fails.
        /// </exception>
        public void Disable(Guid workflowId)
        {
            // Validate workflow exists
            var workflow = GetWorkflowById(workflowId);
            if (workflow == null)
            {
                var ex = new ValidationException("Workflow not found.");
                ex.AddError("id", "Workflow not found.");
                throw ex;
            }

            // Update is_enabled to false
            var patchRecord = new EntityRecord();
            patchRecord["id"] = workflowId;
            patchRecord["is_enabled"] = false;

            var updateResponse = RecMan.UpdateRecord(WORKFLOW_ENTITY, patchRecord);
            if (!updateResponse.Success)
            {
                throw new Exception($"Failed to disable workflow: {updateResponse.Message}");
            }
        }

        /// <summary>
        /// Retrieves all active (enabled) approval workflows.
        /// </summary>
        /// <returns>
        /// A list of <see cref="ApprovalWorkflowModel"/> objects representing all 
        /// enabled workflows, ordered by name ascending. Returns an empty list if 
        /// no active workflows are found.
        /// </returns>
        public List<ApprovalWorkflowModel> GetActiveWorkflows()
        {
            var eqlCommand = "SELECT * FROM approval_workflow WHERE is_enabled = @isEnabled ORDER BY name ASC";
            var eqlParams = new List<EqlParameter>
            {
                new EqlParameter("isEnabled", true)
            };

            var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

            if (eqlResult == null || !eqlResult.Any())
            {
                return new List<ApprovalWorkflowModel>();
            }

            return eqlResult.Select(record => MapToWorkflowModel(record)).ToList();
        }

        /// <summary>
        /// Retrieves the active (enabled) approval workflow configured for a specific entity type.
        /// </summary>
        /// <param name="entityName">
        /// The name of the target entity to find workflow for (e.g., "purchase_order", "expense_request").
        /// </param>
        /// <returns>
        /// The first matching <see cref="ApprovalWorkflowModel"/> for the specified entity, 
        /// or null if no active workflow is configured for the entity.
        /// </returns>
        /// <remarks>
        /// If multiple workflows are configured for the same entity, returns the first one found.
        /// Only enabled workflows are returned.
        /// </remarks>
        public ApprovalWorkflowModel GetWorkflowByEntityName(string entityName)
        {
            if (string.IsNullOrWhiteSpace(entityName))
            {
                return null;
            }

            var eqlCommand = "SELECT * FROM approval_workflow WHERE target_entity_name = @entityName AND is_enabled = @isEnabled";
            var eqlParams = new List<EqlParameter>
            {
                new EqlParameter("entityName", entityName),
                new EqlParameter("isEnabled", true)
            };

            var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

            if (eqlResult == null || !eqlResult.Any())
            {
                return null;
            }

            return MapToWorkflowModel(eqlResult.First());
        }

        /// <summary>
        /// Validates that a workflow has a valid configuration suitable for enabling.
        /// A valid configuration requires:
        /// 1. At least one approval step defined
        /// 2. At least one step marked as the final step (is_final = true)
        /// </summary>
        /// <param name="workflowId">The unique identifier of the workflow to validate.</param>
        /// <exception cref="ValidationException">
        /// Thrown when validation fails with specific error details:
        /// - "Workflow must have at least one step configured." - No steps defined
        /// - "Workflow must have at least one final step configured." - No final step
        /// </exception>
        public void ValidateWorkflowConfiguration(Guid workflowId)
        {
            // Get all steps for the workflow
            var steps = GetWorkflowSteps(workflowId);

            // Check workflow has at least one step
            if (steps == null || steps.Count == 0)
            {
                var ex = new ValidationException("Workflow must have at least one step configured.");
                ex.AddError("steps", "Workflow must have at least one step configured.");
                throw ex;
            }

            // Check at least one step has is_final = true
            var hasFinalStep = steps.Any(s => s.IsFinal);
            if (!hasFinalStep)
            {
                var ex = new ValidationException("Workflow must have at least one final step configured.");
                ex.AddError("is_final", "Workflow must have at least one final step configured.");
                throw ex;
            }
        }

        /// <summary>
        /// Retrieves all approval steps configured for a specific workflow.
        /// </summary>
        /// <param name="workflowId">The unique identifier of the workflow.</param>
        /// <returns>
        /// A list of <see cref="ApprovalStepModel"/> objects representing all steps
        /// for the workflow, ordered by step_order ascending. Returns an empty list
        /// if no steps are configured.
        /// </returns>
        public List<ApprovalStepModel> GetWorkflowSteps(Guid workflowId)
        {
            var eqlCommand = "SELECT * FROM approval_step WHERE workflow_id = @workflowId ORDER BY step_order ASC";
            var eqlParams = new List<EqlParameter>
            {
                new EqlParameter("workflowId", workflowId)
            };

            var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

            if (eqlResult == null || !eqlResult.Any())
            {
                return new List<ApprovalStepModel>();
            }

            return eqlResult.Select(record => MapToStepModel(record)).ToList();
        }

        /// <summary>
        /// Retrieves all routing rules configured for a specific workflow.
        /// </summary>
        /// <param name="workflowId">The unique identifier of the workflow.</param>
        /// <returns>
        /// A list of <see cref="ApprovalRuleModel"/> objects representing all rules
        /// for the workflow, ordered by priority descending (highest priority first).
        /// Returns an empty list if no rules are configured.
        /// </returns>
        public List<ApprovalRuleModel> GetWorkflowRules(Guid workflowId)
        {
            var eqlCommand = "SELECT * FROM approval_rule WHERE workflow_id = @workflowId ORDER BY priority DESC";
            var eqlParams = new List<EqlParameter>
            {
                new EqlParameter("workflowId", workflowId)
            };

            var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

            if (eqlResult == null || !eqlResult.Any())
            {
                return new List<ApprovalRuleModel>();
            }

            return eqlResult.Select(record => MapToRuleModel(record)).ToList();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Retrieves a workflow by its unique identifier.
        /// </summary>
        /// <param name="workflowId">The unique identifier of the workflow.</param>
        /// <returns>
        /// The <see cref="ApprovalWorkflowModel"/> if found, or null if not found.
        /// </returns>
        private ApprovalWorkflowModel GetWorkflowById(Guid workflowId)
        {
            if (workflowId == Guid.Empty)
            {
                return null;
            }

            var eqlCommand = "SELECT * FROM approval_workflow WHERE id = @id";
            var eqlParams = new List<EqlParameter>
            {
                new EqlParameter("id", workflowId)
            };

            var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

            if (eqlResult == null || !eqlResult.Any())
            {
                return null;
            }

            return MapToWorkflowModel(eqlResult.First());
        }

        /// <summary>
        /// Maps an EntityRecord from the approval_workflow entity to an ApprovalWorkflowModel DTO.
        /// </summary>
        /// <param name="record">The entity record to map.</param>
        /// <returns>A populated <see cref="ApprovalWorkflowModel"/> instance.</returns>
        private ApprovalWorkflowModel MapToWorkflowModel(EntityRecord record)
        {
            if (record == null)
            {
                return null;
            }

            var model = new ApprovalWorkflowModel
            {
                Id = record.Properties.ContainsKey("id") && record["id"] != null
                    ? (Guid)record["id"]
                    : Guid.Empty,
                Name = record.Properties.ContainsKey("name") && record["name"] != null
                    ? (string)record["name"]
                    : string.Empty,
                TargetEntityName = record.Properties.ContainsKey("target_entity_name") && record["target_entity_name"] != null
                    ? (string)record["target_entity_name"]
                    : string.Empty,
                IsEnabled = record.Properties.ContainsKey("is_enabled") && record["is_enabled"] != null
                    && (bool)record["is_enabled"],
                CreatedOn = record.Properties.ContainsKey("created_on") && record["created_on"] != null
                    ? (DateTime)record["created_on"]
                    : DateTime.UtcNow,
                CreatedBy = record.Properties.ContainsKey("created_by") && record["created_by"] != null
                    ? (Guid?)record["created_by"]
                    : null
            };

            // Optionally populate step and rule counts
            var steps = GetWorkflowSteps(model.Id);
            var rules = GetWorkflowRules(model.Id);
            model.StepsCount = steps?.Count ?? 0;
            model.RulesCount = rules?.Count ?? 0;

            return model;
        }

        /// <summary>
        /// Maps an EntityRecord from the approval_step entity to an ApprovalStepModel DTO.
        /// </summary>
        /// <param name="record">The entity record to map.</param>
        /// <returns>A populated <see cref="ApprovalStepModel"/> instance.</returns>
        private ApprovalStepModel MapToStepModel(EntityRecord record)
        {
            if (record == null)
            {
                return null;
            }

            return new ApprovalStepModel
            {
                Id = record.Properties.ContainsKey("id") && record["id"] != null
                    ? (Guid)record["id"]
                    : Guid.Empty,
                WorkflowId = record.Properties.ContainsKey("workflow_id") && record["workflow_id"] != null
                    ? (Guid)record["workflow_id"]
                    : Guid.Empty,
                StepOrder = record.Properties.ContainsKey("step_order") && record["step_order"] != null
                    ? Convert.ToInt32(record["step_order"])
                    : 0,
                Name = record.Properties.ContainsKey("name") && record["name"] != null
                    ? (string)record["name"]
                    : string.Empty,
                ApproverType = record.Properties.ContainsKey("approver_type") && record["approver_type"] != null
                    ? (string)record["approver_type"]
                    : string.Empty,
                ApproverId = record.Properties.ContainsKey("approver_id") && record["approver_id"] != null
                    ? (Guid?)record["approver_id"]
                    : null,
                TimeoutHours = record.Properties.ContainsKey("timeout_hours") && record["timeout_hours"] != null
                    ? Convert.ToInt32(record["timeout_hours"])
                    : (int?)null,
                IsFinal = record.Properties.ContainsKey("is_final") && record["is_final"] != null
                    && (bool)record["is_final"]
            };
        }

        /// <summary>
        /// Maps an EntityRecord from the approval_rule entity to an ApprovalRuleModel DTO.
        /// </summary>
        /// <param name="record">The entity record to map.</param>
        /// <returns>A populated <see cref="ApprovalRuleModel"/> instance.</returns>
        private ApprovalRuleModel MapToRuleModel(EntityRecord record)
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
                    ? (string)record["name"]
                    : string.Empty,
                FieldName = record.Properties.ContainsKey("field_name") && record["field_name"] != null
                    ? (string)record["field_name"]
                    : string.Empty,
                Operator = record.Properties.ContainsKey("operator") && record["operator"] != null
                    ? (string)record["operator"]
                    : string.Empty,
                Value = record.Properties.ContainsKey("value") && record["value"] != null
                    ? (string)record["value"]
                    : string.Empty,
                Priority = record.Properties.ContainsKey("priority") && record["priority"] != null
                    ? Convert.ToInt32(record["priority"])
                    : 0
            };
        }

        #endregion
    }
}
