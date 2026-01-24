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
using WebVella.Erp.Plugins.Approval.Services;
using WebVella.Erp.Web;
using WebVella.Erp.Web.Models;
using WebVella.Erp.Web.Services;
using WebVella.Erp.Web.Utils;

namespace WebVella.Erp.Plugins.Approval.Components
{
    /// <summary>
    /// PageComponent for Approval Workflow Configuration administration.
    /// Provides a comprehensive interface for managing approval workflow definitions,
    /// including creating, viewing, editing, and deleting workflows.
    /// </summary>
    /// <remarks>
    /// This component supports all standard WebVella page component render modes:
    /// - Display: Shows the workflow list with CRUD operations
    /// - Design: Preview in page builder
    /// - Options: Configuration panel for component settings
    /// - Help: Documentation for users
    /// - Error: Error display with validation messages
    /// 
    /// The component integrates with WorkflowConfigService to load workflow data
    /// and supports filtering by entity name and inactive workflow visibility.
    /// </remarks>
    [PageComponent(
        Label = "Approval Workflow Config",
        Library = "WebVella",
        Description = "Manage approval workflow definitions, steps, and rules",
        Version = "0.0.1",
        IconClass = "fas fa-cogs",
        Category = "Approval Workflow")]
    public class PcApprovalWorkflowConfig : PageComponent
    {
        #region Properties

        /// <summary>
        /// Gets or sets the ERP request context providing access to HTTP context,
        /// page context, and other request-scoped data.
        /// </summary>
        protected ErpRequestContext ErpRequestContext { get; set; }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="PcApprovalWorkflowConfig"/> component.
        /// </summary>
        /// <param name="coreReqCtx">
        /// The ERP request context injected by the ASP.NET Core framework via [FromServices].
        /// Provides access to current HTTP request, page context, and authentication information.
        /// </param>
        public PcApprovalWorkflowConfig([FromServices] ErpRequestContext coreReqCtx)
        {
            ErpRequestContext = coreReqCtx;
        }

        #endregion

        #region Nested Classes

        /// <summary>
        /// Options class for configuring the Approval Workflow Config component behavior.
        /// These options are set through the page builder's component configuration panel.
        /// </summary>
        public class PcApprovalWorkflowConfigOptions
        {
            /// <summary>
            /// Gets or sets whether to show inactive (disabled) workflows in the list.
            /// When false, only enabled workflows are displayed.
            /// </summary>
            /// <value>
            /// True to include disabled workflows in the display; false (default) to show only active workflows.
            /// </value>
            [JsonProperty(PropertyName = "show_inactive")]
            public bool ShowInactive { get; set; } = false;

            /// <summary>
            /// Gets or sets the number of workflows to display per page.
            /// Used for client-side pagination of the workflow list.
            /// </summary>
            /// <value>
            /// The page size for pagination. Default is 10 items per page.
            /// </value>
            [JsonProperty(PropertyName = "page_size")]
            public int PageSize { get; set; } = 10;

            /// <summary>
            /// Gets or sets the target entity name filter.
            /// When specified, only workflows targeting this specific entity are displayed.
            /// </summary>
            /// <value>
            /// The entity name to filter by, or empty string to show workflows for all entities.
            /// </value>
            [JsonProperty(PropertyName = "filter_by_entity")]
            public string FilterByEntity { get; set; } = "";
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Invokes the component rendering logic based on the current render mode.
        /// </summary>
        /// <param name="context">
        /// The page component context containing node information, data model,
        /// options, and current render mode (Display, Design, Options, Help, or Error).
        /// </param>
        /// <returns>
        /// An <see cref="IViewComponentResult"/> representing the appropriate view for the current mode:
        /// - Display.cshtml for runtime display with workflow data
        /// - Design.cshtml for page builder preview
        /// - Options.cshtml for component configuration
        /// - Help.cshtml for documentation
        /// - Error.cshtml for error display
        /// </returns>
        /// <exception cref="ValidationException">
        /// Caught and displayed in Error view when validation errors occur.
        /// </exception>
        public async Task<IViewComponentResult> InvokeAsync(PageComponentContext context)
        {
            ErpPage currentPage = null;
            try
            {
                #region << Initialization and Validation >>

                // Validate that the component node is properly configured
                if (context.Node == null)
                {
                    return await Task.FromResult<IViewComponentResult>(
                        Content("Error: The node Id is required to be set as query parameter 'nid', when requesting this component"));
                }

                // Retrieve and validate the page model from the data context
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

                // Deserialize component options from stored configuration
                var options = new PcApprovalWorkflowConfigOptions();
                if (context.Options != null)
                {
                    options = JsonConvert.DeserializeObject<PcApprovalWorkflowConfigOptions>(context.Options.ToString());
                }

                // Get component metadata from the library service for display purposes
                var componentMeta = new PageComponentLibraryService().GetComponentMeta(context.Node.ComponentName);

                #endregion

                #region << Populate ViewBag with Common Data >>

                // Set standard ViewBag properties used by all view modes
                ViewBag.Options = options;
                ViewBag.Node = context.Node;
                ViewBag.ComponentMeta = componentMeta;
                ViewBag.RequestContext = ErpRequestContext;
                ViewBag.AppContext = ErpAppContext.Current;
                ViewBag.ComponentContext = context;
                ViewBag.CurrentUser = SecurityContext.CurrentUser;
                ViewBag.CurrentUserJson = JsonConvert.SerializeObject(SecurityContext.CurrentUser);

                #endregion

                #region << Load Workflow Data for Display Mode >>

                // For Display mode, load workflow data from the service
                if (context.Mode != ComponentMode.Options && context.Mode != ComponentMode.Help)
                {
                    // Build the fully qualified site root URL for AJAX calls
                    HttpContext httpContext = null;
                    if (ErpRequestContext.PageContext != null)
                    {
                        httpContext = ErpRequestContext.PageContext.HttpContext;
                    }
                    ViewBag.SiteRootUrl = GetFullyQualifiedApplicationPath(httpContext);

                    // Only load workflows for Display mode (Design mode shows preview with mock data)
                    if (context.Mode == ComponentMode.Display)
                    {
                        try
                        {
                            // Instantiate the workflow configuration service
                            var workflowService = new WorkflowConfigService();

                            // Load all workflows, respecting the ShowInactive option
                            // When ShowInactive is true, GetAll returns all workflows
                            // When ShowInactive is false, GetAll returns only enabled workflows
                            var workflows = workflowService.GetAll(!options.ShowInactive ? false : true);

                            // Apply entity filter if specified in options
                            if (!string.IsNullOrWhiteSpace(options.FilterByEntity))
                            {
                                workflows = workflows
                                    .Where(w => w.TargetEntityName != null &&
                                           w.TargetEntityName.Equals(options.FilterByEntity, StringComparison.OrdinalIgnoreCase))
                                    .ToList();
                            }

                            // Serialize workflows to JSON for client-side rendering
                            ViewBag.WorkflowsJson = JsonConvert.SerializeObject(workflows);
                        }
                        catch (Exception ex)
                        {
                            // On error loading workflows, provide empty array and capture error message
                            // This prevents the component from failing entirely if the entity doesn't exist yet
                            ViewBag.WorkflowsJson = "[]";
                            ViewBag.LoadError = ex.Message;
                        }
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
                        // Unknown mode - display error view
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
                // Handle structured validation exceptions
                ViewBag.Error = ex;
                return await Task.FromResult<IViewComponentResult>(View("Error"));
            }
            catch (Exception ex)
            {
                // Handle unexpected exceptions by wrapping in ValidationException
                ViewBag.Error = new ValidationException()
                {
                    Message = ex.Message
                };
                return await Task.FromResult<IViewComponentResult>(View("Error"));
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Builds the fully qualified application URL from the HTTP context.
        /// </summary>
        /// <param name="context">The HTTP context containing request information.</param>
        /// <returns>
        /// The fully qualified application URL (scheme + host), or empty string if context is null.
        /// </returns>
        private string GetFullyQualifiedApplicationPath(HttpContext context)
        {
            if (context == null)
            {
                return string.Empty;
            }

            return string.Format("{0}://{1}",
                context.Request.Scheme,
                context.Request.Host);
        }

        #endregion
    }
}
