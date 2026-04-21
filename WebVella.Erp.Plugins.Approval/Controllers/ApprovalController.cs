using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Plugins.Approval.Api;
using WebVella.Erp.Plugins.Approval.Services;
using WebVella.Erp.Web.Services;

namespace WebVella.Erp.Plugins.Approval.Controllers
{
    /// <summary>
    /// Controller for Approval Workflow plugin API endpoints.
    /// Provides REST API access to approval operations and dashboard metrics.
    /// All endpoints require authentication; dashboard metrics require Manager role.
    /// </summary>
    [Authorize]
    public class ApprovalController : Controller
    {
        private readonly RecordManager _recordManager;
        private readonly EntityManager _entityManager;
        private readonly SecurityManager _securityManager;
        private readonly IErpService _erpService;

        /// <summary>
        /// List of role names that are authorized to access the dashboard.
        /// </summary>
        private static readonly List<string> AuthorizedDashboardRoles = new List<string>
        {
            "manager",
            "administrator",
            "admin"
        };

        /// <summary>
        /// Initializes a new instance of the ApprovalController.
        /// </summary>
        /// <param name="erpService">The ERP service for accessing application context.</param>
        public ApprovalController(IErpService erpService)
        {
            _recordManager = new RecordManager();
            _entityManager = new EntityManager();
            _securityManager = new SecurityManager();
            _erpService = erpService;
        }

        /// <summary>
        /// Gets the current authenticated user's ID from the HTTP context claims.
        /// </summary>
        public Guid? CurrentUserId
        {
            get
            {
                if (HttpContext?.User?.Claims != null)
                {
                    var nameIdentifier = HttpContext.User.Claims
                        .FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier);
                    
                    if (nameIdentifier != null && Guid.TryParse(nameIdentifier.Value, out Guid userId))
                    {
                        return userId;
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// Gets the current authenticated user's roles from the HTTP context claims.
        /// </summary>
        private IEnumerable<string> CurrentUserRoles
        {
            get
            {
                if (HttpContext?.User?.Claims != null)
                {
                    return HttpContext.User.Claims
                        .Where(x => x.Type == ClaimTypes.Role)
                        .Select(x => x.Value.ToLowerInvariant());
                }
                return Enumerable.Empty<string>();
            }
        }

        /// <summary>
        /// Determines if the current user has a manager or administrator role.
        /// </summary>
        /// <returns>True if the user has an authorized role, false otherwise.</returns>
        private bool IsManagerRole()
        {
            var userRoles = CurrentUserRoles;
            return userRoles.Any(role => 
                AuthorizedDashboardRoles.Contains(role.ToLowerInvariant()));
        }

        #region Dashboard Metrics

        /// <summary>
        /// Retrieves dashboard metrics for the manager approval workflow dashboard.
        /// Returns pending approvals count, average approval time, approval rate,
        /// overdue requests count, and recent activity feed.
        /// </summary>
        /// <param name="from">Optional start date for time-based metrics. Defaults to 30 days ago.</param>
        /// <param name="to">Optional end date for time-based metrics. Defaults to current date.</param>
        /// <returns>ResponseModel containing DashboardMetricsModel on success, or error details on failure.</returns>
        /// <response code="200">Returns the dashboard metrics successfully.</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="403">User does not have the required Manager role.</response>
        /// <response code="500">Internal server error occurred while retrieving metrics.</response>
        [Route("api/v3.0/p/approval/dashboard/metrics")]
        [HttpGet]
        public ActionResult GetDashboardMetrics(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            var response = new ResponseModel();

            try
            {
                // Validate user authentication
                if (!CurrentUserId.HasValue)
                {
                    response.Success = false;
                    response.Message = "User authentication required.";
                    return Unauthorized(response);
                }

                // Validate Manager role
                if (!IsManagerRole())
                {
                    response.Success = false;
                    response.Message = "Access denied. Manager role is required to view dashboard metrics.";
                    return StatusCode(403, response);
                }

                // Set default date range (last 30 days) if not provided
                DateTime toDate = to ?? DateTime.UtcNow;
                DateTime fromDate = from ?? toDate.AddDays(-30);

                // Validate date range
                if (fromDate > toDate)
                {
                    response.Success = false;
                    response.Message = "Invalid date range. 'from' date must be earlier than 'to' date.";
                    return BadRequest(response);
                }

                // Get metrics from service
                var metricsService = new DashboardMetricsService();
                var metrics = metricsService.GetDashboardMetrics(
                    CurrentUserId.Value, 
                    fromDate, 
                    toDate);

                response.Success = true;
                response.Message = "Dashboard metrics retrieved successfully.";
                response.Object = metrics;

                return Ok(response);
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"An error occurred while retrieving dashboard metrics: {ex.Message}";
                response.Errors = new List<ErrorModel>
                {
                    new ErrorModel
                    {
                        Key = "exception",
                        Value = ex.Message,
                        Message = ex.Message
                    }
                };

                return StatusCode(500, response);
            }
        }

        /// <summary>
        /// Health check endpoint for the approval dashboard API.
        /// Can be used to verify the API is operational.
        /// </summary>
        /// <returns>Simple success response indicating API is available.</returns>
        [Route("api/v3.0/p/approval/dashboard/health")]
        [HttpGet]
        [AllowAnonymous]
        public ActionResult GetDashboardHealth()
        {
            var response = new ResponseModel
            {
                Success = true,
                Message = "Approval Dashboard API is operational.",
                Object = new
                {
                    status = "healthy",
                    timestamp = DateTime.UtcNow,
                    version = "1.0.0"
                }
            };

            return Ok(response);
        }

        #endregion
    }
}
