using System;
using System.Reflection;
using Xunit;
using WebVella.Erp.Plugins.Approval;

namespace WebVella.Erp.Plugins.Approval.Tests.Integration
{
    /// <summary>
    /// Integration tests for STORY-001: Plugin Infrastructure.
    /// Verifies that the approval plugin is properly structured and can be loaded.
    /// 
    /// Acceptance Criteria from jira-stories/STORY-001-approval-plugin-infrastructure.md:
    /// - Plugin extends ErpPlugin base class
    /// - Plugin has correct Name property ("approval")
    /// - ProcessPatches() method exists
    /// - SetSchedulePlans() method registers 3 jobs
    /// </summary>
    [Trait("Category", "Integration")]
    public class Story001_PluginInfrastructureTests
    {
        [Fact]
        public void Plugin_ExtendsErpPlugin()
        {
            // Arrange & Act
            var pluginType = typeof(ApprovalPlugin);
            var baseType = pluginType.BaseType;

            // Assert
            Assert.NotNull(baseType);
            Assert.Equal("ErpPlugin", baseType.Name);
        }

        [Fact]
        public void Plugin_HasCorrectName()
        {
            // Arrange
            var plugin = new ApprovalPlugin();

            // Act
            var name = plugin.Name;

            // Assert
            Assert.Equal("approval", name);
        }

        [Fact]
        public void Plugin_HasProcessPatchesMethod()
        {
            // Arrange & Act - ProcessPatches is a public method
            var method = typeof(ApprovalPlugin).GetMethod(
                "ProcessPatches", 
                BindingFlags.Public | BindingFlags.Instance);

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void Plugin_HasSetSchedulePlansMethod()
        {
            // Arrange & Act - SetSchedulePlans is a public method
            var method = typeof(ApprovalPlugin).GetMethod(
                "SetSchedulePlans", 
                BindingFlags.Public | BindingFlags.Instance);

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void Plugin_HasInitializeMethod()
        {
            // Arrange & Act
            var method = typeof(ApprovalPlugin).GetMethod(
                "Initialize", 
                BindingFlags.Public | BindingFlags.Instance);

            // Assert
            Assert.NotNull(method);
            Assert.True(method.IsVirtual, "Initialize should be a virtual method (override)");
        }

        [Fact]
        public void PluginAssembly_ContainsRequiredTypes()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;

            // Act & Assert - Check that key types exist
            Assert.NotNull(assembly.GetType("WebVella.Erp.Plugins.Approval.ApprovalPlugin"));
            Assert.NotNull(assembly.GetType("WebVella.Erp.Plugins.Approval.Services.WorkflowConfigService"));
            Assert.NotNull(assembly.GetType("WebVella.Erp.Plugins.Approval.Services.ApprovalRequestService"));
            Assert.NotNull(assembly.GetType("WebVella.Erp.Plugins.Approval.Controllers.ApprovalController"));
        }
    }
}
