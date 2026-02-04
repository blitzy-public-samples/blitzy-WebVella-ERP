using System;
using System.Reflection;
using System.Linq;
using Xunit;
using WebVella.Erp.Web.Models;
using WebVella.Erp.Plugins.Approval.Services;
using WebVella.Erp.Plugins.Approval.Api;

namespace WebVella.Erp.Plugins.Approval.Tests.Integration
{
    /// <summary>
    /// Integration tests for STORY-009: Manager Dashboard Metrics.
    /// Verifies that dashboard component and metrics service exist.
    /// 
    /// Acceptance Criteria from jira-stories/STORY-009-manager-dashboard-metrics.md:
    /// - PcApprovalDashboard component with auto-refresh capability
    /// - DashboardMetricsService with 5 real-time metrics:
    ///   1. Pending Count
    ///   2. Average Approval Time (hours)
    ///   3. Approval Rate (percentage)
    ///   4. Overdue Count
    ///   5. Recent Activity Count
    /// - Manager/Administrator role authorization
    /// </summary>
    [Trait("Category", "Integration")]
    public class Story009_DashboardTests
    {
        #region PcApprovalDashboard Tests

        [Fact]
        public void PcApprovalDashboard_Exists()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;

            // Act
            var componentType = assembly.GetType(
                "WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard");

            // Assert
            Assert.NotNull(componentType);
        }

        [Fact]
        public void PcApprovalDashboard_ExtendsPageComponent()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var componentType = assembly.GetType(
                "WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard");

            // Act
            var baseType = componentType?.BaseType;

            // Assert
            Assert.NotNull(baseType);
            Assert.Equal("PageComponent", baseType.Name);
        }

        [Fact]
        public void PcApprovalDashboard_HasPageComponentAttribute()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var componentType = assembly.GetType(
                "WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard");

            // Act
            var attribute = componentType?.GetCustomAttribute<PageComponentAttribute>();

            // Assert
            Assert.NotNull(attribute);
            Assert.Contains("Dashboard", attribute.Label);
        }

        [Fact]
        public void PcApprovalDashboard_HasInvokeAsyncMethod()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var componentType = assembly.GetType(
                "WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard");

            // Act
            var method = componentType?.GetMethod("InvokeAsync",
                BindingFlags.Public | BindingFlags.Instance);

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void PcApprovalDashboard_HasOptionsClass()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var componentType = assembly.GetType(
                "WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard");

            // Act - Options class could be nested with various naming conventions
            var optionsType = componentType?.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(t => t.Name.Contains("Options"));

            // Assert
            Assert.NotNull(optionsType);
        }

        #endregion

        #region DashboardMetricsService Tests

        [Fact]
        public void DashboardMetricsService_Exists()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;

            // Act
            var serviceType = assembly.GetType(
                "WebVella.Erp.Plugins.Approval.Services.DashboardMetricsService");

            // Assert
            Assert.NotNull(serviceType);
        }

        [Fact]
        public void DashboardMetricsService_HasGetMetricsMethod()
        {
            // Arrange
            var serviceType = typeof(DashboardMetricsService);

            // Act
            var method = serviceType.GetMethods().FirstOrDefault(m =>
                m.Name.Contains("GetMetrics") || 
                m.Name.Contains("GetDashboard") ||
                m.Name == "Get");

            // Assert
            Assert.NotNull(method);
        }

        #endregion

        #region DashboardMetricsModel Tests

        [Fact]
        public void DashboardMetricsModel_Exists()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;

            // Act
            var modelType = assembly.GetType(
                "WebVella.Erp.Plugins.Approval.Api.DashboardMetricsModel");

            // Assert
            Assert.NotNull(modelType);
        }

        [Fact]
        public void DashboardMetricsModel_HasPendingCountProperty()
        {
            // Arrange
            var modelType = typeof(DashboardMetricsModel);

            // Act
            var property = modelType.GetProperty("PendingCount");

            // Assert
            Assert.NotNull(property);
        }

        [Fact]
        public void DashboardMetricsModel_HasAverageApprovalTimeProperty()
        {
            // Arrange
            var modelType = typeof(DashboardMetricsModel);

            // Act
            var property = modelType.GetProperties().FirstOrDefault(p =>
                p.Name.Contains("Average") || p.Name.Contains("AvgTime"));

            // Assert
            Assert.NotNull(property);
        }

        [Fact]
        public void DashboardMetricsModel_HasApprovalRateProperty()
        {
            // Arrange
            var modelType = typeof(DashboardMetricsModel);

            // Act
            var property = modelType.GetProperties().FirstOrDefault(p =>
                p.Name.Contains("ApprovalRate") || p.Name.Contains("Rate"));

            // Assert
            Assert.NotNull(property);
        }

        [Fact]
        public void DashboardMetricsModel_HasOverdueCountProperty()
        {
            // Arrange
            var modelType = typeof(DashboardMetricsModel);

            // Act
            var property = modelType.GetProperties().FirstOrDefault(p =>
                p.Name.Contains("Overdue") || p.Name.Contains("Escalated"));

            // Assert
            Assert.NotNull(property);
        }

        [Fact]
        public void DashboardMetricsModel_HasRecentActivityProperty()
        {
            // Arrange
            var modelType = typeof(DashboardMetricsModel);

            // Act
            var property = modelType.GetProperties().FirstOrDefault(p =>
                p.Name.Contains("Recent") || p.Name.Contains("Activity"));

            // Assert
            Assert.NotNull(property);
        }

        [Fact]
        public void DashboardMetricsModel_HasAllFiveRequiredMetrics()
        {
            // Arrange
            var modelType = typeof(DashboardMetricsModel);
            var properties = modelType.GetProperties();

            // Assert - All 5 metrics should exist
            Assert.Contains(properties, p => p.Name.Contains("Pending"));
            Assert.Contains(properties, p => p.Name.Contains("Average") || p.Name.Contains("Time"));
            Assert.Contains(properties, p => p.Name.Contains("Rate"));
            Assert.Contains(properties, p => p.Name.Contains("Overdue"));
            Assert.Contains(properties, p => p.Name.Contains("Recent") || p.Name.Contains("Activity"));
        }

        #endregion

        #region Auto-Refresh Tests

        [Fact]
        public void DashboardOptions_HasRefreshIntervalProperty()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var componentType = assembly.GetType(
                "WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard");
            var optionsType = componentType?.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(t => t.Name.Contains("Options"));

            // Act
            var property = optionsType?.GetProperties().FirstOrDefault(p =>
                p.Name.Contains("Refresh") || p.Name.Contains("Interval") || p.Name.Contains("AutoRefresh"));

            // Assert
            Assert.NotNull(property);
        }

        #endregion
    }
}
