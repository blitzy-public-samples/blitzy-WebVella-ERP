using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;

namespace WebVella.Erp.Plugins.Approval
{
	/// <summary>
	/// Initial entity schema migration patch for the Approval plugin.
	/// Creates the five core entities: approval_workflow, approval_step, approval_rule,
	/// approval_request, and approval_history with complete field definitions and relationships.
	/// </summary>
	public partial class ApprovalPlugin : ErpPlugin
	{
		#region Entity GUIDs
		// Entity IDs
		private static readonly Guid APPROVAL_WORKFLOW_ENTITY_ID = new Guid("a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d");
		private static readonly Guid APPROVAL_STEP_ENTITY_ID = new Guid("b2c3d4e5-f6a7-5b6c-9d0e-1f2a3b4c5d6e");
		private static readonly Guid APPROVAL_RULE_ENTITY_ID = new Guid("c3d4e5f6-a7b8-6c7d-0e1f-2a3b4c5d6e7f");
		private static readonly Guid APPROVAL_REQUEST_ENTITY_ID = new Guid("d4e5f6a7-b8c9-7d8e-1f2a-3b4c5d6e7f8a");
		private static readonly Guid APPROVAL_HISTORY_ENTITY_ID = new Guid("e5f6a7b8-c9d0-8e9f-2a3b-4c5d6e7f8a9b");

		// System Field IDs for each entity's "id" field
		private static readonly Guid WORKFLOW_ID_FIELD_ID = new Guid("f6a7b8c9-d0e1-9f0a-3b4c-5d6e7f8a9b0c");
		private static readonly Guid STEP_ID_FIELD_ID = new Guid("a7b8c9d0-e1f2-0a1b-4c5d-6e7f8a9b0c1d");
		private static readonly Guid RULE_ID_FIELD_ID = new Guid("b8c9d0e1-f2a3-1b2c-5d6e-7f8a9b0c1d2e");
		private static readonly Guid REQUEST_ID_FIELD_ID = new Guid("c9d0e1f2-a3b4-2c3d-6e7f-8a9b0c1d2e3f");
		private static readonly Guid HISTORY_ID_FIELD_ID = new Guid("d0e1f2a3-b4c5-3d4e-7f8a-9b0c1d2e3f4a");

		// Relation IDs
		private static readonly Guid WORKFLOW_STEP_RELATION_ID = new Guid("e1f2a3b4-c5d6-4e5f-8a9b-0c1d2e3f4a5b");
		private static readonly Guid WORKFLOW_RULE_RELATION_ID = new Guid("f2a3b4c5-d6e7-5f6a-9b0c-1d2e3f4a5b6c");
		private static readonly Guid WORKFLOW_REQUEST_RELATION_ID = new Guid("a3b4c5d6-e7f8-6a7b-0c1d-2e3f4a5b6c7d");
		private static readonly Guid STEP_REQUEST_RELATION_ID = new Guid("b4c5d6e7-f8a9-7b8c-1d2e-3f4a5b6c7d8e");
		private static readonly Guid REQUEST_HISTORY_RELATION_ID = new Guid("c5d6e7f8-a9b0-8c9d-2e3f-4a5b6c7d8e9f");
		private static readonly Guid USER_CREATED_BY_WORKFLOW_RELATION_ID = new Guid("d6e7f8a9-b0c1-9d0e-3f4a-5b6c7d8e9f0a");
		private static readonly Guid USER_REQUESTED_BY_RELATION_ID = new Guid("e7f8a9b0-c1d2-0e1f-4a5b-6c7d8e9f0a1b");
		private static readonly Guid USER_PERFORMED_BY_RELATION_ID = new Guid("f8a9b0c1-d2e3-1f2a-5b6c-7d8e9f0a1b2c");
		private static readonly Guid HISTORY_STEP_RELATION_ID = new Guid("a9b0c1d2-e3f4-2a3b-6c7d-8e9f0a1b2c3d");
		#endregion

		#region Field GUIDs
		// approval_workflow fields
		private static readonly Guid WORKFLOW_NAME_FIELD_ID = new Guid("10a1b2c3-d4e5-f6a7-b8c9-d0e1f2a3b4c5");
		private static readonly Guid WORKFLOW_TARGET_ENTITY_NAME_FIELD_ID = new Guid("20b2c3d4-e5f6-a7b8-c9d0-e1f2a3b4c5d6");
		private static readonly Guid WORKFLOW_IS_ENABLED_FIELD_ID = new Guid("30c3d4e5-f6a7-b8c9-d0e1-f2a3b4c5d6e7");
		private static readonly Guid WORKFLOW_CREATED_ON_FIELD_ID = new Guid("40d4e5f6-a7b8-c9d0-e1f2-a3b4c5d6e7f8");
		private static readonly Guid WORKFLOW_CREATED_BY_FIELD_ID = new Guid("50e5f6a7-b8c9-d0e1-f2a3-b4c5d6e7f8a9");
		private static readonly Guid WORKFLOW_DESCRIPTION_FIELD_ID = new Guid("60f6a7b8-c9d0-e1f2-a3b4-c5d6e7f8a9b0");

		// approval_step fields
		private static readonly Guid STEP_WORKFLOW_ID_FIELD_ID = new Guid("11a1b2c3-d4e5-f6a7-b8c9-d0e1f2a3b4c6");
		private static readonly Guid STEP_ORDER_FIELD_ID = new Guid("21b2c3d4-e5f6-a7b8-c9d0-e1f2a3b4c5d7");
		private static readonly Guid STEP_NAME_FIELD_ID = new Guid("31c3d4e5-f6a7-b8c9-d0e1-f2a3b4c5d6e8");
		private static readonly Guid STEP_APPROVER_TYPE_FIELD_ID = new Guid("41d4e5f6-a7b8-c9d0-e1f2-a3b4c5d6e7f9");
		private static readonly Guid STEP_APPROVER_ID_FIELD_ID = new Guid("51e5f6a7-b8c9-d0e1-f2a3-b4c5d6e7f8aa");
		private static readonly Guid STEP_TIMEOUT_HOURS_FIELD_ID = new Guid("61f6a7b8-c9d0-e1f2-a3b4-c5d6e7f8a9b1");
		private static readonly Guid STEP_IS_FINAL_FIELD_ID = new Guid("71a7b8c9-d0e1-f2a3-b4c5-d6e7f8a9b0c1");

		// approval_rule fields
		private static readonly Guid RULE_WORKFLOW_ID_FIELD_ID = new Guid("12a1b2c3-d4e5-f6a7-b8c9-d0e1f2a3b4c7");
		private static readonly Guid RULE_NAME_FIELD_ID = new Guid("22b2c3d4-e5f6-a7b8-c9d0-e1f2a3b4c5d8");
		private static readonly Guid RULE_FIELD_NAME_FIELD_ID = new Guid("32c3d4e5-f6a7-b8c9-d0e1-f2a3b4c5d6e9");
		private static readonly Guid RULE_OPERATOR_FIELD_ID = new Guid("42d4e5f6-a7b8-c9d0-e1f2-a3b4c5d6e7fa");
		private static readonly Guid RULE_VALUE_FIELD_ID = new Guid("52e5f6a7-b8c9-d0e1-f2a3-b4c5d6e7f8ab");
		private static readonly Guid RULE_PRIORITY_FIELD_ID = new Guid("62f6a7b8-c9d0-e1f2-a3b4-c5d6e7f8a9b2");

		// approval_request fields
		private static readonly Guid REQUEST_WORKFLOW_ID_FIELD_ID = new Guid("13a1b2c3-d4e5-f6a7-b8c9-d0e1f2a3b4c8");
		private static readonly Guid REQUEST_CURRENT_STEP_ID_FIELD_ID = new Guid("23b2c3d4-e5f6-a7b8-c9d0-e1f2a3b4c5d9");
		private static readonly Guid REQUEST_SOURCE_ENTITY_NAME_FIELD_ID = new Guid("33c3d4e5-f6a7-b8c9-d0e1-f2a3b4c5d6ea");
		private static readonly Guid REQUEST_SOURCE_RECORD_ID_FIELD_ID = new Guid("43d4e5f6-a7b8-c9d0-e1f2-a3b4c5d6e7fb");
		private static readonly Guid REQUEST_STATUS_FIELD_ID = new Guid("53e5f6a7-b8c9-d0e1-f2a3-b4c5d6e7f8ac");
		private static readonly Guid REQUEST_REQUESTED_BY_FIELD_ID = new Guid("63f6a7b8-c9d0-e1f2-a3b4-c5d6e7f8a9b3");
		private static readonly Guid REQUEST_REQUESTED_ON_FIELD_ID = new Guid("73a7b8c9-d0e1-f2a3-b4c5-d6e7f8a9b0c2");
		private static readonly Guid REQUEST_COMPLETED_ON_FIELD_ID = new Guid("83b8c9d0-e1f2-a3b4-c5d6-e7f8a9b0c1d3");
		private static readonly Guid REQUEST_TITLE_FIELD_ID = new Guid("93c9d0e1-f2a3-b4c5-d6e7-f8a9b0c1d2e4");

		// approval_history fields
		private static readonly Guid HISTORY_REQUEST_ID_FIELD_ID = new Guid("14a1b2c3-d4e5-f6a7-b8c9-d0e1f2a3b4c9");
		private static readonly Guid HISTORY_STEP_ID_FIELD_ID = new Guid("24b2c3d4-e5f6-a7b8-c9d0-e1f2a3b4c5da");
		private static readonly Guid HISTORY_ACTION_FIELD_ID = new Guid("34c3d4e5-f6a7-b8c9-d0e1-f2a3b4c5d6eb");
		private static readonly Guid HISTORY_PERFORMED_BY_FIELD_ID = new Guid("44d4e5f6-a7b8-c9d0-e1f2-a3b4c5d6e7fc");
		private static readonly Guid HISTORY_PERFORMED_ON_FIELD_ID = new Guid("54e5f6a7-b8c9-d0e1-f2a3-b4c5d6e7f8ad");
		private static readonly Guid HISTORY_COMMENTS_FIELD_ID = new Guid("64f6a7b8-c9d0-e1f2-a3b4-c5d6e7f8a9b4");
		#endregion

		/// <summary>
		/// Migration patch for 2026-01-23 - Creates initial approval workflow entities and relationships.
		/// </summary>
		/// <param name="entMan">Entity manager for creating entities and fields</param>
		/// <param name="relMan">Relation manager for creating entity relationships</param>
		/// <param name="recMan">Record manager for data operations (not used in schema creation)</param>
		private static void Patch20260123(EntityManager entMan, EntityRelationManager relMan, RecordManager recMan)
		{
			// Standard Role GUIDs from WebVella.Erp.Api.SystemIds
			var administratorRoleId = new Guid("bdc56420-caf0-4030-8a0e-d264938e0cda");
			var regularRoleId = new Guid("f16ec6db-626d-4c27-8de0-3e7ce542c55f");
			var userEntityId = new Guid("b9cebc3b-6443-452a-8e34-b311a73dcc8b");

			#region << ***Create entity*** Entity name: approval_workflow >>
			{
				#region << entity >>
				{
					var entity = new InputEntity();
					var systemFieldIdDictionary = new Dictionary<string, Guid>();
					systemFieldIdDictionary["id"] = WORKFLOW_ID_FIELD_ID;
					entity.Id = APPROVAL_WORKFLOW_ENTITY_ID;
					entity.Name = "approval_workflow";
					entity.Label = "Approval Workflow";
					entity.LabelPlural = "Approval Workflows";
					entity.System = false;
					entity.IconName = "fas fa-project-diagram";
					entity.Color = "#7c3aed";
					entity.RecordScreenIdField = null;
					entity.RecordPermissions = new RecordPermissions();
					entity.RecordPermissions.CanCreate = new List<Guid>();
					entity.RecordPermissions.CanRead = new List<Guid>();
					entity.RecordPermissions.CanUpdate = new List<Guid>();
					entity.RecordPermissions.CanDelete = new List<Guid>();
					// Create - Administrator only
					entity.RecordPermissions.CanCreate.Add(administratorRoleId);
					// Read - Administrator and Regular users
					entity.RecordPermissions.CanRead.Add(administratorRoleId);
					entity.RecordPermissions.CanRead.Add(regularRoleId);
					// Update - Administrator only
					entity.RecordPermissions.CanUpdate.Add(administratorRoleId);
					// Delete - Administrator only
					entity.RecordPermissions.CanDelete.Add(administratorRoleId);
					{
						var response = entMan.CreateEntity(entity, systemFieldIdDictionary);
						if (!response.Success)
							throw new Exception("System error 10050. Entity: approval_workflow creation Message: " + response.Message);
					}
				}
				#endregion
			}
			#endregion

			#region << ***Create field*** Entity: approval_workflow Field Name: name >>
			{
				InputTextField textboxField = new InputTextField();
				textboxField.Id = WORKFLOW_NAME_FIELD_ID;
				textboxField.Name = "name";
				textboxField.Label = "Name";
				textboxField.PlaceholderText = "Enter workflow name";
				textboxField.Description = "Unique name for this approval workflow";
				textboxField.HelpText = null;
				textboxField.Required = true;
				textboxField.Unique = true;
				textboxField.Searchable = true;
				textboxField.Auditable = false;
				textboxField.System = false;
				textboxField.DefaultValue = "";
				textboxField.MaxLength = 256;
				textboxField.EnableSecurity = false;
				textboxField.Permissions = new FieldPermissions();
				textboxField.Permissions.CanRead = new List<Guid>();
				textboxField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_WORKFLOW_ENTITY_ID, textboxField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_workflow Field: name Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_workflow Field Name: target_entity_name >>
			{
				InputTextField textboxField = new InputTextField();
				textboxField.Id = WORKFLOW_TARGET_ENTITY_NAME_FIELD_ID;
				textboxField.Name = "target_entity_name";
				textboxField.Label = "Target Entity Name";
				textboxField.PlaceholderText = "e.g., purchase_order";
				textboxField.Description = "The entity name this workflow applies to";
				textboxField.HelpText = null;
				textboxField.Required = true;
				textboxField.Unique = false;
				textboxField.Searchable = true;
				textboxField.Auditable = false;
				textboxField.System = false;
				textboxField.DefaultValue = "";
				textboxField.MaxLength = 128;
				textboxField.EnableSecurity = false;
				textboxField.Permissions = new FieldPermissions();
				textboxField.Permissions.CanRead = new List<Guid>();
				textboxField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_WORKFLOW_ENTITY_ID, textboxField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_workflow Field: target_entity_name Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_workflow Field Name: description >>
			{
				InputMultiLineTextField textareaField = new InputMultiLineTextField();
				textareaField.Id = WORKFLOW_DESCRIPTION_FIELD_ID;
				textareaField.Name = "description";
				textareaField.Label = "Description";
				textareaField.PlaceholderText = "Enter workflow description";
				textareaField.Description = "Optional description of this workflow";
				textareaField.HelpText = null;
				textareaField.Required = false;
				textareaField.Unique = false;
				textareaField.Searchable = false;
				textareaField.Auditable = false;
				textareaField.System = false;
				textareaField.DefaultValue = null;
				textareaField.MaxLength = null;
				textareaField.VisibleLineNumber = 4;
				textareaField.EnableSecurity = false;
				textareaField.Permissions = new FieldPermissions();
				textareaField.Permissions.CanRead = new List<Guid>();
				textareaField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_WORKFLOW_ENTITY_ID, textareaField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_workflow Field: description Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_workflow Field Name: is_enabled >>
			{
				InputCheckboxField checkboxField = new InputCheckboxField();
				checkboxField.Id = WORKFLOW_IS_ENABLED_FIELD_ID;
				checkboxField.Name = "is_enabled";
				checkboxField.Label = "Is Enabled";
				checkboxField.PlaceholderText = null;
				checkboxField.Description = "Whether this workflow is currently active";
				checkboxField.HelpText = null;
				checkboxField.Required = true;
				checkboxField.Unique = false;
				checkboxField.Searchable = true;
				checkboxField.Auditable = false;
				checkboxField.System = false;
				checkboxField.DefaultValue = true;
				checkboxField.EnableSecurity = false;
				checkboxField.Permissions = new FieldPermissions();
				checkboxField.Permissions.CanRead = new List<Guid>();
				checkboxField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_WORKFLOW_ENTITY_ID, checkboxField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_workflow Field: is_enabled Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_workflow Field Name: created_on >>
			{
				InputDateTimeField datetimeField = new InputDateTimeField();
				datetimeField.Id = WORKFLOW_CREATED_ON_FIELD_ID;
				datetimeField.Name = "created_on";
				datetimeField.Label = "Created On";
				datetimeField.PlaceholderText = null;
				datetimeField.Description = "When this workflow was created";
				datetimeField.HelpText = null;
				datetimeField.Required = true;
				datetimeField.Unique = false;
				datetimeField.Searchable = true;
				datetimeField.Auditable = false;
				datetimeField.System = false;
				datetimeField.DefaultValue = null;
				datetimeField.Format = "yyyy-MMM-dd HH:mm";
				datetimeField.UseCurrentTimeAsDefaultValue = true;
				datetimeField.EnableSecurity = false;
				datetimeField.Permissions = new FieldPermissions();
				datetimeField.Permissions.CanRead = new List<Guid>();
				datetimeField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_WORKFLOW_ENTITY_ID, datetimeField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_workflow Field: created_on Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_workflow Field Name: created_by >>
			{
				InputGuidField guidField = new InputGuidField();
				guidField.Id = WORKFLOW_CREATED_BY_FIELD_ID;
				guidField.Name = "created_by";
				guidField.Label = "Created By";
				guidField.PlaceholderText = null;
				guidField.Description = "User who created this workflow";
				guidField.HelpText = null;
				guidField.Required = false;
				guidField.Unique = false;
				guidField.Searchable = true;
				guidField.Auditable = false;
				guidField.System = false;
				guidField.DefaultValue = null;
				guidField.GenerateNewId = false;
				guidField.EnableSecurity = false;
				guidField.Permissions = new FieldPermissions();
				guidField.Permissions.CanRead = new List<Guid>();
				guidField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_WORKFLOW_ENTITY_ID, guidField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_workflow Field: created_by Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create entity*** Entity name: approval_step >>
			{
				#region << entity >>
				{
					var entity = new InputEntity();
					var systemFieldIdDictionary = new Dictionary<string, Guid>();
					systemFieldIdDictionary["id"] = STEP_ID_FIELD_ID;
					entity.Id = APPROVAL_STEP_ENTITY_ID;
					entity.Name = "approval_step";
					entity.Label = "Approval Step";
					entity.LabelPlural = "Approval Steps";
					entity.System = false;
					entity.IconName = "fas fa-shoe-prints";
					entity.Color = "#059669";
					entity.RecordScreenIdField = null;
					entity.RecordPermissions = new RecordPermissions();
					entity.RecordPermissions.CanCreate = new List<Guid>();
					entity.RecordPermissions.CanRead = new List<Guid>();
					entity.RecordPermissions.CanUpdate = new List<Guid>();
					entity.RecordPermissions.CanDelete = new List<Guid>();
					// Create - Administrator only
					entity.RecordPermissions.CanCreate.Add(administratorRoleId);
					// Read - Administrator and Regular users
					entity.RecordPermissions.CanRead.Add(administratorRoleId);
					entity.RecordPermissions.CanRead.Add(regularRoleId);
					// Update - Administrator only
					entity.RecordPermissions.CanUpdate.Add(administratorRoleId);
					// Delete - Administrator only
					entity.RecordPermissions.CanDelete.Add(administratorRoleId);
					{
						var response = entMan.CreateEntity(entity, systemFieldIdDictionary);
						if (!response.Success)
							throw new Exception("System error 10050. Entity: approval_step creation Message: " + response.Message);
					}
				}
				#endregion
			}
			#endregion

			#region << ***Create field*** Entity: approval_step Field Name: workflow_id >>
			{
				InputGuidField guidField = new InputGuidField();
				guidField.Id = STEP_WORKFLOW_ID_FIELD_ID;
				guidField.Name = "workflow_id";
				guidField.Label = "Workflow";
				guidField.PlaceholderText = null;
				guidField.Description = "The workflow this step belongs to";
				guidField.HelpText = null;
				guidField.Required = false;
				guidField.Unique = false;
				guidField.Searchable = true;
				guidField.Auditable = false;
				guidField.System = false;
				guidField.DefaultValue = null;
				guidField.GenerateNewId = false;
				guidField.EnableSecurity = false;
				guidField.Permissions = new FieldPermissions();
				guidField.Permissions.CanRead = new List<Guid>();
				guidField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_STEP_ENTITY_ID, guidField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_step Field: workflow_id Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_step Field Name: step_order >>
			{
				InputNumberField numberField = new InputNumberField();
				numberField.Id = STEP_ORDER_FIELD_ID;
				numberField.Name = "step_order";
				numberField.Label = "Step Order";
				numberField.PlaceholderText = null;
				numberField.Description = "The sequence order of this step in the workflow";
				numberField.HelpText = null;
				numberField.Required = true;
				numberField.Unique = false;
				numberField.Searchable = true;
				numberField.Auditable = false;
				numberField.System = false;
				numberField.DefaultValue = Decimal.Parse("1");
				numberField.MinValue = Decimal.Parse("1");
				numberField.MaxValue = Decimal.Parse("100");
				numberField.DecimalPlaces = byte.Parse("0");
				numberField.EnableSecurity = false;
				numberField.Permissions = new FieldPermissions();
				numberField.Permissions.CanRead = new List<Guid>();
				numberField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_STEP_ENTITY_ID, numberField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_step Field: step_order Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_step Field Name: name >>
			{
				InputTextField textboxField = new InputTextField();
				textboxField.Id = STEP_NAME_FIELD_ID;
				textboxField.Name = "name";
				textboxField.Label = "Name";
				textboxField.PlaceholderText = "Enter step name";
				textboxField.Description = "Name of this approval step";
				textboxField.HelpText = null;
				textboxField.Required = true;
				textboxField.Unique = false;
				textboxField.Searchable = true;
				textboxField.Auditable = false;
				textboxField.System = false;
				textboxField.DefaultValue = "";
				textboxField.MaxLength = 256;
				textboxField.EnableSecurity = false;
				textboxField.Permissions = new FieldPermissions();
				textboxField.Permissions.CanRead = new List<Guid>();
				textboxField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_STEP_ENTITY_ID, textboxField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_step Field: name Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_step Field Name: approver_type >>
			{
				InputSelectField dropdownField = new InputSelectField();
				dropdownField.Id = STEP_APPROVER_TYPE_FIELD_ID;
				dropdownField.Name = "approver_type";
				dropdownField.Label = "Approver Type";
				dropdownField.PlaceholderText = null;
				dropdownField.Description = "Type of approver for this step";
				dropdownField.HelpText = null;
				dropdownField.Required = true;
				dropdownField.Unique = false;
				dropdownField.Searchable = true;
				dropdownField.Auditable = false;
				dropdownField.System = false;
				dropdownField.DefaultValue = "role";
				dropdownField.Options = new List<SelectOption>
				{
					new SelectOption() { Label = "Role", Value = "role", IconClass = "fas fa-users", Color = "#3b82f6"},
					new SelectOption() { Label = "User", Value = "user", IconClass = "fas fa-user", Color = "#10b981"},
					new SelectOption() { Label = "Department Head", Value = "department_head", IconClass = "fas fa-user-tie", Color = "#f59e0b"}
				};
				dropdownField.EnableSecurity = false;
				dropdownField.Permissions = new FieldPermissions();
				dropdownField.Permissions.CanRead = new List<Guid>();
				dropdownField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_STEP_ENTITY_ID, dropdownField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_step Field: approver_type Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_step Field Name: approver_id >>
			{
				InputGuidField guidField = new InputGuidField();
				guidField.Id = STEP_APPROVER_ID_FIELD_ID;
				guidField.Name = "approver_id";
				guidField.Label = "Approver ID";
				guidField.PlaceholderText = null;
				guidField.Description = "The ID of the role or user who can approve this step";
				guidField.HelpText = null;
				guidField.Required = false;
				guidField.Unique = false;
				guidField.Searchable = true;
				guidField.Auditable = false;
				guidField.System = false;
				guidField.DefaultValue = null;
				guidField.GenerateNewId = false;
				guidField.EnableSecurity = false;
				guidField.Permissions = new FieldPermissions();
				guidField.Permissions.CanRead = new List<Guid>();
				guidField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_STEP_ENTITY_ID, guidField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_step Field: approver_id Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_step Field Name: timeout_hours >>
			{
				InputNumberField numberField = new InputNumberField();
				numberField.Id = STEP_TIMEOUT_HOURS_FIELD_ID;
				numberField.Name = "timeout_hours";
				numberField.Label = "Timeout (Hours)";
				numberField.PlaceholderText = null;
				numberField.Description = "Hours before this step times out (null = no timeout)";
				numberField.HelpText = null;
				numberField.Required = false;
				numberField.Unique = false;
				numberField.Searchable = false;
				numberField.Auditable = false;
				numberField.System = false;
				numberField.DefaultValue = null;
				numberField.MinValue = Decimal.Parse("1");
				numberField.MaxValue = Decimal.Parse("8760"); // 1 year in hours
				numberField.DecimalPlaces = byte.Parse("0");
				numberField.EnableSecurity = false;
				numberField.Permissions = new FieldPermissions();
				numberField.Permissions.CanRead = new List<Guid>();
				numberField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_STEP_ENTITY_ID, numberField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_step Field: timeout_hours Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_step Field Name: is_final >>
			{
				InputCheckboxField checkboxField = new InputCheckboxField();
				checkboxField.Id = STEP_IS_FINAL_FIELD_ID;
				checkboxField.Name = "is_final";
				checkboxField.Label = "Is Final Step";
				checkboxField.PlaceholderText = null;
				checkboxField.Description = "Whether this is the final step in the workflow";
				checkboxField.HelpText = null;
				checkboxField.Required = true;
				checkboxField.Unique = false;
				checkboxField.Searchable = true;
				checkboxField.Auditable = false;
				checkboxField.System = false;
				checkboxField.DefaultValue = false;
				checkboxField.EnableSecurity = false;
				checkboxField.Permissions = new FieldPermissions();
				checkboxField.Permissions.CanRead = new List<Guid>();
				checkboxField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_STEP_ENTITY_ID, checkboxField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_step Field: is_final Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create entity*** Entity name: approval_rule >>
			{
				#region << entity >>
				{
					var entity = new InputEntity();
					var systemFieldIdDictionary = new Dictionary<string, Guid>();
					systemFieldIdDictionary["id"] = RULE_ID_FIELD_ID;
					entity.Id = APPROVAL_RULE_ENTITY_ID;
					entity.Name = "approval_rule";
					entity.Label = "Approval Rule";
					entity.LabelPlural = "Approval Rules";
					entity.System = false;
					entity.IconName = "fas fa-filter";
					entity.Color = "#dc2626";
					entity.RecordScreenIdField = null;
					entity.RecordPermissions = new RecordPermissions();
					entity.RecordPermissions.CanCreate = new List<Guid>();
					entity.RecordPermissions.CanRead = new List<Guid>();
					entity.RecordPermissions.CanUpdate = new List<Guid>();
					entity.RecordPermissions.CanDelete = new List<Guid>();
					// Create - Administrator only
					entity.RecordPermissions.CanCreate.Add(administratorRoleId);
					// Read - Administrator and Regular users
					entity.RecordPermissions.CanRead.Add(administratorRoleId);
					entity.RecordPermissions.CanRead.Add(regularRoleId);
					// Update - Administrator only
					entity.RecordPermissions.CanUpdate.Add(administratorRoleId);
					// Delete - Administrator only
					entity.RecordPermissions.CanDelete.Add(administratorRoleId);
					{
						var response = entMan.CreateEntity(entity, systemFieldIdDictionary);
						if (!response.Success)
							throw new Exception("System error 10050. Entity: approval_rule creation Message: " + response.Message);
					}
				}
				#endregion
			}
			#endregion

			#region << ***Create field*** Entity: approval_rule Field Name: workflow_id >>
			{
				InputGuidField guidField = new InputGuidField();
				guidField.Id = RULE_WORKFLOW_ID_FIELD_ID;
				guidField.Name = "workflow_id";
				guidField.Label = "Workflow";
				guidField.PlaceholderText = null;
				guidField.Description = "The workflow this rule belongs to";
				guidField.HelpText = null;
				guidField.Required = false;
				guidField.Unique = false;
				guidField.Searchable = true;
				guidField.Auditable = false;
				guidField.System = false;
				guidField.DefaultValue = null;
				guidField.GenerateNewId = false;
				guidField.EnableSecurity = false;
				guidField.Permissions = new FieldPermissions();
				guidField.Permissions.CanRead = new List<Guid>();
				guidField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_RULE_ENTITY_ID, guidField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_rule Field: workflow_id Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_rule Field Name: name >>
			{
				InputTextField textboxField = new InputTextField();
				textboxField.Id = RULE_NAME_FIELD_ID;
				textboxField.Name = "name";
				textboxField.Label = "Name";
				textboxField.PlaceholderText = "Enter rule name";
				textboxField.Description = "Name of this approval rule";
				textboxField.HelpText = null;
				textboxField.Required = true;
				textboxField.Unique = false;
				textboxField.Searchable = true;
				textboxField.Auditable = false;
				textboxField.System = false;
				textboxField.DefaultValue = "";
				textboxField.MaxLength = 256;
				textboxField.EnableSecurity = false;
				textboxField.Permissions = new FieldPermissions();
				textboxField.Permissions.CanRead = new List<Guid>();
				textboxField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_RULE_ENTITY_ID, textboxField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_rule Field: name Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_rule Field Name: field_name >>
			{
				InputTextField textboxField = new InputTextField();
				textboxField.Id = RULE_FIELD_NAME_FIELD_ID;
				textboxField.Name = "field_name";
				textboxField.Label = "Field Name";
				textboxField.PlaceholderText = "e.g., amount";
				textboxField.Description = "The field name on the target entity to evaluate";
				textboxField.HelpText = null;
				textboxField.Required = true;
				textboxField.Unique = false;
				textboxField.Searchable = true;
				textboxField.Auditable = false;
				textboxField.System = false;
				textboxField.DefaultValue = "";
				textboxField.MaxLength = 128;
				textboxField.EnableSecurity = false;
				textboxField.Permissions = new FieldPermissions();
				textboxField.Permissions.CanRead = new List<Guid>();
				textboxField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_RULE_ENTITY_ID, textboxField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_rule Field: field_name Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_rule Field Name: operator >>
			{
				InputSelectField dropdownField = new InputSelectField();
				dropdownField.Id = RULE_OPERATOR_FIELD_ID;
				dropdownField.Name = "operator";
				dropdownField.Label = "Operator";
				dropdownField.PlaceholderText = null;
				dropdownField.Description = "The comparison operator for the rule";
				dropdownField.HelpText = null;
				dropdownField.Required = true;
				dropdownField.Unique = false;
				dropdownField.Searchable = true;
				dropdownField.Auditable = false;
				dropdownField.System = false;
				dropdownField.DefaultValue = "eq";
				dropdownField.Options = new List<SelectOption>
				{
					new SelectOption() { Label = "Equals", Value = "eq", IconClass = "", Color = ""},
					new SelectOption() { Label = "Not Equals", Value = "neq", IconClass = "", Color = ""},
					new SelectOption() { Label = "Greater Than", Value = "gt", IconClass = "", Color = ""},
					new SelectOption() { Label = "Greater Than or Equal", Value = "gte", IconClass = "", Color = ""},
					new SelectOption() { Label = "Less Than", Value = "lt", IconClass = "", Color = ""},
					new SelectOption() { Label = "Less Than or Equal", Value = "lte", IconClass = "", Color = ""},
					new SelectOption() { Label = "Contains", Value = "contains", IconClass = "", Color = ""}
				};
				dropdownField.EnableSecurity = false;
				dropdownField.Permissions = new FieldPermissions();
				dropdownField.Permissions.CanRead = new List<Guid>();
				dropdownField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_RULE_ENTITY_ID, dropdownField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_rule Field: operator Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_rule Field Name: value >>
			{
				InputTextField textboxField = new InputTextField();
				textboxField.Id = RULE_VALUE_FIELD_ID;
				textboxField.Name = "value";
				textboxField.Label = "Value";
				textboxField.PlaceholderText = "Enter comparison value";
				textboxField.Description = "The value to compare against";
				textboxField.HelpText = null;
				textboxField.Required = true;
				textboxField.Unique = false;
				textboxField.Searchable = false;
				textboxField.Auditable = false;
				textboxField.System = false;
				textboxField.DefaultValue = "";
				textboxField.MaxLength = 1024;
				textboxField.EnableSecurity = false;
				textboxField.Permissions = new FieldPermissions();
				textboxField.Permissions.CanRead = new List<Guid>();
				textboxField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_RULE_ENTITY_ID, textboxField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_rule Field: value Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_rule Field Name: priority >>
			{
				InputNumberField numberField = new InputNumberField();
				numberField.Id = RULE_PRIORITY_FIELD_ID;
				numberField.Name = "priority";
				numberField.Label = "Priority";
				numberField.PlaceholderText = null;
				numberField.Description = "Rule evaluation priority (higher = evaluated first)";
				numberField.HelpText = null;
				numberField.Required = true;
				numberField.Unique = false;
				numberField.Searchable = true;
				numberField.Auditable = false;
				numberField.System = false;
				numberField.DefaultValue = Decimal.Parse("0");
				numberField.MinValue = Decimal.Parse("0");
				numberField.MaxValue = Decimal.Parse("1000");
				numberField.DecimalPlaces = byte.Parse("0");
				numberField.EnableSecurity = false;
				numberField.Permissions = new FieldPermissions();
				numberField.Permissions.CanRead = new List<Guid>();
				numberField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_RULE_ENTITY_ID, numberField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_rule Field: priority Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create entity*** Entity name: approval_request >>
			{
				#region << entity >>
				{
					var entity = new InputEntity();
					var systemFieldIdDictionary = new Dictionary<string, Guid>();
					systemFieldIdDictionary["id"] = REQUEST_ID_FIELD_ID;
					entity.Id = APPROVAL_REQUEST_ENTITY_ID;
					entity.Name = "approval_request";
					entity.Label = "Approval Request";
					entity.LabelPlural = "Approval Requests";
					entity.System = false;
					entity.IconName = "fas fa-clipboard-check";
					entity.Color = "#0ea5e9";
					entity.RecordScreenIdField = null;
					entity.RecordPermissions = new RecordPermissions();
					entity.RecordPermissions.CanCreate = new List<Guid>();
					entity.RecordPermissions.CanRead = new List<Guid>();
					entity.RecordPermissions.CanUpdate = new List<Guid>();
					entity.RecordPermissions.CanDelete = new List<Guid>();
					// Create - Administrator and Regular users
					entity.RecordPermissions.CanCreate.Add(administratorRoleId);
					entity.RecordPermissions.CanCreate.Add(regularRoleId);
					// Read - Administrator and Regular users
					entity.RecordPermissions.CanRead.Add(administratorRoleId);
					entity.RecordPermissions.CanRead.Add(regularRoleId);
					// Update - Administrator and Regular users
					entity.RecordPermissions.CanUpdate.Add(administratorRoleId);
					entity.RecordPermissions.CanUpdate.Add(regularRoleId);
					// Delete - Administrator only
					entity.RecordPermissions.CanDelete.Add(administratorRoleId);
					{
						var response = entMan.CreateEntity(entity, systemFieldIdDictionary);
						if (!response.Success)
							throw new Exception("System error 10050. Entity: approval_request creation Message: " + response.Message);
					}
				}
				#endregion
			}
			#endregion

			#region << ***Create field*** Entity: approval_request Field Name: workflow_id >>
			{
				InputGuidField guidField = new InputGuidField();
				guidField.Id = REQUEST_WORKFLOW_ID_FIELD_ID;
				guidField.Name = "workflow_id";
				guidField.Label = "Workflow";
				guidField.PlaceholderText = null;
				guidField.Description = "The workflow this request is associated with";
				guidField.HelpText = null;
				guidField.Required = false;
				guidField.Unique = false;
				guidField.Searchable = true;
				guidField.Auditable = false;
				guidField.System = false;
				guidField.DefaultValue = null;
				guidField.GenerateNewId = false;
				guidField.EnableSecurity = false;
				guidField.Permissions = new FieldPermissions();
				guidField.Permissions.CanRead = new List<Guid>();
				guidField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_REQUEST_ENTITY_ID, guidField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_request Field: workflow_id Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_request Field Name: current_step_id >>
			{
				InputGuidField guidField = new InputGuidField();
				guidField.Id = REQUEST_CURRENT_STEP_ID_FIELD_ID;
				guidField.Name = "current_step_id";
				guidField.Label = "Current Step";
				guidField.PlaceholderText = null;
				guidField.Description = "The current step in the approval process";
				guidField.HelpText = null;
				guidField.Required = false;
				guidField.Unique = false;
				guidField.Searchable = true;
				guidField.Auditable = false;
				guidField.System = false;
				guidField.DefaultValue = null;
				guidField.GenerateNewId = false;
				guidField.EnableSecurity = false;
				guidField.Permissions = new FieldPermissions();
				guidField.Permissions.CanRead = new List<Guid>();
				guidField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_REQUEST_ENTITY_ID, guidField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_request Field: current_step_id Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_request Field Name: title >>
			{
				InputTextField textboxField = new InputTextField();
				textboxField.Id = REQUEST_TITLE_FIELD_ID;
				textboxField.Name = "title";
				textboxField.Label = "Title";
				textboxField.PlaceholderText = null;
				textboxField.Description = "Title or description of the approval request";
				textboxField.HelpText = null;
				textboxField.Required = false;
				textboxField.Unique = false;
				textboxField.Searchable = true;
				textboxField.Auditable = false;
				textboxField.System = false;
				textboxField.DefaultValue = null;
				textboxField.MaxLength = 512;
				textboxField.EnableSecurity = false;
				textboxField.Permissions = new FieldPermissions();
				textboxField.Permissions.CanRead = new List<Guid>();
				textboxField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_REQUEST_ENTITY_ID, textboxField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_request Field: title Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_request Field Name: source_entity_name >>
			{
				InputTextField textboxField = new InputTextField();
				textboxField.Id = REQUEST_SOURCE_ENTITY_NAME_FIELD_ID;
				textboxField.Name = "source_entity_name";
				textboxField.Label = "Source Entity Name";
				textboxField.PlaceholderText = null;
				textboxField.Description = "The entity name of the source record";
				textboxField.HelpText = null;
				textboxField.Required = true;
				textboxField.Unique = false;
				textboxField.Searchable = true;
				textboxField.Auditable = false;
				textboxField.System = false;
				textboxField.DefaultValue = "";
				textboxField.MaxLength = 128;
				textboxField.EnableSecurity = false;
				textboxField.Permissions = new FieldPermissions();
				textboxField.Permissions.CanRead = new List<Guid>();
				textboxField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_REQUEST_ENTITY_ID, textboxField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_request Field: source_entity_name Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_request Field Name: source_record_id >>
			{
				InputGuidField guidField = new InputGuidField();
				guidField.Id = REQUEST_SOURCE_RECORD_ID_FIELD_ID;
				guidField.Name = "source_record_id";
				guidField.Label = "Source Record ID";
				guidField.PlaceholderText = null;
				guidField.Description = "The ID of the source record requiring approval";
				guidField.HelpText = null;
				guidField.Required = false;
				guidField.Unique = false;
				guidField.Searchable = true;
				guidField.Auditable = false;
				guidField.System = false;
				guidField.DefaultValue = null;
				guidField.GenerateNewId = false;
				guidField.EnableSecurity = false;
				guidField.Permissions = new FieldPermissions();
				guidField.Permissions.CanRead = new List<Guid>();
				guidField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_REQUEST_ENTITY_ID, guidField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_request Field: source_record_id Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_request Field Name: status >>
			{
				InputSelectField dropdownField = new InputSelectField();
				dropdownField.Id = REQUEST_STATUS_FIELD_ID;
				dropdownField.Name = "status";
				dropdownField.Label = "Status";
				dropdownField.PlaceholderText = null;
				dropdownField.Description = "Current status of the approval request";
				dropdownField.HelpText = null;
				dropdownField.Required = true;
				dropdownField.Unique = false;
				dropdownField.Searchable = true;
				dropdownField.Auditable = false;
				dropdownField.System = false;
				dropdownField.DefaultValue = "pending";
				dropdownField.Options = new List<SelectOption>
				{
					new SelectOption() { Label = "Pending", Value = "pending", IconClass = "fas fa-clock", Color = "#f59e0b"},
					new SelectOption() { Label = "Approved", Value = "approved", IconClass = "fas fa-check-circle", Color = "#10b981"},
					new SelectOption() { Label = "Rejected", Value = "rejected", IconClass = "fas fa-times-circle", Color = "#ef4444"},
					new SelectOption() { Label = "Escalated", Value = "escalated", IconClass = "fas fa-exclamation-triangle", Color = "#f97316"},
					new SelectOption() { Label = "Expired", Value = "expired", IconClass = "fas fa-hourglass-end", Color = "#6b7280"}
				};
				dropdownField.EnableSecurity = false;
				dropdownField.Permissions = new FieldPermissions();
				dropdownField.Permissions.CanRead = new List<Guid>();
				dropdownField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_REQUEST_ENTITY_ID, dropdownField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_request Field: status Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_request Field Name: requested_by >>
			{
				InputGuidField guidField = new InputGuidField();
				guidField.Id = REQUEST_REQUESTED_BY_FIELD_ID;
				guidField.Name = "requested_by";
				guidField.Label = "Requested By";
				guidField.PlaceholderText = null;
				guidField.Description = "The user who submitted this approval request";
				guidField.HelpText = null;
				guidField.Required = false;
				guidField.Unique = false;
				guidField.Searchable = true;
				guidField.Auditable = false;
				guidField.System = false;
				guidField.DefaultValue = null;
				guidField.GenerateNewId = false;
				guidField.EnableSecurity = false;
				guidField.Permissions = new FieldPermissions();
				guidField.Permissions.CanRead = new List<Guid>();
				guidField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_REQUEST_ENTITY_ID, guidField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_request Field: requested_by Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_request Field Name: requested_on >>
			{
				InputDateTimeField datetimeField = new InputDateTimeField();
				datetimeField.Id = REQUEST_REQUESTED_ON_FIELD_ID;
				datetimeField.Name = "requested_on";
				datetimeField.Label = "Requested On";
				datetimeField.PlaceholderText = null;
				datetimeField.Description = "When this approval request was submitted";
				datetimeField.HelpText = null;
				datetimeField.Required = true;
				datetimeField.Unique = false;
				datetimeField.Searchable = true;
				datetimeField.Auditable = false;
				datetimeField.System = false;
				datetimeField.DefaultValue = null;
				datetimeField.Format = "yyyy-MMM-dd HH:mm";
				datetimeField.UseCurrentTimeAsDefaultValue = true;
				datetimeField.EnableSecurity = false;
				datetimeField.Permissions = new FieldPermissions();
				datetimeField.Permissions.CanRead = new List<Guid>();
				datetimeField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_REQUEST_ENTITY_ID, datetimeField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_request Field: requested_on Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_request Field Name: completed_on >>
			{
				InputDateTimeField datetimeField = new InputDateTimeField();
				datetimeField.Id = REQUEST_COMPLETED_ON_FIELD_ID;
				datetimeField.Name = "completed_on";
				datetimeField.Label = "Completed On";
				datetimeField.PlaceholderText = null;
				datetimeField.Description = "When this approval request was completed";
				datetimeField.HelpText = null;
				datetimeField.Required = false;
				datetimeField.Unique = false;
				datetimeField.Searchable = true;
				datetimeField.Auditable = false;
				datetimeField.System = false;
				datetimeField.DefaultValue = null;
				datetimeField.Format = "yyyy-MMM-dd HH:mm";
				datetimeField.UseCurrentTimeAsDefaultValue = false;
				datetimeField.EnableSecurity = false;
				datetimeField.Permissions = new FieldPermissions();
				datetimeField.Permissions.CanRead = new List<Guid>();
				datetimeField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_REQUEST_ENTITY_ID, datetimeField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_request Field: completed_on Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create entity*** Entity name: approval_history >>
			{
				#region << entity >>
				{
					var entity = new InputEntity();
					var systemFieldIdDictionary = new Dictionary<string, Guid>();
					systemFieldIdDictionary["id"] = HISTORY_ID_FIELD_ID;
					entity.Id = APPROVAL_HISTORY_ENTITY_ID;
					entity.Name = "approval_history";
					entity.Label = "Approval History";
					entity.LabelPlural = "Approval History";
					entity.System = false;
					entity.IconName = "fas fa-history";
					entity.Color = "#8b5cf6";
					entity.RecordScreenIdField = null;
					entity.RecordPermissions = new RecordPermissions();
					entity.RecordPermissions.CanCreate = new List<Guid>();
					entity.RecordPermissions.CanRead = new List<Guid>();
					entity.RecordPermissions.CanUpdate = new List<Guid>();
					entity.RecordPermissions.CanDelete = new List<Guid>();
					// Create - Administrator and Regular users
					entity.RecordPermissions.CanCreate.Add(administratorRoleId);
					entity.RecordPermissions.CanCreate.Add(regularRoleId);
					// Read - Administrator and Regular users
					entity.RecordPermissions.CanRead.Add(administratorRoleId);
					entity.RecordPermissions.CanRead.Add(regularRoleId);
					// Update - Administrator only (history should be immutable)
					entity.RecordPermissions.CanUpdate.Add(administratorRoleId);
					// Delete - Administrator only
					entity.RecordPermissions.CanDelete.Add(administratorRoleId);
					{
						var response = entMan.CreateEntity(entity, systemFieldIdDictionary);
						if (!response.Success)
							throw new Exception("System error 10050. Entity: approval_history creation Message: " + response.Message);
					}
				}
				#endregion
			}
			#endregion

			#region << ***Create field*** Entity: approval_history Field Name: request_id >>
			{
				InputGuidField guidField = new InputGuidField();
				guidField.Id = HISTORY_REQUEST_ID_FIELD_ID;
				guidField.Name = "request_id";
				guidField.Label = "Request";
				guidField.PlaceholderText = null;
				guidField.Description = "The approval request this history entry belongs to";
				guidField.HelpText = null;
				guidField.Required = false;
				guidField.Unique = false;
				guidField.Searchable = true;
				guidField.Auditable = false;
				guidField.System = false;
				guidField.DefaultValue = null;
				guidField.GenerateNewId = false;
				guidField.EnableSecurity = false;
				guidField.Permissions = new FieldPermissions();
				guidField.Permissions.CanRead = new List<Guid>();
				guidField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_HISTORY_ENTITY_ID, guidField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_history Field: request_id Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_history Field Name: step_id >>
			{
				InputGuidField guidField = new InputGuidField();
				guidField.Id = HISTORY_STEP_ID_FIELD_ID;
				guidField.Name = "step_id";
				guidField.Label = "Step";
				guidField.PlaceholderText = null;
				guidField.Description = "The step this action was performed on";
				guidField.HelpText = null;
				guidField.Required = false;
				guidField.Unique = false;
				guidField.Searchable = true;
				guidField.Auditable = false;
				guidField.System = false;
				guidField.DefaultValue = null;
				guidField.GenerateNewId = false;
				guidField.EnableSecurity = false;
				guidField.Permissions = new FieldPermissions();
				guidField.Permissions.CanRead = new List<Guid>();
				guidField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_HISTORY_ENTITY_ID, guidField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_history Field: step_id Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_history Field Name: action >>
			{
				InputSelectField dropdownField = new InputSelectField();
				dropdownField.Id = HISTORY_ACTION_FIELD_ID;
				dropdownField.Name = "action";
				dropdownField.Label = "Action";
				dropdownField.PlaceholderText = null;
				dropdownField.Description = "The action that was performed";
				dropdownField.HelpText = null;
				dropdownField.Required = true;
				dropdownField.Unique = false;
				dropdownField.Searchable = true;
				dropdownField.Auditable = false;
				dropdownField.System = false;
				dropdownField.DefaultValue = "submitted";
				dropdownField.Options = new List<SelectOption>
				{
					new SelectOption() { Label = "Submitted", Value = "submitted", IconClass = "fas fa-paper-plane", Color = "#3b82f6"},
					new SelectOption() { Label = "Approved", Value = "approved", IconClass = "fas fa-check", Color = "#10b981"},
					new SelectOption() { Label = "Rejected", Value = "rejected", IconClass = "fas fa-times", Color = "#ef4444"},
					new SelectOption() { Label = "Delegated", Value = "delegated", IconClass = "fas fa-share", Color = "#8b5cf6"},
					new SelectOption() { Label = "Escalated", Value = "escalated", IconClass = "fas fa-level-up-alt", Color = "#f97316"}
				};
				dropdownField.EnableSecurity = false;
				dropdownField.Permissions = new FieldPermissions();
				dropdownField.Permissions.CanRead = new List<Guid>();
				dropdownField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_HISTORY_ENTITY_ID, dropdownField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_history Field: action Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_history Field Name: performed_by >>
			{
				InputGuidField guidField = new InputGuidField();
				guidField.Id = HISTORY_PERFORMED_BY_FIELD_ID;
				guidField.Name = "performed_by";
				guidField.Label = "Performed By";
				guidField.PlaceholderText = null;
				guidField.Description = "The user who performed this action";
				guidField.HelpText = null;
				guidField.Required = false;
				guidField.Unique = false;
				guidField.Searchable = true;
				guidField.Auditable = false;
				guidField.System = false;
				guidField.DefaultValue = null;
				guidField.GenerateNewId = false;
				guidField.EnableSecurity = false;
				guidField.Permissions = new FieldPermissions();
				guidField.Permissions.CanRead = new List<Guid>();
				guidField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_HISTORY_ENTITY_ID, guidField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_history Field: performed_by Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_history Field Name: performed_on >>
			{
				InputDateTimeField datetimeField = new InputDateTimeField();
				datetimeField.Id = HISTORY_PERFORMED_ON_FIELD_ID;
				datetimeField.Name = "performed_on";
				datetimeField.Label = "Performed On";
				datetimeField.PlaceholderText = null;
				datetimeField.Description = "When this action was performed";
				datetimeField.HelpText = null;
				datetimeField.Required = true;
				datetimeField.Unique = false;
				datetimeField.Searchable = true;
				datetimeField.Auditable = false;
				datetimeField.System = false;
				datetimeField.DefaultValue = null;
				datetimeField.Format = "yyyy-MMM-dd HH:mm:ss";
				datetimeField.UseCurrentTimeAsDefaultValue = true;
				datetimeField.EnableSecurity = false;
				datetimeField.Permissions = new FieldPermissions();
				datetimeField.Permissions.CanRead = new List<Guid>();
				datetimeField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_HISTORY_ENTITY_ID, datetimeField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_history Field: performed_on Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create field*** Entity: approval_history Field Name: comments >>
			{
				InputMultiLineTextField textareaField = new InputMultiLineTextField();
				textareaField.Id = HISTORY_COMMENTS_FIELD_ID;
				textareaField.Name = "comments";
				textareaField.Label = "Comments";
				textareaField.PlaceholderText = "Enter any comments";
				textareaField.Description = "Optional comments for this action";
				textareaField.HelpText = null;
				textareaField.Required = false;
				textareaField.Unique = false;
				textareaField.Searchable = true;
				textareaField.Auditable = false;
				textareaField.System = false;
				textareaField.DefaultValue = null;
				textareaField.MaxLength = null;
				textareaField.VisibleLineNumber = 4;
				textareaField.EnableSecurity = false;
				textareaField.Permissions = new FieldPermissions();
				textareaField.Permissions.CanRead = new List<Guid>();
				textareaField.Permissions.CanUpdate = new List<Guid>();
				{
					var response = entMan.CreateField(APPROVAL_HISTORY_ENTITY_ID, textareaField, false);
					if (!response.Success)
						throw new Exception("System error 10060. Entity: approval_history Field: comments Message:" + response.Message);
				}
			}
			#endregion

			// Create entity relationships

			#region << ***Create relation*** Relation name: approval_workflow_1n_step >>
			{
				var relation = new EntityRelation();
				var originEntity = entMan.ReadEntity(APPROVAL_WORKFLOW_ENTITY_ID).Object;
				var originField = originEntity.Fields.SingleOrDefault(x => x.Name == "id");
				var targetEntity = entMan.ReadEntity(APPROVAL_STEP_ENTITY_ID).Object;
				var targetField = targetEntity.Fields.SingleOrDefault(x => x.Name == "workflow_id");
				relation.Id = WORKFLOW_STEP_RELATION_ID;
				relation.Name = "approval_workflow_1n_step";
				relation.Label = "approval_workflow_1n_step";
				relation.Description = "Relationship between workflow and its steps";
				relation.System = false;
				relation.RelationType = EntityRelationType.OneToMany;
				relation.OriginEntityId = originEntity.Id;
				relation.OriginEntityName = originEntity.Name;
				relation.OriginFieldId = originField.Id;
				relation.OriginFieldName = originField.Name;
				relation.TargetEntityId = targetEntity.Id;
				relation.TargetEntityName = targetEntity.Name;
				relation.TargetFieldId = targetField.Id;
				relation.TargetFieldName = targetField.Name;
				{
					var response = relMan.Create(relation);
					if (!response.Success)
						throw new Exception("System error 10060. Relation: approval_workflow_1n_step Create. Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create relation*** Relation name: approval_workflow_1n_rule >>
			{
				var relation = new EntityRelation();
				var originEntity = entMan.ReadEntity(APPROVAL_WORKFLOW_ENTITY_ID).Object;
				var originField = originEntity.Fields.SingleOrDefault(x => x.Name == "id");
				var targetEntity = entMan.ReadEntity(APPROVAL_RULE_ENTITY_ID).Object;
				var targetField = targetEntity.Fields.SingleOrDefault(x => x.Name == "workflow_id");
				relation.Id = WORKFLOW_RULE_RELATION_ID;
				relation.Name = "approval_workflow_1n_rule";
				relation.Label = "approval_workflow_1n_rule";
				relation.Description = "Relationship between workflow and its rules";
				relation.System = false;
				relation.RelationType = EntityRelationType.OneToMany;
				relation.OriginEntityId = originEntity.Id;
				relation.OriginEntityName = originEntity.Name;
				relation.OriginFieldId = originField.Id;
				relation.OriginFieldName = originField.Name;
				relation.TargetEntityId = targetEntity.Id;
				relation.TargetEntityName = targetEntity.Name;
				relation.TargetFieldId = targetField.Id;
				relation.TargetFieldName = targetField.Name;
				{
					var response = relMan.Create(relation);
					if (!response.Success)
						throw new Exception("System error 10060. Relation: approval_workflow_1n_rule Create. Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create relation*** Relation name: approval_workflow_1n_request >>
			{
				var relation = new EntityRelation();
				var originEntity = entMan.ReadEntity(APPROVAL_WORKFLOW_ENTITY_ID).Object;
				var originField = originEntity.Fields.SingleOrDefault(x => x.Name == "id");
				var targetEntity = entMan.ReadEntity(APPROVAL_REQUEST_ENTITY_ID).Object;
				var targetField = targetEntity.Fields.SingleOrDefault(x => x.Name == "workflow_id");
				relation.Id = WORKFLOW_REQUEST_RELATION_ID;
				relation.Name = "approval_workflow_1n_request";
				relation.Label = "approval_workflow_1n_request";
				relation.Description = "Relationship between workflow and approval requests";
				relation.System = false;
				relation.RelationType = EntityRelationType.OneToMany;
				relation.OriginEntityId = originEntity.Id;
				relation.OriginEntityName = originEntity.Name;
				relation.OriginFieldId = originField.Id;
				relation.OriginFieldName = originField.Name;
				relation.TargetEntityId = targetEntity.Id;
				relation.TargetEntityName = targetEntity.Name;
				relation.TargetFieldId = targetField.Id;
				relation.TargetFieldName = targetField.Name;
				{
					var response = relMan.Create(relation);
					if (!response.Success)
						throw new Exception("System error 10060. Relation: approval_workflow_1n_request Create. Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create relation*** Relation name: approval_step_1n_request >>
			{
				var relation = new EntityRelation();
				var originEntity = entMan.ReadEntity(APPROVAL_STEP_ENTITY_ID).Object;
				var originField = originEntity.Fields.SingleOrDefault(x => x.Name == "id");
				var targetEntity = entMan.ReadEntity(APPROVAL_REQUEST_ENTITY_ID).Object;
				var targetField = targetEntity.Fields.SingleOrDefault(x => x.Name == "current_step_id");
				relation.Id = STEP_REQUEST_RELATION_ID;
				relation.Name = "approval_step_1n_request";
				relation.Label = "approval_step_1n_request";
				relation.Description = "Relationship between step and current approval requests";
				relation.System = false;
				relation.RelationType = EntityRelationType.OneToMany;
				relation.OriginEntityId = originEntity.Id;
				relation.OriginEntityName = originEntity.Name;
				relation.OriginFieldId = originField.Id;
				relation.OriginFieldName = originField.Name;
				relation.TargetEntityId = targetEntity.Id;
				relation.TargetEntityName = targetEntity.Name;
				relation.TargetFieldId = targetField.Id;
				relation.TargetFieldName = targetField.Name;
				{
					var response = relMan.Create(relation);
					if (!response.Success)
						throw new Exception("System error 10060. Relation: approval_step_1n_request Create. Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create relation*** Relation name: approval_request_1n_history >>
			{
				var relation = new EntityRelation();
				var originEntity = entMan.ReadEntity(APPROVAL_REQUEST_ENTITY_ID).Object;
				var originField = originEntity.Fields.SingleOrDefault(x => x.Name == "id");
				var targetEntity = entMan.ReadEntity(APPROVAL_HISTORY_ENTITY_ID).Object;
				var targetField = targetEntity.Fields.SingleOrDefault(x => x.Name == "request_id");
				relation.Id = REQUEST_HISTORY_RELATION_ID;
				relation.Name = "approval_request_1n_history";
				relation.Label = "approval_request_1n_history";
				relation.Description = "Relationship between approval request and its history";
				relation.System = false;
				relation.RelationType = EntityRelationType.OneToMany;
				relation.OriginEntityId = originEntity.Id;
				relation.OriginEntityName = originEntity.Name;
				relation.OriginFieldId = originField.Id;
				relation.OriginFieldName = originField.Name;
				relation.TargetEntityId = targetEntity.Id;
				relation.TargetEntityName = targetEntity.Name;
				relation.TargetFieldId = targetField.Id;
				relation.TargetFieldName = targetField.Name;
				{
					var response = relMan.Create(relation);
					if (!response.Success)
						throw new Exception("System error 10060. Relation: approval_request_1n_history Create. Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create relation*** Relation name: approval_step_1n_history >>
			{
				var relation = new EntityRelation();
				var originEntity = entMan.ReadEntity(APPROVAL_STEP_ENTITY_ID).Object;
				var originField = originEntity.Fields.SingleOrDefault(x => x.Name == "id");
				var targetEntity = entMan.ReadEntity(APPROVAL_HISTORY_ENTITY_ID).Object;
				var targetField = targetEntity.Fields.SingleOrDefault(x => x.Name == "step_id");
				relation.Id = HISTORY_STEP_RELATION_ID;
				relation.Name = "approval_step_1n_history";
				relation.Label = "approval_step_1n_history";
				relation.Description = "Relationship between step and history entries";
				relation.System = false;
				relation.RelationType = EntityRelationType.OneToMany;
				relation.OriginEntityId = originEntity.Id;
				relation.OriginEntityName = originEntity.Name;
				relation.OriginFieldId = originField.Id;
				relation.OriginFieldName = originField.Name;
				relation.TargetEntityId = targetEntity.Id;
				relation.TargetEntityName = targetEntity.Name;
				relation.TargetFieldId = targetField.Id;
				relation.TargetFieldName = targetField.Name;
				{
					var response = relMan.Create(relation);
					if (!response.Success)
						throw new Exception("System error 10060. Relation: approval_step_1n_history Create. Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create relation*** Relation name: user_1n_workflow_created_by >>
			{
				var relation = new EntityRelation();
				var originEntity = entMan.ReadEntity(userEntityId).Object;
				var originField = originEntity.Fields.SingleOrDefault(x => x.Name == "id");
				var targetEntity = entMan.ReadEntity(APPROVAL_WORKFLOW_ENTITY_ID).Object;
				var targetField = targetEntity.Fields.SingleOrDefault(x => x.Name == "created_by");
				relation.Id = USER_CREATED_BY_WORKFLOW_RELATION_ID;
				relation.Name = "user_1n_workflow_created_by";
				relation.Label = "user_1n_workflow_created_by";
				relation.Description = "Relationship between user and workflows they created";
				relation.System = false;
				relation.RelationType = EntityRelationType.OneToMany;
				relation.OriginEntityId = originEntity.Id;
				relation.OriginEntityName = originEntity.Name;
				relation.OriginFieldId = originField.Id;
				relation.OriginFieldName = originField.Name;
				relation.TargetEntityId = targetEntity.Id;
				relation.TargetEntityName = targetEntity.Name;
				relation.TargetFieldId = targetField.Id;
				relation.TargetFieldName = targetField.Name;
				{
					var response = relMan.Create(relation);
					if (!response.Success)
						throw new Exception("System error 10060. Relation: user_1n_workflow_created_by Create. Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create relation*** Relation name: user_1n_request_requested_by >>
			{
				var relation = new EntityRelation();
				var originEntity = entMan.ReadEntity(userEntityId).Object;
				var originField = originEntity.Fields.SingleOrDefault(x => x.Name == "id");
				var targetEntity = entMan.ReadEntity(APPROVAL_REQUEST_ENTITY_ID).Object;
				var targetField = targetEntity.Fields.SingleOrDefault(x => x.Name == "requested_by");
				relation.Id = USER_REQUESTED_BY_RELATION_ID;
				relation.Name = "user_1n_request_requested_by";
				relation.Label = "user_1n_request_requested_by";
				relation.Description = "Relationship between user and approval requests they submitted";
				relation.System = false;
				relation.RelationType = EntityRelationType.OneToMany;
				relation.OriginEntityId = originEntity.Id;
				relation.OriginEntityName = originEntity.Name;
				relation.OriginFieldId = originField.Id;
				relation.OriginFieldName = originField.Name;
				relation.TargetEntityId = targetEntity.Id;
				relation.TargetEntityName = targetEntity.Name;
				relation.TargetFieldId = targetField.Id;
				relation.TargetFieldName = targetField.Name;
				{
					var response = relMan.Create(relation);
					if (!response.Success)
						throw new Exception("System error 10060. Relation: user_1n_request_requested_by Create. Message:" + response.Message);
				}
			}
			#endregion

			#region << ***Create relation*** Relation name: user_1n_history_performed_by >>
			{
				var relation = new EntityRelation();
				var originEntity = entMan.ReadEntity(userEntityId).Object;
				var originField = originEntity.Fields.SingleOrDefault(x => x.Name == "id");
				var targetEntity = entMan.ReadEntity(APPROVAL_HISTORY_ENTITY_ID).Object;
				var targetField = targetEntity.Fields.SingleOrDefault(x => x.Name == "performed_by");
				relation.Id = USER_PERFORMED_BY_RELATION_ID;
				relation.Name = "user_1n_history_performed_by";
				relation.Label = "user_1n_history_performed_by";
				relation.Description = "Relationship between user and history actions they performed";
				relation.System = false;
				relation.RelationType = EntityRelationType.OneToMany;
				relation.OriginEntityId = originEntity.Id;
				relation.OriginEntityName = originEntity.Name;
				relation.OriginFieldId = originField.Id;
				relation.OriginFieldName = originField.Name;
				relation.TargetEntityId = targetEntity.Id;
				relation.TargetEntityName = targetEntity.Name;
				relation.TargetFieldId = targetField.Id;
				relation.TargetFieldName = targetField.Name;
				{
					var response = relMan.Create(relation);
					if (!response.Success)
						throw new Exception("System error 10060. Relation: user_1n_history_performed_by Create. Message:" + response.Message);
				}
			}
			#endregion
		}
	}
}
