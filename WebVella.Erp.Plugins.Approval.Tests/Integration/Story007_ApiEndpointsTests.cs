using System;
using System.Reflection;
using System.Linq;
using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebVella.Erp.Plugins.Approval.Controllers;

namespace WebVella.Erp.Plugins.Approval.Tests.Integration
{
    /// <summary>
    /// Integration tests for STORY-007: REST API Endpoints.
    /// Verifies that ApprovalController exists with required endpoints.
    /// 
    /// Acceptance Criteria from jira-stories/STORY-007-approval-rest-api.md:
    /// - ApprovalController with [Authorize] attribute
    /// - Route pattern: /api/v3.0/p/approval/{resource}
    /// - Workflow CRUD endpoints (GET, POST, PUT, DELETE)
    /// - Pending approvals endpoint
    /// - Approve/Reject/Delegate endpoints
    /// - History endpoint
    /// - Dashboard metrics endpoint
    /// </summary>
    [Trait("Category", "Integration")]
    public class Story007_ApiEndpointsTests
    {
        #region Controller Structure Tests

        [Fact]
        public void ApprovalController_Exists()
        {
            // Arrange
            var assembly = typeof(ApprovalPlugin).Assembly;

            // Act
            var controllerType = assembly.GetType("WebVella.Erp.Plugins.Approval.Controllers.ApprovalController");

            // Assert
            Assert.NotNull(controllerType);
        }

        [Fact]
        public void ApprovalController_ExtendsControllerBase()
        {
            // Arrange
            var controllerType = typeof(ApprovalController);

            // Act - Check inheritance chain
            var inheritsFromController = controllerType.IsSubclassOf(typeof(ControllerBase));

            // Assert
            Assert.True(inheritsFromController);
        }

        [Fact]
        public void ApprovalController_HasAuthorizeAttribute()
        {
            // Arrange
            var controllerType = typeof(ApprovalController);

            // Act
            var attribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();

            // Assert
            Assert.NotNull(attribute);
        }

        [Fact]
        public void ApprovalController_HasRouteAttributesOnMethods()
        {
            // Arrange
            var controllerType = typeof(ApprovalController);

            // Act - Routes are defined at method level
            var methodsWithRoutes = controllerType.GetMethods()
                .Where(m => m.GetCustomAttribute<RouteAttribute>() != null)
                .ToList();

            // Assert - Should have multiple methods with [Route] attributes
            Assert.True(methodsWithRoutes.Count >= 10, $"Expected at least 10 methods with Route attributes, found {methodsWithRoutes.Count}");
            
            // Verify routes follow the pattern api/v3.0/p/approval/*
            foreach (var method in methodsWithRoutes)
            {
                var routeAttr = method.GetCustomAttribute<RouteAttribute>();
                Assert.NotNull(routeAttr);
                Assert.Contains("api", routeAttr.Template);
                Assert.Contains("approval", routeAttr.Template.ToLower());
            }
        }

        #endregion

        #region Workflow Endpoints Tests

        [Fact]
        public void Controller_HasGetWorkflowsEndpoint()
        {
            // Arrange
            var controllerType = typeof(ApprovalController);

            // Act
            var method = controllerType.GetMethods().FirstOrDefault(m =>
                m.GetCustomAttribute<HttpGetAttribute>() != null &&
                (m.Name.Contains("Workflow") || m.Name.Contains("GetAll")));

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void Controller_HasCreateWorkflowEndpoint()
        {
            // Arrange
            var controllerType = typeof(ApprovalController);

            // Act
            var method = controllerType.GetMethods().FirstOrDefault(m =>
                m.GetCustomAttribute<HttpPostAttribute>() != null &&
                m.Name.Contains("Workflow"));

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void Controller_HasUpdateWorkflowEndpoint()
        {
            // Arrange
            var controllerType = typeof(ApprovalController);

            // Act
            var method = controllerType.GetMethods().FirstOrDefault(m =>
                m.GetCustomAttribute<HttpPutAttribute>() != null &&
                m.Name.Contains("Workflow"));

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void Controller_HasDeleteWorkflowEndpoint()
        {
            // Arrange
            var controllerType = typeof(ApprovalController);

            // Act
            var method = controllerType.GetMethods().FirstOrDefault(m =>
                m.GetCustomAttribute<HttpDeleteAttribute>() != null &&
                m.Name.Contains("Workflow"));

            // Assert
            Assert.NotNull(method);
        }

        #endregion

        #region Approval Request Endpoints Tests

        [Fact]
        public void Controller_HasGetPendingEndpoint()
        {
            // Arrange
            var controllerType = typeof(ApprovalController);

            // Act
            var method = controllerType.GetMethods().FirstOrDefault(m =>
                m.GetCustomAttribute<HttpGetAttribute>() != null &&
                m.Name.Contains("Pending"));

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void Controller_HasApproveEndpoint()
        {
            // Arrange
            var controllerType = typeof(ApprovalController);

            // Act
            var method = controllerType.GetMethods().FirstOrDefault(m =>
                m.GetCustomAttribute<HttpPostAttribute>() != null &&
                m.Name.Contains("Approve"));

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void Controller_HasRejectEndpoint()
        {
            // Arrange
            var controllerType = typeof(ApprovalController);

            // Act
            var method = controllerType.GetMethods().FirstOrDefault(m =>
                m.GetCustomAttribute<HttpPostAttribute>() != null &&
                m.Name.Contains("Reject"));

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void Controller_HasDelegateEndpoint()
        {
            // Arrange
            var controllerType = typeof(ApprovalController);

            // Act
            var method = controllerType.GetMethods().FirstOrDefault(m =>
                m.GetCustomAttribute<HttpPostAttribute>() != null &&
                m.Name.Contains("Delegate"));

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void Controller_HasGetHistoryEndpoint()
        {
            // Arrange
            var controllerType = typeof(ApprovalController);

            // Act
            var method = controllerType.GetMethods().FirstOrDefault(m =>
                m.GetCustomAttribute<HttpGetAttribute>() != null &&
                m.Name.Contains("History"));

            // Assert
            Assert.NotNull(method);
        }

        #endregion

        #region Dashboard Endpoints Tests

        [Fact]
        public void Controller_HasDashboardMetricsEndpoint()
        {
            // Arrange
            var controllerType = typeof(ApprovalController);

            // Act
            var method = controllerType.GetMethods().FirstOrDefault(m =>
                m.GetCustomAttribute<HttpGetAttribute>() != null &&
                (m.Name.Contains("Metrics") || m.Name.Contains("Dashboard")));

            // Assert
            Assert.NotNull(method);
        }

        #endregion

        #region Steps and Rules Endpoints Tests

        [Fact]
        public void Controller_HasGetStepsEndpoint()
        {
            // Arrange
            var controllerType = typeof(ApprovalController);

            // Act
            var method = controllerType.GetMethods().FirstOrDefault(m =>
                m.GetCustomAttribute<HttpGetAttribute>() != null &&
                m.Name.Contains("Steps"));

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void Controller_HasGetRulesEndpoint()
        {
            // Arrange
            var controllerType = typeof(ApprovalController);

            // Act
            var method = controllerType.GetMethods().FirstOrDefault(m =>
                m.GetCustomAttribute<HttpGetAttribute>() != null &&
                m.Name.Contains("Rules"));

            // Assert
            Assert.NotNull(method);
        }

        #endregion

        #region Return Type Tests

        [Fact]
        public void AllEndpoints_ReturnActionResult()
        {
            // Arrange
            var controllerType = typeof(ApprovalController);
            var httpMethods = new[] { typeof(HttpGetAttribute), typeof(HttpPostAttribute), 
                                      typeof(HttpPutAttribute), typeof(HttpDeleteAttribute) };

            // Act
            var endpointMethods = controllerType.GetMethods()
                .Where(m => httpMethods.Any(attr => m.GetCustomAttribute(attr) != null))
                .ToList();

            // Assert
            Assert.True(endpointMethods.Count >= 10, "Should have at least 10 API endpoints");
            
            foreach (var method in endpointMethods)
            {
                var returnType = method.ReturnType;
                var isActionResult = returnType == typeof(IActionResult) ||
                                     returnType.IsSubclassOf(typeof(ActionResult)) ||
                                     returnType.Name.Contains("ActionResult") ||
                                     returnType.Name.Contains("IActionResult");
                
                Assert.True(isActionResult, $"Method {method.Name} should return IActionResult or ActionResult");
            }
        }

        #endregion
    }
}
