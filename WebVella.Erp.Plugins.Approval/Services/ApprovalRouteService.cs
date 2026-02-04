using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Eql;
using WebVella.Erp.Plugins.Approval.Model;

namespace WebVella.Erp.Plugins.Approval.Services
{
    /// <summary>
    /// Intelligent routing engine for approval workflows.
    /// Evaluates field values and amount thresholds against approval_rule entities to determine the next approval step.
    /// Resolves approvers based on step configuration (User, Role, Manager, Custom).
    /// Determines initial routing when new records require approval based on workflow target entity matching.
    /// </summary>
    /// <remarks>
    /// This service is the core orchestrator for all approval workflow routing decisions.
    /// It works with the following entities:
    /// - approval_workflow: Defines workflow configurations
    /// - approval_step: Defines sequential steps within a workflow
    /// - approval_rule: Defines conditional routing rules for steps
    /// - approval_request: Tracks individual approval requests
    /// 
    /// Key responsibilities:
    /// 1. Determine the next step in a workflow sequence
    /// 2. Evaluate rules against source record data
    /// 3. Resolve approvers based on step configuration
    /// 4. Find matching workflows for new records
    /// </remarks>
    public class ApprovalRouteService : BaseService
    {
        #region Constants

        /// <summary>
        /// Entity name for approval workflows.
        /// </summary>
        private const string ENTITY_WORKFLOW = "approval_workflow";

        /// <summary>
        /// Entity name for approval steps.
        /// </summary>
        private const string ENTITY_STEP = "approval_step";

        /// <summary>
        /// Entity name for approval rules.
        /// </summary>
        private const string ENTITY_RULE = "approval_rule";

        /// <summary>
        /// Entity name for approval requests.
        /// </summary>
        private const string ENTITY_REQUEST = "approval_request";

        /// <summary>
        /// Field name for record ID.
        /// </summary>
        private const string FIELD_ID = "id";

        /// <summary>
        /// Field name for workflow ID foreign key.
        /// </summary>
        private const string FIELD_WORKFLOW_ID = "workflow_id";

        /// <summary>
        /// Field name for step order.
        /// </summary>
        private const string FIELD_STEP_ORDER = "step_order";

        /// <summary>
        /// Field name for step ID foreign key.
        /// </summary>
        private const string FIELD_STEP_ID = "step_id";

        /// <summary>
        /// Field name for current step ID.
        /// </summary>
        private const string FIELD_CURRENT_STEP_ID = "current_step_id";

        /// <summary>
        /// Field name for entity name.
        /// </summary>
        private const string FIELD_ENTITY_NAME = "entity_name";

        /// <summary>
        /// Field name for record ID reference.
        /// </summary>
        private const string FIELD_RECORD_ID = "record_id";

        /// <summary>
        /// Field name for active status.
        /// </summary>
        private const string FIELD_IS_ACTIVE = "is_active";

        /// <summary>
        /// Field name for approver type.
        /// </summary>
        private const string FIELD_APPROVER_TYPE = "approver_type";

        /// <summary>
        /// Field name for approver ID.
        /// </summary>
        private const string FIELD_APPROVER_ID = "approver_id";

        /// <summary>
        /// Field name for rule field name.
        /// </summary>
        private const string FIELD_RULE_FIELD_NAME = "field_name";

        /// <summary>
        /// Field name for rule operator.
        /// </summary>
        private const string FIELD_RULE_OPERATOR = "operator";

        /// <summary>
        /// Field name for rule value.
        /// </summary>
        private const string FIELD_RULE_VALUE = "value";

        #endregion

        #region Public Methods

        /// <summary>
        /// Determines the next step in the workflow sequence after the current step.
        /// </summary>
        /// <param name="workflowId">The workflow ID to search within.</param>
        /// <param name="currentStepId">The current step ID to find the next step after.</param>
        /// <returns>
        /// The ID of the next step in sequence, or null if the workflow is complete
        /// (no more steps after the current one).
        /// </returns>
        /// <remarks>
        /// This method uses step_order values to determine sequence.
        /// Steps are ordered ascending, and the first step with a higher order than
        /// the current step is returned as the next step.
        /// </remarks>
        public Guid? DetermineNextStep(Guid workflowId, Guid currentStepId)
        {
            try
            {
                // Get current step's step_order value
                var currentStep = GetStep(currentStepId);
                if (currentStep == null)
                {
                    return null;
                }

                var currentOrder = currentStep[FIELD_STEP_ORDER] as int? ?? 0;

                // Query for the next step in sequence
                var eqlCommand = $@"SELECT * FROM {ENTITY_STEP} 
                                    WHERE {FIELD_WORKFLOW_ID} = @workflowId 
                                    AND {FIELD_STEP_ORDER} > @currentOrder 
                                    ORDER BY {FIELD_STEP_ORDER} ASC 
                                    PAGE 1 PAGESIZE 1";

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("workflowId", workflowId),
                    new EqlParameter("currentOrder", currentOrder)
                };

                var result = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (result != null && result.Any())
                {
                    return (Guid)result[0][FIELD_ID];
                }

                // No more steps - workflow is complete
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error determining next step for workflow {workflowId} from step {currentStepId}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Evaluates the next step for an approval request by loading request data,
        /// evaluating rules, and determining the appropriate next step.
        /// </summary>
        /// <param name="requestId">The approval request ID to evaluate.</param>
        /// <returns>
        /// The ID of the next step if rules pass and there is a next step,
        /// or null if the workflow is complete or rules block progression.
        /// </returns>
        /// <remarks>
        /// This method:
        /// 1. Loads the approval_request record
        /// 2. Retrieves the source record data for rule evaluation
        /// 3. Evaluates rules for the next step
        /// 4. Returns the next step ID or null if workflow is complete
        /// </remarks>
        public Guid? EvaluateNextStep(Guid requestId)
        {
            try
            {
                // Load approval_request record
                var request = GetApprovalRequest(requestId);
                if (request == null)
                {
                    throw new Exception($"Approval request with ID {requestId} not found.");
                }

                var workflowId = (Guid)request[FIELD_WORKFLOW_ID];
                var currentStepId = request[FIELD_CURRENT_STEP_ID] as Guid?;
                var entityName = request[FIELD_ENTITY_NAME] as string;
                var recordId = (Guid)request[FIELD_RECORD_ID];

                // If no current step, get first step
                if (currentStepId == null)
                {
                    return GetFirstStepForWorkflow(workflowId);
                }

                // Determine next sequential step
                var nextStepId = DetermineNextStep(workflowId, currentStepId.Value);

                if (nextStepId == null)
                {
                    // Workflow is complete
                    return null;
                }

                // Load source record for rule evaluation
                var sourceRecord = GetSourceRecord(recordId, entityName);
                if (sourceRecord == null)
                {
                    // Source record not found - return next step anyway
                    return nextStepId;
                }

                // Check if step applies to this record based on rules
                var nextStep = GetStep(nextStepId.Value);
                if (nextStep != null && StepAppliesToRecord(nextStep, recordId, entityName, workflowId))
                {
                    return nextStepId;
                }

                // If rules don't match, skip to the next step
                return DetermineNextStep(workflowId, nextStepId.Value);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error evaluating next step for request {requestId}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Evaluates all rules for a workflow against a source record to determine
        /// if any rules match the record's field values.
        /// </summary>
        /// <param name="workflowId">The workflow ID containing the rules to evaluate.</param>
        /// <param name="sourceRecord">The source entity record to evaluate against rules.</param>
        /// <returns>
        /// True if any rule matches (OR logic) or if no rules exist,
        /// false if rules exist but none match.
        /// </returns>
        /// <remarks>
        /// Rule evaluation uses OR logic - if any single rule matches, the method returns true.
        /// If no rules are defined for the workflow or its steps, the method returns true
        /// (allowing the workflow to proceed without rule restrictions).
        /// 
        /// Supported operators:
        /// - Equals: Exact match comparison
        /// - NotEquals: Inequality comparison
        /// - GreaterThan: Numeric/date greater than comparison
        /// - LessThan: Numeric/date less than comparison
        /// - Contains: Substring search
        /// - StartsWith: Prefix match
        /// </remarks>
        public bool EvaluateRules(Guid workflowId, EntityRecord sourceRecord)
        {
            try
            {
                if (sourceRecord == null)
                {
                    return true; // No source record, allow workflow to proceed
                }

                // Get all step IDs for this workflow
                var stepIdsQuery = $"SELECT {FIELD_ID} FROM {ENTITY_STEP} WHERE {FIELD_WORKFLOW_ID} = @workflowId";
                var stepParams = new List<EqlParameter> { new EqlParameter("workflowId", workflowId) };
                var steps = new EqlCommand(stepIdsQuery, stepParams).Execute();

                if (steps == null || !steps.Any())
                {
                    return true; // No steps defined, allow workflow to proceed
                }

                // Build list of step IDs for rule query
                var stepIds = steps.Select(s => (Guid)s[FIELD_ID]).ToList();

                // Query rules for this workflow's steps
                var rules = GetRulesForSteps(stepIds);

                if (rules == null || !rules.Any())
                {
                    return true; // No rules defined, allow workflow to proceed
                }

                // Evaluate rules with OR logic - any match returns true
                foreach (var rule in rules)
                {
                    if (EvaluateRuleCondition(rule, sourceRecord))
                    {
                        return true;
                    }
                }

                // No rules matched
                return false;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error evaluating rules for workflow {workflowId}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets the list of authorized approver user IDs for a specific approval step.
        /// </summary>
        /// <param name="stepId">The approval step ID to get approvers for.</param>
        /// <returns>
        /// A list of user GUIDs who are authorized to approve at this step.
        /// Returns an empty list if no approvers are found or for Custom approver type.
        /// </returns>
        /// <remarks>
        /// Approver resolution is based on the step's approver_type field:
        /// - User: Returns the single user ID from approver_id
        /// - Role: Returns all users with the specified role
        /// - Manager: Attempts to resolve the requester's manager (returns empty if not found)
        /// - Custom: Returns empty list (custom logic should be implemented via hooks)
        /// </remarks>
        public List<Guid> GetApproverForStep(Guid stepId)
        {
            try
            {
                var step = GetStep(stepId);
                if (step == null)
                {
                    return new List<Guid>();
                }

                var approverTypeValue = step[FIELD_APPROVER_TYPE] as string;
                var approverId = step[FIELD_APPROVER_ID] as Guid?;

                // Parse approver type
                if (!Enum.TryParse<ApproverType>(approverTypeValue, true, out var approverType))
                {
                    // Default to User if parse fails
                    approverType = ApproverType.User;
                }

                switch (approverType)
                {
                    case ApproverType.User:
                        // Return the specific user
                        if (approverId.HasValue)
                        {
                            return new List<Guid> { approverId.Value };
                        }
                        return new List<Guid>();

                    case ApproverType.Role:
                        // Get all users with this role
                        if (approverId.HasValue)
                        {
                            return GetUsersInRole(approverId.Value);
                        }
                        return new List<Guid>();

                    case ApproverType.Manager:
                        // Resolve manager from org structure
                        // For now, return the fallback approver_id if provided
                        if (approverId.HasValue)
                        {
                            return new List<Guid> { approverId.Value };
                        }
                        return new List<Guid>();

                    case ApproverType.Custom:
                        // Custom logic hook point - return empty list
                        // Custom resolvers should be implemented via hooks
                        return new List<Guid>();

                    default:
                        return new List<Guid>();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting approvers for step {stepId}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets the list of authorized approver user IDs for a specific approval step.
        /// This is an alias for GetApproverForStep for API consistency.
        /// </summary>
        /// <param name="stepId">The approval step ID to get approvers for.</param>
        /// <returns>
        /// A list of user GUIDs who are authorized to approve at this step.
        /// </returns>
        public List<Guid> GetApproversForStep(Guid stepId)
        {
            return GetApproverForStep(stepId);
        }

        /// <summary>
        /// Determines the appropriate workflow and initial step for a new record
        /// based on entity matching and rule evaluation.
        /// </summary>
        /// <param name="recordId">The ID of the record requiring approval.</param>
        /// <param name="entityName">The entity name of the record.</param>
        /// <returns>
        /// A tuple containing (workflowId, stepId) if a matching workflow is found,
        /// or null if no applicable workflow exists for this entity.
        /// </returns>
        /// <remarks>
        /// This method:
        /// 1. Finds all active workflows targeting the specified entity
        /// 2. For each workflow, evaluates rules against the source record
        /// 3. Returns the first matching workflow and its first step
        /// 
        /// Workflows are matched based on:
        /// - entity_name field matching the source entity
        /// - is_active = true
        /// - Rules evaluation passing (or no rules defined)
        /// </remarks>
        public (Guid workflowId, Guid stepId)? DetermineRoute(Guid recordId, string entityName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(entityName))
                {
                    return null;
                }

                // Query for active workflows targeting this entity
                var eqlCommand = $@"SELECT * FROM {ENTITY_WORKFLOW} 
                                    WHERE {FIELD_ENTITY_NAME} = @entityName 
                                    AND {FIELD_IS_ACTIVE} = @isActive";

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("entityName", entityName),
                    new EqlParameter("isActive", true)
                };

                var workflows = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (workflows == null || !workflows.Any())
                {
                    return null;
                }

                // Get source record for rule evaluation
                var sourceRecord = GetSourceRecord(recordId, entityName);

                // Evaluate each workflow
                foreach (var workflow in workflows)
                {
                    var workflowId = (Guid)workflow[FIELD_ID];

                    // Evaluate rules for this workflow
                    var rulesPass = EvaluateRules(workflowId, sourceRecord);

                    if (rulesPass)
                    {
                        // Get first step for this workflow
                        var firstStepId = GetFirstStepForWorkflow(workflowId);

                        if (firstStepId.HasValue)
                        {
                            return (workflowId, firstStepId.Value);
                        }
                    }
                }

                // No matching workflow found
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error determining route for record {recordId} (entity: {entityName}): {ex.Message}", ex);
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Retrieves an approval request record by ID.
        /// </summary>
        /// <param name="requestId">The request ID to retrieve.</param>
        /// <returns>The EntityRecord for the request, or null if not found.</returns>
        private EntityRecord GetApprovalRequest(Guid requestId)
        {
            var eqlCommand = $"SELECT * FROM {ENTITY_REQUEST} WHERE {FIELD_ID} = @requestId";
            var eqlParams = new List<EqlParameter> { new EqlParameter("requestId", requestId) };

            var result = new EqlCommand(eqlCommand, eqlParams).Execute();

            return result?.FirstOrDefault();
        }

        /// <summary>
        /// Retrieves an approval step record by ID.
        /// </summary>
        /// <param name="stepId">The step ID to retrieve.</param>
        /// <returns>The EntityRecord for the step, or null if not found.</returns>
        private EntityRecord GetStep(Guid stepId)
        {
            var eqlCommand = $"SELECT * FROM {ENTITY_STEP} WHERE {FIELD_ID} = @stepId";
            var eqlParams = new List<EqlParameter> { new EqlParameter("stepId", stepId) };

            var result = new EqlCommand(eqlCommand, eqlParams).Execute();

            return result?.FirstOrDefault();
        }

        /// <summary>
        /// Gets the first step for a workflow (step with lowest step_order).
        /// </summary>
        /// <param name="workflowId">The workflow ID to find the first step for.</param>
        /// <returns>The ID of the first step, or null if no steps exist.</returns>
        private Guid? GetFirstStepForWorkflow(Guid workflowId)
        {
            var eqlCommand = $@"SELECT * FROM {ENTITY_STEP} 
                                WHERE {FIELD_WORKFLOW_ID} = @workflowId 
                                ORDER BY {FIELD_STEP_ORDER} ASC 
                                PAGE 1 PAGESIZE 1";

            var eqlParams = new List<EqlParameter> { new EqlParameter("workflowId", workflowId) };

            var result = new EqlCommand(eqlCommand, eqlParams).Execute();

            if (result != null && result.Any())
            {
                return (Guid)result[0][FIELD_ID];
            }

            return null;
        }

        /// <summary>
        /// Gets all users who have a specific role.
        /// </summary>
        /// <param name="roleId">The role ID to get users for.</param>
        /// <returns>List of user IDs with the specified role.</returns>
        private List<Guid> GetUsersInRole(Guid roleId)
        {
            try
            {
                var users = SecMan.GetUsers(roleId);
                return users?.Select(u => u.Id).ToList() ?? new List<Guid>();
            }
            catch (Exception)
            {
                // If role lookup fails, return empty list
                return new List<Guid>();
            }
        }

        /// <summary>
        /// Retrieves a source record by ID and entity name.
        /// </summary>
        /// <param name="recordId">The record ID to retrieve.</param>
        /// <param name="entityName">The entity name containing the record.</param>
        /// <returns>The EntityRecord, or null if not found.</returns>
        private EntityRecord GetSourceRecord(Guid recordId, string entityName)
        {
            if (string.IsNullOrWhiteSpace(entityName))
            {
                return null;
            }

            try
            {
                var eqlCommand = $"SELECT * FROM {entityName} WHERE {FIELD_ID} = @recordId";
                var eqlParams = new List<EqlParameter> { new EqlParameter("recordId", recordId) };

                var result = new EqlCommand(eqlCommand, eqlParams).Execute();

                return result?.FirstOrDefault();
            }
            catch (Exception)
            {
                // Entity might not exist or query failed - return null
                return null;
            }
        }

        /// <summary>
        /// Gets all rules for a list of step IDs.
        /// </summary>
        /// <param name="stepIds">The step IDs to get rules for.</param>
        /// <returns>List of rule records.</returns>
        private EntityRecordList GetRulesForSteps(List<Guid> stepIds)
        {
            if (stepIds == null || !stepIds.Any())
            {
                return new EntityRecordList();
            }

            var eqlParams = new List<EqlParameter>();
            var conditions = new List<string>();

            for (int i = 0; i < stepIds.Count; i++)
            {
                var paramName = $"stepId{i}";
                conditions.Add($"{FIELD_STEP_ID} = @{paramName}");
                eqlParams.Add(new EqlParameter(paramName, stepIds[i]));
            }

            var whereClause = string.Join(" OR ", conditions);
            var eqlCommand = $"SELECT * FROM {ENTITY_RULE} WHERE {whereClause}";

            return new EqlCommand(eqlCommand, eqlParams).Execute();
        }

        /// <summary>
        /// Evaluates if a step applies to a record based on its rules.
        /// </summary>
        /// <param name="step">The step record to evaluate.</param>
        /// <param name="recordId">The record ID to check.</param>
        /// <param name="entityName">The entity name of the record.</param>
        /// <param name="workflowId">The workflow ID for additional context.</param>
        /// <returns>True if the step applies to this record, false otherwise.</returns>
        private bool StepAppliesToRecord(EntityRecord step, Guid recordId, string entityName, Guid workflowId)
        {
            if (step == null)
            {
                return false;
            }

            var stepId = (Guid)step[FIELD_ID];

            // Get rules for this specific step
            var rules = GetRulesForSteps(new List<Guid> { stepId });

            if (rules == null || !rules.Any())
            {
                // No rules means step always applies
                return true;
            }

            // Get source record
            var sourceRecord = GetSourceRecord(recordId, entityName);
            if (sourceRecord == null)
            {
                // No source record, step applies by default
                return true;
            }

            // Evaluate rules - any match means step applies
            foreach (var rule in rules)
            {
                if (EvaluateRuleCondition(rule, sourceRecord))
                {
                    return true;
                }
            }

            // No rules matched
            return false;
        }

        /// <summary>
        /// Evaluates a single rule condition against a source record.
        /// </summary>
        /// <param name="rule">The rule record containing field_name, operator, and value.</param>
        /// <param name="sourceRecord">The source record to evaluate against.</param>
        /// <returns>True if the rule condition is satisfied, false otherwise.</returns>
        private bool EvaluateRuleCondition(EntityRecord rule, EntityRecord sourceRecord)
        {
            if (rule == null || sourceRecord == null)
            {
                return false;
            }

            var fieldName = rule[FIELD_RULE_FIELD_NAME] as string;
            var operatorStr = rule[FIELD_RULE_OPERATOR] as string;
            var ruleValue = rule[FIELD_RULE_VALUE] as string;

            if (string.IsNullOrWhiteSpace(fieldName))
            {
                return false;
            }

            // Check if the field exists in the source record
            if (!sourceRecord.Properties.ContainsKey(fieldName))
            {
                return false;
            }

            var fieldValue = sourceRecord[fieldName];

            // Parse operator
            if (!Enum.TryParse<RuleOperator>(operatorStr, true, out var ruleOperator))
            {
                // Default to Equals if parse fails
                ruleOperator = RuleOperator.Equals;
            }

            // Convert field value to string for comparison
            var fieldValueStr = fieldValue?.ToString() ?? string.Empty;
            ruleValue = ruleValue ?? string.Empty;

            // Evaluate based on operator using switch expression
            return ruleOperator switch
            {
                RuleOperator.Equals => EvaluateEquals(fieldValue, ruleValue),
                RuleOperator.NotEquals => !EvaluateEquals(fieldValue, ruleValue),
                RuleOperator.GreaterThan => EvaluateGreaterThan(fieldValue, ruleValue),
                RuleOperator.LessThan => EvaluateLessThan(fieldValue, ruleValue),
                RuleOperator.Contains => fieldValueStr.Contains(ruleValue, StringComparison.OrdinalIgnoreCase),
                RuleOperator.StartsWith => fieldValueStr.StartsWith(ruleValue, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }

        /// <summary>
        /// Evaluates equality between a field value and a rule value.
        /// Handles type conversions for numbers and dates.
        /// </summary>
        /// <param name="fieldValue">The field value from the record.</param>
        /// <param name="ruleValue">The rule value to compare against.</param>
        /// <returns>True if values are equal.</returns>
        private bool EvaluateEquals(object fieldValue, string ruleValue)
        {
            if (fieldValue == null)
            {
                return string.IsNullOrEmpty(ruleValue);
            }

            var fieldValueStr = fieldValue.ToString();

            // Try numeric comparison
            if (decimal.TryParse(fieldValueStr, out var fieldDecimal) &&
                decimal.TryParse(ruleValue, out var ruleDecimal))
            {
                return fieldDecimal == ruleDecimal;
            }

            // Try GUID comparison
            if (fieldValue is Guid fieldGuid)
            {
                if (Guid.TryParse(ruleValue, out var ruleGuid))
                {
                    return fieldGuid == ruleGuid;
                }
            }

            // Try DateTime comparison
            if (fieldValue is DateTime fieldDateTime)
            {
                if (DateTime.TryParse(ruleValue, out var ruleDateTime))
                {
                    return fieldDateTime == ruleDateTime;
                }
            }

            // String comparison (case-insensitive)
            return string.Equals(fieldValueStr, ruleValue, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Evaluates if a field value is greater than a rule value.
        /// Supports numeric and date comparisons.
        /// </summary>
        /// <param name="fieldValue">The field value from the record.</param>
        /// <param name="ruleValue">The rule value to compare against.</param>
        /// <returns>True if field value is greater than rule value.</returns>
        private bool EvaluateGreaterThan(object fieldValue, string ruleValue)
        {
            if (fieldValue == null)
            {
                return false;
            }

            var fieldValueStr = fieldValue.ToString();

            // Try numeric comparison
            if (decimal.TryParse(fieldValueStr, out var fieldDecimal) &&
                decimal.TryParse(ruleValue, out var ruleDecimal))
            {
                return fieldDecimal > ruleDecimal;
            }

            // Try DateTime comparison
            if (fieldValue is DateTime fieldDateTime)
            {
                if (DateTime.TryParse(ruleValue, out var ruleDateTime))
                {
                    return fieldDateTime > ruleDateTime;
                }
            }

            // Try string comparison for DateTime strings
            if (DateTime.TryParse(fieldValueStr, out var parsedFieldDate) &&
                DateTime.TryParse(ruleValue, out var parsedRuleDate))
            {
                return parsedFieldDate > parsedRuleDate;
            }

            // String comparison
            return string.Compare(fieldValueStr, ruleValue, StringComparison.OrdinalIgnoreCase) > 0;
        }

        /// <summary>
        /// Evaluates if a field value is less than a rule value.
        /// Supports numeric and date comparisons.
        /// </summary>
        /// <param name="fieldValue">The field value from the record.</param>
        /// <param name="ruleValue">The rule value to compare against.</param>
        /// <returns>True if field value is less than rule value.</returns>
        private bool EvaluateLessThan(object fieldValue, string ruleValue)
        {
            if (fieldValue == null)
            {
                return false;
            }

            var fieldValueStr = fieldValue.ToString();

            // Try numeric comparison
            if (decimal.TryParse(fieldValueStr, out var fieldDecimal) &&
                decimal.TryParse(ruleValue, out var ruleDecimal))
            {
                return fieldDecimal < ruleDecimal;
            }

            // Try DateTime comparison
            if (fieldValue is DateTime fieldDateTime)
            {
                if (DateTime.TryParse(ruleValue, out var ruleDateTime))
                {
                    return fieldDateTime < ruleDateTime;
                }
            }

            // Try string comparison for DateTime strings
            if (DateTime.TryParse(fieldValueStr, out var parsedFieldDate) &&
                DateTime.TryParse(ruleValue, out var parsedRuleDate))
            {
                return parsedFieldDate < parsedRuleDate;
            }

            // String comparison
            return string.Compare(fieldValueStr, ruleValue, StringComparison.OrdinalIgnoreCase) < 0;
        }

        #endregion
    }
}
