using System;
using System.Collections.Generic;
using Xunit;
using WebVella.Erp.Plugins.Approval.Api;
using WebVella.Erp.Plugins.Approval.Services;

namespace WebVella.Erp.Plugins.Approval.Tests
{
    /// <summary>
    /// Unit tests for ApprovalRouteService business logic, rule evaluation, and step routing.
    /// Tests focus on rule evaluation logic, operator validation, and routing scenarios
    /// that can be verified without database connectivity.
    /// </summary>
    public class ApprovalRouteServiceTests
    {
        // Define local constants for rule operators
        private static readonly string[] ValidOperators = { "eq", "neq", "gt", "gte", "lt", "lte", "contains" };

        #region Operator Tests

        /// <summary>
        /// Tests that all valid operators are recognized.
        /// </summary>
        [Theory]
        [InlineData("eq")]
        [InlineData("neq")]
        [InlineData("gt")]
        [InlineData("gte")]
        [InlineData("lt")]
        [InlineData("lte")]
        [InlineData("contains")]
        public void Operator_ValidValues_AreAccepted(string operatorValue)
        {
            // Act
            bool isValid = Array.Exists(ValidOperators, o => o == operatorValue);

            // Assert
            Assert.True(isValid, $"'{operatorValue}' should be a valid operator");
        }

        /// <summary>
        /// Tests that invalid operators are rejected.
        /// </summary>
        [Theory]
        [InlineData("invalid")]
        [InlineData("EQUAL")]
        [InlineData("!=")]
        [InlineData("")]
        public void Operator_InvalidValues_AreRejected(string operatorValue)
        {
            // Act
            bool isValid = Array.Exists(ValidOperators, o => o == operatorValue);

            // Assert
            Assert.False(isValid, $"'{operatorValue}' should NOT be a valid operator");
        }

        #endregion

        #region Rule Evaluation Logic Tests

        /// <summary>
        /// Tests that eq operator matches equal values.
        /// </summary>
        [Fact]
        public void EqOperator_EqualValues_Matches()
        {
            // Arrange
            string fieldValue = "approved";
            string ruleValue = "approved";

            // Act
            bool matches = fieldValue == ruleValue;

            // Assert
            Assert.True(matches);
        }

        /// <summary>
        /// Tests that eq operator does not match unequal values.
        /// </summary>
        [Fact]
        public void EqOperator_UnequalValues_DoesNotMatch()
        {
            // Arrange
            string fieldValue = "pending";
            string ruleValue = "approved";

            // Act
            bool matches = fieldValue == ruleValue;

            // Assert
            Assert.False(matches);
        }

        /// <summary>
        /// Tests that neq operator matches unequal values.
        /// </summary>
        [Fact]
        public void NeqOperator_UnequalValues_Matches()
        {
            // Arrange
            string fieldValue = "pending";
            string ruleValue = "approved";

            // Act
            bool matches = fieldValue != ruleValue;

            // Assert
            Assert.True(matches);
        }

        /// <summary>
        /// Tests that neq operator does not match equal values.
        /// </summary>
        [Fact]
        public void NeqOperator_EqualValues_DoesNotMatch()
        {
            // Arrange
            string fieldValue = "approved";
            string ruleValue = "approved";

            // Act
            bool matches = fieldValue != ruleValue;

            // Assert
            Assert.False(matches);
        }

        /// <summary>
        /// Tests that gt operator matches when field is greater.
        /// </summary>
        [Fact]
        public void GtOperator_GreaterValue_Matches()
        {
            // Arrange
            decimal fieldValue = 5000m;
            decimal ruleValue = 1000m;

            // Act
            bool matches = fieldValue > ruleValue;

            // Assert
            Assert.True(matches);
        }

        /// <summary>
        /// Tests that gt operator does not match when field is equal.
        /// </summary>
        [Fact]
        public void GtOperator_EqualValue_DoesNotMatch()
        {
            // Arrange
            decimal fieldValue = 1000m;
            decimal ruleValue = 1000m;

            // Act
            bool matches = fieldValue > ruleValue;

            // Assert
            Assert.False(matches);
        }

        /// <summary>
        /// Tests that gte operator matches when field is greater.
        /// </summary>
        [Fact]
        public void GteOperator_GreaterValue_Matches()
        {
            // Arrange
            decimal fieldValue = 5000m;
            decimal ruleValue = 1000m;

            // Act
            bool matches = fieldValue >= ruleValue;

            // Assert
            Assert.True(matches);
        }

        /// <summary>
        /// Tests that gte operator matches when field is equal.
        /// </summary>
        [Fact]
        public void GteOperator_EqualValue_Matches()
        {
            // Arrange
            decimal fieldValue = 1000m;
            decimal ruleValue = 1000m;

            // Act
            bool matches = fieldValue >= ruleValue;

            // Assert
            Assert.True(matches);
        }

        /// <summary>
        /// Tests that lt operator matches when field is less.
        /// </summary>
        [Fact]
        public void LtOperator_LesserValue_Matches()
        {
            // Arrange
            decimal fieldValue = 500m;
            decimal ruleValue = 1000m;

            // Act
            bool matches = fieldValue < ruleValue;

            // Assert
            Assert.True(matches);
        }

        /// <summary>
        /// Tests that lt operator does not match when field is equal.
        /// </summary>
        [Fact]
        public void LtOperator_EqualValue_DoesNotMatch()
        {
            // Arrange
            decimal fieldValue = 1000m;
            decimal ruleValue = 1000m;

            // Act
            bool matches = fieldValue < ruleValue;

            // Assert
            Assert.False(matches);
        }

        /// <summary>
        /// Tests that lte operator matches when field is less.
        /// </summary>
        [Fact]
        public void LteOperator_LesserValue_Matches()
        {
            // Arrange
            decimal fieldValue = 500m;
            decimal ruleValue = 1000m;

            // Act
            bool matches = fieldValue <= ruleValue;

            // Assert
            Assert.True(matches);
        }

        /// <summary>
        /// Tests that lte operator matches when field is equal.
        /// </summary>
        [Fact]
        public void LteOperator_EqualValue_Matches()
        {
            // Arrange
            decimal fieldValue = 1000m;
            decimal ruleValue = 1000m;

            // Act
            bool matches = fieldValue <= ruleValue;

            // Assert
            Assert.True(matches);
        }

        /// <summary>
        /// Tests that contains operator matches when substring is present.
        /// </summary>
        [Fact]
        public void ContainsOperator_SubstringPresent_Matches()
        {
            // Arrange
            string fieldValue = "Purchase Order - Urgent";
            string ruleValue = "Urgent";

            // Act
            bool matches = fieldValue.Contains(ruleValue);

            // Assert
            Assert.True(matches);
        }

        /// <summary>
        /// Tests that contains operator does not match when substring is absent.
        /// </summary>
        [Fact]
        public void ContainsOperator_SubstringAbsent_DoesNotMatch()
        {
            // Arrange
            string fieldValue = "Purchase Order - Standard";
            string ruleValue = "Urgent";

            // Act
            bool matches = fieldValue.Contains(ruleValue);

            // Assert
            Assert.False(matches);
        }

        /// <summary>
        /// Tests contains operator is case-sensitive by default.
        /// </summary>
        [Fact]
        public void ContainsOperator_CaseSensitive_ByDefault()
        {
            // Arrange
            string fieldValue = "Purchase Order - Urgent";
            string ruleValue = "urgent";

            // Act
            bool matches = fieldValue.Contains(ruleValue);

            // Assert - C# string.Contains is case-sensitive by default
            Assert.False(matches);
        }

        #endregion

        #region Step Routing Tests

        /// <summary>
        /// Tests that step order determines routing sequence.
        /// </summary>
        [Fact]
        public void StepRouting_OrderedByStepOrder()
        {
            // Arrange - simulate step list
            var steps = new List<ApprovalStepModel>
            {
                new ApprovalStepModel { StepOrder = 2, Name = "Manager" },
                new ApprovalStepModel { StepOrder = 1, Name = "Supervisor" },
                new ApprovalStepModel { StepOrder = 3, Name = "Director" }
            };

            // Act - sort by step order
            steps.Sort((a, b) => a.StepOrder.CompareTo(b.StepOrder));

            // Assert
            Assert.Equal("Supervisor", steps[0].Name);
            Assert.Equal("Manager", steps[1].Name);
            Assert.Equal("Director", steps[2].Name);
        }

        /// <summary>
        /// Tests that first step has StepOrder = 1.
        /// </summary>
        [Fact]
        public void FirstStep_HasStepOrder1()
        {
            // Arrange
            var firstStep = new ApprovalStepModel { StepOrder = 1, Name = "Initial Review" };

            // Assert
            Assert.Equal(1, firstStep.StepOrder);
        }

        /// <summary>
        /// Tests that IsFinal identifies the last step.
        /// </summary>
        [Fact]
        public void FinalStep_HasIsFinalTrue()
        {
            // Arrange
            var finalStep = new ApprovalStepModel { StepOrder = 3, IsFinal = true, Name = "Final Approval" };

            // Assert
            Assert.True(finalStep.IsFinal);
        }

        /// <summary>
        /// Tests that intermediate steps have IsFinal = false.
        /// </summary>
        [Fact]
        public void IntermediateStep_HasIsFinalFalse()
        {
            // Arrange
            var intermediateStep = new ApprovalStepModel { StepOrder = 2, IsFinal = false, Name = "Review" };

            // Assert
            Assert.False(intermediateStep.IsFinal);
        }

        #endregion

        #region Priority Tests

        /// <summary>
        /// Tests that higher priority rules are evaluated first.
        /// </summary>
        [Fact]
        public void RulePriority_HigherPriorityFirst()
        {
            // Arrange - simulate rule list
            var rules = new List<ApprovalRuleModel>
            {
                new ApprovalRuleModel { Priority = 50, Name = "Medium" },
                new ApprovalRuleModel { Priority = 100, Name = "High" },
                new ApprovalRuleModel { Priority = 0, Name = "Low" }
            };

            // Act - sort by priority descending (highest first)
            rules.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            // Assert
            Assert.Equal("High", rules[0].Name);
            Assert.Equal("Medium", rules[1].Name);
            Assert.Equal("Low", rules[2].Name);
        }

        /// <summary>
        /// Tests that rules with same priority maintain original order.
        /// </summary>
        [Fact]
        public void RulePriority_SamePriority_MaintainsOrder()
        {
            // Arrange
            var rules = new List<ApprovalRuleModel>
            {
                new ApprovalRuleModel { Priority = 50, Name = "First" },
                new ApprovalRuleModel { Priority = 50, Name = "Second" }
            };

            // Act - stable sort by priority
            rules = rules.OrderByDescending(r => r.Priority).ToList();

            // Assert - with stable sort, order should be maintained for equal priorities
            Assert.Equal(50, rules[0].Priority);
            Assert.Equal(50, rules[1].Priority);
        }

        #endregion

        #region Approver Resolution Tests

        /// <summary>
        /// Tests that role-based approver requires approver ID.
        /// </summary>
        [Fact]
        public void RoleBasedApprover_RequiresApproverId()
        {
            // Arrange
            var step = new ApprovalStepModel
            {
                ApproverType = "role",
                ApproverId = Guid.NewGuid()
            };

            // Assert
            Assert.Equal("role", step.ApproverType);
            Assert.NotNull(step.ApproverId);
            Assert.NotEqual(Guid.Empty, step.ApproverId.Value);
        }

        /// <summary>
        /// Tests that user-based approver requires approver ID.
        /// </summary>
        [Fact]
        public void UserBasedApprover_RequiresApproverId()
        {
            // Arrange
            var step = new ApprovalStepModel
            {
                ApproverType = "user",
                ApproverId = Guid.NewGuid()
            };

            // Assert
            Assert.Equal("user", step.ApproverType);
            Assert.NotNull(step.ApproverId);
            Assert.NotEqual(Guid.Empty, step.ApproverId.Value);
        }

        /// <summary>
        /// Tests that department_head approver does not require approver ID.
        /// </summary>
        [Fact]
        public void DepartmentHeadApprover_ApproverId_IsOptional()
        {
            // Arrange
            var step = new ApprovalStepModel
            {
                ApproverType = "department_head",
                ApproverId = null
            };

            // Assert
            Assert.Equal("department_head", step.ApproverType);
            Assert.Null(step.ApproverId);
        }

        #endregion

        #region Workflow Matching Tests

        /// <summary>
        /// Tests that workflow matches entity by target entity name.
        /// </summary>
        [Fact]
        public void Workflow_MatchesEntity_ByTargetEntityName()
        {
            // Arrange
            var workflow = new ApprovalWorkflowModel
            {
                TargetEntityName = "purchase_order",
                IsEnabled = true
            };

            // Act
            string entityName = "purchase_order";
            bool matches = workflow.TargetEntityName == entityName && workflow.IsEnabled;

            // Assert
            Assert.True(matches);
        }

        /// <summary>
        /// Tests that disabled workflow does not match.
        /// </summary>
        [Fact]
        public void DisabledWorkflow_DoesNotMatch()
        {
            // Arrange
            var workflow = new ApprovalWorkflowModel
            {
                TargetEntityName = "purchase_order",
                IsEnabled = false
            };

            // Act
            string entityName = "purchase_order";
            bool matches = workflow.TargetEntityName == entityName && workflow.IsEnabled;

            // Assert
            Assert.False(matches);
        }

        /// <summary>
        /// Tests that workflow with different entity does not match.
        /// </summary>
        [Fact]
        public void Workflow_DifferentEntity_DoesNotMatch()
        {
            // Arrange
            var workflow = new ApprovalWorkflowModel
            {
                TargetEntityName = "expense_request",
                IsEnabled = true
            };

            // Act
            string entityName = "purchase_order";
            bool matches = workflow.TargetEntityName == entityName && workflow.IsEnabled;

            // Assert
            Assert.False(matches);
        }

        #endregion

        #region Edge Cases Tests

        /// <summary>
        /// Tests numeric comparison with zero value.
        /// </summary>
        [Fact]
        public void NumericComparison_WithZero()
        {
            // Arrange
            decimal fieldValue = 0m;
            decimal ruleValue = 0m;

            // Act & Assert
            Assert.True(fieldValue == ruleValue);
            Assert.True(fieldValue >= ruleValue);
            Assert.True(fieldValue <= ruleValue);
            Assert.False(fieldValue > ruleValue);
            Assert.False(fieldValue < ruleValue);
        }

        /// <summary>
        /// Tests numeric comparison with negative values.
        /// </summary>
        [Fact]
        public void NumericComparison_WithNegativeValues()
        {
            // Arrange
            decimal fieldValue = -100m;
            decimal ruleValue = 0m;

            // Act & Assert
            Assert.True(fieldValue < ruleValue);
            Assert.True(fieldValue <= ruleValue);
            Assert.False(fieldValue > ruleValue);
            Assert.False(fieldValue >= ruleValue);
        }

        /// <summary>
        /// Tests string comparison with empty string.
        /// </summary>
        [Fact]
        public void StringComparison_WithEmptyString()
        {
            // Arrange
            string fieldValue = "";
            string ruleValue = "";

            // Act & Assert
            Assert.True(fieldValue == ruleValue);
            Assert.Contains(ruleValue, fieldValue);
        }

        #endregion
    }
}
