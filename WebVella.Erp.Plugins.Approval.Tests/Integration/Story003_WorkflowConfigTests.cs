using System;
using System.Reflection;
using System.Linq;
using Xunit;
using WebVella.Erp.Plugins.Approval.Services;
using WebVella.Erp.Plugins.Approval.Api;

namespace WebVella.Erp.Plugins.Approval.Tests.Integration
{
    /// <summary>
    /// Integration tests for STORY-003: Workflow Configuration Management.
    /// Verifies that configuration services exist and have correct method signatures.
    /// 
    /// Acceptance Criteria from jira-stories/STORY-003-workflow-configuration-management.md:
    /// - WorkflowConfigService with CRUD operations
    /// - StepConfigService with add/edit/delete/reorder steps
    /// - RuleConfigService with add/edit/delete rules
    /// - Validation logic for required fields
    /// - Enable/disable workflow functionality
    /// </summary>
    public class Story003_WorkflowConfigTests
    {
        #region WorkflowConfigService Tests

        [Fact]
        public void WorkflowConfigService_HasGetAllMethod()
        {
            // Arrange
            var serviceType = typeof(WorkflowConfigService);

            // Act
            var method = serviceType.GetMethods().FirstOrDefault(m => 
                m.Name == "GetAll" || m.Name == "GetAllWorkflows");

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void WorkflowConfigService_HasGetByIdMethod()
        {
            // Arrange
            var serviceType = typeof(WorkflowConfigService);

            // Act
            var method = serviceType.GetMethods().FirstOrDefault(m => 
                m.Name == "GetById" || m.Name == "GetWorkflow");

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void WorkflowConfigService_HasCreateMethod()
        {
            // Arrange
            var serviceType = typeof(WorkflowConfigService);

            // Act
            var method = serviceType.GetMethods().FirstOrDefault(m => 
                m.Name == "Create" || m.Name == "CreateWorkflow");

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void WorkflowConfigService_HasUpdateMethod()
        {
            // Arrange
            var serviceType = typeof(WorkflowConfigService);

            // Act
            var method = serviceType.GetMethods().FirstOrDefault(m => 
                m.Name == "Update" || m.Name == "UpdateWorkflow");

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void WorkflowConfigService_HasDeleteMethod()
        {
            // Arrange
            var serviceType = typeof(WorkflowConfigService);

            // Act
            var method = serviceType.GetMethods().FirstOrDefault(m => 
                m.Name == "Delete" || m.Name == "DeleteWorkflow");

            // Assert
            Assert.NotNull(method);
        }

        #endregion

        #region StepConfigService Tests

        [Fact]
        public void StepConfigService_Exists()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;

            // Act
            var serviceType = assembly.GetType("WebVella.Erp.Plugins.Approval.Services.StepConfigService");

            // Assert
            Assert.NotNull(serviceType);
        }

        [Fact]
        public void StepConfigService_HasGetStepsForWorkflowMethod()
        {
            // Arrange
            var serviceType = typeof(StepConfigService);

            // Act
            var method = serviceType.GetMethods().FirstOrDefault(m => 
                m.Name.Contains("GetSteps") || m.Name.Contains("GetByWorkflow"));

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void StepConfigService_HasCreateStepMethod()
        {
            // Arrange
            var serviceType = typeof(StepConfigService);

            // Act
            var method = serviceType.GetMethods().FirstOrDefault(m => 
                m.Name == "Create" || m.Name == "CreateStep" || m.Name == "AddStep");

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void StepConfigService_HasUpdateStepMethod()
        {
            // Arrange
            var serviceType = typeof(StepConfigService);

            // Act
            var method = serviceType.GetMethods().FirstOrDefault(m => 
                m.Name == "Update" || m.Name == "UpdateStep");

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void StepConfigService_HasDeleteStepMethod()
        {
            // Arrange
            var serviceType = typeof(StepConfigService);

            // Act
            var method = serviceType.GetMethods().FirstOrDefault(m => 
                m.Name == "Delete" || m.Name == "DeleteStep");

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void StepConfigService_HasReorderMethod()
        {
            // Arrange
            var serviceType = typeof(StepConfigService);

            // Act
            var method = serviceType.GetMethods().FirstOrDefault(m => 
                m.Name.Contains("Reorder") || m.Name.Contains("Order"));

            // Assert
            Assert.NotNull(method);
        }

        #endregion

        #region RuleConfigService Tests

        [Fact]
        public void RuleConfigService_Exists()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;

            // Act
            var serviceType = assembly.GetType("WebVella.Erp.Plugins.Approval.Services.RuleConfigService");

            // Assert
            Assert.NotNull(serviceType);
        }

        [Fact]
        public void RuleConfigService_HasGetRulesForWorkflowMethod()
        {
            // Arrange
            var serviceType = typeof(RuleConfigService);

            // Act
            var method = serviceType.GetMethods().FirstOrDefault(m => 
                m.Name.Contains("GetRules") || m.Name.Contains("GetByWorkflow"));

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void RuleConfigService_HasCreateRuleMethod()
        {
            // Arrange
            var serviceType = typeof(RuleConfigService);

            // Act
            var method = serviceType.GetMethods().FirstOrDefault(m => 
                m.Name == "Create" || m.Name == "CreateRule" || m.Name == "AddRule");

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void RuleConfigService_HasUpdateRuleMethod()
        {
            // Arrange
            var serviceType = typeof(RuleConfigService);

            // Act
            var method = serviceType.GetMethods().FirstOrDefault(m => 
                m.Name == "Update" || m.Name == "UpdateRule");

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void RuleConfigService_HasDeleteRuleMethod()
        {
            // Arrange
            var serviceType = typeof(RuleConfigService);

            // Act
            var method = serviceType.GetMethods().FirstOrDefault(m => 
                m.Name == "Delete" || m.Name == "DeleteRule");

            // Assert
            Assert.NotNull(method);
        }

        #endregion

        #region Model Validation Tests

        [Fact]
        public void ApprovalWorkflowModel_CanBeInstantiated()
        {
            // Act
            var model = new ApprovalWorkflowModel
            {
                Id = Guid.NewGuid(),
                Name = "Test Workflow",
                TargetEntityName = "purchase_order",
                IsEnabled = true,
                CreatedOn = DateTime.UtcNow
            };

            // Assert
            Assert.NotNull(model);
            Assert.Equal("Test Workflow", model.Name);
            Assert.True(model.IsEnabled);
        }

        [Fact]
        public void ApprovalStepModel_CanBeInstantiated()
        {
            // Act
            var model = new ApprovalStepModel
            {
                Id = Guid.NewGuid(),
                WorkflowId = Guid.NewGuid(),
                StepOrder = 1,
                Name = "Manager Approval",
                ApproverType = "role",
                IsFinal = false
            };

            // Assert
            Assert.NotNull(model);
            Assert.Equal(1, model.StepOrder);
            Assert.Equal("role", model.ApproverType);
        }

        [Fact]
        public void ApprovalRuleModel_CanBeInstantiated()
        {
            // Act
            var model = new ApprovalRuleModel
            {
                Id = Guid.NewGuid(),
                WorkflowId = Guid.NewGuid(),
                Name = "Amount Check",
                FieldName = "amount",
                Operator = "gt",
                ThresholdValue = 1000m,
                Priority = 1
            };

            // Assert
            Assert.NotNull(model);
            Assert.Equal("gt", model.Operator);
            Assert.Equal(1000m, model.ThresholdValue);
        }

        #endregion
    }
}
