using Microsoft.AspNetCore.Http;
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
using WebVella.Erp.Plugins.Approval.Api;
using WebVella.Erp.Plugins.Approval.Services;
using WebVella.Erp.Web;
using WebVella.Erp.Web.Models;
using WebVella.Erp.Web.Services;

namespace WebVella.Erp.Plugins.Approval.Components
{
    /// <summary>
    /// PageComponent for displaying approval action buttons (approve, reject, delegate).
    /// Renders action buttons with modal dialogs for capturing comments and reasons.
    /// Validates user authorization using ApprovalRouteService before showing action buttons.
    /// </summary>
    /// <remarks>
    /// This component is designed to be placed on approval detail pages where users can
    /// take action on pending approval requests. It handles:
    /// - Loading approval request data from the database
    /// - Validating user authorization using ApprovalRouteService.IsUserAuthorizedApprover
    /// - Loading available users for delegation dropdown
    /// - Rendering appropriate buttons based on request status and user permissions
    /// 
    /// The component supports multiple render modes:
    /// - Display: Shows action buttons with current request context
    /// - Design: Shows placeholder preview for page builder
    /// - Options: Shows configuration panel for component settings
    /// - Help: Shows documentation for component usage
    /// - Error: Shows error messages when component fails
    /// </remarks>
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
        /// Provides access to the current HTTP request context, page information,
        /// and other request-scoped data needed for component rendering.
        /// </summary>
        protected ErpRequestContext ErpRequestContext { get; set; }

        /// <summary>
        /// Constructor with dependency injection for ErpRequestContext.
        /// The [FromServices] attribute enables automatic injection from the DI container.
        /// </summary>
        /// <param name="coreReqCtx">The ERP request context provided by the DI container.</param>
        public PcApprovalAction([FromServices] ErpRequestContext coreReqCtx)
        {
            ErpRequestContext = coreReqCtx;
        }

        /// <summary>
        /// Options class for configuring the PcApprovalAction component.
        /// Contains the request ID binding and visibility settings for each action button.
        /// All properties are serialized with JSON for persistence in page configuration.
        /// </summary>
        public class PcApprovalActionOptions
        {
            /// <summary>
            /// The data source binding for the approval request ID.
            /// Can be a static GUID string (e.g., "12345678-1234-1234-1234-123456789abc")
            /// or a data source expression (e.g., "{{Record.id}}" or "{{UrlQuery.requestId}}").
            /// The component will resolve this binding at render time to obtain the actual request ID.
            /// </summary>
            [JsonProperty(PropertyName = "request_id")]
            public string RequestId { get; set; } = "";

            /// <summary>
            /// Whether to show the Approve button. Default is true.
            /// When set to false, the approve action will be hidden from the user,
            /// useful for read-only views or when approve should be disabled.
            /// </summary>
            [JsonProperty(PropertyName = "show_approve")]
            public bool ShowApprove { get; set; } = true;

            /// <summary>
            /// Whether to show the Reject button. Default is true.
            /// When set to false, the reject action will be hidden from the user,
            /// useful for workflows where rejection is not permitted.
            /// </summary>
            [JsonProperty(PropertyName = "show_reject")]
            public bool ShowReject { get; set; } = true;

            /// <summary>
            /// Whether to show the Delegate button. Default is true.
            /// When set to false, the delegate action will be hidden from the user,
            /// useful for workflows where delegation is not permitted.
            /// </summary>
            [JsonProperty(PropertyName = "show_delegate")]
            public bool ShowDelegate { get; set; } = true;
        }

        /// <summary>
        /// Asynchronously invokes the component to render the appropriate view based on context mode.
        /// Handles Display, Design, Options, Help, and Error modes.
        /// In Display mode, loads approval request data and validates user authorization
        /// using the ApprovalRouteService.
        /// </summary>
        /// <param name="context">The page component context containing node, options, and mode information.</param>
        /// <returns>A task that resolves to the appropriate view component result.</returns>
        /// <exception cref="ValidationException">Thrown when validation errors occur during component processing.</exception>
        public async Task<IViewComponentResult> InvokeAsync(PageComponentContext context)
        {
            ErpPage currentPage = null;
            try
            {
                #region << Init >>
                // Validate that the node ID is provided - required for component identification
                if (context.Node == null)
                {
                    return await Task.FromResult<IViewComponentResult>(Content("Error: The node Id is required to be set as query parameter 'nid', when requesting this component"));
                }

                // Validate that the page model is available and is the correct type
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
                // If options are null, use default values
                var options = new PcApprovalActionOptions();
                if (context.Options != null)
                {
                    options = JsonConvert.DeserializeObject<PcApprovalActionOptions>(context.Options.ToString());
                }

                // Get component metadata for rendering in design mode and options panel
                var componentMeta = new PageComponentLibraryService().GetComponentMeta(context.Node.ComponentName);
                #endregion

                #region << Set ViewBag Values >>
                // Set common ViewBag values used across all render modes
                ViewBag.Options = options;
                ViewBag.Node = context.Node;
                ViewBag.ComponentMeta = componentMeta;
                ViewBag.RequestContext = ErpRequestContext;
                ViewBag.AppContext = ErpAppContext.Current;
                ViewBag.ComponentContext = context;
                ViewBag.CurrentUser = SecurityContext.CurrentUser;
                #endregion

                #region << Load Request Data for Display and Design Modes >>
                // Skip data loading for Options and Help modes as they don't need it
                if (context.Mode != ComponentMode.Options && context.Mode != ComponentMode.Help)
                {
                    // Resolve the request ID from the data source binding or query parameter
                    Guid requestId = Guid.Empty;
                    
                    // First, try to get request ID from query parameter
                    HttpContext httpContext = null;
                    if (ErpRequestContext.PageContext != null)
                    {
                        httpContext = ErpRequestContext.PageContext.HttpContext;
                        if (httpContext?.Request?.Query != null)
                        {
                            var requestIdParam = httpContext.Request.Query["requestId"].FirstOrDefault();
                            if (!string.IsNullOrEmpty(requestIdParam) && Guid.TryParse(requestIdParam, out Guid parsedId))
                            {
                                requestId = parsedId;
                            }
                        }
                    }
                    
                    // If not in query params, try the options
                    if (requestId == Guid.Empty && !string.IsNullOrWhiteSpace(options.RequestId))
                    {
                        // Try to get the value from data source binding (e.g., {{Record.id}})
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

                    // Store the request ID in ViewBag for use in views and AJAX calls
                    ViewBag.RequestId = requestId;

                    // Initialize default values for display
                    EntityRecord request = null;
                    bool canApprove = false;
                    var delegateUsers = new List<EntityRecord>();

                    // Load request data if we have a valid request ID
                    if (requestId != Guid.Empty)
                    {
                        // Query the approval_request record using EQL with parameterized query
                        try
                        {
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
                            // Log the error but don't fail the component - allow display with error indication
                            ViewBag.QueryError = queryEx.Message;
                        }

                        // Check if current user is authorized to approve this request
                        // Using ApprovalRouteService for consistent authorization logic across the system
                        if (request != null && SecurityContext.CurrentUser != null)
                        {
                            canApprove = CheckUserAuthorization(request, SecurityContext.CurrentUser.Id);
                        }

                        // Load users available for delegation
                        // Only load if delegate button is enabled and user can approve
                        if (options.ShowDelegate && canApprove)
                        {
                            try
                            {
                                // Query active users excluding the current user for delegation
                                var usersEql = new EqlCommand(
                                    "SELECT id, username, first_name, last_name, email FROM user WHERE enabled = @enabled ORDER BY username",
                                    new List<EqlParameter>
                                    {
                                        new EqlParameter("enabled", true)
                                    });

                                var usersResult = usersEql.Execute();
                                
                                if (usersResult != null && usersResult.Count > 0)
                                {
                                    // Exclude the current user from delegate options - can't delegate to self
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
                    }

                    // Set ViewBag values for the views to consume
                    ViewBag.Request = request;
                    ViewBag.CanApprove = canApprove;
                    ViewBag.DelegateUsers = delegateUsers;
                    
                    // Set request status information for conditional rendering
                    if (request != null)
                    {
                        var status = request["status"]?.ToString()?.ToLowerInvariant();
                        ViewBag.RequestStatus = status;
                        ViewBag.IsPending = status == "pending";
                    }
                    else
                    {
                        ViewBag.RequestStatus = null;
                        ViewBag.IsPending = false;
                    }
                }
                #endregion

                #region << Return Appropriate View Based on Mode >>
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
                        // Unknown mode - return error view with appropriate message
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
                // Handle validation exceptions with proper error display
                ViewBag.Error = ex;
                return await Task.FromResult<IViewComponentResult>(View("Error"));
            }
            catch (Exception ex)
            {
                // Handle all other exceptions, wrapping in ValidationException for consistent error display
                ViewBag.Error = new ValidationException()
                {
                    Message = ex.Message
                };
                return await Task.FromResult<IViewComponentResult>(View("Error"));
            }
        }

        /// <summary>
        /// Checks if the specified user is authorized to approve the given approval request.
        /// Uses the ApprovalRouteService to validate authorization based on the current step's
        /// approver_type and approver_id settings.
        /// </summary>
        /// <param name="request">The approval request EntityRecord to check authorization for.</param>
        /// <param name="userId">The user ID to check authorization for.</param>
        /// <returns>True if the user is authorized to approve, false otherwise.</returns>
        /// <remarks>
        /// Authorization flow:
        /// 1. Validate request is not null and has a valid status
        /// 2. Check if request status is "pending" (only pending requests can be actioned)
        /// 3. Get the current step ID from the request
        /// 4. Use ApprovalRouteService.IsUserAuthorizedApprover to validate authorization
        /// 
        /// This method encapsulates the authorization check and handles all error conditions
        /// gracefully, returning false if any step fails.
        /// </remarks>
        private bool CheckUserAuthorization(EntityRecord request, Guid userId)
        {
            if (request == null || userId == Guid.Empty)
            {
                return false;
            }

            // Check if request status is pending - only pending requests can be actioned
            var status = request["status"]?.ToString()?.ToLowerInvariant();
            if (status != "pending")
            {
                return false;
            }

            // Get the current step ID from the request
            var currentStepId = request["current_step_id"] as Guid?;
            if (!currentStepId.HasValue || currentStepId.Value == Guid.Empty)
            {
                return false;
            }

            try
            {
                // Use ApprovalRouteService for authorization validation
                // This ensures consistent authorization logic across the system
                var routeService = new ApprovalRouteService();
                return routeService.IsUserAuthorizedApprover(userId, currentStepId.Value);
            }
            catch
            {
                // If authorization check fails for any reason, deny by default
                // This is a security best practice - fail secure
                return false;
            }
        }
    }
}
