using System;
using Newtonsoft.Json;
using WebVella.Erp.Api.Models;

namespace WebVella.Erp.Plugins.Approval.Api
{
    /// <summary>
    /// Enumeration representing comparison operators available for approval routing rules.
    /// Used by ApprovalRouteService to evaluate field values against rule conditions.
    /// </summary>
    public enum ApprovalRuleOperator
    {
        /// <summary>
        /// Equal comparison operator - field value must equal the rule value.
        /// </summary>
        [SelectOption(Label = "eq")]
        Eq = 0,

        /// <summary>
        /// Not equal comparison operator - field value must not equal the rule value.
        /// </summary>
        [SelectOption(Label = "neq")]
        Neq = 1,

        /// <summary>
        /// Greater than comparison operator - field value must be greater than the rule value.
        /// </summary>
        [SelectOption(Label = "gt")]
        Gt = 2,

        /// <summary>
        /// Greater than or equal comparison operator - field value must be greater than or equal to the rule value.
        /// </summary>
        [SelectOption(Label = "gte")]
        Gte = 3,

        /// <summary>
        /// Less than comparison operator - field value must be less than the rule value.
        /// </summary>
        [SelectOption(Label = "lt")]
        Lt = 4,

        /// <summary>
        /// Less than or equal comparison operator - field value must be less than or equal to the rule value.
        /// </summary>
        [SelectOption(Label = "lte")]
        Lte = 5,

        /// <summary>
        /// Contains comparison operator - field value must contain the rule value as a substring.
        /// </summary>
        [SelectOption(Label = "contains")]
        Contains = 6
    }

    /// <summary>
    /// Entity DTO representing an approval routing rule.
    /// Rules define conditional logic that determines which workflow should be applied
    /// to a record based on field values and comparison operators.
    /// Used by RuleConfigService for CRUD operations and ApprovalRouteService for rule evaluation.
    /// </summary>
    public class ApprovalRuleModel
    {
        /// <summary>
        /// Unique identifier for the approval rule.
        /// Primary key, auto-generated GUID.
        /// </summary>
        [JsonProperty(PropertyName = "id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Foreign key reference to the parent approval workflow.
        /// Links this rule to a specific workflow definition.
        /// </summary>
        [JsonProperty(PropertyName = "workflow_id")]
        public Guid WorkflowId { get; set; }

        /// <summary>
        /// Display name for the rule.
        /// Used for administrative identification in the workflow configuration UI.
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// Name of the entity field to evaluate.
        /// Must correspond to a valid field on the target entity of the parent workflow.
        /// </summary>
        [JsonProperty(PropertyName = "field_name")]
        public string FieldName { get; set; }

        /// <summary>
        /// Comparison operator for rule evaluation.
        /// Valid values: eq (equal), neq (not equal), gt (greater than), 
        /// gte (greater than or equal), lt (less than), lte (less than or equal), contains.
        /// </summary>
        [JsonProperty(PropertyName = "operator")]
        public string Operator { get; set; }

        /// <summary>
        /// The threshold value to compare against the entity field.
        /// Stored as decimal with precision for numeric comparisons.
        /// </summary>
        [JsonProperty(PropertyName = "threshold_value")]
        public decimal ThresholdValue { get; set; }

        /// <summary>
        /// String value for text-based comparisons (contains, equals).
        /// Used when the operator requires string matching rather than numeric comparison.
        /// </summary>
        [JsonProperty(PropertyName = "string_value")]
        public string StringValue { get; set; }

        /// <summary>
        /// Priority order for rule evaluation.
        /// Higher priority rules are evaluated first. Default is 0.
        /// When multiple rules match, the highest priority rule determines the workflow.
        /// </summary>
        [JsonProperty(PropertyName = "priority")]
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Optional foreign key reference to the step to route to when this rule matches.
        /// When set, overrides the default sequential step progression.
        /// When null, the workflow follows the default step order.
        /// </summary>
        [JsonProperty(PropertyName = "next_step_id")]
        public Guid? NextStepId { get; set; }
    }
}
