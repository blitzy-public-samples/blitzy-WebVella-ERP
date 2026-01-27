using System;
using Newtonsoft.Json;
using WebVella.Erp.Api.Models;

namespace WebVella.Erp.Plugins.Approval.Api
{
    /// <summary>
    /// Defines the type of approver assigned to an approval step.
    /// Used to determine how the approver is resolved at runtime.
    /// </summary>
    public enum ApproverType
    {
        /// <summary>
        /// Approver is determined by a role. Any user with the specified role can approve.
        /// </summary>
        [SelectOption(Label = "role")]
        Role = 0,

        /// <summary>
        /// Approver is a specific user identified by user ID.
        /// </summary>
        [SelectOption(Label = "user")]
        User = 1,

        /// <summary>
        /// Approver is the department head of the requestor's department.
        /// </summary>
        [SelectOption(Label = "department_head")]
        DepartmentHead = 2
    }

    /// <summary>
    /// Data Transfer Object representing an approval workflow step.
    /// Each step defines who can approve and what happens when the step times out.
    /// Used by StepConfigService for CRUD operations and ApprovalRouteService for step routing.
    /// </summary>
    public class ApprovalStepModel
    {
        /// <summary>
        /// Gets or sets the unique identifier for the approval step.
        /// Primary key, auto-generated GUID.
        /// </summary>
        [JsonProperty(PropertyName = "id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the workflow this step belongs to.
        /// Foreign key reference to approval_workflow entity.
        /// </summary>
        [JsonProperty(PropertyName = "workflow_id")]
        public Guid WorkflowId { get; set; }

        /// <summary>
        /// Gets or sets the ordinal position of this step within the workflow.
        /// Steps are executed in ascending order (1, 2, 3, etc.).
        /// </summary>
        [JsonProperty(PropertyName = "step_order")]
        public int StepOrder { get; set; }

        /// <summary>
        /// Gets or sets the display name of the approval step.
        /// Used for identification in the UI and audit logs.
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the type of approver for this step.
        /// Determines how the approver is resolved: by role, specific user, or department head.
        /// Values: "role", "user", "department_head"
        /// </summary>
        [JsonProperty(PropertyName = "approver_type")]
        public string ApproverType { get; set; }

        /// <summary>
        /// Gets or sets the ID of the approver (role ID or user ID).
        /// Nullable - may be null for department_head approver type where ID is resolved dynamically.
        /// When ApproverType is "role", this references a role ID.
        /// When ApproverType is "user", this references a user ID.
        /// </summary>
        [JsonProperty(PropertyName = "approver_id")]
        public Guid? ApproverId { get; set; }

        /// <summary>
        /// Gets or sets the number of hours before this step times out.
        /// Nullable - when null, the step has no timeout and will wait indefinitely.
        /// When set, the escalation job will escalate requests that exceed this duration.
        /// </summary>
        [JsonProperty(PropertyName = "timeout_hours")]
        public int? TimeoutHours { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this is the final step in the workflow.
        /// When true, approving this step completes the entire approval request.
        /// Defaults to false for intermediate steps.
        /// </summary>
        [JsonProperty(PropertyName = "is_final")]
        public bool IsFinal { get; set; }

        /// <summary>
        /// Gets or sets the JSON configuration for threshold-based routing.
        /// Used to define amount thresholds or other criteria that determine step applicability.
        /// Nullable - when null, no threshold configuration is applied.
        /// Example: {"min_amount": 1000, "max_amount": 5000}
        /// </summary>
        [JsonProperty(PropertyName = "threshold_config")]
        public string ThresholdConfig { get; set; }
    }
}
