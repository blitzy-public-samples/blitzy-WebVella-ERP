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
    /// Service for managing approval workflow configuration.
    /// Provides CRUD operations for the approval_workflow entity.
    /// </summary>
    public class WorkflowConfigService
    {
        private RecordManager _recordManager;
        
        /// <summary>
        /// Entity name for approval workflows.
        /// </summary>
        public const string ENTITY_NAME = "approval_workflow";

        /// <summary>
        /// Gets the RecordManager instance for database operations.
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
        /// Retrieves all approval workflows.
        /// </summary>
        /// <param name="includeInactive">If true, includes disabled workflows. Default is false.</param>
        /// <returns>List of all approval workflow models.</returns>
        public List<ApprovalWorkflowModel> GetAll(bool includeInactive = false)
        {
            var result = new List<ApprovalWorkflowModel>();

            try
            {
                string eql = $"SELECT *,$approval_step_workflow.id,$approval_rule_workflow.id FROM {ENTITY_NAME}";
                
                if (!includeInactive)
                {
                    eql += " WHERE is_enabled = @enabled";
                }
                
                eql += " ORDER BY name ASC";

                var eqlParams = new List<EqlParameter>();
                if (!includeInactive)
                {
                    eqlParams.Add(new EqlParameter("enabled", true));
                }

                var eqlCommand = new EqlCommand(eql, eqlParams.ToArray());
                var queryResult = eqlCommand.Execute();

                if (queryResult != null && queryResult.Any())
                {
                    foreach (var record in queryResult)
                    {
                        var model = MapToModel(record);
                        
                        // Count steps and rules from relation
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
                // Return empty list if entity doesn't exist yet
            }

            return result;
        }

        /// <summary>
        /// Retrieves a specific approval workflow by ID.
        /// </summary>
        /// <param name="id">The workflow ID.</param>
        /// <returns>The approval workflow model or null if not found.</returns>
        public ApprovalWorkflowModel GetById(Guid id)
        {
            try
            {
                var eql = $"SELECT *,$approval_step_workflow.id,$approval_rule_workflow.id FROM {ENTITY_NAME} WHERE id = @id";
                var eqlCommand = new EqlCommand(eql, new EqlParameter("id", id));
                var queryResult = eqlCommand.Execute();

                if (queryResult != null && queryResult.Any())
                {
                    var record = queryResult.First();
                    var model = MapToModel(record);
                    
                    // Count steps and rules from relation
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
                    
                    return model;
                }
            }
            catch (Exception)
            {
                // Return null if entity doesn't exist yet
            }

            return null;
        }

        /// <summary>
        /// Creates a new approval workflow.
        /// </summary>
        /// <param name="model">The workflow model to create.</param>
        /// <returns>The created workflow model with generated ID.</returns>
        /// <exception cref="ValidationException">Thrown when validation fails.</exception>
        public ApprovalWorkflowModel Create(ApprovalWorkflowModel model)
        {
            // Validate required fields
            ValidateModel(model);

            // Generate new ID
            model.Id = Guid.NewGuid();
            model.CreatedOn = DateTime.UtcNow;
            model.CreatedBy = SecurityContext.CurrentUser?.Id ?? Guid.Empty;

            // Create record
            var record = new EntityRecord();
            record["id"] = model.Id;
            record["name"] = model.Name;
            record["target_entity_name"] = model.TargetEntityName;
            record["is_enabled"] = model.IsEnabled;
            record["created_on"] = model.CreatedOn;
            record["created_by"] = model.CreatedBy;

            var response = RecMan.CreateRecord(ENTITY_NAME, record);
            if (!response.Success)
            {
                throw new ValidationException
                {
                    Message = response.Message
                };
            }

            return model;
        }

        /// <summary>
        /// Updates an existing approval workflow.
        /// </summary>
        /// <param name="model">The workflow model to update.</param>
        /// <returns>The updated workflow model.</returns>
        /// <exception cref="ValidationException">Thrown when validation fails or workflow not found.</exception>
        public ApprovalWorkflowModel Update(ApprovalWorkflowModel model)
        {
            // Validate required fields
            ValidateModel(model);

            // Verify workflow exists
            var existing = GetById(model.Id);
            if (existing == null)
            {
                throw new ValidationException
                {
                    Message = "Workflow not found."
                };
            }

            // Update record
            var record = new EntityRecord();
            record["id"] = model.Id;
            record["name"] = model.Name;
            record["target_entity_name"] = model.TargetEntityName;
            record["is_enabled"] = model.IsEnabled;

            var response = RecMan.UpdateRecord(ENTITY_NAME, record);
            if (!response.Success)
            {
                throw new ValidationException
                {
                    Message = response.Message
                };
            }

            return GetById(model.Id);
        }

        /// <summary>
        /// Deletes an approval workflow by ID.
        /// </summary>
        /// <param name="id">The workflow ID to delete.</param>
        /// <exception cref="ValidationException">Thrown when deletion fails or workflow not found.</exception>
        public void Delete(Guid id)
        {
            // Verify workflow exists
            var existing = GetById(id);
            if (existing == null)
            {
                throw new ValidationException
                {
                    Message = "Workflow not found."
                };
            }

            var response = RecMan.DeleteRecord(ENTITY_NAME, id);
            if (!response.Success)
            {
                throw new ValidationException
                {
                    Message = response.Message
                };
            }
        }

        /// <summary>
        /// Toggles the enabled status of a workflow.
        /// </summary>
        /// <param name="id">The workflow ID.</param>
        /// <param name="enabled">The new enabled status.</param>
        /// <returns>The updated workflow model.</returns>
        public ApprovalWorkflowModel SetEnabled(Guid id, bool enabled)
        {
            var existing = GetById(id);
            if (existing == null)
            {
                throw new ValidationException
                {
                    Message = "Workflow not found."
                };
            }

            var record = new EntityRecord();
            record["id"] = id;
            record["is_enabled"] = enabled;

            var response = RecMan.UpdateRecord(ENTITY_NAME, record);
            if (!response.Success)
            {
                throw new ValidationException
                {
                    Message = response.Message
                };
            }

            return GetById(id);
        }

        /// <summary>
        /// Validates the workflow model.
        /// </summary>
        /// <param name="model">The model to validate.</param>
        /// <exception cref="ValidationException">Thrown when validation fails.</exception>
        private void ValidateModel(ApprovalWorkflowModel model)
        {
            if (model == null)
            {
                throw new ValidationException
                {
                    Message = "Workflow model is required."
                };
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                throw new ValidationException
                {
                    Message = "Workflow name is required."
                };
            }

            if (model.Name.Length > 256)
            {
                throw new ValidationException
                {
                    Message = "Workflow name cannot exceed 256 characters."
                };
            }

            if (string.IsNullOrWhiteSpace(model.TargetEntityName))
            {
                throw new ValidationException
                {
                    Message = "Target entity name is required."
                };
            }

            if (model.TargetEntityName.Length > 128)
            {
                throw new ValidationException
                {
                    Message = "Target entity name cannot exceed 128 characters."
                };
            }
        }

        /// <summary>
        /// Maps an entity record to an ApprovalWorkflowModel.
        /// </summary>
        /// <param name="record">The entity record.</param>
        /// <returns>The mapped model.</returns>
        private ApprovalWorkflowModel MapToModel(EntityRecord record)
        {
            return new ApprovalWorkflowModel
            {
                Id = record["id"] != null ? (Guid)record["id"] : Guid.Empty,
                Name = record["name"]?.ToString() ?? string.Empty,
                TargetEntityName = record["target_entity_name"]?.ToString() ?? string.Empty,
                IsEnabled = record["is_enabled"] != null && (bool)record["is_enabled"],
                CreatedOn = record["created_on"] != null ? (DateTime)record["created_on"] : DateTime.MinValue,
                CreatedBy = record["created_by"] != null ? (Guid?)record["created_by"] : null
            };
        }
    }
}
