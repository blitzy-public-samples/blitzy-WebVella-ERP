using System;
using System.Collections.Generic;
using Xunit;
using WebVella.Erp.Plugins.Approval.Api;

namespace WebVella.Erp.Plugins.Approval.Tests
{
    /// <summary>
    /// Unit tests for Dashboard Metrics models and service logic.
    /// Note: Service method tests that require database are in integration tests.
    /// These tests focus on model validation, serialization, and edge cases.
    /// </summary>
    public class DashboardMetricsServiceTests
    {
        #region RecentActivityItem Model Tests

        [Fact]
        public void RecentActivityItem_HasDefaultValues()
        {
            // Arrange & Act
            var item = new RecentActivityItem();

            // Assert
            Assert.NotNull(item.Action);
            Assert.NotNull(item.PerformedBy);
            Assert.NotNull(item.RequestSubject);
            Assert.Equal(Guid.Empty, item.RequestId);
            Assert.Equal(DateTime.MinValue, item.PerformedOn);
        }

        [Fact]
        public void RecentActivityItem_CanSetProperties()
        {
            // Arrange
            var item = new RecentActivityItem();
            var testGuid = Guid.NewGuid();
            var testDate = DateTime.UtcNow;

            // Act
            item.Action = "approved";
            item.PerformedBy = "Test User";
            item.RequestId = testGuid;
            item.RequestSubject = "Test Request";
            item.PerformedOn = testDate;

            // Assert
            Assert.Equal("approved", item.Action);
            Assert.Equal("Test User", item.PerformedBy);
            Assert.Equal(testGuid, item.RequestId);
            Assert.Equal("Test Request", item.RequestSubject);
            Assert.Equal(testDate, item.PerformedOn);
        }

        [Fact]
        public void RecentActivityItem_AcceptsEmptyStrings()
        {
            // Arrange & Act
            var item = new RecentActivityItem
            {
                Action = "",
                PerformedBy = "",
                RequestSubject = ""
            };

            // Assert
            Assert.Equal("", item.Action);
            Assert.Equal("", item.PerformedBy);
            Assert.Equal("", item.RequestSubject);
        }

        [Fact]
        public void RecentActivityItem_AcceptsLongStrings()
        {
            // Arrange
            var longString = new string('x', 10000);
            
            // Act
            var item = new RecentActivityItem
            {
                Action = longString,
                PerformedBy = longString,
                RequestSubject = longString
            };

            // Assert
            Assert.Equal(10000, item.Action.Length);
            Assert.Equal(10000, item.PerformedBy.Length);
            Assert.Equal(10000, item.RequestSubject.Length);
        }

        [Fact]
        public void RecentActivityItem_AcceptsSpecialCharacters()
        {
            // Arrange & Act
            var item = new RecentActivityItem
            {
                Action = "approved <script>alert('xss')</script>",
                PerformedBy = "User & Admin",
                RequestSubject = "Test \"Request\" with 'quotes'"
            };

            // Assert
            Assert.Contains("<script>", item.Action);
            Assert.Contains("&", item.PerformedBy);
            Assert.Contains("\"", item.RequestSubject);
        }

        [Theory]
        [InlineData("approved")]
        [InlineData("rejected")]
        [InlineData("delegated")]
        [InlineData("pending")]
        [InlineData("cancelled")]
        public void RecentActivityItem_AcceptsVariousActionTypes(string action)
        {
            // Arrange & Act
            var item = new RecentActivityItem { Action = action };

            // Assert
            Assert.Equal(action, item.Action);
        }

        #endregion

        #region DashboardMetricsModel Model Tests

        [Fact]
        public void DashboardMetricsModel_HasDefaultRecentActivityList()
        {
            // Arrange & Act
            var model = new DashboardMetricsModel();

            // Assert
            Assert.NotNull(model.RecentActivity);
            Assert.Empty(model.RecentActivity);
        }

        [Fact]
        public void DashboardMetricsModel_HasDefaultDateValues()
        {
            // Arrange & Act
            var model = new DashboardMetricsModel();

            // Assert
            Assert.Equal(DateTime.MinValue, model.MetricsAsOf);
            Assert.Equal(DateTime.MinValue, model.DateRangeStart);
            Assert.Equal(DateTime.MinValue, model.DateRangeEnd);
        }

        [Fact]
        public void DashboardMetricsModel_HasDefaultNumericValues()
        {
            // Arrange & Act
            var model = new DashboardMetricsModel();

            // Assert
            Assert.Equal(0, model.PendingApprovalsCount);
            Assert.Equal(0.0, model.AverageApprovalTimeHours);
            Assert.Equal(0.0, model.ApprovalRatePercent);
            Assert.Equal(0, model.OverdueRequestsCount);
        }

        [Fact]
        public void DashboardMetricsModel_CanSetAllProperties()
        {
            // Arrange
            var model = new DashboardMetricsModel();
            var testDate = DateTime.UtcNow;

            // Act
            model.PendingApprovalsCount = 10;
            model.AverageApprovalTimeHours = 4.5;
            model.ApprovalRatePercent = 87.5;
            model.OverdueRequestsCount = 2;
            model.MetricsAsOf = testDate;
            model.DateRangeStart = testDate.AddDays(-30);
            model.DateRangeEnd = testDate;

            // Assert
            Assert.Equal(10, model.PendingApprovalsCount);
            Assert.Equal(4.5, model.AverageApprovalTimeHours);
            Assert.Equal(87.5, model.ApprovalRatePercent);
            Assert.Equal(2, model.OverdueRequestsCount);
            Assert.Equal(testDate, model.MetricsAsOf);
            Assert.Equal(testDate.AddDays(-30), model.DateRangeStart);
            Assert.Equal(testDate, model.DateRangeEnd);
        }

        [Fact]
        public void DashboardMetricsModel_AcceptsNegativePendingCount()
        {
            // Arrange & Act - Edge case where negative might occur
            var model = new DashboardMetricsModel { PendingApprovalsCount = -5 };

            // Assert - Should accept (validation is at business logic layer)
            Assert.Equal(-5, model.PendingApprovalsCount);
        }

        [Fact]
        public void DashboardMetricsModel_AcceptsNegativeOverdueCount()
        {
            // Arrange & Act
            var model = new DashboardMetricsModel { OverdueRequestsCount = -1 };

            // Assert
            Assert.Equal(-1, model.OverdueRequestsCount);
        }

        [Fact]
        public void DashboardMetricsModel_AcceptsLargeNumbers()
        {
            // Arrange & Act
            var model = new DashboardMetricsModel
            {
                PendingApprovalsCount = int.MaxValue,
                OverdueRequestsCount = int.MaxValue,
                AverageApprovalTimeHours = double.MaxValue,
                ApprovalRatePercent = 100.0
            };

            // Assert
            Assert.Equal(int.MaxValue, model.PendingApprovalsCount);
            Assert.Equal(int.MaxValue, model.OverdueRequestsCount);
            Assert.Equal(double.MaxValue, model.AverageApprovalTimeHours);
        }

        [Fact]
        public void DashboardMetricsModel_ApprovalRateCanExceed100()
        {
            // Arrange & Act - Model doesn't enforce 0-100 range
            var model = new DashboardMetricsModel { ApprovalRatePercent = 150.0 };

            // Assert
            Assert.Equal(150.0, model.ApprovalRatePercent);
        }

        [Fact]
        public void DashboardMetricsModel_ApprovalRateCanBeNegative()
        {
            // Arrange & Act - Model doesn't enforce 0-100 range
            var model = new DashboardMetricsModel { ApprovalRatePercent = -10.0 };

            // Assert
            Assert.Equal(-10.0, model.ApprovalRatePercent);
        }

        [Fact]
        public void DashboardMetricsModel_AcceptsFutureDates()
        {
            // Arrange
            var futureDate = DateTime.UtcNow.AddYears(100);

            // Act
            var model = new DashboardMetricsModel
            {
                MetricsAsOf = futureDate,
                DateRangeStart = futureDate,
                DateRangeEnd = futureDate.AddDays(30)
            };

            // Assert
            Assert.Equal(futureDate, model.MetricsAsOf);
        }

        [Fact]
        public void DashboardMetricsModel_AcceptsHistoricalDates()
        {
            // Arrange
            var historicalDate = new DateTime(1900, 1, 1);

            // Act
            var model = new DashboardMetricsModel
            {
                MetricsAsOf = historicalDate,
                DateRangeStart = historicalDate,
                DateRangeEnd = historicalDate.AddDays(30)
            };

            // Assert
            Assert.Equal(historicalDate, model.MetricsAsOf);
        }

        [Fact]
        public void DashboardMetricsModel_CanAddRecentActivityItems()
        {
            // Arrange
            var model = new DashboardMetricsModel();
            var item = new RecentActivityItem
            {
                Action = "approved",
                PerformedBy = "Test User",
                RequestId = Guid.NewGuid()
            };

            // Act
            model.RecentActivity.Add(item);

            // Assert
            Assert.Single(model.RecentActivity);
            Assert.Equal("approved", model.RecentActivity[0].Action);
        }

        [Fact]
        public void DashboardMetricsModel_CanAddMultipleRecentActivityItems()
        {
            // Arrange
            var model = new DashboardMetricsModel();

            // Act
            for (int i = 0; i < 100; i++)
            {
                model.RecentActivity.Add(new RecentActivityItem
                {
                    Action = $"action_{i}",
                    PerformedBy = $"User {i}"
                });
            }

            // Assert
            Assert.Equal(100, model.RecentActivity.Count);
            Assert.Equal("action_0", model.RecentActivity[0].Action);
            Assert.Equal("action_99", model.RecentActivity[99].Action);
        }

        [Fact]
        public void DashboardMetricsModel_CanReplaceRecentActivityList()
        {
            // Arrange
            var model = new DashboardMetricsModel();
            var newList = new List<RecentActivityItem>
            {
                new RecentActivityItem { Action = "new1" },
                new RecentActivityItem { Action = "new2" }
            };

            // Act
            model.RecentActivity = newList;

            // Assert
            Assert.Equal(2, model.RecentActivity.Count);
            Assert.Same(newList, model.RecentActivity);
        }

        #endregion

        #region JSON Property Naming Convention Tests

        [Fact]
        public void DashboardMetricsModel_HasJsonPropertyAttributes()
        {
            // Arrange
            var modelType = typeof(DashboardMetricsModel);
            
            // Act & Assert - Check property naming
            var pendingProp = modelType.GetProperty("PendingApprovalsCount");
            var jsonAttr = pendingProp?.GetCustomAttributes(typeof(Newtonsoft.Json.JsonPropertyAttribute), false);
            Assert.NotEmpty(jsonAttr);
            
            var avgTimeProp = modelType.GetProperty("AverageApprovalTimeHours");
            jsonAttr = avgTimeProp?.GetCustomAttributes(typeof(Newtonsoft.Json.JsonPropertyAttribute), false);
            Assert.NotEmpty(jsonAttr);
        }

        [Fact]
        public void RecentActivityItem_HasJsonPropertyAttributes()
        {
            // Arrange
            var modelType = typeof(RecentActivityItem);
            
            // Act & Assert
            var actionProp = modelType.GetProperty("Action");
            var jsonAttr = actionProp?.GetCustomAttributes(typeof(Newtonsoft.Json.JsonPropertyAttribute), false);
            Assert.NotEmpty(jsonAttr);
            
            var performedByProp = modelType.GetProperty("PerformedBy");
            jsonAttr = performedByProp?.GetCustomAttributes(typeof(Newtonsoft.Json.JsonPropertyAttribute), false);
            Assert.NotEmpty(jsonAttr);
        }

        #endregion

        #region Date Range Validation Tests

        [Fact]
        public void DashboardMetricsModel_DateRangeCanBeInverted()
        {
            // Arrange - End before start
            var now = DateTime.UtcNow;

            // Act
            var model = new DashboardMetricsModel
            {
                DateRangeStart = now,
                DateRangeEnd = now.AddDays(-30)
            };

            // Assert - Model accepts (validation at service layer)
            Assert.True(model.DateRangeStart > model.DateRangeEnd);
        }

        [Fact]
        public void DashboardMetricsModel_DateRangeCanBeEqual()
        {
            // Arrange
            var now = DateTime.UtcNow;

            // Act
            var model = new DashboardMetricsModel
            {
                DateRangeStart = now,
                DateRangeEnd = now
            };

            // Assert
            Assert.Equal(model.DateRangeStart, model.DateRangeEnd);
        }

        [Fact]
        public void DashboardMetricsModel_AcceptsMinMaxDateRange()
        {
            // Arrange & Act
            var model = new DashboardMetricsModel
            {
                DateRangeStart = DateTime.MinValue,
                DateRangeEnd = DateTime.MaxValue
            };

            // Assert
            Assert.Equal(DateTime.MinValue, model.DateRangeStart);
            Assert.Equal(DateTime.MaxValue, model.DateRangeEnd);
        }

        #endregion

        #region Precision Tests

        [Fact]
        public void DashboardMetricsModel_PreservesDoublePrecision()
        {
            // Arrange
            var preciseValue = 12.3456789012345;

            // Act
            var model = new DashboardMetricsModel
            {
                AverageApprovalTimeHours = preciseValue,
                ApprovalRatePercent = preciseValue
            };

            // Assert
            Assert.Equal(preciseValue, model.AverageApprovalTimeHours);
            Assert.Equal(preciseValue, model.ApprovalRatePercent);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.1)]
        [InlineData(50.0)]
        [InlineData(99.9)]
        [InlineData(100.0)]
        public void DashboardMetricsModel_AcceptsValidApprovalRates(double rate)
        {
            // Arrange & Act
            var model = new DashboardMetricsModel { ApprovalRatePercent = rate };

            // Assert
            Assert.Equal(rate, model.ApprovalRatePercent);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1.0)]
        [InlineData(24.0)]
        [InlineData(168.0)] // 1 week
        [InlineData(720.0)] // 30 days
        public void DashboardMetricsModel_AcceptsValidApprovalTimes(double hours)
        {
            // Arrange & Act
            var model = new DashboardMetricsModel { AverageApprovalTimeHours = hours };

            // Assert
            Assert.Equal(hours, model.AverageApprovalTimeHours);
        }

        #endregion
    }
}
