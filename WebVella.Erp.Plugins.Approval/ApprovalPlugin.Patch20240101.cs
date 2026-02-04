using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Web.Models;

namespace WebVella.Erp.Plugins.Approval
{
    /// <summary>
    /// Migration patch for creating the Approval plugin entity schemas.
    /// This patch creates 5 entities (approval_workflow, approval_step, approval_rule,
    /// approval_request, approval_history) and their relationships following WebVella's
    /// idempotent, transaction-scoped migration pattern.
    /// </summary>
    public partial class ApprovalPlugin : ErpPlugin
    {
        #region << Fixed GUIDs for Deterministic Provisioning >>
        
        // Entity GUIDs
        private static readonly Guid APPROVAL_WORKFLOW_ENTITY_ID = new Guid("F16B8D4A-1C2E-4A3F-9B5D-6E7C8F9A0B1D");
        private static readonly Guid APPROVAL_STEP_ENTITY_ID = new Guid("A2B3C4D5-E6F7-8901-ABCD-EF1234567890");
        private static readonly Guid APPROVAL_RULE_ENTITY_ID = new Guid("B3C4D5E6-F7A8-9012-BCDE-F12345678901");
        private static readonly Guid APPROVAL_REQUEST_ENTITY_ID = new Guid("C4D5E6F7-A8B9-0123-CDEF-123456789012");
        private static readonly Guid APPROVAL_HISTORY_ENTITY_ID = new Guid("D5E6F7A8-B9C0-1234-DEFA-234567890123");
        
        // Relation GUIDs
        private static readonly Guid WORKFLOW_STEP_RELATION_ID = new Guid("E6F7A8B9-C0D1-2345-EFAB-345678901234");
        private static readonly Guid STEP_RULE_RELATION_ID = new Guid("F7A8B9C0-D1E2-3456-FABC-456789012345");
        private static readonly Guid WORKFLOW_REQUEST_RELATION_ID = new Guid("A8B9C0D1-E2F3-4567-ABCD-567890123456");
        private static readonly Guid STEP_REQUEST_RELATION_ID = new Guid("B9C0D1E2-F3A4-5678-BCDE-678901234567");
        private static readonly Guid REQUEST_HISTORY_RELATION_ID = new Guid("C0D1E2F3-A4B5-6789-CDEF-789012345678");
        
        // Field GUIDs for approval_workflow entity
        private static readonly Guid WORKFLOW_ID_FIELD_ID = new Guid("D1E2F3A4-B5C6-7890-DEFA-890123456789");
        private static readonly Guid WORKFLOW_NAME_FIELD_ID = new Guid("E2F3A4B5-C6D7-8901-EFAB-901234567890");
        private static readonly Guid WORKFLOW_ENTITY_NAME_FIELD_ID = new Guid("F3A4B5C6-D7E8-9012-FABC-012345678901");
        private static readonly Guid WORKFLOW_IS_ACTIVE_FIELD_ID = new Guid("A4B5C6D7-E8F9-0123-ABCD-123456789ABC");
        private static readonly Guid WORKFLOW_CREATED_ON_FIELD_ID = new Guid("B5C6D7E8-F9A0-1234-BCDE-234567890BCD");
        private static readonly Guid WORKFLOW_CREATED_BY_FIELD_ID = new Guid("C6D7E8F9-A0B1-2345-CDEF-345678901CDE");
        private static readonly Guid WORKFLOW_DESCRIPTION_FIELD_ID = new Guid("D7E8F9A0-B1C2-3456-DEFA-456789012DEF");
        
        // Field GUIDs for approval_step entity
        private static readonly Guid STEP_ID_FIELD_ID = new Guid("E8F9A0B1-C2D3-4567-EFAB-567890123EFA");
        private static readonly Guid STEP_WORKFLOW_ID_FIELD_ID = new Guid("F9A0B1C2-D3E4-5678-FABC-678901234FAB");
        private static readonly Guid STEP_STEP_ORDER_FIELD_ID = new Guid("A0B1C2D3-E4F5-6789-ABCD-789012345ABC");
        private static readonly Guid STEP_NAME_FIELD_ID = new Guid("B1C2D3E4-F5A6-7890-BCDE-890123456BCD");
        private static readonly Guid STEP_APPROVER_TYPE_FIELD_ID = new Guid("C2D3E4F5-A6B7-8901-CDEF-901234567CDE");
        private static readonly Guid STEP_APPROVER_ID_FIELD_ID = new Guid("D3E4F5A6-B7C8-9012-DEFA-012345678DEF");
        private static readonly Guid STEP_SLA_HOURS_FIELD_ID = new Guid("E4F5A6B7-C8D9-0123-EFAB-123456789EFA");
        private static readonly Guid STEP_DESCRIPTION_FIELD_ID = new Guid("F5A6B7C8-D9E0-1234-FABC-234567890FAB");
        
        // Field GUIDs for approval_rule entity
        private static readonly Guid RULE_ID_FIELD_ID = new Guid("A6B7C8D9-E0F1-2345-ABCD-345678901ABC");
        private static readonly Guid RULE_STEP_ID_FIELD_ID = new Guid("B7C8D9E0-F1A2-3456-BCDE-456789012BCD");
        private static readonly Guid RULE_RULE_TYPE_FIELD_ID = new Guid("C8D9E0F1-A2B3-4567-CDEF-567890123CDE");
        private static readonly Guid RULE_FIELD_NAME_FIELD_ID = new Guid("D9E0F1A2-B3C4-5678-DEFA-678901234DEF");
        private static readonly Guid RULE_OPERATOR_FIELD_ID = new Guid("E0F1A2B3-C4D5-6789-EFAB-789012345EFA");
        private static readonly Guid RULE_VALUE_FIELD_ID = new Guid("F1A2B3C4-D5E6-7890-FABC-890123456FAB");
        private static readonly Guid RULE_ACTION_FIELD_ID = new Guid("A2B3C4D5-E6F7-8901-ABCD-901234567ABC");
        
        // Field GUIDs for approval_request entity
        private static readonly Guid REQUEST_ID_FIELD_ID = new Guid("B3C4D5E6-F7A8-9012-BCDE-012345678BCD");
        private static readonly Guid REQUEST_WORKFLOW_ID_FIELD_ID = new Guid("C4D5E6F7-A8B9-0123-CDEF-123456789CDE");
        private static readonly Guid REQUEST_ENTITY_NAME_FIELD_ID = new Guid("D5E6F7A8-B9C0-1234-DEFA-234567890DEF");
        private static readonly Guid REQUEST_RECORD_ID_FIELD_ID = new Guid("E6F7A8B9-C0D1-2345-EFAB-345678901EFA");
        private static readonly Guid REQUEST_CURRENT_STEP_ID_FIELD_ID = new Guid("F7A8B9C0-D1E2-3456-FABC-456789012FAB");
        private static readonly Guid REQUEST_STATUS_FIELD_ID = new Guid("A8B9C0D1-E2F3-4567-ABCD-567890123ABC");
        private static readonly Guid REQUEST_CREATED_ON_FIELD_ID = new Guid("B9C0D1E2-F3A4-5678-BCDE-678901234BCD");
        private static readonly Guid REQUEST_DUE_DATE_FIELD_ID = new Guid("C0D1E2F3-A4B5-6789-CDEF-789012345CDE");
        private static readonly Guid REQUEST_CREATED_BY_FIELD_ID = new Guid("D0E1F2A3-B4C5-6780-DEF0-890123456DEA");
        private static readonly Guid REQUEST_PRIORITY_FIELD_ID = new Guid("E1F2A3B4-C5D6-7891-EF01-901234567EFB");
        
        // Field GUIDs for approval_history entity
        private static readonly Guid HISTORY_ID_FIELD_ID = new Guid("D1E2F3A4-B5C6-7890-DEFA-890123456DEF");
        private static readonly Guid HISTORY_REQUEST_ID_FIELD_ID = new Guid("E2F3A4B5-C6D7-8901-EFAB-901234567EFA");
        private static readonly Guid HISTORY_STEP_ID_FIELD_ID = new Guid("F3A4B5C6-D7E8-9012-FABC-012345678FAB");
        private static readonly Guid HISTORY_ACTION_FIELD_ID = new Guid("A4B5C6D7-E8F9-0123-ABCD-123456789ABD");
        private static readonly Guid HISTORY_PERFORMED_BY_FIELD_ID = new Guid("B5C6D7E8-F9A0-1234-BCDE-234567890BCE");
        private static readonly Guid HISTORY_PERFORMED_ON_FIELD_ID = new Guid("C6D7E8F9-A0B1-2345-CDEF-345678901CDF");
        private static readonly Guid HISTORY_COMMENTS_FIELD_ID = new Guid("D7E8F9A0-B1C2-3456-DEFA-456789012DEA");
        
        #endregion

        /// <summary>
        /// Migration patch that creates the 5 entity schemas and their relationships.
        /// This patch is idempotent - it checks for existing entities/relations before creation.
        /// All operations are wrapped in the current transaction for atomicity.
        /// </summary>
        /// <param name="entMan">EntityManager instance for entity operations</param>
        /// <param name="relMan">EntityRelationManager instance for relation operations</param>
        /// <param name="recMan">RecordManager instance for record operations</param>
        private static void Patch20240101(EntityManager entMan, EntityRelationManager relMan, RecordManager recMan)
        {
            #region << Create approval_workflow entity >>
            {
                var existingEntity = entMan.ReadEntity(APPROVAL_WORKFLOW_ENTITY_ID);
                if (existingEntity.Object == null)
                {
                    var entity = new InputEntity
                    {
                        Id = APPROVAL_WORKFLOW_ENTITY_ID,
                        Name = "approval_workflow",
                        Label = "Approval Workflow",
                        LabelPlural = "Approval Workflows",
                        System = false,
                        IconName = "fa fa-check-double",
                        Color = "#4CAF50",
                        RecordPermissions = new RecordPermissions
                        {
                            CanRead = new List<Guid> { SystemIds.AdministratorRoleId, SystemIds.RegularRoleId },
                            CanCreate = new List<Guid> { SystemIds.AdministratorRoleId },
                            CanUpdate = new List<Guid> { SystemIds.AdministratorRoleId },
                            CanDelete = new List<Guid> { SystemIds.AdministratorRoleId }
                        }
                    };
                    
                    var createResponse = entMan.CreateEntity(entity);
                    if (!createResponse.Success)
                    {
                        throw new Exception($"Failed to create approval_workflow entity: {createResponse.Message}");
                    }
                    
                    // Create additional fields for approval_workflow
                    CreateWorkflowFields(entMan, APPROVAL_WORKFLOW_ENTITY_ID);
                }
            }
            #endregion

            #region << Create approval_step entity >>
            {
                var existingEntity = entMan.ReadEntity(APPROVAL_STEP_ENTITY_ID);
                if (existingEntity.Object == null)
                {
                    var entity = new InputEntity
                    {
                        Id = APPROVAL_STEP_ENTITY_ID,
                        Name = "approval_step",
                        Label = "Approval Step",
                        LabelPlural = "Approval Steps",
                        System = false,
                        IconName = "fa fa-list-ol",
                        Color = "#2196F3",
                        RecordPermissions = new RecordPermissions
                        {
                            CanRead = new List<Guid> { SystemIds.AdministratorRoleId, SystemIds.RegularRoleId },
                            CanCreate = new List<Guid> { SystemIds.AdministratorRoleId },
                            CanUpdate = new List<Guid> { SystemIds.AdministratorRoleId },
                            CanDelete = new List<Guid> { SystemIds.AdministratorRoleId }
                        }
                    };
                    
                    var createResponse = entMan.CreateEntity(entity);
                    if (!createResponse.Success)
                    {
                        throw new Exception($"Failed to create approval_step entity: {createResponse.Message}");
                    }
                    
                    // Create additional fields for approval_step
                    CreateStepFields(entMan, APPROVAL_STEP_ENTITY_ID);
                }
            }
            #endregion

            #region << Create approval_rule entity >>
            {
                var existingEntity = entMan.ReadEntity(APPROVAL_RULE_ENTITY_ID);
                if (existingEntity.Object == null)
                {
                    var entity = new InputEntity
                    {
                        Id = APPROVAL_RULE_ENTITY_ID,
                        Name = "approval_rule",
                        Label = "Approval Rule",
                        LabelPlural = "Approval Rules",
                        System = false,
                        IconName = "fa fa-filter",
                        Color = "#FF9800",
                        RecordPermissions = new RecordPermissions
                        {
                            CanRead = new List<Guid> { SystemIds.AdministratorRoleId, SystemIds.RegularRoleId },
                            CanCreate = new List<Guid> { SystemIds.AdministratorRoleId },
                            CanUpdate = new List<Guid> { SystemIds.AdministratorRoleId },
                            CanDelete = new List<Guid> { SystemIds.AdministratorRoleId }
                        }
                    };
                    
                    var createResponse = entMan.CreateEntity(entity);
                    if (!createResponse.Success)
                    {
                        throw new Exception($"Failed to create approval_rule entity: {createResponse.Message}");
                    }
                    
                    // Create additional fields for approval_rule
                    CreateRuleFields(entMan, APPROVAL_RULE_ENTITY_ID);
                }
            }
            #endregion

            #region << Create approval_request entity >>
            {
                var existingEntity = entMan.ReadEntity(APPROVAL_REQUEST_ENTITY_ID);
                if (existingEntity.Object == null)
                {
                    var entity = new InputEntity
                    {
                        Id = APPROVAL_REQUEST_ENTITY_ID,
                        Name = "approval_request",
                        Label = "Approval Request",
                        LabelPlural = "Approval Requests",
                        System = false,
                        IconName = "fa fa-file-signature",
                        Color = "#9C27B0",
                        RecordPermissions = new RecordPermissions
                        {
                            CanRead = new List<Guid> { SystemIds.AdministratorRoleId, SystemIds.RegularRoleId },
                            CanCreate = new List<Guid> { SystemIds.AdministratorRoleId, SystemIds.RegularRoleId },
                            CanUpdate = new List<Guid> { SystemIds.AdministratorRoleId, SystemIds.RegularRoleId },
                            CanDelete = new List<Guid> { SystemIds.AdministratorRoleId }
                        }
                    };
                    
                    var createResponse = entMan.CreateEntity(entity);
                    if (!createResponse.Success)
                    {
                        throw new Exception($"Failed to create approval_request entity: {createResponse.Message}");
                    }
                    
                    // Create additional fields for approval_request
                    CreateRequestFields(entMan, APPROVAL_REQUEST_ENTITY_ID);
                }
            }
            #endregion

            #region << Create approval_history entity >>
            {
                var existingEntity = entMan.ReadEntity(APPROVAL_HISTORY_ENTITY_ID);
                if (existingEntity.Object == null)
                {
                    var entity = new InputEntity
                    {
                        Id = APPROVAL_HISTORY_ENTITY_ID,
                        Name = "approval_history",
                        Label = "Approval History",
                        LabelPlural = "Approval History",
                        System = false,
                        IconName = "fa fa-history",
                        Color = "#607D8B",
                        RecordPermissions = new RecordPermissions
                        {
                            CanRead = new List<Guid> { SystemIds.AdministratorRoleId, SystemIds.RegularRoleId },
                            CanCreate = new List<Guid> { SystemIds.AdministratorRoleId, SystemIds.RegularRoleId },
                            CanUpdate = new List<Guid> { SystemIds.AdministratorRoleId },
                            CanDelete = new List<Guid> { SystemIds.AdministratorRoleId }
                        }
                    };
                    
                    var createResponse = entMan.CreateEntity(entity);
                    if (!createResponse.Success)
                    {
                        throw new Exception($"Failed to create approval_history entity: {createResponse.Message}");
                    }
                    
                    // Create additional fields for approval_history
                    CreateHistoryFields(entMan, APPROVAL_HISTORY_ENTITY_ID);
                }
            }
            #endregion

            #region << Create workflow -> step relation (1:N) >>
            {
                var existingRelation = relMan.Read(WORKFLOW_STEP_RELATION_ID);
                if (existingRelation.Object == null)
                {
                    var workflowEntity = entMan.ReadEntity(APPROVAL_WORKFLOW_ENTITY_ID).Object;
                    var stepEntity = entMan.ReadEntity(APPROVAL_STEP_ENTITY_ID).Object;
                    
                    if (workflowEntity != null && stepEntity != null)
                    {
                        var relation = new EntityRelation
                        {
                            Id = WORKFLOW_STEP_RELATION_ID,
                            Name = "approval_workflow_steps",
                            Label = "Workflow Steps",
                            Description = "Links approval workflow to its steps",
                            System = false,
                            RelationType = EntityRelationType.OneToMany,
                            OriginEntityId = workflowEntity.Id,
                            OriginFieldId = workflowEntity.Fields.First(f => f.Name == "id").Id,
                            TargetEntityId = stepEntity.Id,
                            TargetFieldId = stepEntity.Fields.First(f => f.Name == "workflow_id").Id
                        };
                        
                        var createResponse = relMan.Create(relation);
                        if (!createResponse.Success)
                        {
                            throw new Exception($"Failed to create workflow_steps relation: {createResponse.Message}");
                        }
                    }
                }
            }
            #endregion

            #region << Create step -> rule relation (1:N) >>
            {
                var existingRelation = relMan.Read(STEP_RULE_RELATION_ID);
                if (existingRelation.Object == null)
                {
                    var stepEntity = entMan.ReadEntity(APPROVAL_STEP_ENTITY_ID).Object;
                    var ruleEntity = entMan.ReadEntity(APPROVAL_RULE_ENTITY_ID).Object;
                    
                    if (stepEntity != null && ruleEntity != null)
                    {
                        var relation = new EntityRelation
                        {
                            Id = STEP_RULE_RELATION_ID,
                            Name = "approval_step_rules",
                            Label = "Step Rules",
                            Description = "Links approval step to its rules",
                            System = false,
                            RelationType = EntityRelationType.OneToMany,
                            OriginEntityId = stepEntity.Id,
                            OriginFieldId = stepEntity.Fields.First(f => f.Name == "id").Id,
                            TargetEntityId = ruleEntity.Id,
                            TargetFieldId = ruleEntity.Fields.First(f => f.Name == "step_id").Id
                        };
                        
                        var createResponse = relMan.Create(relation);
                        if (!createResponse.Success)
                        {
                            throw new Exception($"Failed to create step_rules relation: {createResponse.Message}");
                        }
                    }
                }
            }
            #endregion

            #region << Create workflow -> request relation (1:N) >>
            {
                var existingRelation = relMan.Read(WORKFLOW_REQUEST_RELATION_ID);
                if (existingRelation.Object == null)
                {
                    var workflowEntity = entMan.ReadEntity(APPROVAL_WORKFLOW_ENTITY_ID).Object;
                    var requestEntity = entMan.ReadEntity(APPROVAL_REQUEST_ENTITY_ID).Object;
                    
                    if (workflowEntity != null && requestEntity != null)
                    {
                        var relation = new EntityRelation
                        {
                            Id = WORKFLOW_REQUEST_RELATION_ID,
                            Name = "approval_workflow_requests",
                            Label = "Workflow Requests",
                            Description = "Links approval workflow to its requests",
                            System = false,
                            RelationType = EntityRelationType.OneToMany,
                            OriginEntityId = workflowEntity.Id,
                            OriginFieldId = workflowEntity.Fields.First(f => f.Name == "id").Id,
                            TargetEntityId = requestEntity.Id,
                            TargetFieldId = requestEntity.Fields.First(f => f.Name == "workflow_id").Id
                        };
                        
                        var createResponse = relMan.Create(relation);
                        if (!createResponse.Success)
                        {
                            throw new Exception($"Failed to create workflow_requests relation: {createResponse.Message}");
                        }
                    }
                }
            }
            #endregion

            #region << Create step -> request relation (1:N) for current step >>
            {
                var existingRelation = relMan.Read(STEP_REQUEST_RELATION_ID);
                if (existingRelation.Object == null)
                {
                    var stepEntity = entMan.ReadEntity(APPROVAL_STEP_ENTITY_ID).Object;
                    var requestEntity = entMan.ReadEntity(APPROVAL_REQUEST_ENTITY_ID).Object;
                    
                    if (stepEntity != null && requestEntity != null)
                    {
                        var relation = new EntityRelation
                        {
                            Id = STEP_REQUEST_RELATION_ID,
                            Name = "approval_step_requests",
                            Label = "Step Requests",
                            Description = "Links approval step to requests currently at this step",
                            System = false,
                            RelationType = EntityRelationType.OneToMany,
                            OriginEntityId = stepEntity.Id,
                            OriginFieldId = stepEntity.Fields.First(f => f.Name == "id").Id,
                            TargetEntityId = requestEntity.Id,
                            TargetFieldId = requestEntity.Fields.First(f => f.Name == "current_step_id").Id
                        };
                        
                        var createResponse = relMan.Create(relation);
                        if (!createResponse.Success)
                        {
                            throw new Exception($"Failed to create step_requests relation: {createResponse.Message}");
                        }
                    }
                }
            }
            #endregion

            #region << Create request -> history relation (1:N) >>
            {
                var existingRelation = relMan.Read(REQUEST_HISTORY_RELATION_ID);
                if (existingRelation.Object == null)
                {
                    var requestEntity = entMan.ReadEntity(APPROVAL_REQUEST_ENTITY_ID).Object;
                    var historyEntity = entMan.ReadEntity(APPROVAL_HISTORY_ENTITY_ID).Object;
                    
                    if (requestEntity != null && historyEntity != null)
                    {
                        var relation = new EntityRelation
                        {
                            Id = REQUEST_HISTORY_RELATION_ID,
                            Name = "approval_request_history",
                            Label = "Request History",
                            Description = "Links approval request to its history entries",
                            System = false,
                            RelationType = EntityRelationType.OneToMany,
                            OriginEntityId = requestEntity.Id,
                            OriginFieldId = requestEntity.Fields.First(f => f.Name == "id").Id,
                            TargetEntityId = historyEntity.Id,
                            TargetFieldId = historyEntity.Fields.First(f => f.Name == "request_id").Id
                        };
                        
                        var createResponse = relMan.Create(relation);
                        if (!createResponse.Success)
                        {
                            throw new Exception($"Failed to create request_history relation: {createResponse.Message}");
                        }
                    }
                }
            }
            #endregion
        }

        #region << Field Creation Helper Methods >>

        /// <summary>
        /// Creates all additional fields for the approval_workflow entity.
        /// Fields: name, entity_name, is_active, created_on, created_by, description
        /// </summary>
        /// <param name="entMan">EntityManager instance</param>
        /// <param name="entityId">Target entity ID</param>
        private static void CreateWorkflowFields(EntityManager entMan, Guid entityId)
        {
            // name field - required text field for workflow name
            {
                var field = new InputTextField
                {
                    Id = WORKFLOW_NAME_FIELD_ID,
                    Name = "name",
                    Label = "Name",
                    PlaceholderText = "Enter workflow name",
                    Description = "The name of the approval workflow",
                    HelpText = "A unique, descriptive name for this workflow",
                    Required = true,
                    Unique = true,
                    Searchable = true,
                    Auditable = true,
                    System = false,
                    MaxLength = 200
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create name field: {response.Message}");
                }
            }

            // entity_name field - text field for target entity name
            {
                var field = new InputTextField
                {
                    Id = WORKFLOW_ENTITY_NAME_FIELD_ID,
                    Name = "entity_name",
                    Label = "Target Entity",
                    PlaceholderText = "Enter entity name",
                    Description = "The name of the entity this workflow applies to",
                    HelpText = "The entity that triggers this approval workflow",
                    Required = true,
                    Unique = false,
                    Searchable = true,
                    Auditable = true,
                    System = false,
                    MaxLength = 100
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create entity_name field: {response.Message}");
                }
            }

            // is_active field - boolean for workflow active status
            {
                var field = new InputCheckboxField
                {
                    Id = WORKFLOW_IS_ACTIVE_FIELD_ID,
                    Name = "is_active",
                    Label = "Is Active",
                    PlaceholderText = "",
                    Description = "Whether this workflow is currently active",
                    HelpText = "Only active workflows will process new requests",
                    Required = true,
                    Unique = false,
                    Searchable = true,
                    Auditable = true,
                    System = false,
                    DefaultValue = true
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create is_active field: {response.Message}");
                }
            }

            // created_on field - datetime for record creation timestamp
            {
                var field = new InputDateTimeField
                {
                    Id = WORKFLOW_CREATED_ON_FIELD_ID,
                    Name = "created_on",
                    Label = "Created On",
                    PlaceholderText = "",
                    Description = "When this workflow was created",
                    HelpText = "Automatically set when the workflow is created",
                    Required = true,
                    Unique = false,
                    Searchable = true,
                    Auditable = false,
                    System = false,
                    UseCurrentTimeAsDefaultValue = true,
                    Format = "yyyy-MM-dd HH:mm:ss"
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create created_on field: {response.Message}");
                }
            }

            // created_by field - guid for user who created the workflow
            {
                var field = new InputGuidField
                {
                    Id = WORKFLOW_CREATED_BY_FIELD_ID,
                    Name = "created_by",
                    Label = "Created By",
                    PlaceholderText = "",
                    Description = "The user who created this workflow",
                    HelpText = "Reference to the user entity",
                    Required = false,
                    Unique = false,
                    Searchable = true,
                    Auditable = true,
                    System = false,
                    GenerateNewId = false
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create created_by field: {response.Message}");
                }
            }

            // description field - optional text for workflow description
            {
                var field = new InputMultiLineTextField
                {
                    Id = WORKFLOW_DESCRIPTION_FIELD_ID,
                    Name = "description",
                    Label = "Description",
                    PlaceholderText = "Enter workflow description",
                    Description = "Detailed description of this workflow",
                    HelpText = "Optional description to explain the workflow purpose",
                    Required = false,
                    Unique = false,
                    Searchable = true,
                    Auditable = true,
                    System = false,
                    MaxLength = 2000,
                    VisibleLineNumber = 4
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create description field: {response.Message}");
                }
            }
        }

        /// <summary>
        /// Creates all additional fields for the approval_step entity.
        /// Fields: workflow_id, step_order, name, approver_type, approver_id, sla_hours, description
        /// </summary>
        /// <param name="entMan">EntityManager instance</param>
        /// <param name="entityId">Target entity ID</param>
        private static void CreateStepFields(EntityManager entMan, Guid entityId)
        {
            // workflow_id field - FK reference to parent workflow
            {
                var field = new InputGuidField
                {
                    Id = STEP_WORKFLOW_ID_FIELD_ID,
                    Name = "workflow_id",
                    Label = "Workflow",
                    PlaceholderText = "",
                    Description = "Reference to the parent workflow",
                    HelpText = "The workflow this step belongs to",
                    Required = true,
                    Unique = false,
                    Searchable = true,
                    Auditable = true,
                    System = false,
                    GenerateNewId = false
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create workflow_id field: {response.Message}");
                }
            }

            // step_order field - integer for ordering steps in workflow
            {
                var field = new InputNumberField
                {
                    Id = STEP_STEP_ORDER_FIELD_ID,
                    Name = "step_order",
                    Label = "Step Order",
                    PlaceholderText = "Enter step order",
                    Description = "The order of this step in the workflow",
                    HelpText = "Steps are executed in ascending order",
                    Required = true,
                    Unique = false,
                    Searchable = true,
                    Auditable = true,
                    System = false,
                    DefaultValue = 1,
                    MinValue = 1,
                    MaxValue = 100,
                    DecimalPlaces = 0
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create step_order field: {response.Message}");
                }
            }

            // name field - step name
            {
                var field = new InputTextField
                {
                    Id = STEP_NAME_FIELD_ID,
                    Name = "name",
                    Label = "Name",
                    PlaceholderText = "Enter step name",
                    Description = "The name of this approval step",
                    HelpText = "A descriptive name for this step",
                    Required = true,
                    Unique = false,
                    Searchable = true,
                    Auditable = true,
                    System = false,
                    MaxLength = 200
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create name field: {response.Message}");
                }
            }

            // approver_type field - enum-like text for approver type (User, Role, Manager, Custom)
            {
                var field = new InputTextField
                {
                    Id = STEP_APPROVER_TYPE_FIELD_ID,
                    Name = "approver_type",
                    Label = "Approver Type",
                    PlaceholderText = "Select approver type",
                    Description = "The type of approver for this step",
                    HelpText = "User, Role, Manager, or Custom",
                    Required = true,
                    Unique = false,
                    Searchable = true,
                    Auditable = true,
                    System = false,
                    DefaultValue = "User",
                    MaxLength = 50
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create approver_type field: {response.Message}");
                }
            }

            // approver_id field - guid for specific approver (user or role ID)
            {
                var field = new InputGuidField
                {
                    Id = STEP_APPROVER_ID_FIELD_ID,
                    Name = "approver_id",
                    Label = "Approver",
                    PlaceholderText = "",
                    Description = "The ID of the approver (user or role)",
                    HelpText = "Reference to user or role depending on approver_type",
                    Required = false,
                    Unique = false,
                    Searchable = true,
                    Auditable = true,
                    System = false,
                    GenerateNewId = false
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create approver_id field: {response.Message}");
                }
            }

            // sla_hours field - integer for SLA duration in hours
            {
                var field = new InputNumberField
                {
                    Id = STEP_SLA_HOURS_FIELD_ID,
                    Name = "sla_hours",
                    Label = "SLA Hours",
                    PlaceholderText = "Enter SLA hours",
                    Description = "Service level agreement time in hours",
                    HelpText = "Hours before the request is considered overdue",
                    Required = false,
                    Unique = false,
                    Searchable = true,
                    Auditable = true,
                    System = false,
                    DefaultValue = 24,
                    MinValue = 1,
                    MaxValue = 720,
                    DecimalPlaces = 0
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create sla_hours field: {response.Message}");
                }
            }

            // description field - optional step description
            {
                var field = new InputMultiLineTextField
                {
                    Id = STEP_DESCRIPTION_FIELD_ID,
                    Name = "description",
                    Label = "Description",
                    PlaceholderText = "Enter step description",
                    Description = "Detailed description of this step",
                    HelpText = "Optional instructions for the approver",
                    Required = false,
                    Unique = false,
                    Searchable = false,
                    Auditable = true,
                    System = false,
                    MaxLength = 2000,
                    VisibleLineNumber = 3
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create description field: {response.Message}");
                }
            }
        }

        /// <summary>
        /// Creates all additional fields for the approval_rule entity.
        /// Fields: step_id, rule_type, field_name, operator, value, action
        /// </summary>
        /// <param name="entMan">EntityManager instance</param>
        /// <param name="entityId">Target entity ID</param>
        private static void CreateRuleFields(EntityManager entMan, Guid entityId)
        {
            // step_id field - FK reference to parent step
            {
                var field = new InputGuidField
                {
                    Id = RULE_STEP_ID_FIELD_ID,
                    Name = "step_id",
                    Label = "Step",
                    PlaceholderText = "",
                    Description = "Reference to the parent step",
                    HelpText = "The step this rule belongs to",
                    Required = true,
                    Unique = false,
                    Searchable = true,
                    Auditable = true,
                    System = false,
                    GenerateNewId = false
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create step_id field: {response.Message}");
                }
            }

            // rule_type field - text for rule type (condition, skip, etc.)
            {
                var field = new InputTextField
                {
                    Id = RULE_RULE_TYPE_FIELD_ID,
                    Name = "rule_type",
                    Label = "Rule Type",
                    PlaceholderText = "Select rule type",
                    Description = "The type of rule",
                    HelpText = "Condition, Skip, or Auto-approve",
                    Required = true,
                    Unique = false,
                    Searchable = true,
                    Auditable = true,
                    System = false,
                    DefaultValue = "Condition",
                    MaxLength = 50
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create rule_type field: {response.Message}");
                }
            }

            // field_name field - text for the entity field to evaluate
            {
                var field = new InputTextField
                {
                    Id = RULE_FIELD_NAME_FIELD_ID,
                    Name = "field_name",
                    Label = "Field Name",
                    PlaceholderText = "Enter field name",
                    Description = "The field to evaluate in the rule",
                    HelpText = "The name of the field from the target entity",
                    Required = false,
                    Unique = false,
                    Searchable = true,
                    Auditable = true,
                    System = false,
                    MaxLength = 100
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create field_name field: {response.Message}");
                }
            }

            // operator field - text for comparison operator
            {
                var field = new InputTextField
                {
                    Id = RULE_OPERATOR_FIELD_ID,
                    Name = "operator",
                    Label = "Operator",
                    PlaceholderText = "Select operator",
                    Description = "The comparison operator",
                    HelpText = "Equals, NotEquals, GreaterThan, LessThan, Contains, StartsWith",
                    Required = false,
                    Unique = false,
                    Searchable = true,
                    Auditable = true,
                    System = false,
                    DefaultValue = "Equals",
                    MaxLength = 50
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create operator field: {response.Message}");
                }
            }

            // value field - text for comparison value
            {
                var field = new InputTextField
                {
                    Id = RULE_VALUE_FIELD_ID,
                    Name = "value",
                    Label = "Value",
                    PlaceholderText = "Enter comparison value",
                    Description = "The value to compare against",
                    HelpText = "The expected value for the comparison",
                    Required = false,
                    Unique = false,
                    Searchable = false,
                    Auditable = true,
                    System = false,
                    MaxLength = 500
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create value field: {response.Message}");
                }
            }

            // action field - text for rule action on match
            {
                var field = new InputTextField
                {
                    Id = RULE_ACTION_FIELD_ID,
                    Name = "action",
                    Label = "Action",
                    PlaceholderText = "Select action",
                    Description = "The action to take when the rule matches",
                    HelpText = "Continue, Skip, AutoApprove, AutoReject",
                    Required = true,
                    Unique = false,
                    Searchable = true,
                    Auditable = true,
                    System = false,
                    DefaultValue = "Continue",
                    MaxLength = 50
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create action field: {response.Message}");
                }
            }
        }

        /// <summary>
        /// Creates all additional fields for the approval_request entity.
        /// Fields: workflow_id, entity_name, record_id, current_step_id, status, created_on, due_date, created_by, priority
        /// </summary>
        /// <param name="entMan">EntityManager instance</param>
        /// <param name="entityId">Target entity ID</param>
        private static void CreateRequestFields(EntityManager entMan, Guid entityId)
        {
            // workflow_id field - FK reference to workflow
            {
                var field = new InputGuidField
                {
                    Id = REQUEST_WORKFLOW_ID_FIELD_ID,
                    Name = "workflow_id",
                    Label = "Workflow",
                    PlaceholderText = "",
                    Description = "Reference to the workflow",
                    HelpText = "The workflow processing this request",
                    Required = true,
                    Unique = false,
                    Searchable = true,
                    Auditable = true,
                    System = false,
                    GenerateNewId = false
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create workflow_id field: {response.Message}");
                }
            }

            // entity_name field - text for source entity name
            {
                var field = new InputTextField
                {
                    Id = REQUEST_ENTITY_NAME_FIELD_ID,
                    Name = "entity_name",
                    Label = "Entity Name",
                    PlaceholderText = "",
                    Description = "The entity type of the record being approved",
                    HelpText = "Name of the source entity",
                    Required = true,
                    Unique = false,
                    Searchable = true,
                    Auditable = true,
                    System = false,
                    MaxLength = 100
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create entity_name field: {response.Message}");
                }
            }

            // record_id field - guid for the record being approved
            {
                var field = new InputGuidField
                {
                    Id = REQUEST_RECORD_ID_FIELD_ID,
                    Name = "record_id",
                    Label = "Record",
                    PlaceholderText = "",
                    Description = "The ID of the record being approved",
                    HelpText = "Reference to the source record",
                    Required = true,
                    Unique = false,
                    Searchable = true,
                    Auditable = true,
                    System = false,
                    GenerateNewId = false
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create record_id field: {response.Message}");
                }
            }

            // current_step_id field - FK reference to current step
            {
                var field = new InputGuidField
                {
                    Id = REQUEST_CURRENT_STEP_ID_FIELD_ID,
                    Name = "current_step_id",
                    Label = "Current Step",
                    PlaceholderText = "",
                    Description = "Reference to the current approval step",
                    HelpText = "The step this request is currently at",
                    Required = false,
                    Unique = false,
                    Searchable = true,
                    Auditable = true,
                    System = false,
                    GenerateNewId = false
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create current_step_id field: {response.Message}");
                }
            }

            // status field - text for request status
            {
                var field = new InputTextField
                {
                    Id = REQUEST_STATUS_FIELD_ID,
                    Name = "status",
                    Label = "Status",
                    PlaceholderText = "",
                    Description = "The current status of the request",
                    HelpText = "Pending, Approved, Rejected, Delegated, Escalated, Cancelled",
                    Required = true,
                    Unique = false,
                    Searchable = true,
                    Auditable = true,
                    System = false,
                    DefaultValue = "Pending",
                    MaxLength = 50
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create status field: {response.Message}");
                }
            }

            // created_on field - datetime for request creation
            {
                var field = new InputDateTimeField
                {
                    Id = REQUEST_CREATED_ON_FIELD_ID,
                    Name = "created_on",
                    Label = "Created On",
                    PlaceholderText = "",
                    Description = "When this request was created",
                    HelpText = "Automatically set when the request is created",
                    Required = true,
                    Unique = false,
                    Searchable = true,
                    Auditable = false,
                    System = false,
                    UseCurrentTimeAsDefaultValue = true,
                    Format = "yyyy-MM-dd HH:mm:ss"
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create created_on field: {response.Message}");
                }
            }

            // due_date field - datetime for request due date (SLA deadline)
            {
                var field = new InputDateTimeField
                {
                    Id = REQUEST_DUE_DATE_FIELD_ID,
                    Name = "due_date",
                    Label = "Due Date",
                    PlaceholderText = "",
                    Description = "When this request is due for approval",
                    HelpText = "Calculated from SLA hours",
                    Required = false,
                    Unique = false,
                    Searchable = true,
                    Auditable = true,
                    System = false,
                    UseCurrentTimeAsDefaultValue = false,
                    Format = "yyyy-MM-dd HH:mm:ss"
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create due_date field: {response.Message}");
                }
            }

            // created_by field - guid for user who initiated the request
            {
                var field = new InputGuidField
                {
                    Id = REQUEST_CREATED_BY_FIELD_ID,
                    Name = "created_by",
                    Label = "Created By",
                    PlaceholderText = "",
                    Description = "The user who initiated this request",
                    HelpText = "Reference to the user entity",
                    Required = false,
                    Unique = false,
                    Searchable = true,
                    Auditable = true,
                    System = false,
                    GenerateNewId = false
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create created_by field: {response.Message}");
                }
            }

            // priority field - integer for request priority
            {
                var field = new InputNumberField
                {
                    Id = REQUEST_PRIORITY_FIELD_ID,
                    Name = "priority",
                    Label = "Priority",
                    PlaceholderText = "Enter priority",
                    Description = "The priority level of this request",
                    HelpText = "1 = Low, 2 = Normal, 3 = High, 4 = Urgent",
                    Required = false,
                    Unique = false,
                    Searchable = true,
                    Auditable = true,
                    System = false,
                    DefaultValue = 2,
                    MinValue = 1,
                    MaxValue = 4,
                    DecimalPlaces = 0
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create priority field: {response.Message}");
                }
            }
        }

        /// <summary>
        /// Creates all additional fields for the approval_history entity.
        /// Fields: request_id, step_id, action, performed_by, performed_on, comments
        /// </summary>
        /// <param name="entMan">EntityManager instance</param>
        /// <param name="entityId">Target entity ID</param>
        private static void CreateHistoryFields(EntityManager entMan, Guid entityId)
        {
            // request_id field - FK reference to parent request
            {
                var field = new InputGuidField
                {
                    Id = HISTORY_REQUEST_ID_FIELD_ID,
                    Name = "request_id",
                    Label = "Request",
                    PlaceholderText = "",
                    Description = "Reference to the approval request",
                    HelpText = "The request this history entry belongs to",
                    Required = true,
                    Unique = false,
                    Searchable = true,
                    Auditable = false,
                    System = false,
                    GenerateNewId = false
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create request_id field: {response.Message}");
                }
            }

            // step_id field - guid for the step at which this action occurred
            {
                var field = new InputGuidField
                {
                    Id = HISTORY_STEP_ID_FIELD_ID,
                    Name = "step_id",
                    Label = "Step",
                    PlaceholderText = "",
                    Description = "The step at which this action occurred",
                    HelpText = "Reference to the approval step",
                    Required = false,
                    Unique = false,
                    Searchable = true,
                    Auditable = false,
                    System = false,
                    GenerateNewId = false
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create step_id field: {response.Message}");
                }
            }

            // action field - text for the action taken
            {
                var field = new InputTextField
                {
                    Id = HISTORY_ACTION_FIELD_ID,
                    Name = "action",
                    Label = "Action",
                    PlaceholderText = "",
                    Description = "The action that was taken",
                    HelpText = "Approve, Reject, Delegate, Escalate, Comment, etc.",
                    Required = true,
                    Unique = false,
                    Searchable = true,
                    Auditable = false,
                    System = false,
                    MaxLength = 50
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create action field: {response.Message}");
                }
            }

            // performed_by field - guid for user who performed the action
            {
                var field = new InputGuidField
                {
                    Id = HISTORY_PERFORMED_BY_FIELD_ID,
                    Name = "performed_by",
                    Label = "Performed By",
                    PlaceholderText = "",
                    Description = "The user who performed this action",
                    HelpText = "Reference to the user entity",
                    Required = true,
                    Unique = false,
                    Searchable = true,
                    Auditable = false,
                    System = false,
                    GenerateNewId = false
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create performed_by field: {response.Message}");
                }
            }

            // performed_on field - datetime for when the action occurred
            {
                var field = new InputDateTimeField
                {
                    Id = HISTORY_PERFORMED_ON_FIELD_ID,
                    Name = "performed_on",
                    Label = "Performed On",
                    PlaceholderText = "",
                    Description = "When this action was performed",
                    HelpText = "Automatically set when the action is recorded",
                    Required = true,
                    Unique = false,
                    Searchable = true,
                    Auditable = false,
                    System = false,
                    UseCurrentTimeAsDefaultValue = true,
                    Format = "yyyy-MM-dd HH:mm:ss"
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create performed_on field: {response.Message}");
                }
            }

            // comments field - multiline text for action comments
            {
                var field = new InputMultiLineTextField
                {
                    Id = HISTORY_COMMENTS_FIELD_ID,
                    Name = "comments",
                    Label = "Comments",
                    PlaceholderText = "Enter comments",
                    Description = "Comments or notes about the action",
                    HelpText = "Optional comments from the approver",
                    Required = false,
                    Unique = false,
                    Searchable = true,
                    Auditable = false,
                    System = false,
                    MaxLength = 4000,
                    VisibleLineNumber = 4
                };
                var response = entMan.CreateField(entityId, field);
                if (!response.Success)
                {
                    throw new Exception($"Failed to create comments field: {response.Message}");
                }
            }
        }

        #endregion
    }
}
