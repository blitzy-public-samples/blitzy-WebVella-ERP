using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Eql;
using WebVella.Erp.Exceptions;
using WebVella.Erp.Web;
using WebVella.Erp.Web.Models;
using WebVella.Erp.Web.Services;

namespace WebVella.Erp.Plugins.Approval.Components
{
    /// <summary>
    /// PageComponent for displaying approval action buttons (approve, reject, delegate).
    /// Renders action buttons with modal dialogs for capturing comments and reasons.
    /// Validates user authorization before showing action buttons.
    /// </summary>
    [PageComponent(
        Label = "Approval Action",
        Library = "WebVella",
        Description = "Approve, reject, or delegate approval requests",
        Version = "0.0.1",
        IconClass = "fas fa-check-circle",
        Category = "Approval Workflow")]
    public class PcApprovalAction : PageComponent
    {
        /// <summary>
        /// The ERP request context injected via constructor.
        /// Provides access to the current HTTP request context and page information.
        /// </summary>
        protected ErpRequestContext ErpRequestContext { get; set; }

        /// <summary>
        /// Constructor with dependency injection for ErpRequestContext.
        /// </summary>
        /// <param name="coreReqCtx">The ERP request context provided by the DI container.</param>
        public PcApprovalAction([FromServices] ErpRequestContext coreReqCtx)
        {
            ErpRequestContext = coreReqCtx;
        }

        /// <summary>
        /// Options class for configuring the PcApprovalAction component.
        /// Contains the request ID binding and visibility settings for each action button.
        /// </summary>
        public class PcApprovalActionOptions
        {
            /// <summary>
            /// The data source binding for the approval request ID.
            /// Can be a static GUID string or a data source expression like {{Record.id}}.
            /// </summary>
            [JsonProperty(PropertyName = "request_id")]
            public string RequestId { get; set; } = "";

            /// <summary>
            /// Whether to show the Approve button. Default is true.
            /// </summary>
            [JsonProperty(PropertyName = "show_approve")]
            public bool ShowApprove { get; set; } = true;

            /// <summary>
            /// Whether to show the Reject button. Default is true.
            /// </summary>
            [JsonProperty(PropertyName = "show_reject")]
            public bool ShowReject { get; set; } = true;

            /// <summary>
            /// Whether to show the Delegate button. Default is true.
            /// </summary>
            [JsonProperty(PropertyName = "show_delegate")]
            public bool ShowDelegate { get; set; } = true;
        }

        /// <summary>
        /// Asynchronously invokes the component to render the appropriate view based on context mode.
        /// Handles Display, Design, Options, Help, and Error modes.
        /// In Display mode, loads approval request data and validates user authorization.
        /// </summary>
        /// <param name="context">The page component context containing node, options, and mode information.</param>
        /// <returns>A task that resolves to the appropriate view component result.</returns>
        public async Task<IViewComponentResult> InvokeAsync(PageComponentContext context)
        {
            ErpPage currentPage = null;
            try
            {
                #region << Init >>
                // Validate that the node ID is provided
                if (context.Node == null)
                {
                    return await Task.FromResult<IViewComponentResult>(Content("Error: The node Id is required to be set as query parameter 'nid', when requesting this component"));
                }

                // Validate that the page model is available
                var pageFromModel = context.DataModel.GetProperty("Page");
                if (pageFromModel == null)
                {
                    return await Task.FromResult<IViewComponentResult>(Content("Error: PageModel cannot be null"));
                }
                else if (pageFromModel is ErpPage)
                {
                    currentPage = (ErpPage)pageFromModel;
                }
                else
                {
                    return await Task.FromResult<IViewComponentResult>(Content("Error: PageModel does not have Page property or it is not from ErpPage Type"));
                }

                // Deserialize component options from the context
                var options = new PcApprovalActionOptions();
                if (context.Options != null)
                {
                    options = JsonConvert.DeserializeObject<PcApprovalActionOptions>(context.Options.ToString());
                }

                // Get component metadata for rendering
                var componentMeta = new PageComponentLibraryService().GetComponentMeta(context.Node.ComponentName);
                #endregion

                #region << Set ViewBag Values >>
                ViewBag.Options = options;
                ViewBag.Node = context.Node;
                ViewBag.ComponentMeta = componentMeta;
                ViewBag.RequestContext = ErpRequestContext;
                ViewBag.AppContext = ErpAppContext.Current;
                ViewBag.ComponentContext = context;
                ViewBag.CurrentUser = SecurityContext.CurrentUser;
                #endregion

                #region << Load Request Data for Display Mode >>
                if (context.Mode != ComponentMode.Options && context.Mode != ComponentMode.Help)
                {
                    // Resolve the request ID from the data source binding
                    Guid requestId = Guid.Empty;
                    
                    if (!string.IsNullOrWhiteSpace(options.RequestId))
                    {
                        // Try to get the value from data source binding
                        var requestIdValue = context.DataModel.GetPropertyValueByDataSource(options.RequestId);
                        
                        if (requestIdValue != null)
                        {
                            if (requestIdValue is Guid guidValue)
                            {
                                requestId = guidValue;
                            }
                            else if (requestIdValue is string stringValue && Guid.TryParse(stringValue, out Guid parsedGuid))
                            {
                                requestId = parsedGuid;
                            }
                        }
                        else if (Guid.TryParse(options.RequestId, out Guid directGuid))
                        {
                            // If data source binding returns null, try parsing the option directly as a GUID
                            requestId = directGuid;
                        }
                    }

                    // Store the request ID in ViewBag for use in views
                    ViewBag.RequestId = requestId;

                    // Initialize default values
                    EntityRecord request = null;
                    bool canApprove = false;
                    var delegateUsers = new List<EntityRecord>();

                    // Load request data if we have a valid request ID
                    if (requestId != Guid.Empty)
                    {
                        try
                        {
                            // Query the approval_request record using EQL with parameterized query
                            var requestEql = new EqlCommand(
                                "SELECT * FROM approval_request WHERE id = @requestId",
                                new List<EqlParameter>
                                {
                                    new EqlParameter("requestId", requestId)
                                });

                            var requestResult = requestEql.Execute();
                            
                            if (requestResult != null && requestResult.Count > 0)
                            {
                                request = requestResult.FirstOrDefault();
                            }
                        }
                        catch (Exception queryEx)
                        {
                            // Log the error but don't fail the component
                            ViewBag.QueryError = queryEx.Message;
                        }

                        // Check if current user is authorized to approve this request
                        if (request != null && SecurityContext.CurrentUser != null)
                        {
                            canApprove = IsUserAuthorizedApprover(request, SecurityContext.CurrentUser.Id);
                        }

                        // Load users available for delegation
                        try
                        {
                            // Query active users excluding the current user
                            var usersEql = new EqlCommand(
                                "SELECT id, username, first_name, last_name, email FROM user WHERE enabled = @enabled ORDER BY username",
                                new List<EqlParameter>
                                {
                                    new EqlParameter("enabled", true)
                                });

                            var usersResult = usersEql.Execute();
                            
                            if (usersResult != null && usersResult.Count > 0)
                            {
                                // Exclude the current user from delegate options
                                var currentUserId = SecurityContext.CurrentUser?.Id ?? Guid.Empty;
                                delegateUsers = usersResult.Where(u => 
                                    u["id"] != null && 
                                    (Guid)u["id"] != currentUserId
                                ).ToList();
                            }
                        }
                        catch (Exception userQueryEx)
                        {
                            // Log the error but continue with empty delegate list
                            ViewBag.UserQueryError = userQueryEx.Message;
                        }
                    }

                    ViewBag.Request = request;
                    ViewBag.CanApprove = canApprove;
                    ViewBag.DelegateUsers = delegateUsers;
                }
                #endregion

                #region << Return Appropriate View >>
                switch (context.Mode)
                {
                    case ComponentMode.Display:
                        return await Task.FromResult<IViewComponentResult>(View("Display"));
                    case ComponentMode.Design:
                        return await Task.FromResult<IViewComponentResult>(View("Design"));
                    case ComponentMode.Options:
                        return await Task.FromResult<IViewComponentResult>(View("Options"));
                    case ComponentMode.Help:
                        return await Task.FromResult<IViewComponentResult>(View("Help"));
                    default:
                        ViewBag.Error = new ValidationException()
                        {
                            Message = "Unknown component mode"
                        };
                        return await Task.FromResult<IViewComponentResult>(View("Error"));
                }
                #endregion
            }
            catch (ValidationException ex)
            {
                ViewBag.Error = ex;
                return await Task.FromResult<IViewComponentResult>(View("Error"));
            }
            catch (Exception ex)
            {
                ViewBag.Error = new ValidationException()
                {
                    Message = ex.Message
                };
                return await Task.FromResult<IViewComponentResult>(View("Error"));
            }
        }

        /// <summary>
        /// Checks if the specified user is authorized to approve the given approval request.
        /// Authorization is based on the current step's approver_type and approver_id settings.
        /// </summary>
        /// <param name="request">The approval request EntityRecord to check authorization for.</param>
        /// <param name="userId">The user ID to check authorization for.</param>
        /// <returns>True if the user is authorized to approve, false otherwise.</returns>
        private bool IsUserAuthorizedApprover(EntityRecord request, Guid userId)
        {
            if (request == null || userId == Guid.Empty)
            {
                return false;
            }

            // Check if request is in pending status
            var status = request["status"]?.ToString()?.ToLower();
            if (status != "pending")
            {
                return false;
            }

            // Get the current step ID
            var currentStepId = request["current_step_id"] as Guid?;
            if (currentStepId == null || currentStepId == Guid.Empty)
            {
                return false;
            }

            try
            {
                // Query the approval_step to get approver settings
                var stepEql = new EqlCommand(
                    "SELECT * FROM approval_step WHERE id = @stepId",
                    new List<EqlParameter>
                    {
                        new EqlParameter("stepId", currentStepId)
                    });

                var stepResult = stepEql.Execute();
                
                if (stepResult == null || stepResult.Count == 0)
                {
                    return false;
                }

                var step = stepResult.FirstOrDefault();
                var approverType = step["approver_type"]?.ToString()?.ToLower();
                var approverId = step["approver_id"] as Guid?;

                switch (approverType)
                {
                    case "user":
                        // Direct user assignment - check if the user matches
                        return approverId.HasValue && approverId.Value == userId;
                    
                    case "role":
                        // Role-based assignment - check if user has the specified role
                        if (!approverId.HasValue || SecurityContext.CurrentUser == null)
                        {
                            return false;
                        }
                        
                        // Check if user has the specified role
                        foreach (var role in SecurityContext.CurrentUser.Roles)
                        {
                            if (role.Id == approverId.Value)
                            {
                                return true;
                            }
                        }
                        return false;
                    
                    case "department_head":
                        // Department head assignment - for now, allow any user with Manager or Administrator role
                        // This is a simplified implementation; a full implementation would check organizational hierarchy
                        if (SecurityContext.CurrentUser == null)
                        {
                            return false;
                        }
                        
                        foreach (var role in SecurityContext.CurrentUser.Roles)
                        {
                            var roleName = role.Name?.ToLower();
                            if (roleName == "manager" || roleName == "administrator" || roleName == "admin")
                            {
                                return true;
                            }
                        }
                        return false;
                    
                    default:
                        // Unknown approver type - deny by default
                        return false;
                }
            }
            catch
            {
                // If we can't determine authorization, deny by default
                return false;
            }
        }
    }
}
