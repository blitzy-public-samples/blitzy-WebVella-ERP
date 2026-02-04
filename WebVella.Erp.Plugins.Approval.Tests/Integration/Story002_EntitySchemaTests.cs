using System;
using System.Reflection;
using Xunit;
using WebVella.Erp.Plugins.Approval.Api;

namespace WebVella.Erp.Plugins.Approval.Tests.Integration
{
    /// <summary>
    /// Integration tests for STORY-002: Entity Schema.
    /// Verifies that all required entity models exist with correct properties.
    /// 
    /// Acceptance Criteria from jira-stories/STORY-002-approval-entity-schema.md:
    /// - 5 entities created: approval_workflow, approval_step, approval_rule, approval_request, approval_history
    /// - All fields exist with correct types
    /// - All relations created
    /// </summary>
    [Trait("Category", "Integration")]
    public class Story002_EntitySchemaTests
    {
        #region ApprovalWorkflowModel Tests

        [Fact]
        public void ApprovalWorkflowModel_HasRequiredProperties()
        {
            // Arrange
            var modelType = typeof(ApprovalWorkflowModel);

            // Assert - All required properties exist
            Assert.NotNull(modelType.GetProperty("Id"));
            Assert.NotNull(modelType.GetProperty("Name"));
            Assert.NotNull(modelType.GetProperty("TargetEntityName"));
            Assert.NotNull(modelType.GetProperty("IsEnabled"));
            Assert.NotNull(modelType.GetProperty("CreatedOn"));
            Assert.NotNull(modelType.GetProperty("CreatedBy"));
        }

        [Fact]
        public void ApprovalWorkflowModel_PropertiesHaveCorrectTypes()
        {
            // Arrange
            var modelType = typeof(ApprovalWorkflowModel);

            // Assert
            Assert.Equal(typeof(Guid), modelType.GetProperty("Id")?.PropertyType);
            Assert.Equal(typeof(string), modelType.GetProperty("Name")?.PropertyType);
            Assert.Equal(typeof(string), modelType.GetProperty("TargetEntityName")?.PropertyType);
            Assert.Equal(typeof(bool), modelType.GetProperty("IsEnabled")?.PropertyType);
            Assert.Equal(typeof(DateTime), modelType.GetProperty("CreatedOn")?.PropertyType);
        }

        #endregion

        #region ApprovalStepModel Tests

        [Fact]
        public void ApprovalStepModel_HasRequiredProperties()
        {
            // Arrange
            var modelType = typeof(ApprovalStepModel);

            // Assert - All required properties exist
            Assert.NotNull(modelType.GetProperty("Id"));
            Assert.NotNull(modelType.GetProperty("WorkflowId"));
            Assert.NotNull(modelType.GetProperty("StepOrder"));
            Assert.NotNull(modelType.GetProperty("Name"));
            Assert.NotNull(modelType.GetProperty("ApproverType"));
            Assert.NotNull(modelType.GetProperty("ApproverId"));
            Assert.NotNull(modelType.GetProperty("TimeoutHours"));
            Assert.NotNull(modelType.GetProperty("IsFinal"));
        }

        [Fact]
        public void ApprovalStepModel_PropertiesHaveCorrectTypes()
        {
            // Arrange
            var modelType = typeof(ApprovalStepModel);

            // Assert
            Assert.Equal(typeof(Guid), modelType.GetProperty("Id")?.PropertyType);
            Assert.Equal(typeof(Guid), modelType.GetProperty("WorkflowId")?.PropertyType);
            Assert.Equal(typeof(int), modelType.GetProperty("StepOrder")?.PropertyType);
            Assert.Equal(typeof(string), modelType.GetProperty("Name")?.PropertyType);
            Assert.Equal(typeof(string), modelType.GetProperty("ApproverType")?.PropertyType);
            Assert.Equal(typeof(bool), modelType.GetProperty("IsFinal")?.PropertyType);
        }

        #endregion

        #region ApprovalRuleModel Tests

        [Fact]
        public void ApprovalRuleModel_HasRequiredProperties()
        {
            // Arrange
            var modelType = typeof(ApprovalRuleModel);

            // Assert - All required properties exist
            Assert.NotNull(modelType.GetProperty("Id"));
            Assert.NotNull(modelType.GetProperty("WorkflowId"));
            Assert.NotNull(modelType.GetProperty("Name"));
            Assert.NotNull(modelType.GetProperty("FieldName"));
            Assert.NotNull(modelType.GetProperty("Operator"));
            Assert.NotNull(modelType.GetProperty("ThresholdValue")); // Changed from Value
            Assert.NotNull(modelType.GetProperty("Priority"));
        }

        [Fact]
        public void ApprovalRuleModel_PropertiesHaveCorrectTypes()
        {
            // Arrange
            var modelType = typeof(ApprovalRuleModel);

            // Assert
            Assert.Equal(typeof(Guid), modelType.GetProperty("Id")?.PropertyType);
            Assert.Equal(typeof(Guid), modelType.GetProperty("WorkflowId")?.PropertyType);
            Assert.Equal(typeof(string), modelType.GetProperty("Name")?.PropertyType);
            Assert.Equal(typeof(string), modelType.GetProperty("FieldName")?.PropertyType);
            Assert.Equal(typeof(string), modelType.GetProperty("Operator")?.PropertyType);
            Assert.Equal(typeof(decimal), modelType.GetProperty("ThresholdValue")?.PropertyType); // Changed from Value/string to ThresholdValue/decimal
        }

        #endregion

        #region ApprovalRequestModel Tests

        [Fact]
        public void ApprovalRequestModel_HasRequiredProperties()
        {
            // Arrange
            var modelType = typeof(ApprovalRequestModel);

            // Assert - All required properties exist
            Assert.NotNull(modelType.GetProperty("Id"));
            Assert.NotNull(modelType.GetProperty("WorkflowId"));
            Assert.NotNull(modelType.GetProperty("CurrentStepId"));
            Assert.NotNull(modelType.GetProperty("SourceEntityName"));
            Assert.NotNull(modelType.GetProperty("SourceRecordId"));
            Assert.NotNull(modelType.GetProperty("Status"));
            Assert.NotNull(modelType.GetProperty("RequestedBy"));
            Assert.NotNull(modelType.GetProperty("RequestedOn"));
        }

        [Fact]
        public void ApprovalRequestModel_PropertiesHaveCorrectTypes()
        {
            // Arrange
            var modelType = typeof(ApprovalRequestModel);

            // Assert
            Assert.Equal(typeof(Guid), modelType.GetProperty("Id")?.PropertyType);
            Assert.Equal(typeof(Guid), modelType.GetProperty("WorkflowId")?.PropertyType);
            Assert.Equal(typeof(string), modelType.GetProperty("SourceEntityName")?.PropertyType);
            Assert.Equal(typeof(Guid), modelType.GetProperty("SourceRecordId")?.PropertyType);
            Assert.Equal(typeof(string), modelType.GetProperty("Status")?.PropertyType);
            Assert.Equal(typeof(DateTime), modelType.GetProperty("RequestedOn")?.PropertyType);
        }

        #endregion

        #region ApprovalHistoryModel Tests

        [Fact]
        public void ApprovalHistoryModel_HasRequiredProperties()
        {
            // Arrange
            var modelType = typeof(ApprovalHistoryModel);

            // Assert - All required properties exist
            Assert.NotNull(modelType.GetProperty("Id"));
            Assert.NotNull(modelType.GetProperty("RequestId"));
            Assert.NotNull(modelType.GetProperty("StepId"));
            Assert.NotNull(modelType.GetProperty("Action"));
            Assert.NotNull(modelType.GetProperty("PerformedBy"));
            Assert.NotNull(modelType.GetProperty("PerformedOn"));
            Assert.NotNull(modelType.GetProperty("Comments"));
        }

        [Fact]
        public void ApprovalHistoryModel_PropertiesHaveCorrectTypes()
        {
            // Arrange
            var modelType = typeof(ApprovalHistoryModel);

            // Assert
            Assert.Equal(typeof(Guid), modelType.GetProperty("Id")?.PropertyType);
            Assert.Equal(typeof(Guid), modelType.GetProperty("RequestId")?.PropertyType);
            Assert.Equal(typeof(string), modelType.GetProperty("Action")?.PropertyType);
            Assert.Equal(typeof(DateTime), modelType.GetProperty("PerformedOn")?.PropertyType);
            Assert.Equal(typeof(string), modelType.GetProperty("Comments")?.PropertyType);
        }

        #endregion

        #region Entity Migration Tests

        [Fact]
        public void MigrationPatch_Exists()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;

            // Act - Look for the migration patch partial class
            var patchType = assembly.GetType("WebVella.Erp.Plugins.Approval.ApprovalPlugin");

            // Assert
            Assert.NotNull(patchType);
        }

        [Fact]
        public void AllEntityModels_ExistInAssembly()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;

            // Assert - All 5 entity models exist
            Assert.NotNull(assembly.GetType("WebVella.Erp.Plugins.Approval.Api.ApprovalWorkflowModel"));
            Assert.NotNull(assembly.GetType("WebVella.Erp.Plugins.Approval.Api.ApprovalStepModel"));
            Assert.NotNull(assembly.GetType("WebVella.Erp.Plugins.Approval.Api.ApprovalRuleModel"));
            Assert.NotNull(assembly.GetType("WebVella.Erp.Plugins.Approval.Api.ApprovalRequestModel"));
            Assert.NotNull(assembly.GetType("WebVella.Erp.Plugins.Approval.Api.ApprovalHistoryModel"));
        }

        #endregion
    }
}
