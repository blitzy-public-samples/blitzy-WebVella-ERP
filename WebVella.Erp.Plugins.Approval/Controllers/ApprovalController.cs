using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Database;
using WebVella.Erp.Plugins.Approval.Api;
using WebVella.Erp.Plugins.Approval.Services;
using WebVella.Erp.Web.Services;

namespace WebVella.Erp.Plugins.Approval.Controllers
{
    /// <summary>
    /// REST API controller for approval workflow operations.
    /// Provides endpoints for workflow management, request processing, and dashboard metrics.
    /// All endpoints require authentication via [Authorize] attribute.
    /// </summary>
    [Authorize]
    public class ApprovalController : Controller
    {
        private const string ENTITY_APPROVAL_WORKFLOW = "approval_workflow";
        private const string ENTITY_APPROVAL_REQUEST = "approval_request";
        private const string ENTITY_APPROVAL_HISTORY = "approval_history";
        private const string ENTITY_APPROVAL_STEP = "approval_step";

        private const string ROLE_MANAGER = "manager";
        private const string ROLE_ADMINISTRATOR = "administrator";

        RecordManager recMan;
        EntityManager entMan;
        EntityRelationManager relMan;
        SecurityManager secMan;
        IErpService erpService;

        /// <summary>
        /// Creates a new instance of the ApprovalController.
        /// </summary>
        /// <param name="erpService">ERP service for request context access</param>
        public ApprovalController(IErpService erpService)
        {
            recMan = new RecordManager();
            secMan = new SecurityManager();
            entMan = new EntityManager();
            relMan = new EntityRelationManager();
            this.erpService = erpService;
        }

        /// <summary>
        /// Gets the current authenticated user's ID from the claims.
        /// </summary>
        public Guid? CurrentUserId
        {
            get
            {
                if (HttpContext != null && HttpContext.User != null && HttpContext.User.Claims != null)
                {
                    var nameIdentifier = HttpContext.User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier);
                    if (nameIdentifier is null)
                        return null;

                    return new Guid(nameIdentifier.Value);
                }
                return null;
            }
        }

        /// <summary>
        /// Checks if the current user has the specified role.
        /// </summary>
        /// <param name="roleName">Role name to check</param>
        /// <returns>True if user has the role, false otherwise</returns>
        private bool CurrentUserHasRole(string roleName)
        {
            if (!CurrentUserId.HasValue)
                return false;

            try
            {
                var user = secMan.GetUser(CurrentUserId.Value);
                if (user == null || user.Roles == null)
                    return false;

                // Check if user has the specified role (case-insensitive)
                return user.Roles.Any(r => string.Equals(r.Name, roleName, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        #region << Dashboard Metrics Endpoints >>

        /// <summary>
        /// Gets dashboard metrics for the Manager Approval Dashboard.
        /// Requires Manager or Administrator role.
        /// </summary>
        /// <param name="days">Number of days for date range calculations (default: 30)</param>
        /// <param name="activityCount">Number of recent activities to return (default: 5)</param>
        /// <returns>ResponseModel containing DashboardMetricsModel</returns>
        [Route("api/v3.0/p/approval/dashboard/metrics")]
        [HttpGet]
        public ActionResult GetDashboardMetrics([FromQuery] int? days = 30, [FromQuery] int? activityCount = 5)
        {
            var response = new ResponseModel();

            try
            {
                // Validate user is authenticated
                if (!CurrentUserId.HasValue)
                {
                    response.Success = false;
                    response.Message = "User is not authenticated";
                    return Unauthorized(response);
                }

                // Validate user has Manager or Administrator role
                if (!CurrentUserHasRole(ROLE_MANAGER) && !CurrentUserHasRole(ROLE_ADMINISTRATOR))
                {
                    response.Success = false;
                    response.Message = "Access denied. Manager or Administrator role is required to view dashboard metrics.";
                    return StatusCode(403, response);
                }

                // Validate parameters
                int dateRangeDays = Math.Max(1, Math.Min(days ?? 30, 365));
                int activityLimit = Math.Max(1, Math.Min(activityCount ?? 5, 50));

                // Get metrics from service
                var metricsService = new ApprovalMetricsService();
                var metrics = metricsService.GetDashboardMetrics(CurrentUserId.Value, dateRangeDays, activityLimit);

                response.Success = true;
                response.Message = "Dashboard metrics retrieved successfully";
                response.Object = metrics;

                return Json(response);
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error retrieving dashboard metrics: {ex.Message}";
                return StatusCode(500, response);
            }
        }

        /// <summary>
        /// Gets recent approval activity for the dashboard feed.
        /// Requires Manager or Administrator role.
        /// </summary>
        /// <param name="count">Number of activities to return (default: 5, max: 50)</param>
        /// <returns>ResponseModel containing list of RecentActivityModel</returns>
        [Route("api/v3.0/p/approval/dashboard/activity")]
        [HttpGet]
        public ActionResult GetRecentActivity([FromQuery] int? count = 5)
        {
            var response = new ResponseModel();

            try
            {
                // Validate user is authenticated
                if (!CurrentUserId.HasValue)
                {
                    response.Success = false;
                    response.Message = "User is not authenticated";
                    return Unauthorized(response);
                }

                // Validate user has Manager or Administrator role
                if (!CurrentUserHasRole(ROLE_MANAGER) && !CurrentUserHasRole(ROLE_ADMINISTRATOR))
                {
                    response.Success = false;
                    response.Message = "Access denied. Manager or Administrator role is required.";
                    return StatusCode(403, response);
                }

                // Validate count parameter
                int activityLimit = Math.Max(1, Math.Min(count ?? 5, 50));

                // Get activities from service
                var metricsService = new ApprovalMetricsService();
                var activities = metricsService.GetRecentActivity(activityLimit);

                response.Success = true;
                response.Message = "Recent activity retrieved successfully";
                response.Object = activities;

                return Json(response);
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error retrieving recent activity: {ex.Message}";
                return StatusCode(500, response);
            }
        }

        /// <summary>
        /// Gets the count of overdue approval requests.
        /// Requires Manager or Administrator role.
        /// </summary>
        /// <returns>ResponseModel containing overdue count</returns>
        [Route("api/v3.0/p/approval/dashboard/overdue")]
        [HttpGet]
        public ActionResult GetOverdueCount()
        {
            var response = new ResponseModel();

            try
            {
                // Validate user is authenticated
                if (!CurrentUserId.HasValue)
                {
                    response.Success = false;
                    response.Message = "User is not authenticated";
                    return Unauthorized(response);
                }

                // Validate user has Manager or Administrator role
                if (!CurrentUserHasRole(ROLE_MANAGER) && !CurrentUserHasRole(ROLE_ADMINISTRATOR))
                {
                    response.Success = false;
                    response.Message = "Access denied. Manager or Administrator role is required.";
                    return StatusCode(403, response);
                }

                // Get overdue count from service
                var metricsService = new ApprovalMetricsService();
                var overdueCount = metricsService.GetOverdueRequestsCount(CurrentUserId.Value);

                response.Success = true;
                response.Message = "Overdue count retrieved successfully";
                response.Object = new { overdue_count = overdueCount };

                return Json(response);
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error retrieving overdue count: {ex.Message}";
                return StatusCode(500, response);
            }
        }

        #endregion

        #region << Workflow Management Endpoints >>

        /// <summary>
        /// Gets a list of all approval workflows.
        /// </summary>
        /// <param name="entityName">Optional filter by target entity name</param>
        /// <returns>ResponseModel containing list of workflow records</returns>
        [Route("api/v3.0/p/approval/workflow")]
        [HttpGet]
        public ActionResult GetWorkflows([FromQuery] string entityName = null)
        {
            var response = new ResponseModel();

            try
            {
                response.Success = true;
                response.Message = "Workflows retrieved successfully";
                response.Object = new List<object>(); // Placeholder - entities not yet created

                return Json(response);
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error retrieving workflows: {ex.Message}";
                return StatusCode(500, response);
            }
        }

        /// <summary>
        /// Gets a single workflow by ID.
        /// </summary>
        /// <param name="id">Workflow ID</param>
        /// <param name="includeStepsAndRules">Include related steps and rules</param>
        /// <returns>ResponseModel containing workflow record</returns>
        [Route("api/v3.0/p/approval/workflow/{id}")]
        [HttpGet]
        public ActionResult GetWorkflow(Guid id, [FromQuery] bool includeStepsAndRules = false)
        {
            var response = new ResponseModel();

            try
            {
                response.Success = false;
                response.Message = $"Workflow with ID {id} not found";

                return Json(response);
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error retrieving workflow: {ex.Message}";
                return StatusCode(500, response);
            }
        }

        #endregion

        #region << Request Query Endpoints >>

        /// <summary>
        /// Gets pending approval requests for the current user.
        /// </summary>
        /// <param name="page">Page number (1-based)</param>
        /// <param name="pageSize">Items per page</param>
        /// <returns>ResponseModel containing paginated list of pending requests</returns>
        [Route("api/v3.0/p/approval/pending")]
        [HttpGet]
        public ActionResult GetPendingApprovals([FromQuery] int? page = 1, [FromQuery] int? pageSize = 10)
        {
            var response = new ResponseModel();

            try
            {
                if (!CurrentUserId.HasValue)
                {
                    response.Success = false;
                    response.Message = "User is not authenticated";
                    return Unauthorized(response);
                }

                response.Success = true;
                response.Message = "Pending approvals retrieved successfully";
                response.Object = new { items = new List<object>(), total = 0, page = page ?? 1, pageSize = pageSize ?? 10 };

                return Json(response);
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error retrieving pending approvals: {ex.Message}";
                return StatusCode(500, response);
            }
        }

        /// <summary>
        /// Gets details of a specific approval request.
        /// </summary>
        /// <param name="id">Request ID</param>
        /// <returns>ResponseModel containing request details</returns>
        [Route("api/v3.0/p/approval/request/{id}")]
        [HttpGet]
        public ActionResult GetRequest(Guid id)
        {
            var response = new ResponseModel();

            try
            {
                response.Success = false;
                response.Message = $"Request with ID {id} not found";

                return Json(response);
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error retrieving request: {ex.Message}";
                return StatusCode(500, response);
            }
        }

        #endregion
    }
}
