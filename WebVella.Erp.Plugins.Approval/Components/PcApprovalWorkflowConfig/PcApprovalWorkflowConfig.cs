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

namespace WebVella.Erp.Plugins.Approval.Components
{
    /// <summary>
    /// PageComponent class for Approval Workflow Configuration.
    /// Provides administrative interface for managing approval workflow definitions, steps, and rules.
    /// Supports full CRUD operations and workflow enable/disable functionality.
    /// </summary>
    [PageComponent(
        Label = "Approval Workflow Config",
        Library = "WebVella",
        Description = "Manage approval workflow definitions, steps, and rules",
        Version = "0.0.1",
        IconClass = "fas fa-cogs",
        Category = "Approval Workflow")]
    public class PcApprovalWorkflowConfig : PageComponent
    {
        /// <summary>
        /// Gets or sets the ERP request context.
        /// </summary>
        protected ErpRequestContext ErpRequestContext { get; set; }

        /// <summary>
        /// Initializes a new instance of the PcApprovalWorkflowConfig component.
        /// </summary>
        /// <param name="coreReqCtx">The ERP request context injected by the framework.</param>
        public PcApprovalWorkflowConfig([FromServices] ErpRequestContext coreReqCtx)
        {
            ErpRequestContext = coreReqCtx;
        }

        /// <summary>
        /// Options class for configuring the Approval Workflow Config component.
        /// </summary>
        public class PcApprovalWorkflowConfigOptions
        {
            /// <summary>
            /// Gets or sets whether to show inactive (disabled) workflows.
            /// Default is false (only shows active workflows).
            /// </summary>
            [JsonProperty(PropertyName = "show_inactive")]
            public bool ShowInactive { get; set; } = false;

            /// <summary>
            /// Gets or sets the page size for pagination.
            /// Default is 10 items per page.
            /// </summary>
            [JsonProperty(PropertyName = "page_size")]
            public int PageSize { get; set; } = 10;

            /// <summary>
            /// Gets or sets the filter for target entity name.
            /// When set, only workflows targeting this entity are shown.
            /// Empty string means show all.
            /// </summary>
            [JsonProperty(PropertyName = "filter_by_entity")]
            public string FilterByEntity { get; set; } = "";
        }

        /// <summary>
        /// Invokes the component rendering based on the current mode.
        /// </summary>
        /// <param name="context">The page component context containing rendering information.</param>
        /// <returns>The view component result for the appropriate view.</returns>
        public async Task<IViewComponentResult> InvokeAsync(PageComponentContext context)
        {
            ErpPage currentPage = null;
            try
            {
                #region << Init >>
                if (context.Node == null)
                {
                    return await Task.FromResult<IViewComponentResult>(
                        Content("Error: The node Id is required to be set as query parameter 'nid', when requesting this component"));
                }

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

                var options = new PcApprovalWorkflowConfigOptions();
                if (context.Options != null)
                {
                    options = JsonConvert.DeserializeObject<PcApprovalWorkflowConfigOptions>(context.Options.ToString());
                }

                var componentMeta = new PageComponentLibraryService().GetComponentMeta(context.Node.ComponentName);
                #endregion

                ViewBag.Options = options;
                ViewBag.Node = context.Node;
                ViewBag.ComponentMeta = componentMeta;
                ViewBag.RequestContext = ErpRequestContext;
                ViewBag.AppContext = ErpAppContext.Current;
                ViewBag.ComponentContext = context;
                ViewBag.CurrentUser = SecurityContext.CurrentUser;
                ViewBag.CurrentUserJson = JsonConvert.SerializeObject(SecurityContext.CurrentUser);

                // Build site root URL from HttpContext
                HttpContext httpContext = null;
                if (ErpRequestContext.PageContext != null)
                {
                    httpContext = ErpRequestContext.PageContext.HttpContext;
                }
                ViewBag.SiteRootUrl = GetFullyQualifiedApplicationPath(httpContext);

                // For Display mode, load workflow data
                if (context.Mode == ComponentMode.Display)
                {
                    try
                    {
                        var workflowService = new WorkflowConfigService();
                        var workflows = workflowService.GetAll(options.ShowInactive);

                        // Apply entity filter if specified
                        if (!string.IsNullOrWhiteSpace(options.FilterByEntity))
                        {
                            workflows = workflows.Where(w => 
                                w.TargetEntityName.Equals(options.FilterByEntity, StringComparison.OrdinalIgnoreCase))
                                .ToList();
                        }

                        ViewBag.WorkflowsJson = JsonConvert.SerializeObject(workflows);
                    }
                    catch (Exception ex)
                    {
                        // On error loading workflows, set empty array
                        ViewBag.WorkflowsJson = "[]";
                        ViewBag.LoadError = ex.Message;
                    }
                }

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
        /// Gets the fully qualified application path including scheme and host.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>The fully qualified application path.</returns>
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
    }
}
