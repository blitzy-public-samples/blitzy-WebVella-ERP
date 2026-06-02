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
    /// The REST surface is mounted under the route prefix <c>/api/v3.0/p/approval/</c> and authenticates requests with the <c>JWT_OR_COOKIE</c> scheme.
    /// The class-level <c>[Authorize]</c> secures every action by default, a posture inverted only by <c>[AllowAnonymous]</c> on the health endpoint.
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
        /// Per ADR-004 (dual-layer authorization) this is the single source of truth for the controller layer's role allow-list.
        /// It intentionally mirrors the separately-declared <c>AuthorizedRoles</c> constant in <c>Components/PcApprovalDashboard/PcApprovalDashboard.cs</c> (~L41) — the same {manager, administrator, admin} values enforced at two reachable entry paths, deliberately not a single shared constant.
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
        /// Performs an allow-list membership check against <c>AuthorizedDashboardRoles</c>.
        /// The comparison is robust to identity-provider casing because role claims are lowercased both in <c>CurrentUserRoles</c> and again per role here.
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
        /// <response code="400">The date range is invalid because 'from' is later than 'to'.</response>
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
                // An explicit authenticated-user guard returns a structured ResponseModel with 401 for an expired or absent session, rather than relying solely on the [Authorize] redirect.
                if (!CurrentUserId.HasValue)
                {
                    response.Success = false;
                    response.Message = "User authentication required.";
                    return Unauthorized(response);
                }

                // Enforces the AuthorizedDashboardRoles allow-list at the controller layer per ADR-004, so an authenticated non-manager is refused with 403.
                if (!IsManagerRole())
                {
                    response.Success = false;
                    response.Message = "Access denied. Manager role is required to view dashboard metrics.";
                    return StatusCode(403, response);
                }

                // Default to the last 30 days when the caller omits the range so an unbounded query window is never issued against the metrics service.
                DateTime toDate = to ?? DateTime.UtcNow;
                DateTime fromDate = from ?? toDate.AddDays(-30);

                // Reject an inverted range (from later than to) early with 400 so the service never runs an impossible, always-empty query.
                if (fromDate > toDate)
                {
                    response.Success = false;
                    response.Message = "Invalid date range. 'from' date must be earlier than 'to' date.";
                    return BadRequest(response);
                }

                // A fresh DashboardMetricsService is constructed per request (no shared state) and its result is wrapped in ResponseModel.Object, returned as 200 on success.
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
                // Convert any unexpected exception into a standardized ErrorModel payload (HTTP 500) so API consumers always receive a consistent ResponseModel shape.
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
        /// This endpoint is intentionally anonymous so external availability checks require no credentials.
        /// </summary>
        /// <returns>Simple success response indicating API is available.</returns>
        [Route("api/v3.0/p/approval/dashboard/health")]
        [HttpGet]
        // The liveness probe must be reachable without authentication so uptime monitors and load balancers can verify availability before or independently of any login. This single endpoint therefore inverts the class-level [Authorize].
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
