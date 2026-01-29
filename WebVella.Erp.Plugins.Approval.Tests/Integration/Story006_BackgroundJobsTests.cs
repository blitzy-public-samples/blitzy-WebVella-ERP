using System;
using System.Reflection;
using System.Linq;
using Xunit;
using WebVella.Erp.Jobs;

namespace WebVella.Erp.Plugins.Approval.Tests.Integration
{
    /// <summary>
    /// Integration tests for STORY-006: Background Jobs.
    /// Verifies that background jobs exist and have correct attributes.
    /// 
    /// Acceptance Criteria from jira-stories/STORY-006-notification-escalation-jobs.md:
    /// - ProcessApprovalNotificationsJob (5-minute interval)
    /// - ProcessApprovalEscalationsJob (30-minute interval)
    /// - CleanupExpiredApprovalsJob (daily at 00:10 UTC)
    /// - All jobs extend ErpJob base class
    /// - All jobs have [Job] attribute with unique GUID
    /// </summary>
    public class Story006_BackgroundJobsTests
    {
        #region ProcessApprovalNotificationsJob Tests

        [Fact]
        public void ProcessApprovalNotificationsJob_Exists()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;

            // Act
            var jobType = assembly.GetType("WebVella.Erp.Plugins.Approval.Jobs.ProcessApprovalNotificationsJob");

            // Assert
            Assert.NotNull(jobType);
        }

        [Fact]
        public void ProcessApprovalNotificationsJob_ExtendsErpJob()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var jobType = assembly.GetType("WebVella.Erp.Plugins.Approval.Jobs.ProcessApprovalNotificationsJob");

            // Act
            var baseType = jobType?.BaseType;

            // Assert
            Assert.NotNull(baseType);
            Assert.Equal("ErpJob", baseType.Name);
        }

        [Fact]
        public void ProcessApprovalNotificationsJob_HasJobAttribute()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var jobType = assembly.GetType("WebVella.Erp.Plugins.Approval.Jobs.ProcessApprovalNotificationsJob");

            // Act
            var attribute = jobType?.GetCustomAttribute<JobAttribute>();

            // Assert
            Assert.NotNull(attribute);
            Assert.NotEqual(Guid.Empty, attribute.Id);
        }

        [Fact]
        public void ProcessApprovalNotificationsJob_HasExecuteMethod()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var jobType = assembly.GetType("WebVella.Erp.Plugins.Approval.Jobs.ProcessApprovalNotificationsJob");

            // Act
            var method = jobType?.GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance);

            // Assert
            Assert.NotNull(method);
        }

        #endregion

        #region ProcessApprovalEscalationsJob Tests

        [Fact]
        public void ProcessApprovalEscalationsJob_Exists()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;

            // Act
            var jobType = assembly.GetType("WebVella.Erp.Plugins.Approval.Jobs.ProcessApprovalEscalationsJob");

            // Assert
            Assert.NotNull(jobType);
        }

        [Fact]
        public void ProcessApprovalEscalationsJob_ExtendsErpJob()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var jobType = assembly.GetType("WebVella.Erp.Plugins.Approval.Jobs.ProcessApprovalEscalationsJob");

            // Act
            var baseType = jobType?.BaseType;

            // Assert
            Assert.NotNull(baseType);
            Assert.Equal("ErpJob", baseType.Name);
        }

        [Fact]
        public void ProcessApprovalEscalationsJob_HasJobAttribute()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var jobType = assembly.GetType("WebVella.Erp.Plugins.Approval.Jobs.ProcessApprovalEscalationsJob");

            // Act
            var attribute = jobType?.GetCustomAttribute<JobAttribute>();

            // Assert
            Assert.NotNull(attribute);
            Assert.NotEqual(Guid.Empty, attribute.Id);
        }

        [Fact]
        public void ProcessApprovalEscalationsJob_HasExecuteMethod()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var jobType = assembly.GetType("WebVella.Erp.Plugins.Approval.Jobs.ProcessApprovalEscalationsJob");

            // Act
            var method = jobType?.GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance);

            // Assert
            Assert.NotNull(method);
        }

        #endregion

        #region CleanupExpiredApprovalsJob Tests

        [Fact]
        public void CleanupExpiredApprovalsJob_Exists()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;

            // Act
            var jobType = assembly.GetType("WebVella.Erp.Plugins.Approval.Jobs.CleanupExpiredApprovalsJob");

            // Assert
            Assert.NotNull(jobType);
        }

        [Fact]
        public void CleanupExpiredApprovalsJob_ExtendsErpJob()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var jobType = assembly.GetType("WebVella.Erp.Plugins.Approval.Jobs.CleanupExpiredApprovalsJob");

            // Act
            var baseType = jobType?.BaseType;

            // Assert
            Assert.NotNull(baseType);
            Assert.Equal("ErpJob", baseType.Name);
        }

        [Fact]
        public void CleanupExpiredApprovalsJob_HasJobAttribute()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var jobType = assembly.GetType("WebVella.Erp.Plugins.Approval.Jobs.CleanupExpiredApprovalsJob");

            // Act
            var attribute = jobType?.GetCustomAttribute<JobAttribute>();

            // Assert
            Assert.NotNull(attribute);
            Assert.NotEqual(Guid.Empty, attribute.Id);
        }

        [Fact]
        public void CleanupExpiredApprovalsJob_HasExecuteMethod()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var jobType = assembly.GetType("WebVella.Erp.Plugins.Approval.Jobs.CleanupExpiredApprovalsJob");

            // Act
            var method = jobType?.GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance);

            // Assert
            Assert.NotNull(method);
        }

        #endregion

        #region Job Uniqueness Tests

        [Fact]
        public void AllJobs_HaveUniqueGuids()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;
            var jobTypes = new[]
            {
                assembly.GetType("WebVella.Erp.Plugins.Approval.Jobs.ProcessApprovalNotificationsJob"),
                assembly.GetType("WebVella.Erp.Plugins.Approval.Jobs.ProcessApprovalEscalationsJob"),
                assembly.GetType("WebVella.Erp.Plugins.Approval.Jobs.CleanupExpiredApprovalsJob")
            };

            // Act
            var guids = jobTypes
                .Select(t => t?.GetCustomAttribute<JobAttribute>()?.Id)
                .Where(g => g.HasValue)
                .Select(g => g.Value)
                .ToList();

            // Assert
            Assert.Equal(3, guids.Count);
            Assert.Equal(3, guids.Distinct().Count()); // All GUIDs should be unique
        }

        #endregion
    }
}
