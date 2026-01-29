using System;
using System.IO;
using System.Reflection;
using System.Linq;
using Xunit;
using WebVella.Erp.Web.Models;

namespace WebVella.Erp.Plugins.Approval.Tests.Integration
{
    /// <summary>
    /// Integration tests for STORY-008: UI Components.
    /// Verifies that all page components exist with required structure.
    /// 
    /// Acceptance Criteria from jira-stories/STORY-008-approval-ui-components.md:
    /// - PcApprovalWorkflowConfig component for workflow administration
    /// - PcApprovalRequestList component for request listing
    /// - PcApprovalAction component for approve/reject/delegate buttons
    /// - PcApprovalHistory component for timeline audit display
    /// - All components have [PageComponent] attribute
    /// - All components have required view files (Display, Design, Options, Help, Error)
    /// </summary>
    public class Story008_UiComponentsTests
    {
        #region PcApprovalWorkflowConfig Tests

        [Fact]
        public void PcApprovalWorkflowConfig_Exists()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;

            // Act
            var componentType = assembly.GetType(
                "WebVella.Erp.Plugins.Approval.Components.PcApprovalWorkflowConfig");

            // Assert
            Assert.NotNull(componentType);
        }

        [Fact]
        public void PcApprovalWorkflowConfig_ExtendsPageComponent()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var componentType = assembly.GetType(
                "WebVella.Erp.Plugins.Approval.Components.PcApprovalWorkflowConfig");

            // Act
            var baseType = componentType?.BaseType;

            // Assert
            Assert.NotNull(baseType);
            Assert.Equal("PageComponent", baseType.Name);
        }

        [Fact]
        public void PcApprovalWorkflowConfig_HasPageComponentAttribute()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var componentType = assembly.GetType(
                "WebVella.Erp.Plugins.Approval.Components.PcApprovalWorkflowConfig");

            // Act
            var attribute = componentType?.GetCustomAttribute<PageComponentAttribute>();

            // Assert
            Assert.NotNull(attribute);
            Assert.Contains("Approval", attribute.Category);
        }

        [Fact]
        public void PcApprovalWorkflowConfig_HasInvokeAsyncMethod()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var componentType = assembly.GetType(
                "WebVella.Erp.Plugins.Approval.Components.PcApprovalWorkflowConfig");

            // Act
            var method = componentType?.GetMethod("InvokeAsync", 
                BindingFlags.Public | BindingFlags.Instance);

            // Assert
            Assert.NotNull(method);
        }

        #endregion

        #region PcApprovalRequestList Tests

        [Fact]
        public void PcApprovalRequestList_Exists()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;

            // Act
            var componentType = assembly.GetType(
                "WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList");

            // Assert
            Assert.NotNull(componentType);
        }

        [Fact]
        public void PcApprovalRequestList_ExtendsPageComponent()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var componentType = assembly.GetType(
                "WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList");

            // Act
            var baseType = componentType?.BaseType;

            // Assert
            Assert.NotNull(baseType);
            Assert.Equal("PageComponent", baseType.Name);
        }

        [Fact]
        public void PcApprovalRequestList_HasPageComponentAttribute()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var componentType = assembly.GetType(
                "WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList");

            // Act
            var attribute = componentType?.GetCustomAttribute<PageComponentAttribute>();

            // Assert
            Assert.NotNull(attribute);
        }

        #endregion

        #region PcApprovalAction Tests

        [Fact]
        public void PcApprovalAction_Exists()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;

            // Act
            var componentType = assembly.GetType(
                "WebVella.Erp.Plugins.Approval.Components.PcApprovalAction");

            // Assert
            Assert.NotNull(componentType);
        }

        [Fact]
        public void PcApprovalAction_ExtendsPageComponent()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var componentType = assembly.GetType(
                "WebVella.Erp.Plugins.Approval.Components.PcApprovalAction");

            // Act
            var baseType = componentType?.BaseType;

            // Assert
            Assert.NotNull(baseType);
            Assert.Equal("PageComponent", baseType.Name);
        }

        [Fact]
        public void PcApprovalAction_HasPageComponentAttribute()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var componentType = assembly.GetType(
                "WebVella.Erp.Plugins.Approval.Components.PcApprovalAction");

            // Act
            var attribute = componentType?.GetCustomAttribute<PageComponentAttribute>();

            // Assert
            Assert.NotNull(attribute);
        }

        #endregion

        #region PcApprovalHistory Tests

        [Fact]
        public void PcApprovalHistory_Exists()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;

            // Act
            var componentType = assembly.GetType(
                "WebVella.Erp.Plugins.Approval.Components.PcApprovalHistory");

            // Assert
            Assert.NotNull(componentType);
        }

        [Fact]
        public void PcApprovalHistory_ExtendsPageComponent()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var componentType = assembly.GetType(
                "WebVella.Erp.Plugins.Approval.Components.PcApprovalHistory");

            // Act
            var baseType = componentType?.BaseType;

            // Assert
            Assert.NotNull(baseType);
            Assert.Equal("PageComponent", baseType.Name);
        }

        [Fact]
        public void PcApprovalHistory_HasPageComponentAttribute()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var componentType = assembly.GetType(
                "WebVella.Erp.Plugins.Approval.Components.PcApprovalHistory");

            // Act
            var attribute = componentType?.GetCustomAttribute<PageComponentAttribute>();

            // Assert
            Assert.NotNull(attribute);
        }

        #endregion

        #region Component Count Tests

        [Fact]
        public void AllFourUIComponents_ExistInAssembly()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;

            // Act
            var componentTypes = assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<PageComponentAttribute>() != null)
                .ToList();

            // Assert - Should have at least 4 page components (may have 5 with Dashboard)
            Assert.True(componentTypes.Count >= 4, 
                $"Expected at least 4 page components, found {componentTypes.Count}");
        }

        [Fact]
        public void AllComponents_HaveOptionsNestedClass()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var componentNames = new[]
            {
                "WebVella.Erp.Plugins.Approval.Components.PcApprovalWorkflowConfig",
                "WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList",
                "WebVella.Erp.Plugins.Approval.Components.PcApprovalAction",
                "WebVella.Erp.Plugins.Approval.Components.PcApprovalHistory"
            };

            // Assert - Check each component has some form of options class
            foreach (var componentName in componentNames)
            {
                var componentType = assembly.GetType(componentName);
                Assert.NotNull(componentType);
                
                // Options class could be nested or standalone with naming convention
                var optionsType = componentType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(t => t.Name.Contains("Options"));
                Assert.NotNull(optionsType);
            }
        }

        #endregion
    }
}
