# STORY-007: Approval REST API Endpoints

## Description

Implement the REST API layer for the WebVella ERP Approval Workflow system. This story creates the `ApprovalController` class that exposes HTTP endpoints for all approval workflow operations, enabling external integrations and UI consumption through a standardized RESTful interface.

The API layer provides:

- **Workflow Management Endpoints**: Full CRUD operations for approval workflow definitions, allowing administrators to create, retrieve, update, and delete workflows via REST calls. These endpoints support the admin configuration UI (STORY-008) and enable external system integrations.
- **Request Action Endpoints**: Endpoints for processing approval decisions including approve, reject, and delegate operations. Each endpoint validates the current user's authorization before delegating to the service layer.
- **Query Endpoints**: Endpoints for retrieving pending approvals for the current user, workflow details with related steps and rules, and request history for audit purposes.

The controller follows the established WebVella ERP API patterns:

- Uses `[Authorize]` attribute for authentication enforcement
- Returns `ResponseModel` envelope for all responses (containing success, message, and object properties)
- Delegates all business logic to the service layer (STORY-004)
- Uses `SecurityContext.CurrentUser` for authentication context
- Follows the route pattern `/api/v3.0/p/approval/{resource}/{action}`

All endpoints support both JSON request/response bodies and adhere to RESTful conventions for HTTP verbs (GET for queries, POST for mutations). The API is designed to be consumed by the approval UI components (STORY-008) and external systems requiring approval workflow automation.

## Business Value

- **External Integration Capability**: REST API enables external systems (ERP integrations, mobile apps, third-party tools) to interact with the approval workflow programmatically without direct database access or UI dependency.
- **UI Consumption**: Provides the data layer for the approval UI components (STORY-008), enabling dynamic workflow configuration, request processing, and history display through AJAX calls.
- **Standardized API Contracts**: Consistent ResponseModel envelope pattern simplifies client-side error handling and response parsing across all consuming applications.
- **Security Enforcement**: Centralized `[Authorize]` attribute ensures all API access requires authentication, preventing unauthorized workflow manipulation.
- **Separation of Concerns**: API layer decouples presentation from business logic, allowing independent evolution of UI components and enabling API versioning for backward compatibility.
- **Audit and Compliance**: All actions flow through defined endpoints with consistent logging patterns, supporting compliance requirements for approval workflow traceability.
- **Developer Experience**: Well-documented REST endpoints with predictable patterns reduce integration time for both internal and external development teams.

## Acceptance Criteria

### Workflow Management Endpoints
- [ ] **AC1**: `GET /api/v3.0/p/approval/workflow` returns a list of all approval workflows with optional query parameter `?entityName={name}` to filter by target entity, returning `ResponseModel` with `Object` containing workflow array
- [ ] **AC2**: `GET /api/v3.0/p/approval/workflow/{id}` returns a single workflow by ID with optional `?includeStepsAndRules=true` parameter that includes related `approval_step` and `approval_rule` records in the response
- [ ] **AC3**: `POST /api/v3.0/p/approval/workflow` creates a new workflow from JSON body `{name, targetEntity}`, validates admin permission, returns created workflow record with generated ID
- [ ] **AC4**: `PUT /api/v3.0/p/approval/workflow/{id}` updates an existing workflow with JSON body containing updatable fields, validates admin permission, returns updated workflow record
- [ ] **AC5**: `DELETE /api/v3.0/p/approval/workflow/{id}` deletes a workflow after validating no pending requests exist, returns success/failure response with appropriate message

### Request Action Endpoints
- [ ] **AC6**: `POST /api/v3.0/p/approval/request/{id}/approve` processes approval action with JSON body `{comments}`, validates current user is authorized approver for current step, advances workflow, and logs action to history
- [ ] **AC7**: `POST /api/v3.0/p/approval/request/{id}/reject` processes rejection action with JSON body `{comments}`, validates current user is authorized approver, transitions request to rejected status, and logs action
- [ ] **AC8**: `POST /api/v3.0/p/approval/request/{id}/delegate` delegates approval to another user with JSON body `{delegateToUserId, comments}`, validates delegator authorization, reassigns approver, and logs delegation action

### Query Endpoints
- [ ] **AC9**: `GET /api/v3.0/p/approval/pending` returns all approval requests pending action by the current user, with optional pagination via `?page={n}&pageSize={n}` query parameters
- [ ] **AC10**: `GET /api/v3.0/p/approval/request/{id}` returns full details of an approval request including current status, workflow information, and history records

### Cross-Cutting Concerns
- [ ] **AC11**: All endpoints require authentication via `[Authorize]` attribute and return HTTP 401 for unauthenticated requests
- [ ] **AC12**: All endpoints return `ResponseModel` with consistent structure: `{success: bool, message: string, object: any, errors: array}`
- [ ] **AC13**: All mutation endpoints (POST, PUT, DELETE) validate appropriate permissions before executing operations and return HTTP 403 with descriptive error message for permission failures

## Technical Implementation Details

### Files/Modules to Create

| File Path | Description |
|-----------|-------------|
| `WebVella.Erp.Plugins.Approval/Controllers/ApprovalController.cs` | Main REST API controller handling all approval endpoints |
| `WebVella.Erp.Plugins.Approval/Api/ApproveRequestModel.cs` | DTO for approve/reject request body |
| `WebVella.Erp.Plugins.Approval/Api/DelegateRequestModel.cs` | DTO for delegation request body |
| `WebVella.Erp.Plugins.Approval/Api/CreateWorkflowModel.cs` | DTO for workflow creation request body |
| `WebVella.Erp.Plugins.Approval/Api/UpdateWorkflowModel.cs` | DTO for workflow update request body |

### Key Classes and Functions

#### ApprovalController.cs

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Database;
using WebVella.Erp.Plugins.Approval.Services;
using WebVella.Erp.Plugins.Approval.Api;

namespace WebVella.Erp.Plugins.Approval.Controllers
{
    /// <summary>
    /// REST API controller for approval workflow operations.
    /// Provides endpoints for workflow management, request processing, and queries.
    /// All endpoints require authentication via [Authorize] attribute.
    /// </summary>
    [Authorize]
    public class ApprovalController : Controller
    {
        private const string ENTITY_APPROVAL_WORKFLOW = "approval_workflow";
        private const string ENTITY_APPROVAL_REQUEST = "approval_request";
        
        RecordManager recMan;
        EntityManager entMan;
        EntityRelationManager relMan;
        SecurityManager secMan;
        IErpService erpService;

        /// <summary>
        /// Constructor with dependency injection of IErpService
        /// </summary>
        /// <param name="erpService">ERP service instance</param>
        public ApprovalController(IErpService erpService)
        {
            recMan = new RecordManager();
            secMan = new SecurityManager();
            entMan = new EntityManager();
            relMan = new EntityRelationManager();
            this.erpService = erpService;
        }

        /// <summary>
        /// Gets the current authenticated user's ID from claims
        /// </summary>
        public Guid? CurrentUserId
        {
            get
            {
                if (HttpContext != null && HttpContext.User != null && HttpContext.User.Claims != null)
                {
                    var nameIdentifier = HttpContext.User.Claims.FirstOrDefault(
                        x => x.Type == ClaimTypes.NameIdentifier);
                    if (nameIdentifier is null)
                        return null;

                    return new Guid(nameIdentifier.Value);
                }
                return null;
            }
        }

        #region << Workflow Management Endpoints >>
        
        /// <summary>
        /// Gets all workflows or filters by target entity name
        /// </summary>
        /// <param name="entityName">Optional entity name filter</param>
        /// <returns>List of workflow records</returns>
        [Route("api/v3.0/p/approval/workflow")]
        [HttpGet]
        public ActionResult GetWorkflows([FromQuery] string entityName = null)
        {
            var response = new ResponseModel();
            try
            {
                var workflowService = new ApprovalWorkflowService();
                
                if (!string.IsNullOrEmpty(entityName))
                {
                    response.Object = workflowService.GetWorkflowsForEntity(entityName);
                }
                else
                {
                    response.Object = workflowService.GetAllWorkflows();
                }
                
                response.Success = true;
                response.Message = "Workflows retrieved successfully";
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message;
            }
            
            return Json(response);
        }

        /// <summary>
        /// Gets a single workflow by ID with optional relation expansion
        /// </summary>
        /// <param name="id">Workflow GUID</param>
        /// <param name="includeStepsAndRules">Include related steps and rules</param>
        /// <returns>Workflow record with optional relations</returns>
        [Route("api/v3.0/p/approval/workflow/{id}")]
        [HttpGet]
        public ActionResult GetWorkflow(Guid id, [FromQuery] bool includeStepsAndRules = false)
        {
            var response = new ResponseModel();
            try
            {
                var workflowService = new ApprovalWorkflowService();
                var workflow = workflowService.GetWorkflow(id, includeStepsAndRules);
                
                if (workflow == null)
                {
                    response.Success = false;
                    response.Message = $"Workflow {id} not found";
                    return Json(response);
                }
                
                response.Object = workflow;
                response.Success = true;
                response.Message = "Workflow retrieved successfully";
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message;
            }
            
            return Json(response);
        }

        /// <summary>
        /// Creates a new approval workflow
        /// </summary>
        /// <param name="model">Workflow creation data</param>
        /// <returns>Created workflow record</returns>
        [Route("api/v3.0/p/approval/workflow")]
        [HttpPost]
        public ActionResult CreateWorkflow([FromBody] CreateWorkflowModel model)
        {
            var response = new ResponseModel();
            
            if (model == null)
            {
                response.Success = false;
                response.Message = "Request body is required";
                return Json(response);
            }
            
            if (string.IsNullOrEmpty(model.Name))
            {
                response.Success = false;
                response.Message = "Workflow name is required";
                return Json(response);
            }
            
            if (string.IsNullOrEmpty(model.TargetEntity))
            {
                response.Success = false;
                response.Message = "Target entity is required";
                return Json(response);
            }
            
            try
            {
                var currentUserId = CurrentUserId;
                if (!currentUserId.HasValue)
                {
                    response.Success = false;
                    response.Message = "User authentication required";
                    return Json(response);
                }
                
                var workflowService = new ApprovalWorkflowService();
                var workflow = workflowService.CreateWorkflow(
                    model.Name, 
                    model.TargetEntity, 
                    currentUserId.Value);
                
                response.Object = workflow;
                response.Success = true;
                response.Message = "Workflow created successfully";
            }
            catch (UnauthorizedAccessException ex)
            {
                response.Success = false;
                response.Message = ex.Message;
                Response.StatusCode = 403;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message;
            }
            
            return Json(response);
        }

        /// <summary>
        /// Updates an existing approval workflow
        /// </summary>
        /// <param name="id">Workflow ID</param>
        /// <param name="model">Fields to update</param>
        /// <returns>Updated workflow record</returns>
        [Route("api/v3.0/p/approval/workflow/{id}")]
        [HttpPut]
        public ActionResult UpdateWorkflow(Guid id, [FromBody] UpdateWorkflowModel model)
        {
            var response = new ResponseModel();
            
            if (model == null)
            {
                response.Success = false;
                response.Message = "Request body is required";
                return Json(response);
            }
            
            try
            {
                var workflowService = new ApprovalWorkflowService();
                
                var updateRecord = new EntityRecord();
                if (!string.IsNullOrEmpty(model.Name))
                    updateRecord["name"] = model.Name;
                if (!string.IsNullOrEmpty(model.TargetEntity))
                    updateRecord["target_entity"] = model.TargetEntity;
                if (model.IsEnabled.HasValue)
                    updateRecord["is_enabled"] = model.IsEnabled.Value;
                
                var workflow = workflowService.UpdateWorkflow(id, updateRecord);
                
                response.Object = workflow;
                response.Success = true;
                response.Message = "Workflow updated successfully";
            }
            catch (UnauthorizedAccessException ex)
            {
                response.Success = false;
                response.Message = ex.Message;
                Response.StatusCode = 403;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message;
            }
            
            return Json(response);
        }

        /// <summary>
        /// Deletes an approval workflow
        /// </summary>
        /// <param name="id">Workflow ID to delete</param>
        /// <returns>Success/failure response</returns>
        [Route("api/v3.0/p/approval/workflow/{id}")]
        [HttpDelete]
        public ActionResult DeleteWorkflow(Guid id)
        {
            var response = new ResponseModel();
            
            try
            {
                var workflowService = new ApprovalWorkflowService();
                var deleted = workflowService.DeleteWorkflow(id);
                
                response.Success = deleted;
                response.Message = deleted 
                    ? "Workflow deleted successfully" 
                    : "Failed to delete workflow";
            }
            catch (UnauthorizedAccessException ex)
            {
                response.Success = false;
                response.Message = ex.Message;
                Response.StatusCode = 403;
            }
            catch (InvalidOperationException ex)
            {
                response.Success = false;
                response.Message = ex.Message;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message;
            }
            
            return Json(response);
        }
        
        #endregion

        #region << Request Action Endpoints >>
        
        /// <summary>
        /// Approves an approval request
        /// </summary>
        /// <param name="id">Request ID</param>
        /// <param name="model">Approval details including comments</param>
        /// <returns>Updated request record</returns>
        [Route("api/v3.0/p/approval/request/{id}/approve")]
        [HttpPost]
        public ActionResult ApproveRequest(Guid id, [FromBody] ApproveRequestModel model)
        {
            var response = new ResponseModel();
            
            try
            {
                var currentUserId = CurrentUserId;
                if (!currentUserId.HasValue)
                {
                    response.Success = false;
                    response.Message = "User authentication required";
                    return Json(response);
                }
                
                var comments = model?.Comments ?? string.Empty;
                
                var requestService = new ApprovalRequestService();
                var request = requestService.ApproveRequest(
                    id, 
                    currentUserId.Value, 
                    comments);
                
                response.Object = request;
                response.Success = true;
                response.Message = "Request approved successfully";
            }
            catch (UnauthorizedAccessException ex)
            {
                response.Success = false;
                response.Message = ex.Message;
                Response.StatusCode = 403;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message;
            }
            
            return Json(response);
        }

        /// <summary>
        /// Rejects an approval request
        /// </summary>
        /// <param name="id">Request ID</param>
        /// <param name="model">Rejection details including comments</param>
        /// <returns>Updated request record</returns>
        [Route("api/v3.0/p/approval/request/{id}/reject")]
        [HttpPost]
        public ActionResult RejectRequest(Guid id, [FromBody] ApproveRequestModel model)
        {
            var response = new ResponseModel();
            
            try
            {
                var currentUserId = CurrentUserId;
                if (!currentUserId.HasValue)
                {
                    response.Success = false;
                    response.Message = "User authentication required";
                    return Json(response);
                }
                
                var comments = model?.Comments ?? string.Empty;
                
                var requestService = new ApprovalRequestService();
                var request = requestService.RejectRequest(
                    id, 
                    currentUserId.Value, 
                    comments);
                
                response.Object = request;
                response.Success = true;
                response.Message = "Request rejected successfully";
            }
            catch (UnauthorizedAccessException ex)
            {
                response.Success = false;
                response.Message = ex.Message;
                Response.StatusCode = 403;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message;
            }
            
            return Json(response);
        }

        /// <summary>
        /// Delegates an approval request to another user
        /// </summary>
        /// <param name="id">Request ID</param>
        /// <param name="model">Delegation details including target user and comments</param>
        /// <returns>Updated request record</returns>
        [Route("api/v3.0/p/approval/request/{id}/delegate")]
        [HttpPost]
        public ActionResult DelegateRequest(Guid id, [FromBody] DelegateRequestModel model)
        {
            var response = new ResponseModel();
            
            if (model == null)
            {
                response.Success = false;
                response.Message = "Request body is required";
                return Json(response);
            }
            
            if (model.DelegateToUserId == Guid.Empty)
            {
                response.Success = false;
                response.Message = "DelegateToUserId is required";
                return Json(response);
            }
            
            try
            {
                var currentUserId = CurrentUserId;
                if (!currentUserId.HasValue)
                {
                    response.Success = false;
                    response.Message = "User authentication required";
                    return Json(response);
                }
                
                var comments = model.Comments ?? string.Empty;
                
                var requestService = new ApprovalRequestService();
                var request = requestService.DelegateRequest(
                    id, 
                    currentUserId.Value, 
                    model.DelegateToUserId, 
                    comments);
                
                response.Object = request;
                response.Success = true;
                response.Message = "Request delegated successfully";
            }
            catch (UnauthorizedAccessException ex)
            {
                response.Success = false;
                response.Message = ex.Message;
                Response.StatusCode = 403;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message;
            }
            
            return Json(response);
        }
        
        #endregion

        #region << Query Endpoints >>
        
        /// <summary>
        /// Gets approval requests pending action by the current user
        /// </summary>
        /// <param name="page">Page number (1-based)</param>
        /// <param name="pageSize">Items per page</param>
        /// <returns>List of pending approval requests</returns>
        [Route("api/v3.0/p/approval/pending")]
        [HttpGet]
        public ActionResult GetPendingApprovals(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 20)
        {
            var response = new ResponseModel();
            
            try
            {
                var currentUserId = CurrentUserId;
                if (!currentUserId.HasValue)
                {
                    response.Success = false;
                    response.Message = "User authentication required";
                    return Json(response);
                }
                
                var requestService = new ApprovalRequestService();
                var pendingRequests = requestService.GetPendingRequestsForUser(
                    currentUserId.Value, 
                    page, 
                    pageSize);
                
                response.Object = pendingRequests;
                response.Success = true;
                response.Message = "Pending approvals retrieved successfully";
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message;
            }
            
            return Json(response);
        }

        /// <summary>
        /// Gets full details of an approval request including history
        /// </summary>
        /// <param name="id">Request ID</param>
        /// <returns>Request record with history</returns>
        [Route("api/v3.0/p/approval/request/{id}")]
        [HttpGet]
        public ActionResult GetRequest(Guid id)
        {
            var response = new ResponseModel();
            
            try
            {
                var requestService = new ApprovalRequestService();
                var request = requestService.GetRequest(id, includeHistory: true);
                
                if (request == null)
                {
                    response.Success = false;
                    response.Message = $"Request {id} not found";
                    return Json(response);
                }
                
                response.Object = request;
                response.Success = true;
                response.Message = "Request retrieved successfully";
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message;
            }
            
            return Json(response);
        }

        /// <summary>
        /// Gets approval history for a specific request
        /// </summary>
        /// <param name="id">Request ID</param>
        /// <returns>List of history records</returns>
        [Route("api/v3.0/p/approval/request/{id}/history")]
        [HttpGet]
        public ActionResult GetRequestHistory(Guid id)
        {
            var response = new ResponseModel();
            
            try
            {
                var historyService = new ApprovalHistoryService();
                var history = historyService.GetRequestHistory(id);
                
                response.Object = history;
                response.Success = true;
                response.Message = "Request history retrieved successfully";
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message;
            }
            
            return Json(response);
        }
        
        #endregion
    }
}
```

**Source Pattern**: `WebVella.Erp.Plugins.Project/Controllers/ProjectController.cs`

#### Request DTOs

```csharp
// Api/CreateWorkflowModel.cs
namespace WebVella.Erp.Plugins.Approval.Api
{
    /// <summary>
    /// DTO for workflow creation requests
    /// </summary>
    public class CreateWorkflowModel
    {
        /// <summary>
        /// Unique workflow name
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Target entity name (e.g., "purchase_order", "expense_request")
        /// </summary>
        public string TargetEntity { get; set; }
    }
}

// Api/UpdateWorkflowModel.cs
namespace WebVella.Erp.Plugins.Approval.Api
{
    /// <summary>
    /// DTO for workflow update requests
    /// </summary>
    public class UpdateWorkflowModel
    {
        /// <summary>
        /// Updated workflow name (optional)
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Updated target entity (optional)
        /// </summary>
        public string TargetEntity { get; set; }
        
        /// <summary>
        /// Enable/disable workflow (optional)
        /// </summary>
        public bool? IsEnabled { get; set; }
    }
}

// Api/ApproveRequestModel.cs
namespace WebVella.Erp.Plugins.Approval.Api
{
    /// <summary>
    /// DTO for approve/reject request body
    /// </summary>
    public class ApproveRequestModel
    {
        /// <summary>
        /// Optional comments for the approval action
        /// </summary>
        public string Comments { get; set; }
    }
}

// Api/DelegateRequestModel.cs
namespace WebVella.Erp.Plugins.Approval.Api
{
    /// <summary>
    /// DTO for delegation request body
    /// </summary>
    public class DelegateRequestModel
    {
        /// <summary>
        /// User ID to delegate approval to
        /// </summary>
        public Guid DelegateToUserId { get; set; }
        
        /// <summary>
        /// Optional comments for the delegation
        /// </summary>
        public string Comments { get; set; }
    }
}
```

### Integration Points

| Integration | Description | Source Reference |
|-------------|-------------|------------------|
| ApprovalWorkflowService | Service layer for workflow CRUD operations | STORY-004, `Services/ApprovalWorkflowService.cs` |
| ApprovalRequestService | Service layer for request lifecycle management | STORY-004, `Services/ApprovalRequestService.cs` |
| ApprovalHistoryService | Service layer for audit history retrieval | STORY-004, `Services/ApprovalHistoryService.cs` |
| SecurityContext | Authentication context for current user validation | `WebVella.Erp/Api/SecurityContext.cs` |
| ResponseModel | Standard API response envelope | `WebVella.Erp/Api/Models/BaseModels.cs` |
| EntityRecord | Data container for workflow and request records | `WebVella.Erp/Api/Models/EntityRecord.cs` |

### Technical Approach

#### Controller Pattern

The `ApprovalController` follows the established WebVella ERP controller pattern demonstrated in `ProjectController`:

1. **Authentication**: The `[Authorize]` attribute on the controller class ensures all endpoints require authentication. Unauthenticated requests receive HTTP 401.

2. **Dependency Injection**: `IErpService` is injected via constructor, with manager instances (`RecordManager`, `EntityManager`, `SecurityManager`, `EntityRelationManager`) instantiated for internal use.

3. **Current User Resolution**: The `CurrentUserId` property extracts the authenticated user's ID from JWT claims using `ClaimTypes.NameIdentifier`.

4. **Response Pattern**: All endpoints return `ActionResult` with `Json(ResponseModel)` for consistent JSON responses:
   ```csharp
   var response = new ResponseModel();
   response.Success = true/false;
   response.Message = "descriptive message";
   response.Object = resultData;
   return Json(response);
   ```

5. **Error Handling**: Try-catch blocks with specific exception handling:
   - `UnauthorizedAccessException`: Sets HTTP 403 and returns permission error
   - `InvalidOperationException`: Returns validation errors (e.g., can't delete workflow with pending requests)
   - General `Exception`: Returns error message in response

6. **Service Delegation**: All business logic is delegated to service classes (STORY-004):
   ```csharp
   var workflowService = new ApprovalWorkflowService();
   var workflow = workflowService.CreateWorkflow(name, entity, userId);
   ```

#### Route Structure

Routes follow the WebVella ERP pattern `/api/v3.0/p/{plugin}/{resource}/{action}`:

| Route | Method | Description |
|-------|--------|-------------|
| `/api/v3.0/p/approval/workflow` | GET | List workflows |
| `/api/v3.0/p/approval/workflow` | POST | Create workflow |
| `/api/v3.0/p/approval/workflow/{id}` | GET | Get single workflow |
| `/api/v3.0/p/approval/workflow/{id}` | PUT | Update workflow |
| `/api/v3.0/p/approval/workflow/{id}` | DELETE | Delete workflow |
| `/api/v3.0/p/approval/request/{id}` | GET | Get request details |
| `/api/v3.0/p/approval/request/{id}/approve` | POST | Approve request |
| `/api/v3.0/p/approval/request/{id}/reject` | POST | Reject request |
| `/api/v3.0/p/approval/request/{id}/delegate` | POST | Delegate request |
| `/api/v3.0/p/approval/request/{id}/history` | GET | Get request history |
| `/api/v3.0/p/approval/pending` | GET | Get user's pending approvals |

#### Request/Response Examples

**Create Workflow Request:**
```http
POST /api/v3.0/p/approval/workflow
Content-Type: application/json
Authorization: Bearer {token}

{
  "name": "Purchase Order Approval",
  "targetEntity": "purchase_order"
}
```

**Create Workflow Response:**
```json
{
  "success": true,
  "message": "Workflow created successfully",
  "object": {
    "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "name": "Purchase Order Approval",
    "target_entity": "purchase_order",
    "is_enabled": true,
    "created_on": "2026-01-15T10:30:00Z",
    "created_by": "user-guid-here"
  },
  "errors": []
}
```

**Approve Request:**
```http
POST /api/v3.0/p/approval/request/{id}/approve
Content-Type: application/json
Authorization: Bearer {token}

{
  "comments": "Approved - within budget allocation"
}
```

**Get Pending Approvals:**
```http
GET /api/v3.0/p/approval/pending?page=1&pageSize=10
Authorization: Bearer {token}
```

**Pending Approvals Response:**
```json
{
  "success": true,
  "message": "Pending approvals retrieved successfully",
  "object": [
    {
      "id": "request-guid-1",
      "source_record_id": "purchase-order-guid",
      "source_entity": "purchase_order",
      "workflow_id": "workflow-guid",
      "current_step_id": "step-guid",
      "status": "pending",
      "created_on": "2026-01-15T09:00:00Z",
      "created_by": "requester-guid"
    }
  ],
  "errors": []
}
```

## Dependencies

| Story ID | Dependency Description |
|----------|----------------------|
| **STORY-004** | Service Layer - Required for `ApprovalWorkflowService`, `ApprovalRequestService`, and `ApprovalHistoryService` that handle all business logic for API endpoints |

## Effort Estimate

**5 Story Points**

Rationale:
- Single controller file with well-established patterns from `ProjectController`
- Clear service layer delegation (STORY-004) simplifies implementation
- Standard CRUD operations with minimal business logic in controller
- DTO classes are simple data containers
- Testing straightforward with mock services

## Labels

`workflow`, `approval`, `backend`, `api`, `rest`
