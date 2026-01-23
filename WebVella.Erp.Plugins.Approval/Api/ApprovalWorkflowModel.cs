using System;
using Newtonsoft.Json;

namespace WebVella.Erp.Plugins.Approval.Api
{
    /// <summary>
    /// Data Transfer Object (DTO) representing an approval workflow definition.
    /// This model is used by WorkflowConfigService for CRUD operations and 
    /// workflow activation/deactivation management.
    /// </summary>
    /// <remarks>
    /// Maps to the 'approval_workflow' entity with the following database schema:
    /// - id (Guid, Primary Key, Auto-generated)
    /// - name (Text, Required, Max 256 characters)
    /// - target_entity_name (Text, Required, Max 128 characters)
    /// - is_enabled (Checkbox, Default true)
    /// - created_on (DateTime, Auto-generated)
    /// - created_by (Guid, Foreign Key to user)
    /// </remarks>
    public class ApprovalWorkflowModel
    {
        /// <summary>
        /// Gets or sets the unique identifier for the approval workflow.
        /// This is the primary key in the approval_workflow entity.
        /// </summary>
        [JsonProperty(PropertyName = "id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the display name of the approval workflow.
        /// Maximum length is 256 characters.
        /// </summary>
        /// <example>Purchase Order Approval</example>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the name of the target entity that this workflow applies to.
        /// Maximum length is 128 characters.
        /// This determines which entity records will trigger this approval workflow.
        /// </summary>
        /// <example>purchase_order</example>
        [JsonProperty(PropertyName = "target_entity_name")]
        public string TargetEntityName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the workflow is currently enabled.
        /// When false, the workflow will not be triggered for new records.
        /// Default value is true.
        /// </summary>
        [JsonProperty(PropertyName = "is_enabled")]
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the timestamp when the workflow was created.
        /// This value is automatically set when the workflow is first created.
        /// </summary>
        [JsonProperty(PropertyName = "created_on")]
        public DateTime CreatedOn { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the user who created this workflow.
        /// This is a foreign key reference to the user entity.
        /// </summary>
        [JsonProperty(PropertyName = "created_by")]
        public Guid CreatedBy { get; set; }
    }
}
