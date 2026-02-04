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
    /// This component renders different views based on the component mode (Display, Design, Options, Help, Error).
    /// </summary>
    /// <remarks>
    /// The component loads workflow data from the approval_workflow entity and provides
    /// JSON-serialized data for client-side rendering and interaction. It supports filtering
    /// by entity name and showing/hiding inactive workflows based on configuration options.
    /// </remarks>
    [PageComponent(Label = "Approval Workflow Config", Library = "WebVella", 
        Description = "Admin component for managing approval workflows, steps, and rules", 
        Version = "0.0.1", IconClass = "fas fa-sitemap")]
    public class PcApprovalWorkflowConfig : PageComponent
    {
        /// <summary>
        /// Gets or sets the ERP request context injected via dependency injection.
        /// Provides access to the current request context including page context and HTTP context.
        /// </summary>
        protected ErpRequestContext ErpRequestContext { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PcApprovalWorkflowConfig"/> class.
        /// </summary>
        /// <param name="coreReqCtx">The ERP request context from dependency injection.</param>
        /// <exception cref="ArgumentNullException">Thrown when coreReqCtx is null.</exception>
        public PcApprovalWorkflowConfig([FromServices] ErpRequestContext coreReqCtx)
        {
            ErpRequestContext = coreReqCtx ?? throw new ArgumentNullException(nameof(coreReqCtx));
        }

        /// <summary>
        /// Options class for configuring the Approval Workflow Config component.
        /// These options are serialized/deserialized from the component's configuration in the page builder.
        /// </summary>
        public class PcApprovalWorkflowConfigOptions
        {
            /// <summary>
            /// Gets or sets the entity name filter for workflows.
            /// When set, only shows workflows configured for the specified entity.
            /// Leave empty to show all workflows regardless of target entity.
            /// </summary>
            [JsonProperty(PropertyName = "filter_entity")]
            public string FilterEntity { get; set; } = "";

            /// <summary>
            /// Gets or sets whether to show inactive workflows in the list.
            /// Default is false, meaning only active workflows are displayed.
            /// Set to true to include deactivated workflows in the configuration interface.
            /// </summary>
            [JsonProperty(PropertyName = "show_inactive")]
            public bool ShowInactive { get; set; } = false;

            /// <summary>
            /// Gets or sets the default view mode for the workflow list (list or grid).
            /// Controls how workflows are displayed in the configuration interface.
            /// </summary>
            [JsonProperty(PropertyName = "default_view")]
            public string DefaultView { get; set; } = "list";

            /// <summary>
            /// Gets or sets the number of workflows to display per page.
            /// Controls pagination in the workflow list view.
            /// </summary>
            [JsonProperty(PropertyName = "page_size")]
            public int PageSize { get; set; } = 10;
        }

        /// <summary>
        /// Invokes the component and returns the appropriate view based on the context mode.
        /// Loads workflow data for Display and Design modes, and provides configuration UI for Options mode.
        /// </summary>
        /// <param name="context">The page component context containing mode, node, options, and data model.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the view component result.</returns>
        /// <exception cref="ValidationException">Thrown when validation fails or when the component encounters an error.</exception>
        public async Task<IViewComponentResult> InvokeAsync(PageComponentContext context)
        {
            ErpPage currentPage = null;
            try
            {
                #region << Init >>
                // Validate that the component node is provided
                if (context.Node == null)
                {
                    return await Task.FromResult<IViewComponentResult>(Content("Error: The node Id is required to be set as query parameter 'nid', when requesting this component"));
                }

                // Validate and retrieve the page from the data model
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
                var options = new PcApprovalWorkflowConfigOptions();
                if (context.Options != null)
                {
                    options = JsonConvert.DeserializeObject<PcApprovalWorkflowConfigOptions>(context.Options.ToString());
                }

                // Get component metadata from the library service
                var componentMeta = new PageComponentLibraryService().GetComponentMeta(context.Node.ComponentName);
                #endregion

                #region << Populate ViewBag with common data >>
                ViewBag.Options = options;
                ViewBag.Node = context.Node;
                ViewBag.ComponentMeta = componentMeta;
                ViewBag.RequestContext = ErpRequestContext;
                ViewBag.AppContext = ErpAppContext.Current;
                ViewBag.ComponentContext = context;
                ViewBag.CurrentUser = SecurityContext.CurrentUser;
                ViewBag.CurrentUserJson = JsonConvert.SerializeObject(SecurityContext.CurrentUser);
                #endregion

                #region << Load data for Display and Design modes >>
                // For Display and Design modes, load workflow data from the database
                if (context.Mode != ComponentMode.Options && context.Mode != ComponentMode.Help)
                {
                    // Load workflows based on the configured options
                    var workflows = GetWorkflows(options);
                    ViewBag.WorkflowsJson = JsonConvert.SerializeObject(workflows);
                    
                    // Provide enum options for the configuration dropdowns
                    ViewBag.ApproverTypesJson = JsonConvert.SerializeObject(GetEnumSelectOptions<ApproverType>());
                    ViewBag.RuleOperatorsJson = JsonConvert.SerializeObject(GetEnumSelectOptions<RuleOperator>());
                    
                    // Build the site root URL for AJAX requests
                    HttpContext httpContext = null;
                    if (ErpRequestContext.PageContext != null)
                    {
                        httpContext = ErpRequestContext.PageContext.HttpContext;
                    }
                    ViewBag.SiteRootUrl = ApprovalUtils.FullyQualifiedApplicationPath(httpContext);
                }
                #endregion

                #region << Return appropriate view based on mode >>
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
                // Handle validation exceptions by displaying the error view
                ViewBag.Error = ex;
                return await Task.FromResult<IViewComponentResult>(View("Error"));
            }
            catch (Exception ex)
            {
                // Wrap general exceptions in a ValidationException for consistent error handling
                ViewBag.Error = new ValidationException()
                {
                    Message = ex.Message
                };
                return await Task.FromResult<IViewComponentResult>(View("Error"));
            }
        }

        /// <summary>
        /// Retrieves workflows from the database with optional filtering.
        /// Queries the approval_workflow entity and applies filters based on component options.
        /// </summary>
        /// <param name="options">The component options containing filter settings.</param>
        /// <returns>A list of workflow entity records matching the filter criteria, sorted by name.</returns>
        private List<EntityRecord> GetWorkflows(PcApprovalWorkflowConfigOptions options)
        {
            var result = new List<EntityRecord>();
            try
            {
                // Build EQL query with dynamic conditions based on options
                var eql = "SELECT * FROM approval_workflow";
                var conditions = new List<string>();

                // Apply entity name filter if specified
                if (!string.IsNullOrWhiteSpace(options.FilterEntity))
                {
                    conditions.Add("entity_name = @entity_name");
                }

                // Apply active status filter unless ShowInactive is enabled
                if (!options.ShowInactive)
                {
                    conditions.Add("is_active = @is_active");
                }

                // Append WHERE clause if any conditions exist
                if (conditions.Count > 0)
                {
                    eql += " WHERE " + string.Join(" AND ", conditions);
                }

                // Always sort by name for consistent ordering
                eql += " ORDER BY name ASC";

                // Build parameter list for the query
                var parameters = new List<EqlParameter>();
                if (!string.IsNullOrWhiteSpace(options.FilterEntity))
                {
                    parameters.Add(new EqlParameter("entity_name", options.FilterEntity));
                }
                if (!options.ShowInactive)
                {
                    parameters.Add(new EqlParameter("is_active", true));
                }

                // Execute the query and retrieve results
                var command = new EqlCommand(eql, parameters.ToArray());
                result = command.Execute();
            }
            catch (Exception)
            {
                // Return empty list on error to prevent component from crashing
                // The error will be visible in the UI as an empty workflow list
                result = new List<EntityRecord>();
            }
            return result ?? new List<EntityRecord>();
        }

        /// <summary>
        /// Converts an enum type to a list of select options for dropdown controls.
        /// Each enum value becomes a SelectOption with Value and Label properties.
        /// </summary>
        /// <typeparam name="T">The enum type to convert. Must be a struct and an Enum.</typeparam>
        /// <returns>A list of SelectOption objects representing all enum values.</returns>
        /// <remarks>
        /// The Label is derived from the enum value name, converted to a more readable format.
        /// For better display labels, consider using the SelectOptionAttribute on enum values.
        /// </remarks>
        private List<SelectOption> GetEnumSelectOptions<T>() where T : struct, Enum
        {
            var selectOptions = new List<SelectOption>();
            
            foreach (var enumValue in Enum.GetValues<T>())
            {
                // Get the enum name as the default label
                var enumName = enumValue.ToString();
                var label = enumName;
                
                // Check for SelectOptionAttribute to get custom label
                var memberInfo = typeof(T).GetMember(enumName).FirstOrDefault();
                if (memberInfo != null)
                {
                    var attribute = memberInfo.GetCustomAttributes(typeof(SelectOptionAttribute), false)
                        .Cast<SelectOptionAttribute>()
                        .FirstOrDefault();
                    if (attribute != null && !string.IsNullOrEmpty(attribute.Label))
                    {
                        label = attribute.Label;
                    }
                }
                
                selectOptions.Add(new SelectOption
                {
                    Value = enumName,
                    Label = label
                });
            }
            
            return selectOptions;
        }
    }
}
