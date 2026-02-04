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
using WebVella.Erp.Diagnostics;
using WebVella.Erp.Eql;
using WebVella.Erp.Plugins.Approval.Model;
using WebVella.Erp.Plugins.Approval.Services;
using WebVella.Erp.Web.Services;

namespace WebVella.Erp.Plugins.Approval.Controllers
{
    /// <summary>
    /// REST API controller for the Approval plugin providing endpoints under /api/v3.0/p/approval/*.
    /// Implements workflow CRUD operations, request operations (approve/reject/delegate),
    /// history retrieval, and dashboard metrics endpoint.
    /// Uses ResponseModel for standardized responses and integrates with ApprovalRouteService,
    /// ApprovalRequestService, ApprovalHistoryService, and DashboardMetricsService for business logic.
    /// </summary>
    [Authorize]
    public class ApprovalController : Controller
    {
        #region Constants

        /// <summary>
        /// Entity name for approval workflows.
        /// </summary>
        private const string ENTITY_WORKFLOW = "approval_workflow";

        /// <summary>
        /// Entity name for approval steps.
        /// </summary>
        private const string ENTITY_STEP = "approval_step";

        /// <summary>
        /// Entity name for approval rules.
        /// </summary>
        private const string ENTITY_RULE = "approval_rule";

        /// <summary>
        /// Entity name for approval requests.
        /// </summary>
        private const string ENTITY_REQUEST = "approval_request";

        /// <summary>
        /// Field name for record ID.
        /// </summary>
        private const string FIELD_ID = "id";

        /// <summary>
        /// Field name for name.
        /// </summary>
        private const string FIELD_NAME = "name";

        /// <summary>
        /// Field name for entity name.
        /// </summary>
        private const string FIELD_ENTITY_NAME = "entity_name";

        /// <summary>
        /// Field name for is_active flag.
        /// </summary>
        private const string FIELD_IS_ACTIVE = "is_active";

        /// <summary>
        /// Field name for created_on timestamp.
        /// </summary>
        private const string FIELD_CREATED_ON = "created_on";

        /// <summary>
        /// Field name for created_by user ID.
        /// </summary>
        private const string FIELD_CREATED_BY = "created_by";

        /// <summary>
        /// Field name for status.
        /// </summary>
        private const string FIELD_STATUS = "status";

        /// <summary>
        /// Field name for workflow_id foreign key.
        /// </summary>
        private const string FIELD_WORKFLOW_ID = "workflow_id";

        /// <summary>
        /// Field name for comments.
        /// </summary>
        private const string FIELD_COMMENTS = "comments";

        /// <summary>
        /// Field name for delegate to user ID.
        /// </summary>
        private const string FIELD_DELEGATE_TO = "delegateToUserId";

        /// <summary>
        /// Default page size for paginated requests.
        /// </summary>
        private const int DEFAULT_PAGE_SIZE = 20;

        /// <summary>
        /// Maximum page size for paginated requests.
        /// </summary>
        private const int MAX_PAGE_SIZE = 100;

        /// <summary>
        /// Default number of days for dashboard date range.
        /// </summary>
        private const int DEFAULT_DASHBOARD_DAYS = 30;

        #endregion

        #region Private Fields

        /// <summary>
        /// Record manager for entity CRUD operations.
        /// </summary>
        private readonly RecordManager recMan;

        /// <summary>
        /// Entity manager for schema access.
        /// </summary>
        private readonly EntityManager entMan;

        /// <summary>
        /// Entity relation manager for relationship management.
        /// </summary>
        private readonly EntityRelationManager relMan;

        /// <summary>
        /// Security manager for user/permission operations.
        /// </summary>
        private readonly SecurityManager secMan;

        /// <summary>
        /// ERP service for dependency injection.
        /// </summary>
        private readonly IErpService erpService;

        /// <summary>
        /// Service for workflow routing and step determination.
        /// </summary>
        private readonly ApprovalRouteService routeService;

        /// <summary>
        /// Service for request lifecycle management.
        /// </summary>
        private readonly ApprovalRequestService requestService;

        /// <summary>
        /// Service for audit trail management.
        /// </summary>
        private readonly ApprovalHistoryService historyService;

        /// <summary>
        /// Service for real-time dashboard metrics calculation.
        /// </summary>
        private readonly DashboardMetricsService metricsService;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="ApprovalController"/> class.
        /// Sets up all required managers and services for approval workflow operations.
        /// </summary>
        /// <param name="erpService">The ERP service instance for dependency injection.</param>
        public ApprovalController(IErpService erpService)
        {
            recMan = new RecordManager();
            secMan = new SecurityManager();
            entMan = new EntityManager();
            relMan = new EntityRelationManager();
            this.erpService = erpService;

            // Initialize approval services
            routeService = new ApprovalRouteService();
            requestService = new ApprovalRequestService();
            historyService = new ApprovalHistoryService();
            metricsService = new DashboardMetricsService();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the current authenticated user's ID from the HTTP context claims.
        /// </summary>
        /// <returns>
        /// The user's GUID if authenticated and the claim exists, null otherwise.
        /// </returns>
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

        #region << Workflows >>

        /// <summary>
        /// Retrieves all approval workflows with optional filtering.
        /// </summary>
        /// <param name="isActive">Optional filter for active/inactive workflows.</param>
        /// <param name="entityName">Optional filter for workflows targeting a specific entity.</param>
        /// <returns>
        /// ActionResult containing ResponseModel with list of workflows on success,
        /// or error message on failure.
        /// </returns>
        [Route("api/v3.0/p/approval/workflows")]
        [HttpGet]
        public ActionResult GetWorkflows([FromQuery] bool? isActive = null, [FromQuery] string entityName = null)
        {
            var response = new ResponseModel();

            try
            {
                // Build EQL query with optional filters
                var whereConditions = new List<string>();
                var eqlParams = new List<EqlParameter>();

                if (isActive.HasValue)
                {
                    whereConditions.Add($"{FIELD_IS_ACTIVE} = @isActive");
                    eqlParams.Add(new EqlParameter("isActive", isActive.Value));
                }

                if (!string.IsNullOrWhiteSpace(entityName))
                {
                    whereConditions.Add($"{FIELD_ENTITY_NAME} = @entityName");
                    eqlParams.Add(new EqlParameter("entityName", entityName));
                }

                var whereClause = whereConditions.Any()
                    ? $"WHERE {string.Join(" AND ", whereConditions)}"
                    : string.Empty;

                var eqlCommand = $@"SELECT * FROM {ENTITY_WORKFLOW} 
                                    {whereClause} 
                                    ORDER BY {FIELD_NAME} ASC";

                var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

                response.Success = true;
                response.Message = $"Retrieved {eqlResult?.Count ?? 0} workflow(s).";
                response.Object = eqlResult ?? new EntityRecordList();
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error retrieving workflows: {ex.Message}";
                new Log().Create(LogType.Error, "ApprovalController", ex);
            }

            return Json(response);
        }

        /// <summary>
        /// Retrieves a specific workflow by ID with its associated steps.
        /// </summary>
        /// <param name="id">The unique identifier of the workflow to retrieve.</param>
        /// <returns>
        /// ActionResult containing ResponseModel with workflow details and steps on success,
        /// or error message on failure.
        /// </returns>
        [Route("api/v3.0/p/approval/workflows/{id}")]
        [HttpGet]
        public ActionResult GetWorkflow(Guid id)
        {
            var response = new ResponseModel();

            try
            {
                if (id == Guid.Empty)
                {
                    response.Success = false;
                    response.Message = "Invalid workflow ID provided.";
                    return Json(response);
                }

                // Get workflow record
                var workflowQuery = $"SELECT * FROM {ENTITY_WORKFLOW} WHERE {FIELD_ID} = @workflowId";
                var workflowParams = new List<EqlParameter> { new EqlParameter("workflowId", id) };
                var workflowResult = new EqlCommand(workflowQuery, workflowParams).Execute();

                if (workflowResult == null || !workflowResult.Any())
                {
                    response.Success = false;
                    response.Message = $"Workflow with ID {id} not found.";
                    return Json(response);
                }

                var workflow = workflowResult.First();

                // Get associated steps ordered by step_order
                var stepsQuery = $@"SELECT * FROM {ENTITY_STEP} 
                                    WHERE {FIELD_WORKFLOW_ID} = @workflowId 
                                    ORDER BY step_order ASC";
                var stepsParams = new List<EqlParameter> { new EqlParameter("workflowId", id) };
                var stepsResult = new EqlCommand(stepsQuery, stepsParams).Execute();

                // Add steps to workflow record
                workflow["steps"] = stepsResult ?? new EntityRecordList();

                // Get rules for each step
                if (stepsResult != null && stepsResult.Any())
                {
                    foreach (var step in stepsResult)
                    {
                        var stepId = (Guid)step[FIELD_ID];
                        var rulesQuery = $"SELECT * FROM {ENTITY_RULE} WHERE step_id = @stepId ORDER BY {FIELD_ID}";
                        var rulesParams = new List<EqlParameter> { new EqlParameter("stepId", stepId) };
                        var rulesResult = new EqlCommand(rulesQuery, rulesParams).Execute();
                        step["rules"] = rulesResult ?? new EntityRecordList();
                    }
                }

                response.Success = true;
                response.Message = "Workflow retrieved successfully.";
                response.Object = workflow;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error retrieving workflow: {ex.Message}";
                new Log().Create(LogType.Error, "ApprovalController", ex);
            }

            return Json(response);
        }

        /// <summary>
        /// Creates a new approval workflow.
        /// </summary>
        /// <param name="record">The EntityRecord containing workflow data with required fields: name, entity_name.</param>
        /// <returns>
        /// ActionResult containing ResponseModel with created workflow on success,
        /// or error message on failure.
        /// </returns>
        [Route("api/v3.0/p/approval/workflows")]
        [HttpPost]
        public ActionResult CreateWorkflow([FromBody] EntityRecord record)
        {
            var response = new ResponseModel();

            try
            {
                // Validate required fields
                if (record == null)
                {
                    response.Success = false;
                    response.Message = "Workflow data is required.";
                    return Json(response);
                }

                if (!record.Properties.ContainsKey(FIELD_NAME) || string.IsNullOrWhiteSpace(record[FIELD_NAME]?.ToString()))
                {
                    response.Success = false;
                    response.Message = "Workflow name is required.";
                    return Json(response);
                }

                if (!record.Properties.ContainsKey(FIELD_ENTITY_NAME) || string.IsNullOrWhiteSpace(record[FIELD_ENTITY_NAME]?.ToString()))
                {
                    response.Success = false;
                    response.Message = "Entity name is required.";
                    return Json(response);
                }

                // Validate current user
                if (!CurrentUserId.HasValue)
                {
                    response.Success = false;
                    response.Message = "User authentication required.";
                    return Json(response);
                }

                // Set system fields
                record[FIELD_ID] = Guid.NewGuid();
                record[FIELD_CREATED_ON] = DateTime.UtcNow;
                record[FIELD_CREATED_BY] = CurrentUserId.Value;
                record[FIELD_IS_ACTIVE] = record.Properties.ContainsKey(FIELD_IS_ACTIVE) 
                    ? record[FIELD_IS_ACTIVE] 
                    : true;

                // Create the workflow record
                var createResponse = recMan.CreateRecord(ENTITY_WORKFLOW, record);

                if (!createResponse.Success)
                {
                    response.Success = false;
                    response.Message = $"Failed to create workflow: {createResponse.Message}";
                    return Json(response);
                }

                response.Success = true;
                response.Message = "Workflow created successfully.";
                response.Object = record;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error creating workflow: {ex.Message}";
                new Log().Create(LogType.Error, "ApprovalController", ex);
            }

            return Json(response);
        }

        /// <summary>
        /// Updates an existing approval workflow.
        /// </summary>
        /// <param name="id">The unique identifier of the workflow to update.</param>
        /// <param name="record">The EntityRecord containing updated workflow data.</param>
        /// <returns>
        /// ActionResult containing ResponseModel with success status on success,
        /// or error message on failure.
        /// </returns>
        [Route("api/v3.0/p/approval/workflows/{id}")]
        [HttpPut]
        public ActionResult UpdateWorkflow(Guid id, [FromBody] EntityRecord record)
        {
            var response = new ResponseModel();

            try
            {
                if (id == Guid.Empty)
                {
                    response.Success = false;
                    response.Message = "Invalid workflow ID provided.";
                    return Json(response);
                }

                if (record == null)
                {
                    response.Success = false;
                    response.Message = "Workflow data is required.";
                    return Json(response);
                }

                // Verify workflow exists
                var existingQuery = $"SELECT * FROM {ENTITY_WORKFLOW} WHERE {FIELD_ID} = @workflowId";
                var existingParams = new List<EqlParameter> { new EqlParameter("workflowId", id) };
                var existingResult = new EqlCommand(existingQuery, existingParams).Execute();

                if (existingResult == null || !existingResult.Any())
                {
                    response.Success = false;
                    response.Message = $"Workflow with ID {id} not found.";
                    return Json(response);
                }

                // Ensure ID is set correctly for update
                record[FIELD_ID] = id;

                // Update the workflow record
                var updateResponse = recMan.UpdateRecord(ENTITY_WORKFLOW, record);

                if (!updateResponse.Success)
                {
                    response.Success = false;
                    response.Message = $"Failed to update workflow: {updateResponse.Message}";
                    return Json(response);
                }

                response.Success = true;
                response.Message = "Workflow updated successfully.";
                response.Object = record;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error updating workflow: {ex.Message}";
                new Log().Create(LogType.Error, "ApprovalController", ex);
            }

            return Json(response);
        }

        /// <summary>
        /// Deletes an approval workflow by ID.
        /// </summary>
        /// <param name="id">The unique identifier of the workflow to delete.</param>
        /// <returns>
        /// ActionResult containing ResponseModel with success status on success,
        /// or error message on failure.
        /// </returns>
        /// <remarks>
        /// Deletion is prevented if there are active approval requests referencing this workflow.
        /// </remarks>
        [Route("api/v3.0/p/approval/workflows/{id}")]
        [HttpDelete]
        public ActionResult DeleteWorkflow(Guid id)
        {
            var response = new ResponseModel();

            try
            {
                if (id == Guid.Empty)
                {
                    response.Success = false;
                    response.Message = "Invalid workflow ID provided.";
                    return Json(response);
                }

                // Verify workflow exists
                var existingQuery = $"SELECT * FROM {ENTITY_WORKFLOW} WHERE {FIELD_ID} = @workflowId";
                var existingParams = new List<EqlParameter> { new EqlParameter("workflowId", id) };
                var existingResult = new EqlCommand(existingQuery, existingParams).Execute();

                if (existingResult == null || !existingResult.Any())
                {
                    response.Success = false;
                    response.Message = $"Workflow with ID {id} not found.";
                    return Json(response);
                }

                // Check for active requests referencing this workflow
                var pendingStatus = ApprovalStatus.Pending.ToString().ToLowerInvariant();
                var activeRequestsQuery = $@"SELECT COUNT(*) as count FROM {ENTITY_REQUEST} 
                                             WHERE {FIELD_WORKFLOW_ID} = @workflowId 
                                             AND {FIELD_STATUS} = @status";
                var activeRequestsParams = new List<EqlParameter>
                {
                    new EqlParameter("workflowId", id),
                    new EqlParameter("status", pendingStatus)
                };
                var activeRequestsResult = new EqlCommand(activeRequestsQuery, activeRequestsParams).Execute();

                if (activeRequestsResult != null && activeRequestsResult.Any())
                {
                    var count = activeRequestsResult.First()["count"];
                    if (count != null && Convert.ToInt64(count) > 0)
                    {
                        response.Success = false;
                        response.Message = "Cannot delete workflow with active approval requests. Please complete or cancel pending requests first.";
                        return Json(response);
                    }
                }

                using (var connection = DbContext.Current.CreateConnection())
                {
                    try
                    {
                        connection.BeginTransaction();

                        // Delete associated rules for all steps
                        var stepsQuery = $"SELECT {FIELD_ID} FROM {ENTITY_STEP} WHERE {FIELD_WORKFLOW_ID} = @workflowId";
                        var stepsParams = new List<EqlParameter> { new EqlParameter("workflowId", id) };
                        var stepsResult = new EqlCommand(stepsQuery, stepsParams).Execute();

                        if (stepsResult != null && stepsResult.Any())
                        {
                            foreach (var step in stepsResult)
                            {
                                var stepId = (Guid)step[FIELD_ID];

                                // Delete rules for this step
                                var rulesQuery = $"SELECT {FIELD_ID} FROM {ENTITY_RULE} WHERE step_id = @stepId";
                                var rulesParams = new List<EqlParameter> { new EqlParameter("stepId", stepId) };
                                var rulesResult = new EqlCommand(rulesQuery, rulesParams).Execute();

                                if (rulesResult != null)
                                {
                                    foreach (var rule in rulesResult)
                                    {
                                        recMan.DeleteRecord(ENTITY_RULE, (Guid)rule[FIELD_ID]);
                                    }
                                }

                                // Delete the step
                                recMan.DeleteRecord(ENTITY_STEP, stepId);
                            }
                        }

                        // Delete the workflow
                        var deleteResponse = recMan.DeleteRecord(ENTITY_WORKFLOW, id);

                        if (!deleteResponse.Success)
                        {
                            connection.RollbackTransaction();
                            response.Success = false;
                            response.Message = $"Failed to delete workflow: {deleteResponse.Message}";
                            return Json(response);
                        }

                        connection.CommitTransaction();

                        response.Success = true;
                        response.Message = "Workflow and associated steps deleted successfully.";
                    }
                    catch
                    {
                        connection.RollbackTransaction();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error deleting workflow: {ex.Message}";
                new Log().Create(LogType.Error, "ApprovalController", ex);
            }

            return Json(response);
        }

        #endregion

        #region << Requests >>

        /// <summary>
        /// Retrieves approval requests with optional filtering by status and workflow ID.
        /// </summary>
        /// <param name="status">Optional filter for request status (pending, approved, rejected, etc.).</param>
        /// <param name="workflowId">Optional filter for requests of a specific workflow.</param>
        /// <param name="page">Page number for pagination (1-based). Defaults to 1.</param>
        /// <param name="pageSize">Number of records per page. Defaults to 20, max 100.</param>
        /// <returns>
        /// ActionResult containing ResponseModel with list of requests on success,
        /// or error message on failure.
        /// </returns>
        [Route("api/v3.0/p/approval/requests")]
        [HttpGet]
        public ActionResult GetRequests(
            [FromQuery] string status = null,
            [FromQuery] Guid? workflowId = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = DEFAULT_PAGE_SIZE)
        {
            var response = new ResponseModel();

            try
            {
                // Validate pagination parameters
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = DEFAULT_PAGE_SIZE;
                if (pageSize > MAX_PAGE_SIZE) pageSize = MAX_PAGE_SIZE;

                // Build EQL query with optional filters
                var whereConditions = new List<string>();
                var eqlParams = new List<EqlParameter>();

                if (!string.IsNullOrWhiteSpace(status))
                {
                    whereConditions.Add($"{FIELD_STATUS} = @status");
                    eqlParams.Add(new EqlParameter("status", status.ToLowerInvariant()));
                }

                if (workflowId.HasValue && workflowId.Value != Guid.Empty)
                {
                    whereConditions.Add($"{FIELD_WORKFLOW_ID} = @workflowId");
                    eqlParams.Add(new EqlParameter("workflowId", workflowId.Value));
                }

                var whereClause = whereConditions.Any()
                    ? $"WHERE {string.Join(" AND ", whereConditions)}"
                    : string.Empty;

                var eqlCommand = $@"SELECT * FROM {ENTITY_REQUEST} 
                                    {whereClause} 
                                    ORDER BY {FIELD_CREATED_ON} DESC 
                                    PAGE {page} PAGESIZE {pageSize}";

                var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

                response.Success = true;
                response.Message = $"Retrieved {eqlResult?.Count ?? 0} request(s).";
                response.Object = new
                {
                    records = eqlResult ?? new EntityRecordList(),
                    page = page,
                    pageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error retrieving requests: {ex.Message}";
                new Log().Create(LogType.Error, "ApprovalController", ex);
            }

            return Json(response);
        }

        /// <summary>
        /// Retrieves a specific approval request by ID with related workflow and step details.
        /// </summary>
        /// <param name="id">The unique identifier of the request to retrieve.</param>
        /// <returns>
        /// ActionResult containing ResponseModel with request details on success,
        /// or error message on failure.
        /// </returns>
        [Route("api/v3.0/p/approval/requests/{id}")]
        [HttpGet]
        public ActionResult GetRequest(Guid id)
        {
            var response = new ResponseModel();

            try
            {
                if (id == Guid.Empty)
                {
                    response.Success = false;
                    response.Message = "Invalid request ID provided.";
                    return Json(response);
                }

                var request = requestService.GetRequest(id);

                if (request == null)
                {
                    response.Success = false;
                    response.Message = $"Approval request with ID {id} not found.";
                    return Json(response);
                }

                // Enrich with workflow details
                var workflowId = request[FIELD_WORKFLOW_ID] as Guid?;
                if (workflowId.HasValue)
                {
                    var workflowQuery = $"SELECT {FIELD_NAME}, {FIELD_ENTITY_NAME} FROM {ENTITY_WORKFLOW} WHERE {FIELD_ID} = @workflowId";
                    var workflowParams = new List<EqlParameter> { new EqlParameter("workflowId", workflowId.Value) };
                    var workflowResult = new EqlCommand(workflowQuery, workflowParams).Execute();

                    if (workflowResult != null && workflowResult.Any())
                    {
                        request["workflow_name"] = workflowResult.First()[FIELD_NAME];
                        request["workflow_entity_name"] = workflowResult.First()[FIELD_ENTITY_NAME];
                    }
                }

                // Enrich with current step details
                var currentStepId = request["current_step_id"] as Guid?;
                if (currentStepId.HasValue)
                {
                    var stepQuery = $"SELECT {FIELD_NAME}, step_order, approver_type FROM {ENTITY_STEP} WHERE {FIELD_ID} = @stepId";
                    var stepParams = new List<EqlParameter> { new EqlParameter("stepId", currentStepId.Value) };
                    var stepResult = new EqlCommand(stepQuery, stepParams).Execute();

                    if (stepResult != null && stepResult.Any())
                    {
                        request["current_step_name"] = stepResult.First()[FIELD_NAME];
                        request["current_step_order"] = stepResult.First()["step_order"];
                        request["current_step_approver_type"] = stepResult.First()["approver_type"];
                    }
                }

                response.Success = true;
                response.Message = "Request retrieved successfully.";
                response.Object = request;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error retrieving request: {ex.Message}";
                new Log().Create(LogType.Error, "ApprovalController", ex);
            }

            return Json(response);
        }

        /// <summary>
        /// Approves an approval request at the current step.
        /// If this is the final step, the request status becomes 'approved'.
        /// If more steps remain, the request advances to the next step.
        /// </summary>
        /// <param name="id">The unique identifier of the request to approve.</param>
        /// <param name="record">The EntityRecord containing optional comments.</param>
        /// <returns>
        /// ActionResult containing ResponseModel with updated request on success,
        /// or error message on failure.
        /// </returns>
        [Route("api/v3.0/p/approval/requests/{id}/approve")]
        [HttpPost]
        public ActionResult ApproveRequest(Guid id, [FromBody] EntityRecord record)
        {
            var response = new ResponseModel();

            try
            {
                if (id == Guid.Empty)
                {
                    response.Success = false;
                    response.Message = "Invalid request ID provided.";
                    return Json(response);
                }

                if (!CurrentUserId.HasValue)
                {
                    response.Success = false;
                    response.Message = "User authentication required.";
                    return Json(response);
                }

                // Validate request exists and is pending
                var existingRequest = requestService.GetRequest(id);
                if (existingRequest == null)
                {
                    response.Success = false;
                    response.Message = $"Approval request with ID {id} not found.";
                    return Json(response);
                }

                var currentStatus = existingRequest[FIELD_STATUS] as string;
                var pendingStatus = ApprovalStatus.Pending.ToString().ToLowerInvariant();

                if (!string.Equals(currentStatus, pendingStatus, StringComparison.OrdinalIgnoreCase))
                {
                    response.Success = false;
                    response.Message = $"Cannot approve request with status '{currentStatus}'. Request must be in 'pending' status.";
                    return Json(response);
                }

                // Extract comments from request body
                string comments = null;
                if (record != null && record.Properties.ContainsKey(FIELD_COMMENTS))
                {
                    comments = record[FIELD_COMMENTS]?.ToString();
                }

                // Perform approval via service
                var updatedRequest = requestService.ApproveRequest(id, CurrentUserId.Value, comments);

                response.Success = true;
                response.Message = "Request approved successfully.";
                response.Object = updatedRequest;
            }
            catch (UnauthorizedAccessException ex)
            {
                response.Success = false;
                response.Message = ex.Message;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error approving request: {ex.Message}";
                new Log().Create(LogType.Error, "ApprovalController", ex);
            }

            return Json(response);
        }

        /// <summary>
        /// Rejects an approval request. The request status becomes 'rejected'.
        /// </summary>
        /// <param name="id">The unique identifier of the request to reject.</param>
        /// <param name="record">The EntityRecord containing required comments explaining the rejection reason.</param>
        /// <returns>
        /// ActionResult containing ResponseModel with updated request on success,
        /// or error message on failure.
        /// </returns>
        [Route("api/v3.0/p/approval/requests/{id}/reject")]
        [HttpPost]
        public ActionResult RejectRequest(Guid id, [FromBody] EntityRecord record)
        {
            var response = new ResponseModel();

            try
            {
                if (id == Guid.Empty)
                {
                    response.Success = false;
                    response.Message = "Invalid request ID provided.";
                    return Json(response);
                }

                if (!CurrentUserId.HasValue)
                {
                    response.Success = false;
                    response.Message = "User authentication required.";
                    return Json(response);
                }

                // Validate request exists and is pending
                var existingRequest = requestService.GetRequest(id);
                if (existingRequest == null)
                {
                    response.Success = false;
                    response.Message = $"Approval request with ID {id} not found.";
                    return Json(response);
                }

                var currentStatus = existingRequest[FIELD_STATUS] as string;
                var pendingStatus = ApprovalStatus.Pending.ToString().ToLowerInvariant();

                if (!string.Equals(currentStatus, pendingStatus, StringComparison.OrdinalIgnoreCase))
                {
                    response.Success = false;
                    response.Message = $"Cannot reject request with status '{currentStatus}'. Request must be in 'pending' status.";
                    return Json(response);
                }

                // Extract comments from request body - required for rejection
                string comments = null;
                if (record != null && record.Properties.ContainsKey(FIELD_COMMENTS))
                {
                    comments = record[FIELD_COMMENTS]?.ToString();
                }

                if (string.IsNullOrWhiteSpace(comments))
                {
                    response.Success = false;
                    response.Message = "Comments are required when rejecting a request. Please provide a reason for rejection.";
                    return Json(response);
                }

                // Perform rejection via service
                var updatedRequest = requestService.RejectRequest(id, CurrentUserId.Value, comments);

                response.Success = true;
                response.Message = "Request rejected successfully.";
                response.Object = updatedRequest;
            }
            catch (UnauthorizedAccessException ex)
            {
                response.Success = false;
                response.Message = ex.Message;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error rejecting request: {ex.Message}";
                new Log().Create(LogType.Error, "ApprovalController", ex);
            }

            return Json(response);
        }

        /// <summary>
        /// Delegates an approval request to another user.
        /// </summary>
        /// <param name="id">The unique identifier of the request to delegate.</param>
        /// <param name="record">The EntityRecord containing delegateToUserId (required) and optional comments.</param>
        /// <returns>
        /// ActionResult containing ResponseModel with updated request on success,
        /// or error message on failure.
        /// </returns>
        [Route("api/v3.0/p/approval/requests/{id}/delegate")]
        [HttpPost]
        public ActionResult DelegateRequest(Guid id, [FromBody] EntityRecord record)
        {
            var response = new ResponseModel();

            try
            {
                if (id == Guid.Empty)
                {
                    response.Success = false;
                    response.Message = "Invalid request ID provided.";
                    return Json(response);
                }

                if (!CurrentUserId.HasValue)
                {
                    response.Success = false;
                    response.Message = "User authentication required.";
                    return Json(response);
                }

                if (record == null)
                {
                    response.Success = false;
                    response.Message = "Request body is required.";
                    return Json(response);
                }

                // Validate request exists and is pending
                var existingRequest = requestService.GetRequest(id);
                if (existingRequest == null)
                {
                    response.Success = false;
                    response.Message = $"Approval request with ID {id} not found.";
                    return Json(response);
                }

                var currentStatus = existingRequest[FIELD_STATUS] as string;
                var pendingStatus = ApprovalStatus.Pending.ToString().ToLowerInvariant();

                if (!string.Equals(currentStatus, pendingStatus, StringComparison.OrdinalIgnoreCase))
                {
                    response.Success = false;
                    response.Message = $"Cannot delegate request with status '{currentStatus}'. Request must be in 'pending' status.";
                    return Json(response);
                }

                // Extract delegateToUserId from request body - required for delegation
                Guid delegateToUserId = Guid.Empty;
                if (record.Properties.ContainsKey(FIELD_DELEGATE_TO) && record[FIELD_DELEGATE_TO] != null)
                {
                    var delegateValue = record[FIELD_DELEGATE_TO];
                    if (delegateValue is Guid guidValue)
                    {
                        delegateToUserId = guidValue;
                    }
                    else if (Guid.TryParse(delegateValue.ToString(), out Guid parsedGuid))
                    {
                        delegateToUserId = parsedGuid;
                    }
                }

                if (delegateToUserId == Guid.Empty)
                {
                    response.Success = false;
                    response.Message = "Valid delegateToUserId is required for delegation.";
                    return Json(response);
                }

                // Validate delegate user exists
                var userResult = secMan.GetUser(delegateToUserId);
                if (userResult == null)
                {
                    response.Success = false;
                    response.Message = $"User with ID {delegateToUserId} not found. Cannot delegate to non-existent user.";
                    return Json(response);
                }

                // Cannot delegate to self
                if (delegateToUserId == CurrentUserId.Value)
                {
                    response.Success = false;
                    response.Message = "Cannot delegate request to yourself.";
                    return Json(response);
                }

                // Extract comments from request body
                string comments = null;
                if (record.Properties.ContainsKey(FIELD_COMMENTS))
                {
                    comments = record[FIELD_COMMENTS]?.ToString();
                }

                // Perform delegation via service
                var updatedRequest = requestService.DelegateRequest(id, CurrentUserId.Value, delegateToUserId, comments);

                response.Success = true;
                response.Message = $"Request delegated successfully to user {delegateToUserId}.";
                response.Object = updatedRequest;
            }
            catch (UnauthorizedAccessException ex)
            {
                response.Success = false;
                response.Message = ex.Message;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error delegating request: {ex.Message}";
                new Log().Create(LogType.Error, "ApprovalController", ex);
            }

            return Json(response);
        }

        #endregion

        #region << History >>

        /// <summary>
        /// Retrieves the complete history of actions for a specific approval request.
        /// Returns all audit trail entries ordered by performed date descending.
        /// </summary>
        /// <param name="id">The unique identifier of the request to get history for.</param>
        /// <returns>
        /// ActionResult containing ResponseModel with list of history records on success,
        /// or error message on failure.
        /// </returns>
        [Route("api/v3.0/p/approval/requests/{id}/history")]
        [HttpGet]
        public ActionResult GetRequestHistory(Guid id)
        {
            var response = new ResponseModel();

            try
            {
                if (id == Guid.Empty)
                {
                    response.Success = false;
                    response.Message = "Invalid request ID provided.";
                    return Json(response);
                }

                // Verify request exists
                var existingRequest = requestService.GetRequest(id);
                if (existingRequest == null)
                {
                    response.Success = false;
                    response.Message = $"Approval request with ID {id} not found.";
                    return Json(response);
                }

                // Get history via service
                var historyRecords = historyService.GetRequestHistory(id);

                response.Success = true;
                response.Message = $"Retrieved {historyRecords?.Count ?? 0} history record(s).";
                response.Object = historyRecords ?? new EntityRecordList();
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error retrieving request history: {ex.Message}";
                new Log().Create(LogType.Error, "ApprovalController", ex);
            }

            return Json(response);
        }

        #endregion

        #region << Dashboard >>

        /// <summary>
        /// Retrieves real-time dashboard metrics for approval workflow monitoring.
        /// </summary>
        /// <param name="fromDate">Optional start date for time-based metrics. Defaults to 30 days ago.</param>
        /// <param name="toDate">Optional end date for time-based metrics. Defaults to current date.</param>
        /// <returns>
        /// ActionResult containing ResponseModel with DashboardMetricsModel on success,
        /// or error message on failure.
        /// </returns>
        [Route("api/v3.0/p/approval/dashboard/metrics")]
        [HttpGet]
        public ActionResult GetDashboardMetrics(
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            var response = new ResponseModel();

            try
            {
                if (!CurrentUserId.HasValue)
                {
                    response.Success = false;
                    response.Message = "User authentication required.";
                    return Json(response);
                }

                // Set default date range if not provided (last 30 days)
                var effectiveToDate = toDate ?? DateTime.UtcNow;
                var effectiveFromDate = fromDate ?? effectiveToDate.AddDays(-DEFAULT_DASHBOARD_DAYS);

                // Validate date range
                if (effectiveFromDate > effectiveToDate)
                {
                    response.Success = false;
                    response.Message = "From date cannot be greater than to date.";
                    return Json(response);
                }

                // Get metrics via service
                var metrics = metricsService.GetDashboardMetrics(CurrentUserId.Value, effectiveFromDate, effectiveToDate);

                response.Success = true;
                response.Message = "Dashboard metrics retrieved successfully.";
                response.Object = metrics;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error retrieving dashboard metrics: {ex.Message}";
                new Log().Create(LogType.Error, "ApprovalController", ex);
            }

            return Json(response);
        }

        #endregion
    }
}
