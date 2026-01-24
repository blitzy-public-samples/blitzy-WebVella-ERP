using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Eql;
using WebVella.Erp.Plugins.Approval.Api;
using WebVella.Erp.Plugins.Approval.Services;
using WebVella.Erp.Web.Services;

namespace WebVella.Erp.Plugins.Approval.Controllers
{
    /// <summary>
    /// REST API controller for the Approval plugin implementing all approval-related HTTP endpoints.
    /// Decorated with [Authorize] attribute to ensure all endpoints require authentication.
    /// Mounts endpoints under /api/v3.0/p/approval/ route prefix.
    /// Provides 12 endpoints for workflow CRUD, pending approvals list, request operations,
    /// history retrieval, and dashboard metrics.
    /// </summary>
    /// <remarks>
    /// Follows WebVella controller patterns from ProjectController.cs:
    /// - Uses ResponseModel for standardized API responses
    /// - Services are instantiated inline following WebVella pattern
    /// - Implements CurrentUserId property for user identification
    /// - All endpoints include comprehensive error handling
    /// </remarks>
    [Authorize]
    public class ApprovalController : Controller
    {
        #region << Private Fields >>

        /// <summary>
        /// RecordManager instance for direct database operations when needed.
        /// </summary>
        private RecordManager recMan;

        /// <summary>
        /// SecurityManager instance for user and role lookups.
        /// </summary>
        private SecurityManager secMan;

        /// <summary>
        /// IErpService instance for ERP core service operations.
        /// </summary>
        private IErpService erpService;

        #endregion

        #region << Constructor >>

        /// <summary>
        /// Initializes a new instance of the <see cref="ApprovalController"/> class.
        /// </summary>
        /// <param name="erpService">The ERP service injected by the DI container.</param>
        public ApprovalController(IErpService erpService)
        {
            recMan = new RecordManager();
            secMan = new SecurityManager();
            this.erpService = erpService;
        }

        #endregion

        #region << Properties >>

        /// <summary>
        /// Gets the current authenticated user's ID from the HTTP context claims.
        /// Returns null if the user is not authenticated or the claim is not present.
        /// </summary>
        /// <remarks>
        /// Follows the pattern from WebVella.Erp.Plugins.Project.Controllers.ProjectController.
        /// Extracts the NameIdentifier claim from HttpContext.User.Claims collection.
        /// </remarks>
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

        #endregion

        #region << Workflow CRUD Endpoints >>

        /// <summary>
        /// Retrieves all approval workflows.
        /// </summary>
        /// <returns>JSON response containing list of all workflows wrapped in ResponseModel.</returns>
        /// <response code="200">Returns the list of workflows.</response>
        /// <response code="500">If an error occurs during retrieval.</response>
        [Route("api/v3.0/p/approval/workflow")]
        [HttpGet]
        public ActionResult GetAllWorkflows()
        {
            var response = new ResponseModel();

            try
            {
                var workflowService = new WorkflowConfigService();
                var workflows = workflowService.GetAll();

                response.Success = true;
                response.Message = "Workflows retrieved successfully.";
                response.Object = workflows;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error retrieving workflows: {ex.Message}";
                response.Object = null;
            }

            return Json(response);
        }

        /// <summary>
        /// Retrieves a specific approval workflow by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier (GUID) of the workflow to retrieve.</param>
        /// <returns>JSON response containing the workflow wrapped in ResponseModel, or error if not found.</returns>
        /// <response code="200">Returns the requested workflow.</response>
        /// <response code="404">If workflow with specified ID is not found.</response>
        /// <response code="500">If an error occurs during retrieval.</response>
        [Route("api/v3.0/p/approval/workflow/{id}")]
        [HttpGet]
        public ActionResult GetWorkflow(Guid id)
        {
            var response = new ResponseModel();

            try
            {
                if (id == Guid.Empty)
                {
                    response.Success = false;
                    response.Message = "Workflow ID is required.";
                    return Json(response);
                }

                var workflowService = new WorkflowConfigService();
                var workflow = workflowService.GetById(id);

                if (workflow == null)
                {
                    response.Success = false;
                    response.Message = $"Workflow with ID '{id}' not found.";
                    return Json(response);
                }

                response.Success = true;
                response.Message = "Workflow retrieved successfully.";
                response.Object = workflow;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error retrieving workflow: {ex.Message}";
                response.Object = null;
            }

            return Json(response);
        }

        /// <summary>
        /// Creates a new approval workflow.
        /// </summary>
        /// <param name="model">The workflow model containing the configuration data.</param>
        /// <returns>JSON response containing the created workflow wrapped in ResponseModel.</returns>
        /// <response code="200">Returns the created workflow.</response>
        /// <response code="400">If the model validation fails.</response>
        /// <response code="500">If an error occurs during creation.</response>
        [Route("api/v3.0/p/approval/workflow")]
        [HttpPost]
        public ActionResult CreateWorkflow([FromBody] ApprovalWorkflowModel model)
        {
            var response = new ResponseModel();

            try
            {
                if (model == null)
                {
                    response.Success = false;
                    response.Message = "Workflow model is required.";
                    return Json(response);
                }

                var workflowService = new WorkflowConfigService();
                var created = workflowService.Create(model);

                response.Success = true;
                response.Message = "Workflow created successfully.";
                response.Object = created;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error creating workflow: {ex.Message}";
                response.Object = null;
            }

            return Json(response);
        }

        /// <summary>
        /// Updates an existing approval workflow.
        /// </summary>
        /// <param name="id">The unique identifier of the workflow to update.</param>
        /// <param name="model">The workflow model containing the updated configuration data.</param>
        /// <returns>JSON response containing the updated workflow wrapped in ResponseModel.</returns>
        /// <response code="200">Returns the updated workflow.</response>
        /// <response code="400">If the model validation fails or ID mismatch.</response>
        /// <response code="404">If workflow with specified ID is not found.</response>
        /// <response code="500">If an error occurs during update.</response>
        [Route("api/v3.0/p/approval/workflow/{id}")]
        [HttpPut]
        public ActionResult UpdateWorkflow(Guid id, [FromBody] ApprovalWorkflowModel model)
        {
            var response = new ResponseModel();

            try
            {
                if (id == Guid.Empty)
                {
                    response.Success = false;
                    response.Message = "Workflow ID is required.";
                    return Json(response);
                }

                if (model == null)
                {
                    response.Success = false;
                    response.Message = "Workflow model is required.";
                    return Json(response);
                }

                // Ensure model ID matches route ID
                model.Id = id;

                var workflowService = new WorkflowConfigService();
                var updated = workflowService.Update(model);

                response.Success = true;
                response.Message = "Workflow updated successfully.";
                response.Object = updated;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error updating workflow: {ex.Message}";
                response.Object = null;
            }

            return Json(response);
        }

        /// <summary>
        /// Deletes an approval workflow by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the workflow to delete.</param>
        /// <returns>JSON response indicating success or failure wrapped in ResponseModel.</returns>
        /// <response code="200">If workflow was deleted successfully.</response>
        /// <response code="400">If workflow has active requests and cannot be deleted.</response>
        /// <response code="404">If workflow with specified ID is not found.</response>
        /// <response code="500">If an error occurs during deletion.</response>
        [Route("api/v3.0/p/approval/workflow/{id}")]
        [HttpDelete]
        public ActionResult DeleteWorkflow(Guid id)
        {
            var response = new ResponseModel();

            try
            {
                if (id == Guid.Empty)
                {
                    response.Success = false;
                    response.Message = "Workflow ID is required.";
                    return Json(response);
                }

                var workflowService = new WorkflowConfigService();
                workflowService.Delete(id);

                response.Success = true;
                response.Message = "Workflow deleted successfully.";
                response.Object = null;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error deleting workflow: {ex.Message}";
                response.Object = null;
            }

            return Json(response);
        }

        #endregion

        #region << Approval Request Endpoints >>

        /// <summary>
        /// Retrieves pending approval requests for the current authenticated user.
        /// Returns requests where the user is an authorized approver for the current step.
        /// </summary>
        /// <returns>JSON response containing list of pending requests wrapped in ResponseModel.</returns>
        /// <response code="200">Returns the list of pending requests.</response>
        /// <response code="401">If user is not authenticated.</response>
        /// <response code="500">If an error occurs during retrieval.</response>
        [Route("api/v3.0/p/approval/pending")]
        [HttpGet]
        public ActionResult GetPendingApprovals()
        {
            var response = new ResponseModel();

            try
            {
                var userId = CurrentUserId;
                if (!userId.HasValue)
                {
                    response.Success = false;
                    response.Message = "User authentication required.";
                    return Json(response);
                }

                var requestService = new ApprovalRequestService();
                var pending = requestService.GetPending(userId);

                response.Success = true;
                response.Message = "Pending approvals retrieved successfully.";
                response.Object = pending;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error retrieving pending approvals: {ex.Message}";
                response.Object = null;
            }

            return Json(response);
        }

        /// <summary>
        /// Retrieves a specific approval request by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier (GUID) of the approval request.</param>
        /// <returns>JSON response containing the request wrapped in ResponseModel, or error if not found.</returns>
        /// <response code="200">Returns the requested approval request.</response>
        /// <response code="404">If request with specified ID is not found.</response>
        /// <response code="500">If an error occurs during retrieval.</response>
        [Route("api/v3.0/p/approval/request/{id}")]
        [HttpGet]
        public ActionResult GetRequest(Guid id)
        {
            var response = new ResponseModel();

            try
            {
                if (id == Guid.Empty)
                {
                    response.Success = false;
                    response.Message = "Request ID is required.";
                    return Json(response);
                }

                var requestService = new ApprovalRequestService();
                var request = requestService.GetById(id);

                if (request == null)
                {
                    response.Success = false;
                    response.Message = $"Approval request with ID '{id}' not found.";
                    return Json(response);
                }

                response.Success = true;
                response.Message = "Approval request retrieved successfully.";
                response.Object = request;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error retrieving approval request: {ex.Message}";
                response.Object = null;
            }

            return Json(response);
        }

        /// <summary>
        /// Approves an approval request, advancing it to the next step or completing the workflow.
        /// Validates that the current user is authorized to approve the request's current step.
        /// </summary>
        /// <param name="id">The unique identifier of the approval request to approve.</param>
        /// <param name="model">The approve request model containing optional comments.</param>
        /// <returns>JSON response containing the updated request wrapped in ResponseModel.</returns>
        /// <response code="200">Returns the updated approval request.</response>
        /// <response code="400">If user is not authorized to approve.</response>
        /// <response code="401">If user is not authenticated.</response>
        /// <response code="404">If request with specified ID is not found.</response>
        /// <response code="500">If an error occurs during approval.</response>
        [Route("api/v3.0/p/approval/request/{id}/approve")]
        [HttpPost]
        public ActionResult ApproveRequest(Guid id, [FromBody] ApproveRequestModel model)
        {
            var response = new ResponseModel();

            try
            {
                if (id == Guid.Empty)
                {
                    response.Success = false;
                    response.Message = "Request ID is required.";
                    return Json(response);
                }

                var userId = CurrentUserId;
                if (!userId.HasValue)
                {
                    response.Success = false;
                    response.Message = "User authentication required.";
                    return Json(response);
                }

                var comments = model?.Comments;
                var requestService = new ApprovalRequestService();
                var updatedRequest = requestService.Approve(id, userId.Value, comments);

                response.Success = true;
                response.Message = "Approval request approved successfully.";
                response.Object = updatedRequest;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error approving request: {ex.Message}";
                response.Object = null;
            }

            return Json(response);
        }

        /// <summary>
        /// Rejects an approval request, terminating the workflow with rejected status.
        /// Validates that the current user is authorized to reject the request's current step.
        /// </summary>
        /// <param name="id">The unique identifier of the approval request to reject.</param>
        /// <param name="model">The reject request model containing the rejection reason and optional comments.</param>
        /// <returns>JSON response containing the updated request wrapped in ResponseModel.</returns>
        /// <response code="200">Returns the updated approval request with rejected status.</response>
        /// <response code="400">If reason is not provided or user is not authorized to reject.</response>
        /// <response code="401">If user is not authenticated.</response>
        /// <response code="404">If request with specified ID is not found.</response>
        /// <response code="500">If an error occurs during rejection.</response>
        [Route("api/v3.0/p/approval/request/{id}/reject")]
        [HttpPost]
        public ActionResult RejectRequest(Guid id, [FromBody] RejectRequestModel model)
        {
            var response = new ResponseModel();

            try
            {
                if (id == Guid.Empty)
                {
                    response.Success = false;
                    response.Message = "Request ID is required.";
                    return Json(response);
                }

                var userId = CurrentUserId;
                if (!userId.HasValue)
                {
                    response.Success = false;
                    response.Message = "User authentication required.";
                    return Json(response);
                }

                if (model == null || string.IsNullOrWhiteSpace(model.Reason))
                {
                    response.Success = false;
                    response.Message = "Rejection reason is required.";
                    return Json(response);
                }

                var requestService = new ApprovalRequestService();
                var updatedRequest = requestService.Reject(id, userId.Value, model.Reason, model.Comments);

                response.Success = true;
                response.Message = "Approval request rejected successfully.";
                response.Object = updatedRequest;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error rejecting request: {ex.Message}";
                response.Object = null;
            }

            return Json(response);
        }

        /// <summary>
        /// Delegates an approval request to another user for the current step.
        /// Validates that the current user is authorized to delegate the request.
        /// </summary>
        /// <param name="id">The unique identifier of the approval request to delegate.</param>
        /// <param name="model">The delegate request model containing the target user ID and optional comments.</param>
        /// <returns>JSON response containing the request wrapped in ResponseModel.</returns>
        /// <response code="200">Returns the approval request after delegation.</response>
        /// <response code="400">If delegate user ID is not provided or user is not authorized to delegate.</response>
        /// <response code="401">If user is not authenticated.</response>
        /// <response code="404">If request with specified ID is not found.</response>
        /// <response code="500">If an error occurs during delegation.</response>
        [Route("api/v3.0/p/approval/request/{id}/delegate")]
        [HttpPost]
        public ActionResult DelegateRequest(Guid id, [FromBody] DelegateRequestModel model)
        {
            var response = new ResponseModel();

            try
            {
                if (id == Guid.Empty)
                {
                    response.Success = false;
                    response.Message = "Request ID is required.";
                    return Json(response);
                }

                var userId = CurrentUserId;
                if (!userId.HasValue)
                {
                    response.Success = false;
                    response.Message = "User authentication required.";
                    return Json(response);
                }

                if (model == null || model.DelegateToUserId == Guid.Empty)
                {
                    response.Success = false;
                    response.Message = "Delegate to user ID is required.";
                    return Json(response);
                }

                var requestService = new ApprovalRequestService();
                var updatedRequest = requestService.Delegate(id, userId.Value, model.DelegateToUserId, model.Comments);

                response.Success = true;
                response.Message = "Approval request delegated successfully.";
                response.Object = updatedRequest;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error delegating request: {ex.Message}";
                response.Object = null;
            }

            return Json(response);
        }

        #endregion

        #region << History Endpoint >>

        /// <summary>
        /// Retrieves the approval history for a specific request.
        /// Returns the complete audit trail from submission to current state.
        /// </summary>
        /// <param name="id">The unique identifier of the approval request.</param>
        /// <returns>JSON response containing list of history records wrapped in ResponseModel.</returns>
        /// <response code="200">Returns the list of history records.</response>
        /// <response code="404">If request with specified ID is not found.</response>
        /// <response code="500">If an error occurs during retrieval.</response>
        [Route("api/v3.0/p/approval/request/{id}/history")]
        [HttpGet]
        public ActionResult GetRequestHistory(Guid id)
        {
            var response = new ResponseModel();

            try
            {
                if (id == Guid.Empty)
                {
                    response.Success = false;
                    response.Message = "Request ID is required.";
                    return Json(response);
                }

                var historyService = new ApprovalHistoryService();
                var history = historyService.GetByRequestId(id);

                response.Success = true;
                response.Message = "Approval history retrieved successfully.";
                response.Object = history;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error retrieving approval history: {ex.Message}";
                response.Object = null;
            }

            return Json(response);
        }

        #endregion

        #region << Dashboard Metrics Endpoint >>

        /// <summary>
        /// Retrieves dashboard metrics for the approval workflow system.
        /// Requires Manager or Administrator role access.
        /// Returns real-time KPIs including pending count, average approval time, approval rate,
        /// overdue count, and recent activity count.
        /// </summary>
        /// <returns>JSON response containing dashboard metrics wrapped in ResponseModel.</returns>
        /// <response code="200">Returns the dashboard metrics.</response>
        /// <response code="401">If user is not authenticated.</response>
        /// <response code="403">If user does not have Manager or Administrator role.</response>
        /// <response code="500">If an error occurs during metrics calculation.</response>
        [Route("api/v3.0/p/approval/dashboard/metrics")]
        [HttpGet]
        public ActionResult GetDashboardMetrics()
        {
            var response = new ResponseModel();

            try
            {
                // Validate user is authenticated
                var userId = CurrentUserId;
                if (!userId.HasValue)
                {
                    response.Success = false;
                    response.Message = "User authentication required.";
                    return Json(response);
                }

                // Validate user has Manager or Administrator role
                if (!IsManagerOrAdministrator())
                {
                    response.Success = false;
                    response.Message = "Access denied. Manager or Administrator role required.";
                    return Json(response);
                }

                var metricsService = new DashboardMetricsService();
                var metrics = metricsService.GetDashboardMetrics();

                response.Success = true;
                response.Message = "Dashboard metrics retrieved successfully.";
                response.Object = metrics;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error retrieving dashboard metrics: {ex.Message}";
                response.Object = null;
            }

            return Json(response);
        }

        #endregion

        #region << Private Helper Methods >>

        /// <summary>
        /// Validates whether the current user has Manager or Administrator role.
        /// Used for dashboard access control.
        /// </summary>
        /// <returns>True if the user has Manager or Administrator role; otherwise, false.</returns>
        private bool IsManagerOrAdministrator()
        {
            try
            {
                var currentUser = SecurityContext.CurrentUser;
                if (currentUser == null)
                {
                    return false;
                }

                // Check if user has Manager or Administrator role
                if (currentUser.Roles != null)
                {
                    foreach (var role in currentUser.Roles)
                    {
                        var roleName = role.Name?.ToLowerInvariant();
                        if (roleName == "manager" || roleName == "administrator" || roleName == "admin")
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception)
            {
                // SecurityContext may throw if not in valid context
                return false;
            }
        }

        #endregion
    }
}
