using System;
using System.Reflection;
using System.Linq;
using Xunit;
using WebVella.Erp.Hooks;

namespace WebVella.Erp.Plugins.Approval.Tests.Integration
{
    /// <summary>
    /// Integration tests for STORY-005: Hooks Integration.
    /// Verifies that entity hooks exist and implement correct interfaces.
    /// 
    /// Acceptance Criteria from jira-stories/STORY-005-approval-hooks-integration.md:
    /// - PurchaseOrderApproval hook triggers on purchase_order creation
    /// - ExpenseRequestApproval hook triggers on expense_request creation
    /// - ApprovalRequest hook validates pre-create and logs post-update
    /// - Hooks decorated with [HookAttachment] attribute
    /// </summary>
    public class Story005_HooksIntegrationTests
    {
        #region ApprovalRequest Hook Tests

        [Fact]
        public void ApprovalRequestHook_Exists()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;

            // Act
            var hookType = assembly.GetType("WebVella.Erp.Plugins.Approval.Hooks.Api.ApprovalRequest");

            // Assert
            Assert.NotNull(hookType);
        }

        [Fact]
        public void ApprovalRequestHook_HasHookAttachmentAttribute()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var hookType = assembly.GetType("WebVella.Erp.Plugins.Approval.Hooks.Api.ApprovalRequest");

            // Act
            var attribute = hookType?.GetCustomAttribute<HookAttachmentAttribute>();

            // Assert
            Assert.NotNull(attribute);
            Assert.Equal("approval_request", attribute.Key);
        }

        [Fact]
        public void ApprovalRequestHook_ImplementsPreCreateInterface()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var hookType = assembly.GetType("WebVella.Erp.Plugins.Approval.Hooks.Api.ApprovalRequest");

            // Act
            var implementsPreCreate = hookType?.GetInterfaces().Any(i => 
                i.Name.Contains("PreCreate") || i.Name.Contains("IErpPreCreateRecordHook"));

            // Assert
            Assert.True(implementsPreCreate);
        }

        [Fact]
        public void ApprovalRequestHook_ImplementsPostUpdateInterface()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var hookType = assembly.GetType("WebVella.Erp.Plugins.Approval.Hooks.Api.ApprovalRequest");

            // Act
            var implementsPostUpdate = hookType?.GetInterfaces().Any(i => 
                i.Name.Contains("PostUpdate") || i.Name.Contains("IErpPostUpdateRecordHook"));

            // Assert
            Assert.True(implementsPostUpdate);
        }

        #endregion

        #region PurchaseOrderApproval Hook Tests

        [Fact]
        public void PurchaseOrderApprovalHook_Exists()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;

            // Act
            var hookType = assembly.GetType("WebVella.Erp.Plugins.Approval.Hooks.Api.PurchaseOrderApproval");

            // Assert
            Assert.NotNull(hookType);
        }

        [Fact]
        public void PurchaseOrderApprovalHook_HasHookAttachmentAttribute()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var hookType = assembly.GetType("WebVella.Erp.Plugins.Approval.Hooks.Api.PurchaseOrderApproval");

            // Act
            var attribute = hookType?.GetCustomAttribute<HookAttachmentAttribute>();

            // Assert
            Assert.NotNull(attribute);
            Assert.Equal("purchase_order", attribute.Key);
        }

        [Fact]
        public void PurchaseOrderApprovalHook_ImplementsPostCreateInterface()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var hookType = assembly.GetType("WebVella.Erp.Plugins.Approval.Hooks.Api.PurchaseOrderApproval");

            // Act
            var implementsPostCreate = hookType?.GetInterfaces().Any(i => 
                i.Name.Contains("PostCreate") || i.Name.Contains("IErpPostCreateRecordHook"));

            // Assert
            Assert.True(implementsPostCreate);
        }

        #endregion

        #region ExpenseRequestApproval Hook Tests

        [Fact]
        public void ExpenseRequestApprovalHook_Exists()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;

            // Act
            var hookType = assembly.GetType("WebVella.Erp.Plugins.Approval.Hooks.Api.ExpenseRequestApproval");

            // Assert
            Assert.NotNull(hookType);
        }

        [Fact]
        public void ExpenseRequestApprovalHook_HasHookAttachmentAttribute()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var hookType = assembly.GetType("WebVella.Erp.Plugins.Approval.Hooks.Api.ExpenseRequestApproval");

            // Act
            var attribute = hookType?.GetCustomAttribute<HookAttachmentAttribute>();

            // Assert
            Assert.NotNull(attribute);
            Assert.Equal("expense_request", attribute.Key);
        }

        [Fact]
        public void ExpenseRequestApprovalHook_ImplementsPostCreateInterface()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var hookType = assembly.GetType("WebVella.Erp.Plugins.Approval.Hooks.Api.ExpenseRequestApproval");

            // Act
            var implementsPostCreate = hookType?.GetInterfaces().Any(i => 
                i.Name.Contains("PostCreate") || i.Name.Contains("IErpPostCreateRecordHook"));

            // Assert
            Assert.True(implementsPostCreate);
        }

        #endregion

        #region Hook Method Signature Tests

        [Fact]
        public void AllHooks_HaveExecuteMethod()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var hookTypes = new[]
            {
                assembly.GetType("WebVella.Erp.Plugins.Approval.Hooks.Api.ApprovalRequest"),
                assembly.GetType("WebVella.Erp.Plugins.Approval.Hooks.Api.PurchaseOrderApproval"),
                assembly.GetType("WebVella.Erp.Plugins.Approval.Hooks.Api.ExpenseRequestApproval")
            };

            // Assert
            foreach (var hookType in hookTypes)
            {
                Assert.NotNull(hookType);
                
                // Check for OnPreCreateRecord, OnPostCreateRecord, or OnPostUpdateRecord methods
                var hasHookMethod = hookType.GetMethods().Any(m => 
                    m.Name.Contains("OnPreCreate") || 
                    m.Name.Contains("OnPostCreate") || 
                    m.Name.Contains("OnPostUpdate"));
                
                Assert.True(hasHookMethod, $"Hook {hookType.Name} should have a hook method");
            }
        }

        #endregion
    }
}
