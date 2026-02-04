/*
 * PcApprovalWorkflowConfig.cs
 * 
 * Purpose: PageComponent class for admin workflow configuration management.
 * Provides the workflow, step, and rule configuration interface for the Approval plugin.
 * 
 * Requirements: STORY-008 - UI component specifications
 */
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Diagnostics;
using WebVella.Erp.Eql;
using WebVella.Erp.Exceptions;
using WebVella.Erp.Plugins.Approval.Model;
using WebVella.Erp.Plugins.Approval.Utils;
using WebVella.Erp.Web;
using WebVella.Erp.Web.Models;
using WebVella.Erp.Web.Services;

namespace WebVella.Erp.Plugins.Approval.Components
{
    /// <summary>
    /// PageComponent for managing approval workflows, steps, and rules.
    /// Provides admin interface for creating, editing, and deleting workflow configurations.
    /// </summary>
    [PageComponent(Label = "Approval Workflow Config", Library = "WebVella", 
        Description = "Admin component for managing approval workflows, steps, and rules", 
        Version = "0.0.1", IconClass = "fas fa-sitemap")]
    public class PcApprovalWorkflowConfig : PageComponent
    {
        /// <summary>
        /// Gets or sets the ERP request context injected via DI.
        /// </summary>
        protected ErpRequestContext ErpRequestContext { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PcApprovalWorkflowConfig"/> class.
        /// </summary>
        /// <param name="coreReqCtx">The ERP request context from dependency injection.</param>
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
            /// Gets or sets the entity name filter for workflows.
            /// When set, only shows workflows for the specified entity.
            /// </summary>
            [JsonProperty(PropertyName = "filter_entity")]
            public string FilterEntity { get; set; } = "";

            /// <summary>
            /// Gets or sets whether to show inactive workflows in the list.
            /// </summary>
            [JsonProperty(PropertyName = "show_inactive")]
            public bool ShowInactive { get; set; } = false;

            /// <summary>
            /// Gets or sets the default view mode (list or grid).
            /// </summary>
            [JsonProperty(PropertyName = "default_view")]
            public string DefaultView { get; set; } = "list";

            /// <summary>
            /// Gets or sets the number of workflows to display per page.
            /// </summary>
            [JsonProperty(PropertyName = "page_size")]
            public int PageSize { get; set; } = 10;
        }

        /// <summary>
        /// Invokes the component and returns the appropriate view based on the context mode.
        /// </summary>
        /// <param name="context">The page component context containing mode, node, and options.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the view component result.</returns>
        public async Task<IViewComponentResult> InvokeAsync(PageComponentContext context)
        {
            ErpPage currentPage = null;
            try
            {
                #region << Init >>
                if (context.Node == null)
                {
                    return await Task.FromResult<IViewComponentResult>(Content("Error: The node Id is required to be set as query parameter 'nid', when requesting this component"));
                }

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

                // For Display and Design modes, load workflow data
                if (context.Mode != ComponentMode.Options && context.Mode != ComponentMode.Help)
                {
                    var workflows = GetWorkflows(options);
                    ViewBag.WorkflowsJson = JsonConvert.SerializeObject(workflows);
                    ViewBag.ApproverTypesJson = JsonConvert.SerializeObject(GetEnumSelectOptions<ApproverType>());
                    ViewBag.RuleOperatorsJson = JsonConvert.SerializeObject(GetEnumSelectOptions<RuleOperator>());
                    ViewBag.ApprovalStatusesJson = JsonConvert.SerializeObject(GetEnumSelectOptions<ApprovalStatus>());
                    ViewBag.ApprovalActionTypesJson = JsonConvert.SerializeObject(GetEnumSelectOptions<ApprovalActionType>());
                    
                    HttpContext httpContext = null;
                    if (ErpRequestContext.PageContext != null)
                    {
                        httpContext = ErpRequestContext.PageContext.HttpContext;
                    }
                    ViewBag.SiteRootUrl = ApprovalUtils.FullyQualifiedApplicationPath(httpContext);
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
        /// Retrieves workflows from the database with optional filtering.
        /// </summary>
        /// <param name="options">The component options containing filter settings.</param>
        /// <returns>A list of workflow entity records.</returns>
        private List<EntityRecord> GetWorkflows(PcApprovalWorkflowConfigOptions options)
        {
            var result = new List<EntityRecord>();
            try
            {
                // Build EQL query with filters
                var eql = "SELECT * FROM approval_workflow";
                var conditions = new List<string>();

                // Apply entity name filter if specified
                if (!string.IsNullOrWhiteSpace(options.FilterEntity))
                {
                    conditions.Add("entity_name = @entity_name");
                }

                // Apply inactive filter unless ShowInactive is true
                if (!options.ShowInactive)
                {
                    conditions.Add("is_active = @is_active");
                }

                if (conditions.Count > 0)
                {
                    eql += " WHERE " + string.Join(" AND ", conditions);
                }

                eql += " ORDER BY name ASC";

                var parameters = new List<EqlParameter>();
                if (!string.IsNullOrWhiteSpace(options.FilterEntity))
                {
                    parameters.Add(new EqlParameter("entity_name", options.FilterEntity));
                }
                if (!options.ShowInactive)
                {
                    parameters.Add(new EqlParameter("is_active", true));
                }

                var command = new EqlCommand(eql, parameters.ToArray());
                result = command.Execute();
            }
            catch (Exception ex)
            {
                // Log error but return empty list to avoid crashing the component
                new Log().Create(LogType.Error, "PcApprovalWorkflowConfig", ex);
            }
            return result ?? new List<EntityRecord>();
        }

        /// <summary>
        /// Converts an enum type to a list of select options for dropdowns.
        /// </summary>
        /// <typeparam name="T">The enum type to convert.</typeparam>
        /// <returns>A list of select options with Value and Label from enum names.</returns>
        private List<SelectOption> GetEnumSelectOptions<T>() where T : struct, Enum
        {
            return Enum.GetValues<T>()
                .Select(e => new SelectOption
                {
                    Value = e.ToString(),
                    Label = e.ToString()
                })
                .ToList();
        }
    }
}
