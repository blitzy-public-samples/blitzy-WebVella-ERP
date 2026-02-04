using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Exceptions;
using WebVella.Erp.Plugins.Approval.Model;
using WebVella.Erp.Plugins.Approval.Services;
using WebVella.Erp.Web;
using WebVella.Erp.Web.Models;
using WebVella.Erp.Web.Services;

namespace WebVella.Erp.Plugins.Approval.Components
{
    /// <summary>
    /// PageComponent that renders approval action buttons (Approve, Reject, Delegate) with a comment modal dialog.
    /// This component is responsible for displaying the appropriate action buttons based on user authorization
    /// and the current status of the approval request.
    /// </summary>
    /// <remarks>
    /// The component performs the following operations:
    /// <list type="bullet">
    ///   <item>
    ///     <description>Loads the approval request data using ApprovalRequestService</description>
    ///   </item>
    ///   <item>
    ///     <description>Validates user authorization via ApprovalRouteService.GetApproversForStep()</description>
    ///   </item>
    ///   <item>
    ///     <description>Populates ViewBag with request data and authorization flags</description>
    ///   </item>
    ///   <item>
    ///     <description>Renders appropriate view based on ComponentMode (Display, Design, Options, Help, Error)</description>
    ///   </item>
    /// </list>
    /// 
    /// Component Options:
    /// <list type="bullet">
    ///   <item><description>RequestId - DataSource path to the approval request ID</description></item>
    ///   <item><description>ShowApproveButton - Whether to display the approve button (default: true)</description></item>
    ///   <item><description>ShowRejectButton - Whether to display the reject button (default: true)</description></item>
    ///   <item><description>ShowDelegateButton - Whether to display the delegate button (default: true)</description></item>
    ///   <item><description>RequireCommentsOnApprove - Whether comments are required for approval (default: false)</description></item>
    ///   <item><description>RequireCommentsOnDelegate - Whether comments are required for delegation (default: false)</description></item>
    /// </list>
    /// 
    /// ViewBag Properties Set:
    /// <list type="bullet">
    ///   <item><description>Options - The deserialized PcApprovalActionOptions</description></item>
    ///   <item><description>Node - The page component node</description></item>
    ///   <item><description>ComponentMeta - Component metadata from PageComponentLibraryService</description></item>
    ///   <item><description>RequestContext - The ErpRequestContext instance</description></item>
    ///   <item><description>AppContext - The current ErpAppContext</description></item>
    ///   <item><description>ComponentContext - The PageComponentContext</description></item>
    ///   <item><description>CurrentUser - The currently authenticated user</description></item>
    ///   <item><description>CurrentUserJson - JSON serialized current user for client-side access</description></item>
    ///   <item><description>CanApprove - Boolean indicating if current user can perform approval actions</description></item>
    ///   <item><description>RequestId - The resolved request ID</description></item>
    ///   <item><description>RequestStatus - The current status of the approval request</description></item>
    ///   <item><description>RequestJson - JSON serialized request data for client-side access</description></item>
    /// </list>
    /// </remarks>
    [PageComponent(
        Label = "Approval Action",
        Library = "WebVella",
        Description = "Renders approval action buttons (Approve, Reject, Delegate) with comment modal",
        Version = "0.0.1",
        IconClass = "fas fa-check-circle")]
    public class PcApprovalAction : PageComponent
    {
        #region Properties

        /// <summary>
        /// Gets or sets the ERP request context injected via dependency injection.
        /// Provides access to the current page context, HTTP context, and other request-scoped services.
        /// </summary>
        protected ErpRequestContext ErpRequestContext { get; set; }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="PcApprovalAction"/> class.
        /// </summary>
        /// <param name="coreReqCtx">The ERP request context injected by the framework.</param>
        public PcApprovalAction([FromServices] ErpRequestContext coreReqCtx)
        {
            ErpRequestContext = coreReqCtx;
        }

        #endregion

        #region Options Class

        /// <summary>
        /// Configuration options for the PcApprovalAction component.
        /// These options control the behavior and appearance of the approval action buttons.
        /// </summary>
        /// <remarks>
        /// All properties are serialized/deserialized using Newtonsoft.Json with specific property names
        /// to maintain compatibility with the WebVella component options storage format.
        /// </remarks>
        public class PcApprovalActionOptions
        {
            /// <summary>
            /// Gets or sets the data source path for the approval request ID.
            /// This can be a direct GUID string or a data source expression (e.g., "{{Record.id}}").
            /// </summary>
            [JsonProperty(PropertyName = "request_id")]
            public string RequestId { get; set; } = "";

            /// <summary>
            /// Gets or sets a value indicating whether to display the Approve button.
            /// Default is true.
            /// </summary>
            [JsonProperty(PropertyName = "show_approve_button")]
            public bool ShowApproveButton { get; set; } = true;

            /// <summary>
            /// Gets or sets a value indicating whether to display the Reject button.
            /// Default is true.
            /// </summary>
            [JsonProperty(PropertyName = "show_reject_button")]
            public bool ShowRejectButton { get; set; } = true;

            /// <summary>
            /// Gets or sets a value indicating whether to display the Delegate button.
            /// Default is true.
            /// </summary>
            [JsonProperty(PropertyName = "show_delegate_button")]
            public bool ShowDelegateButton { get; set; } = true;

            /// <summary>
            /// Gets or sets a value indicating whether comments are required when approving a request.
            /// Default is false.
            /// </summary>
            [JsonProperty(PropertyName = "require_comments_on_approve")]
            public bool RequireCommentsOnApprove { get; set; } = false;

            /// <summary>
            /// Gets or sets a value indicating whether comments are required when delegating a request.
            /// Default is false.
            /// </summary>
            [JsonProperty(PropertyName = "require_comments_on_delegate")]
            public bool RequireCommentsOnDelegate { get; set; } = false;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Invokes the component to render the approval action buttons based on the current context and user authorization.
        /// </summary>
        /// <param name="context">The page component context containing node information, options, and data model.</param>
        /// <returns>
        /// A task that resolves to an IViewComponentResult representing the appropriate view
        /// (Display, Design, Options, Help, or Error) based on the component mode.
        /// </returns>
        /// <exception cref="ValidationException">
        /// Thrown when:
        /// <list type="bullet">
        ///   <item><description>The context node is null</description></item>
        ///   <item><description>The page model is missing or invalid</description></item>
        ///   <item><description>An unknown component mode is encountered</description></item>
        /// </list>
        /// </exception>
        /// <remarks>
        /// This method follows the standard WebVella PageComponent pattern:
        /// <list type="number">
        ///   <item><description>Validate context and page model</description></item>
        ///   <item><description>Deserialize component options</description></item>
        ///   <item><description>Populate ViewBag with common properties</description></item>
        ///   <item><description>For Display/Design modes, load request data and check authorization</description></item>
        ///   <item><description>Return appropriate view based on ComponentMode</description></item>
        /// </list>
        /// </remarks>
        public async Task<IViewComponentResult> InvokeAsync(PageComponentContext context)
        {
            ErpPage currentPage = null;
            try
            {
                #region << Init >>

                // Validate that context node is present
                if (context.Node == null)
                {
                    return await Task.FromResult<IViewComponentResult>(
                        Content("Error: The node Id is required to be set as query parameter 'nid', when requesting this component"));
                }

                // Validate and extract the page from the data model
                var pageFromModel = context.DataModel.GetProperty("Page");
                if (pageFromModel == null)
                {
                    return await Task.FromResult<IViewComponentResult>(
                        Content("Error: PageModel cannot be null"));
                }
                else if (pageFromModel is ErpPage)
                {
                    currentPage = (ErpPage)pageFromModel;
                }
                else
                {
                    return await Task.FromResult<IViewComponentResult>(
                        Content("Error: PageModel does not have Page property or it is not from ErpPage Type"));
                }

                // Deserialize component options
                var options = new PcApprovalActionOptions();
                if (context.Options != null)
                {
                    options = JsonConvert.DeserializeObject<PcApprovalActionOptions>(context.Options.ToString());
                }

                // Get component metadata
                var componentMeta = new PageComponentLibraryService().GetComponentMeta(context.Node.ComponentName);

                #endregion

                #region << Populate ViewBag >>

                // Set common ViewBag properties
                ViewBag.Options = options;
                ViewBag.Node = context.Node;
                ViewBag.ComponentMeta = componentMeta;
                ViewBag.RequestContext = ErpRequestContext;
                ViewBag.AppContext = ErpAppContext.Current;
                ViewBag.ComponentContext = context;
                ViewBag.CurrentUser = SecurityContext.CurrentUser;
                ViewBag.CurrentUserJson = JsonConvert.SerializeObject(SecurityContext.CurrentUser);

                #endregion

                #region << Load Request Data for Display/Design Modes >>

                // Only load request data for Display and Design modes
                if (context.Mode != ComponentMode.Options && context.Mode != ComponentMode.Help)
                {
                    // Resolve request ID from data source
                    var requestIdValue = context.DataModel.GetPropertyValueByDataSource(options.RequestId);
                    Guid? requestId = null;

                    // Parse request ID - can be Guid directly or string
                    if (requestIdValue != null)
                    {
                        if (requestIdValue is Guid guidValue)
                        {
                            requestId = guidValue;
                        }
                        else if (Guid.TryParse(requestIdValue.ToString(), out var parsedGuid))
                        {
                            requestId = parsedGuid;
                        }
                    }

                    if (requestId.HasValue && requestId.Value != Guid.Empty)
                    {
                        // Load the approval request
                        var requestService = new ApprovalRequestService();
                        var request = requestService.GetRequest(requestId.Value);

                        if (request != null)
                        {
                            // Get current step ID for authorization check
                            var currentStepId = request["current_step_id"] as Guid?;
                            var requestStatus = request["status"] as string;

                            // Default authorization to false
                            bool canApprove = false;

                            // Check if user is authorized to approve at current step
                            if (currentStepId.HasValue && currentStepId.Value != Guid.Empty)
                            {
                                var routeService = new ApprovalRouteService();
                                var approvers = routeService.GetApproversForStep(currentStepId.Value);

                                if (approvers != null && SecurityContext.CurrentUser != null)
                                {
                                    canApprove = approvers.Contains(SecurityContext.CurrentUser.Id);
                                }
                            }

                            // Only allow actions on pending requests
                            var pendingStatus = ApprovalStatus.Pending.ToString().ToLowerInvariant();
                            if (!string.Equals(requestStatus, pendingStatus, StringComparison.OrdinalIgnoreCase))
                            {
                                canApprove = false;
                            }

                            // Set ViewBag properties for request data
                            ViewBag.CanApprove = canApprove;
                            ViewBag.RequestId = requestId.Value;
                            ViewBag.RequestStatus = requestStatus;
                            ViewBag.RequestJson = JsonConvert.SerializeObject(request);
                            ViewBag.Request = request;
                        }
                        else
                        {
                            // Request not found
                            ViewBag.CanApprove = false;
                            ViewBag.RequestId = requestId.Value;
                            ViewBag.RequestStatus = null;
                            ViewBag.RequestJson = null;
                            ViewBag.Request = null;
                        }
                    }
                    else
                    {
                        // No valid request ID provided
                        ViewBag.CanApprove = false;
                        ViewBag.RequestId = null;
                        ViewBag.RequestStatus = null;
                        ViewBag.RequestJson = null;
                        ViewBag.Request = null;
                    }
                }

                #endregion

                #region << Return View Based on Mode >>

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

        #endregion
    }
}
