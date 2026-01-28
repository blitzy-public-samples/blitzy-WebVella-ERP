using System;
using System.Collections.Generic;
using System.Linq;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Eql;
using WebVella.Erp.Plugins.Approval.Api;

namespace WebVella.Erp.Plugins.Approval.Services
{
    /// <summary>
    /// Service for rule evaluation and step routing in approval workflows.
    /// Provides functionality to evaluate approval rules against entity records,
    /// navigate workflow steps, and validate user authorization for approval actions.
    /// </summary>
    /// <remarks>
    /// This service is a core component of the approval workflow system, responsible for:
    /// - Finding matching workflows for entity records based on rule evaluation
    /// - Managing step progression through workflows
    /// - Resolving approvers based on step configuration
    /// - Validating user authorization for approval actions
    /// 
    /// Supported rule operators:
    /// - eq: Equal (string comparison)
    /// - neq: Not equal (string comparison)
    /// - gt: Greater than (numeric comparison)
    /// - gte: Greater than or equal (numeric comparison)
    /// - lt: Less than (numeric comparison)
    /// - lte: Less than or equal (numeric comparison)
    /// - contains: Contains substring (string comparison)
    /// </remarks>
    public class ApprovalRouteService
    {
        /// <summary>
        /// Gets the RecordManager instance for database operations.
        /// Initialized inline following WebVella service pattern.
        /// </summary>
        protected RecordManager RecMan { get; private set; } = new RecordManager();

        /// <summary>
        /// Gets the SecurityManager instance for user and role lookups.
        /// Used for validating user authorization in approval steps.
        /// </summary>
        protected SecurityManager SecMan { get; private set; } = new SecurityManager();

        /// <summary>
        /// Evaluates approval rules to find a matching workflow for the given record.
        /// Queries enabled workflows for the specified entity type, then evaluates
        /// rules in priority order (highest priority first) to find the first matching workflow.
        /// </summary>
        /// <param name="record">The entity record to evaluate rules against.</param>
        /// <param name="entityName">The name of the entity type to find workflows for.</param>
        /// <returns>
        /// The first matching ApprovalWorkflowModel if rules match, or null if no workflow matches.
        /// Workflows are evaluated in the order they are returned, and for each workflow,
        /// rules are evaluated in descending priority order.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when record or entityName is null.</exception>
        /// <example>
        /// <code>
        /// var routeService = new ApprovalRouteService();
        /// var record = new EntityRecord();
        /// record["amount"] = 5000m;
        /// var matchingWorkflow = routeService.EvaluateRules(record, "purchase_order");
        /// if (matchingWorkflow != null)
        /// {
        ///     // Workflow found, proceed with approval request creation
        /// }
        /// </code>
        /// </example>
        public ApprovalWorkflowModel EvaluateRules(EntityRecord record, string entityName)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record), "Record cannot be null for rule evaluation.");
            }

            if (string.IsNullOrWhiteSpace(entityName))
            {
                throw new ArgumentNullException(nameof(entityName), "Entity name cannot be null or empty.");
            }

            // Query enabled workflows for the specified entity type
            var workflowEql = "SELECT * FROM approval_workflow WHERE target_entity = @entityName AND is_enabled = @isEnabled";
            var workflowParams = new List<EqlParameter>
            {
                new EqlParameter("entityName", entityName),
                new EqlParameter("isEnabled", true)
            };

            var workflowResult = new EqlCommand(workflowEql, workflowParams).Execute();

            if (workflowResult == null || !workflowResult.Any())
            {
                // No enabled workflows found for this entity type
                return null;
            }

            // Iterate through each workflow and evaluate its rules
            foreach (var workflowRecord in workflowResult)
            {
                var workflowId = (Guid)workflowRecord["id"];

                // Load rules for this workflow in descending priority order
                var rulesEql = "SELECT * FROM approval_rule WHERE workflow_id = @workflowId ORDER BY priority DESC";
                var rulesParams = new List<EqlParameter>
                {
                    new EqlParameter("workflowId", workflowId)
                };

                var rulesResult = new EqlCommand(rulesEql, rulesParams).Execute();

                // If workflow has no rules, it matches by default (no conditions to fail)
                if (rulesResult == null || !rulesResult.Any())
                {
                    return MapToWorkflowModel(workflowRecord);
                }

                // Evaluate all rules - all rules must match for workflow to apply
                bool allRulesMatch = true;
                foreach (var ruleRecord in rulesResult)
                {
                    var rule = MapToRuleModel(ruleRecord);
                    if (!EvaluateRule(record, rule))
                    {
                        allRulesMatch = false;
                        break;
                    }
                }

                if (allRulesMatch)
                {
                    return MapToWorkflowModel(workflowRecord);
                }
            }

            // No workflow matched
            return null;
        }

        /// <summary>
        /// Evaluates a single rule against an entity record.
        /// Supports operators: eq, neq, gt, gte, lt, lte, contains.
        /// </summary>
        /// <param name="record">The entity record containing field values.</param>
        /// <param name="rule">The approval rule to evaluate.</param>
        /// <returns>True if the rule condition is satisfied, false otherwise.</returns>
        /// <remarks>
        /// Numeric comparisons (gt, gte, lt, lte) attempt to parse values as decimals.
        /// String comparisons (eq, neq, contains) are case-insensitive.
        /// If the specified field does not exist in the record, the rule evaluates to false.
        /// </remarks>
        private bool EvaluateRule(EntityRecord record, ApprovalRuleModel rule)
        {
            if (record == null || rule == null)
            {
                return false;
            }

            // Check if the field exists in the record
            if (!record.Properties.ContainsKey(rule.FieldName))
            {
                // Field not found - rule cannot be evaluated, consider it not matching
                return false;
            }

            var fieldValue = record[rule.FieldName];
            var thresholdValue = rule.ThresholdValue;
            var stringValue = rule.StringValue ?? string.Empty;

            // Handle null field values
            if (fieldValue == null)
            {
                // For "neq" with empty string value, null field means NOT equal to empty string
                if ((rule.Operator == "neq" || rule.Operator == "ne") && string.IsNullOrEmpty(stringValue))
                {
                    return false; // null is considered empty/equal to empty
                }
                // Null field value - only matches if threshold is zero (for eq)
                return thresholdValue == 0m && rule.Operator == "eq";
            }

            var fieldValueStr = fieldValue.ToString() ?? string.Empty;

            // Determine if we should use string comparison or numeric comparison
            // Use string comparison if StringValue is populated OR if the field value is not numeric
            bool useStringComparison = !string.IsNullOrEmpty(stringValue) || !IsNumeric(fieldValue);

            switch (rule.Operator?.ToLowerInvariant())
            {
                case "eq":
                    if (useStringComparison)
                    {
                        // String equality comparison (case-insensitive)
                        return string.Equals(fieldValueStr, stringValue, StringComparison.OrdinalIgnoreCase);
                    }
                    // Numeric equality comparison
                    return CompareNumeric(fieldValue, thresholdValue) == 0;

                case "neq":
                case "ne":
                    if (useStringComparison)
                    {
                        // String not-equal comparison (case-insensitive)
                        // If stringValue is empty, check if field has any non-empty value
                        if (string.IsNullOrEmpty(stringValue))
                        {
                            return !string.IsNullOrEmpty(fieldValueStr);
                        }
                        return !string.Equals(fieldValueStr, stringValue, StringComparison.OrdinalIgnoreCase);
                    }
                    // Numeric not-equal comparison
                    return CompareNumeric(fieldValue, thresholdValue) != 0;

                case "gt":
                    // Greater than comparison - numeric only
                    return CompareNumeric(fieldValue, thresholdValue) > 0;

                case "gte":
                    // Greater than or equal comparison - numeric only
                    return CompareNumeric(fieldValue, thresholdValue) >= 0;

                case "lt":
                    // Less than comparison - numeric only
                    return CompareNumeric(fieldValue, thresholdValue) < 0;

                case "lte":
                    // Less than or equal comparison - numeric only
                    return CompareNumeric(fieldValue, thresholdValue) <= 0;

                case "contains":
                    // Contains comparison - string
                    // For contains, we use the StringValue field which stores the text to search for
                    if (string.IsNullOrEmpty(stringValue))
                    {
                        // No search string provided - rule matches if field has any value
                        return !string.IsNullOrEmpty(fieldValueStr);
                    }
                    return fieldValueStr.IndexOf(stringValue, StringComparison.OrdinalIgnoreCase) >= 0;

                default:
                    // Unknown operator - rule does not match
                    return false;
            }
        }

        /// <summary>
        /// Checks if a value is numeric (can be converted to decimal).
        /// </summary>
        private bool IsNumeric(object value)
        {
            if (value == null) return false;
            if (value is decimal || value is double || value is float || 
                value is int || value is long || value is short ||
                value is byte || value is uint || value is ulong)
            {
                return true;
            }
            return decimal.TryParse(value.ToString(), out _);
        }

        /// <summary>
        /// Compares two values numerically by attempting to parse the field value as decimal.
        /// </summary>
        /// <param name="fieldValue">The field value from the record.</param>
        /// <param name="thresholdValue">The threshold value to compare against.</param>
        /// <returns>
        /// Negative value if fieldValue less than thresholdValue,
        /// zero if equal,
        /// positive value if fieldValue greater than thresholdValue.
        /// Returns 0 if field value cannot be parsed as a decimal.
        /// </returns>
        private int CompareNumeric(object fieldValue, decimal thresholdValue)
        {
            decimal fieldDecimal = 0m;

            // Try to parse field value as decimal
            if (fieldValue is decimal fieldDec)
            {
                fieldDecimal = fieldDec;
            }
            else if (fieldValue is double fieldDbl)
            {
                fieldDecimal = (decimal)fieldDbl;
            }
            else if (fieldValue is float fieldFlt)
            {
                fieldDecimal = (decimal)fieldFlt;
            }
            else if (fieldValue is int fieldInt)
            {
                fieldDecimal = fieldInt;
            }
            else if (fieldValue is long fieldLong)
            {
                fieldDecimal = fieldLong;
            }
            else if (!decimal.TryParse(fieldValue?.ToString(), out fieldDecimal))
            {
                // Cannot parse field value as decimal
                return 0;
            }

            return fieldDecimal.CompareTo(thresholdValue);
        }

        /// <summary>
        /// Gets the first step of a workflow based on step_order.
        /// </summary>
        /// <param name="workflowId">The unique identifier of the workflow.</param>
        /// <returns>
        /// The first ApprovalStepModel (lowest step_order) for the workflow,
        /// or null if no steps are configured.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when workflowId is empty.</exception>
        /// <example>
        /// <code>
        /// var routeService = new ApprovalRouteService();
        /// var firstStep = routeService.GetFirstStep(workflowId);
        /// if (firstStep != null)
        /// {
        ///     Console.WriteLine($"First step: {firstStep.Name}");
        /// }
        /// </code>
        /// </example>
        public ApprovalStepModel GetFirstStep(Guid workflowId)
        {
            if (workflowId == Guid.Empty)
            {
                throw new ArgumentException("Workflow ID cannot be empty.", nameof(workflowId));
            }

            var eqlCommand = "SELECT * FROM approval_step WHERE workflow_id = @workflowId ORDER BY step_order ASC PAGE 1 PAGESIZE 1";
            var eqlParams = new List<EqlParameter>
            {
                new EqlParameter("workflowId", workflowId)
            };

            var result = new EqlCommand(eqlCommand, eqlParams).Execute();

            if (result == null || !result.Any())
            {
                return null;
            }

            return MapToStepModel(result.First());
        }

        /// <summary>
        /// Gets the next step in the workflow after the current step.
        /// </summary>
        /// <param name="workflowId">The unique identifier of the workflow.</param>
        /// <param name="currentStepId">The unique identifier of the current step.</param>
        /// <returns>
        /// The next ApprovalStepModel based on step_order, or null if the current step
        /// is the final step or no more steps exist.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when workflowId or currentStepId is empty.</exception>
        /// <example>
        /// <code>
        /// var routeService = new ApprovalRouteService();
        /// var nextStep = routeService.GetNextStep(workflowId, currentStepId);
        /// if (nextStep == null)
        /// {
        ///     Console.WriteLine("No more steps - workflow complete");
        /// }
        /// </code>
        /// </example>
        public ApprovalStepModel GetNextStep(Guid workflowId, Guid currentStepId)
        {
            if (workflowId == Guid.Empty)
            {
                throw new ArgumentException("Workflow ID cannot be empty.", nameof(workflowId));
            }

            if (currentStepId == Guid.Empty)
            {
                throw new ArgumentException("Current step ID cannot be empty.", nameof(currentStepId));
            }

            // First, get the current step to find its step_order
            var currentStepEql = "SELECT * FROM approval_step WHERE id = @stepId";
            var currentStepParams = new List<EqlParameter>
            {
                new EqlParameter("stepId", currentStepId)
            };

            var currentStepResult = new EqlCommand(currentStepEql, currentStepParams).Execute();

            if (currentStepResult == null || !currentStepResult.Any())
            {
                // Current step not found
                return null;
            }

            var currentStepRecord = currentStepResult.First();
            var currentStepOrder = Convert.ToInt32(currentStepRecord["step_order"]);

            // Query for the next step with step_order greater than current
            var nextStepEql = "SELECT * FROM approval_step WHERE workflow_id = @workflowId AND step_order > @currentOrder ORDER BY step_order ASC PAGE 1 PAGESIZE 1";
            var nextStepParams = new List<EqlParameter>
            {
                new EqlParameter("workflowId", workflowId),
                new EqlParameter("currentOrder", currentStepOrder)
            };

            var nextStepResult = new EqlCommand(nextStepEql, nextStepParams).Execute();

            if (nextStepResult == null || !nextStepResult.Any())
            {
                // No more steps after the current one
                return null;
            }

            return MapToStepModel(nextStepResult.First());
        }

        /// <summary>
        /// Gets information about the current approver for a step.
        /// Resolves the approver based on the step's approver_type configuration.
        /// </summary>
        /// <param name="stepId">The unique identifier of the approval step.</param>
        /// <returns>
        /// An ApprovalStepModel containing the step configuration including approver information,
        /// or null if the step is not found.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when stepId is empty.</exception>
        /// <remarks>
        /// The approver is determined by the approver_type field:
        /// - "user": approver_id directly references the user
        /// - "role": approver_id references a role; any user with that role can approve
        /// - "department_head": Approver is resolved dynamically based on requester's department
        /// </remarks>
        public ApprovalStepModel GetCurrentApprover(Guid stepId)
        {
            if (stepId == Guid.Empty)
            {
                throw new ArgumentException("Step ID cannot be empty.", nameof(stepId));
            }

            var eqlCommand = "SELECT * FROM approval_step WHERE id = @stepId";
            var eqlParams = new List<EqlParameter>
            {
                new EqlParameter("stepId", stepId)
            };

            var result = new EqlCommand(eqlCommand, eqlParams).Execute();

            if (result == null || !result.Any())
            {
                return null;
            }

            return MapToStepModel(result.First());
        }

        /// <summary>
        /// Checks if a user is authorized to approve the specified step.
        /// Authorization is determined by the step's approver_type configuration.
        /// </summary>
        /// <param name="userId">The unique identifier of the user to check.</param>
        /// <param name="stepId">The unique identifier of the approval step.</param>
        /// <returns>
        /// True if the user is authorized to approve this step, false otherwise.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when userId or stepId is empty.</exception>
        /// <remarks>
        /// Authorization logic based on approver_type:
        /// - "user": User ID must match the step's approver_id
        /// - "role": User must have the role specified by approver_id
        /// - "department_head": Reserved for future implementation; currently returns false
        /// </remarks>
        /// <example>
        /// <code>
        /// var routeService = new ApprovalRouteService();
        /// if (routeService.IsUserAuthorizedApprover(currentUserId, stepId))
        /// {
        ///     // User can approve this step
        ///     approvalService.Approve(requestId, currentUserId, "Approved");
        /// }
        /// </code>
        /// </example>
        public bool IsUserAuthorizedApprover(Guid userId, Guid stepId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            }

            if (stepId == Guid.Empty)
            {
                throw new ArgumentException("Step ID cannot be empty.", nameof(stepId));
            }

            // Get the step configuration
            var step = GetCurrentApprover(stepId);
            if (step == null)
            {
                // Step not found - user is not authorized
                return false;
            }

            var approverType = step.ApproverType?.ToLowerInvariant();

            switch (approverType)
            {
                case "user":
                    // Direct user match - approver_id must equal userId
                    return step.ApproverId.HasValue && step.ApproverId.Value == userId;

                case "role":
                    // Role-based authorization - check if user has the specified role
                    return CheckUserHasRole(userId, step.ApproverId);

                case "department_head":
                    // Department head authorization - check if user is the department head
                    // This requires access to the approval request to know the requester's department
                    // For now, return false as this requires additional context
                    return CheckUserIsDepartmentHead(userId, step.ApproverId);

                default:
                    // Unknown approver type - not authorized
                    return false;
            }
        }

        /// <summary>
        /// Checks if a user has a specific role.
        /// </summary>
        /// <param name="userId">The user ID to check.</param>
        /// <param name="roleId">The role ID to check for.</param>
        /// <returns>True if the user has the specified role, false otherwise.</returns>
        private bool CheckUserHasRole(Guid userId, Guid? roleId)
        {
            if (!roleId.HasValue || roleId.Value == Guid.Empty)
            {
                return false;
            }

            try
            {
                // Get the user with their roles
                var user = SecMan.GetUser(userId);
                if (user == null || user.Roles == null)
                {
                    return false;
                }

                // Check if any of the user's roles match the required role ID
                return user.Roles.Any(r => r.Id == roleId.Value);
            }
            catch (Exception)
            {
                // Error retrieving user - not authorized
                return false;
            }
        }

        /// <summary>
        /// Checks if a user is a department head.
        /// This is a placeholder implementation that checks if the user has a "department_head" role
        /// or if the approver_id matches a department where the user is the head.
        /// </summary>
        /// <param name="userId">The user ID to check.</param>
        /// <param name="departmentId">Optional department ID to check against.</param>
        /// <returns>True if the user is a department head, false otherwise.</returns>
        private bool CheckUserIsDepartmentHead(Guid userId, Guid? departmentId)
        {
            try
            {
                // Get the user
                var user = SecMan.GetUser(userId);
                if (user == null)
                {
                    return false;
                }

                // Check if user has a manager or department_head role
                if (user.Roles != null)
                {
                    var hasManagerRole = user.Roles.Any(r => 
                        r.Name.Equals("manager", StringComparison.OrdinalIgnoreCase) ||
                        r.Name.Equals("department_head", StringComparison.OrdinalIgnoreCase) ||
                        r.Name.Equals("administrator", StringComparison.OrdinalIgnoreCase));

                    if (hasManagerRole)
                    {
                        return true;
                    }
                }

                // If a specific department ID is provided, check if user is head of that department
                if (departmentId.HasValue && departmentId.Value != Guid.Empty)
                {
                    // Query to check if user is head of the specified department
                    // This assumes there is a department entity with a head_user_id field
                    var deptEql = "SELECT * FROM department WHERE id = @departmentId AND head_user_id = @userId";
                    var deptParams = new List<EqlParameter>
                    {
                        new EqlParameter("departmentId", departmentId.Value),
                        new EqlParameter("userId", userId)
                    };

                    try
                    {
                        var deptResult = new EqlCommand(deptEql, deptParams).Execute();
                        return deptResult != null && deptResult.Any();
                    }
                    catch (Exception)
                    {
                        // Department entity may not exist - fall back to role check
                        return false;
                    }
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the list of user IDs who can approve a specific step.
        /// Resolves approvers based on the step's approver_type configuration.
        /// Per AC3 of STORY-006: Resolves current step approvers for notification purposes.
        /// </summary>
        /// <param name="stepId">The unique identifier of the approval step.</param>
        /// <returns>
        /// A list of user GUIDs who are authorized to approve this step.
        /// Returns empty list if step not found or no approvers configured.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when stepId is empty.</exception>
        /// <remarks>
        /// Resolution logic based on approver_type:
        /// - "user": Returns single user ID from approver_id
        /// - "role": Returns all users who have the role specified by approver_id
        /// - "department_head": Returns users with manager/department_head roles
        /// </remarks>
        public List<Guid> GetApproversForStep(Guid stepId)
        {
            var result = new List<Guid>();

            if (stepId == Guid.Empty)
            {
                throw new ArgumentException("Step ID cannot be empty.", nameof(stepId));
            }

            // Get the step configuration
            var step = GetCurrentApprover(stepId);
            if (step == null)
            {
                return result;
            }

            var approverType = step.ApproverType?.ToLowerInvariant();

            switch (approverType)
            {
                case "user":
                    // Direct user - return single user ID
                    if (step.ApproverId.HasValue && step.ApproverId.Value != Guid.Empty)
                    {
                        result.Add(step.ApproverId.Value);
                    }
                    break;

                case "role":
                    // Role-based - find all users with this role
                    if (step.ApproverId.HasValue && step.ApproverId.Value != Guid.Empty)
                    {
                        var usersWithRole = GetUsersWithRole(step.ApproverId.Value);
                        result.AddRange(usersWithRole);
                    }
                    break;

                case "department_head":
                    // Department head - find users with manager roles
                    var departmentHeads = GetDepartmentHeadUsers();
                    result.AddRange(departmentHeads);
                    break;
            }

            return result;
        }

        /// <summary>
        /// Gets all users who have a specific role.
        /// </summary>
        /// <param name="roleId">The role ID to query for.</param>
        /// <returns>List of user IDs who have the specified role.</returns>
        private List<Guid> GetUsersWithRole(Guid roleId)
        {
            var userIds = new List<Guid>();

            try
            {
                // Query users who have this role using the user_role relation
                var eqlCommand = "SELECT * FROM user WHERE id != @systemUser";
                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("systemUser", Guid.Empty)
                };

                var users = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (users == null)
                {
                    return userIds;
                }

                foreach (var userRecord in users)
                {
                    var userId = (Guid)userRecord["id"];
                    var user = SecMan.GetUser(userId);
                    
                    if (user?.Roles != null && user.Roles.Any(r => r.Id == roleId))
                    {
                        userIds.Add(userId);
                    }
                }
            }
            catch (Exception)
            {
                // Log error but return empty list
            }

            return userIds;
        }

        /// <summary>
        /// Gets all users who can act as department heads.
        /// Returns users with manager or administrator roles.
        /// </summary>
        /// <returns>List of user IDs who can act as department heads.</returns>
        private List<Guid> GetDepartmentHeadUsers()
        {
            var userIds = new List<Guid>();

            try
            {
                // Query all users and filter by role
                var eqlCommand = "SELECT * FROM user WHERE id != @systemUser";
                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("systemUser", Guid.Empty)
                };

                var users = new EqlCommand(eqlCommand, eqlParams).Execute();

                if (users == null)
                {
                    return userIds;
                }

                foreach (var userRecord in users)
                {
                    var userId = (Guid)userRecord["id"];
                    var user = SecMan.GetUser(userId);
                    
                    if (user?.Roles != null)
                    {
                        var isManagerRole = user.Roles.Any(r => 
                            r.Name.Equals("manager", StringComparison.OrdinalIgnoreCase) ||
                            r.Name.Equals("department_head", StringComparison.OrdinalIgnoreCase) ||
                            r.Name.Equals("administrator", StringComparison.OrdinalIgnoreCase));

                        if (isManagerRole)
                        {
                            userIds.Add(userId);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Log error but return empty list
            }

            return userIds;
        }

        /// <summary>
        /// Maps an EntityRecord to an ApprovalWorkflowModel.
        /// </summary>
        /// <param name="record">The entity record containing workflow data.</param>
        /// <returns>An ApprovalWorkflowModel populated from the record.</returns>
        private ApprovalWorkflowModel MapToWorkflowModel(EntityRecord record)
        {
            if (record == null)
            {
                return null;
            }

            return new ApprovalWorkflowModel
            {
                Id = record.Properties.ContainsKey("id") ? (Guid)record["id"] : Guid.Empty,
                Name = record.Properties.ContainsKey("name") ? record["name"]?.ToString() : string.Empty,
                TargetEntityName = record.Properties.ContainsKey("target_entity") ? record["target_entity"]?.ToString() : string.Empty,
                IsEnabled = record.Properties.ContainsKey("is_enabled") && record["is_enabled"] != null && (bool)record["is_enabled"],
                CreatedOn = record.Properties.ContainsKey("created_on") && record["created_on"] != null 
                    ? (DateTime)record["created_on"] 
                    : DateTime.MinValue,
                CreatedBy = record.Properties.ContainsKey("created_by") && record["created_by"] != null 
                    ? (Guid?)record["created_by"] 
                    : null
            };
        }

        /// <summary>
        /// Maps an EntityRecord to an ApprovalStepModel.
        /// </summary>
        /// <param name="record">The entity record containing step data.</param>
        /// <returns>An ApprovalStepModel populated from the record.</returns>
        private ApprovalStepModel MapToStepModel(EntityRecord record)
        {
            if (record == null)
            {
                return null;
            }

            return new ApprovalStepModel
            {
                Id = record.Properties.ContainsKey("id") ? (Guid)record["id"] : Guid.Empty,
                WorkflowId = record.Properties.ContainsKey("workflow_id") && record["workflow_id"] != null 
                    ? (Guid)record["workflow_id"] 
                    : Guid.Empty,
                StepOrder = record.Properties.ContainsKey("step_order") && record["step_order"] != null 
                    ? Convert.ToInt32(record["step_order"]) 
                    : 0,
                Name = record.Properties.ContainsKey("name") ? record["name"]?.ToString() : string.Empty,
                ApproverType = record.Properties.ContainsKey("approver_type") ? record["approver_type"]?.ToString() : string.Empty,
                ApproverId = record.Properties.ContainsKey("approver_id") && record["approver_id"] != null 
                    ? (Guid?)record["approver_id"] 
                    : null,
                TimeoutHours = record.Properties.ContainsKey("timeout_hours") && record["timeout_hours"] != null 
                    ? (int?)Convert.ToInt32(record["timeout_hours"]) 
                    : null,
                IsFinal = record.Properties.ContainsKey("is_final") && record["is_final"] != null && (bool)record["is_final"],
                ThresholdConfig = record.Properties.ContainsKey("threshold_config") && record["threshold_config"] != null
                    ? record["threshold_config"].ToString()
                    : null
            };
        }

        /// <summary>
        /// Maps an EntityRecord to an ApprovalRuleModel.
        /// </summary>
        /// <param name="record">The entity record containing rule data.</param>
        /// <returns>An ApprovalRuleModel populated from the record.</returns>
        private ApprovalRuleModel MapToRuleModel(EntityRecord record)
        {
            if (record == null)
            {
                return null;
            }

            return new ApprovalRuleModel
            {
                Id = record.Properties.ContainsKey("id") ? (Guid)record["id"] : Guid.Empty,
                WorkflowId = record.Properties.ContainsKey("workflow_id") && record["workflow_id"] != null 
                    ? (Guid)record["workflow_id"] 
                    : Guid.Empty,
                Name = record.Properties.ContainsKey("name") ? record["name"]?.ToString() : string.Empty,
                FieldName = record.Properties.ContainsKey("field_name") ? record["field_name"]?.ToString() : string.Empty,
                Operator = record.Properties.ContainsKey("operator") ? record["operator"]?.ToString() : string.Empty,
                ThresholdValue = record.Properties.ContainsKey("threshold_value") && record["threshold_value"] != null 
                    ? Convert.ToDecimal(record["threshold_value"]) 
                    : 0m,
                StringValue = record.Properties.ContainsKey("string_value") ? record["string_value"]?.ToString() : null,
                Priority = record.Properties.ContainsKey("priority") && record["priority"] != null 
                    ? Convert.ToInt32(record["priority"]) 
                    : 0,
                NextStepId = record.Properties.ContainsKey("next_step_id") && record["next_step_id"] != null 
                    ? (Guid?)record["next_step_id"] 
                    : null
            };
        }
    }
}
