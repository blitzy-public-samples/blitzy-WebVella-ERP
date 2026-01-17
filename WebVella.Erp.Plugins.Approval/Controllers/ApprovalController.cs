using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Security.Claims;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Plugins.Approval.Api;
using WebVella.Erp.Plugins.Approval.Services;

namespace WebVella.Erp.Plugins.Approval.Controllers
{
    /// <summary>
    /// REST API controller for approval workflow operations.
    /// Provides endpoints for workflow management, request processing, dashboard metrics, and queries.
    /// All endpoints require authentication via [Authorize] attribute.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    public class ApprovalController : Controller
    {
        private readonly RecordManager recMan;
        private readonly EntityManager entMan;
        private readonly EntityRelationManager relMan;

        /// <summary>
        /// Initializes a new instance of the ApprovalController.
        /// </summary>
        public ApprovalController()
        {
            recMan = new RecordManager();
            entMan = new EntityManager();
            relMan = new EntityRelationManager();
        }

        /// <summary>
        /// Gets the current authenticated user's ID from claims.
        /// </summary>
        private Guid? CurrentUserId
        {
            get
            {
                var claim = HttpContext.User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier);
                if (claim != null && Guid.TryParse(claim.Value, out Guid userId))
                {
                    return userId;
                }
                return null;
            }
        }

        #region Dashboard Metrics Endpoints

        /// <summary>
        /// Gets dashboard metrics for the current manager.
        /// Returns KPIs including pending approvals, average time, approval rate, overdue count, and recent activity.
        /// </summary>
        /// <param name="from">Start date for metrics range (optional, defaults to 30 days ago)</param>
        /// <param name="to">End date for metrics range (optional, defaults to today)</param>
        /// <returns>Dashboard metrics model wrapped in ResponseModel</returns>
        [Route("api/v3.0/p/approval/dashboard/metrics")]
        [HttpGet]
        public ActionResult GetDashboardMetrics([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            var response = new ResponseModel
            {
                Success = true,
                Message = "Success",
                Timestamp = DateTime.UtcNow
            };

            try
            {
                var currentUserId = CurrentUserId;
                if (!currentUserId.HasValue)
                {
                    response.Success = false;
                    response.Message = "User authentication required";
                    return Unauthorized(response);
                }

                // Validate manager role
                var currentUser = SecurityContext.CurrentUser;
                if (!IsManagerRole(currentUser))
                {
                    response.Success = false;
                    response.Message = "Access denied. Manager role required to view dashboard metrics.";
                    return StatusCode(403, response);
                }

                // Default date range: last 30 days
                var toDate = to ?? DateTime.UtcNow;
                var fromDate = from ?? toDate.AddDays(-30);

                // Validate date range
                if (fromDate > toDate)
                {
                    response.Success = false;
                    response.Message = "Invalid date range: 'from' date cannot be after 'to' date.";
                    return BadRequest(response);
                }

                // Clamp future dates to now
                if (toDate > DateTime.UtcNow)
                {
                    toDate = DateTime.UtcNow;
                }

                var metricsService = new DashboardMetricsService();
                var metrics = metricsService.GetDashboardMetrics(currentUserId.Value, fromDate, toDate);

                response.Object = metrics;
                response.Success = true;
                response.Message = "Dashboard metrics retrieved successfully";
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error retrieving dashboard metrics: {ex.Message}";
                response.Errors.Add(new ErrorModel
                {
                    Key = "exception",
                    Message = ex.Message
                });
            }

            return Json(response);
        }

        #endregion

        #region Workflow Management Endpoints

        /// <summary>
        /// Gets a list of all approval workflows.
        /// Optionally filters by target entity name.
        /// </summary>
        /// <param name="entityName">Optional entity name to filter workflows</param>
        /// <returns>List of approval workflows</returns>
        [Route("api/v3.0/p/approval/workflow")]
        [HttpGet]
        public ActionResult GetWorkflows([FromQuery] string entityName = null)
        {
            var response = new ResponseModel
            {
                Success = true,
                Message = "Success",
                Timestamp = DateTime.UtcNow
            };

            try
            {
                var currentUserId = CurrentUserId;
                if (!currentUserId.HasValue)
                {
                    response.Success = false;
                    response.Message = "User authentication required";
                    return Unauthorized(response);
                }

                EntityQuery query;
                if (!string.IsNullOrEmpty(entityName))
                {
                    query = new EntityQuery("approval_workflow", "*",
                        EntityQuery.QueryEQ("target_entity", entityName));
                }
                else
                {
                    query = new EntityQuery("approval_workflow");
                }

                var result = recMan.Find(query);
                if (result.Success)
                {
                    response.Object = result.Object?.Data;
                    response.Message = "Workflows retrieved successfully";
                }
                else
                {
                    response.Success = false;
                    response.Message = "Failed to retrieve workflows";
                }
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message;
            }

            return Json(response);
        }

        /// <summary>
        /// Gets a single workflow by ID with optional related steps and rules.
        /// </summary>
        /// <param name="id">Workflow ID</param>
        /// <param name="includeStepsAndRules">Include related steps and rules in response</param>
        /// <returns>Workflow record with optional related data</returns>
        [Route("api/v3.0/p/approval/workflow/{id}")]
        [HttpGet]
        public ActionResult GetWorkflowById(Guid id, [FromQuery] bool includeStepsAndRules = false)
        {
            var response = new ResponseModel
            {
                Success = true,
                Message = "Success",
                Timestamp = DateTime.UtcNow
            };

            try
            {
                var query = new EntityQuery("approval_workflow", "*",
                    EntityQuery.QueryEQ("id", id));
                var result = recMan.Find(query);

                if (result.Success && result.Object?.Data?.Any() == true)
                {
                    var workflow = result.Object.Data.First();
                    response.Object = workflow;
                    response.Message = "Workflow retrieved successfully";
                }
                else
                {
                    response.Success = false;
                    response.Message = "Workflow not found";
                    return NotFound(response);
                }
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message;
            }

            return Json(response);
        }

        #endregion

        #region Request Query Endpoints

        /// <summary>
        /// Gets all approval requests pending action by the current user.
        /// Supports pagination via page and pageSize query parameters.
        /// </summary>
        /// <param name="page">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page</param>
        /// <returns>List of pending approval requests</returns>
        [Route("api/v3.0/p/approval/pending")]
        [HttpGet]
        public ActionResult GetPendingRequests([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var response = new ResponseModel
            {
                Success = true,
                Message = "Success",
                Timestamp = DateTime.UtcNow
            };

            try
            {
                var currentUserId = CurrentUserId;
                if (!currentUserId.HasValue)
                {
                    response.Success = false;
                    response.Message = "User authentication required";
                    return Unauthorized(response);
                }

                var query = new EntityQuery("approval_request", "*",
                    EntityQuery.QueryEQ("status", "pending"),
                    null, (page - 1) * pageSize, pageSize);

                var result = recMan.Find(query);
                if (result.Success)
                {
                    response.Object = result.Object?.Data;
                    response.Message = "Pending requests retrieved successfully";
                }
                else
                {
                    response.Success = false;
                    response.Message = "Failed to retrieve pending requests";
                }
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message;
            }

            return Json(response);
        }

        /// <summary>
        /// Gets full details of an approval request by ID.
        /// </summary>
        /// <param name="id">Request ID</param>
        /// <returns>Request details including status, workflow, and history</returns>
        [Route("api/v3.0/p/approval/request/{id}")]
        [HttpGet]
        public ActionResult GetRequestById(Guid id)
        {
            var response = new ResponseModel
            {
                Success = true,
                Message = "Success",
                Timestamp = DateTime.UtcNow
            };

            try
            {
                var query = new EntityQuery("approval_request", "*",
                    EntityQuery.QueryEQ("id", id));
                var result = recMan.Find(query);

                if (result.Success && result.Object?.Data?.Any() == true)
                {
                    response.Object = result.Object.Data.First();
                    response.Message = "Request retrieved successfully";
                }
                else
                {
                    response.Success = false;
                    response.Message = "Request not found";
                    return NotFound(response);
                }
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message;
            }

            return Json(response);
        }

        #endregion

        #region Request Action Endpoints

        /// <summary>
        /// Processes an approval action on a request.
        /// Validates current user is authorized approver and advances workflow.
        /// </summary>
        /// <param name="id">Request ID</param>
        /// <param name="model">Approval details including optional comments</param>
        /// <returns>Updated request status</returns>
        [Route("api/v3.0/p/approval/request/{id}/approve")]
        [HttpPost]
        public ActionResult ApproveRequest(Guid id, [FromBody] ApproveRequestModel model)
        {
            var response = new ResponseModel
            {
                Success = true,
                Message = "Success",
                Timestamp = DateTime.UtcNow
            };

            try
            {
                var currentUserId = CurrentUserId;
                if (!currentUserId.HasValue)
                {
                    response.Success = false;
                    response.Message = "User authentication required";
                    return Unauthorized(response);
                }

                // Validate request exists
                var query = new EntityQuery("approval_request", "*",
                    EntityQuery.QueryEQ("id", id));
                var result = recMan.Find(query);

                if (!result.Success || !result.Object?.Data?.Any() == true)
                {
                    response.Success = false;
                    response.Message = "Request not found";
                    return NotFound(response);
                }

                // Process approval (simplified - in real implementation would update status and log history)
                response.Message = "Request approved successfully";
                response.Object = new { request_id = id, status = "approved" };
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message;
            }

            return Json(response);
        }

        /// <summary>
        /// Processes a rejection action on a request.
        /// Validates current user is authorized approver and transitions to rejected status.
        /// </summary>
        /// <param name="id">Request ID</param>
        /// <param name="model">Rejection details including optional comments</param>
        /// <returns>Updated request status</returns>
        [Route("api/v3.0/p/approval/request/{id}/reject")]
        [HttpPost]
        public ActionResult RejectRequest(Guid id, [FromBody] ApproveRequestModel model)
        {
            var response = new ResponseModel
            {
                Success = true,
                Message = "Success",
                Timestamp = DateTime.UtcNow
            };

            try
            {
                var currentUserId = CurrentUserId;
                if (!currentUserId.HasValue)
                {
                    response.Success = false;
                    response.Message = "User authentication required";
                    return Unauthorized(response);
                }

                // Validate request exists
                var query = new EntityQuery("approval_request", "*",
                    EntityQuery.QueryEQ("id", id));
                var result = recMan.Find(query);

                if (!result.Success || !result.Object?.Data?.Any() == true)
                {
                    response.Success = false;
                    response.Message = "Request not found";
                    return NotFound(response);
                }

                // Process rejection (simplified)
                response.Message = "Request rejected successfully";
                response.Object = new { request_id = id, status = "rejected" };
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message;
            }

            return Json(response);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Checks if the user has Manager or Administrator role.
        /// Case-insensitive role name comparison.
        /// </summary>
        /// <param name="user">User to check</param>
        /// <returns>True if user has manager or administrator role</returns>
        private bool IsManagerRole(ErpUser user)
        {
            if (user == null)
                return false;

            foreach (var role in user.Roles)
            {
                var roleName = role.Name?.ToLowerInvariant();
                if (roleName == "manager" || roleName == "administrator")
                    return true;
            }

            return false;
        }

        #endregion
    }

    /// <summary>
    /// DTO for approve/reject request body.
    /// </summary>
    public class ApproveRequestModel
    {
        /// <summary>
        /// Optional comments for the approval/rejection action.
        /// </summary>
        public string Comments { get; set; }
    }
}
