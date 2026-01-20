using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Exceptions;
using WebVella.Erp.Plugins.Approval.Api;
using WebVella.Erp.Plugins.Approval.Services;
using WebVella.Erp.Web;
using WebVella.Erp.Web.Models;
using WebVella.Erp.Web.Services;

namespace WebVella.Erp.Plugins.Approval.Components
{
    /// <summary>
    /// Manager Approval Dashboard PageComponent for WebVella ERP.
    /// Displays real-time approval workflow metrics including:
    /// - Pending approvals count
    /// - Average approval time
    /// - Approval rate percentage
    /// - Overdue requests count
    /// - Recent activity feed
    /// 
    /// Supports auto-refresh at configurable intervals and date range filtering.
    /// Requires Manager or Administrator role for access.
    /// </summary>
    [PageComponent(
        Label = "Approval Dashboard",
        Library = "WebVella",
        Description = "Real-time approval workflow metrics dashboard for managers",
        Version = "0.0.1",
        IconClass = "fas fa-tachometer-alt",
        Category = "Approval Workflow")]
    public class PcApprovalDashboard : PageComponent
    {
        /// <summary>
        /// ERP request context for accessing current page and application state.
        /// </summary>
        protected ErpRequestContext ErpRequestContext { get; set; }

        /// <summary>
        /// Creates a new instance of the PcApprovalDashboard component.
        /// </summary>
        /// <param name="coreReqCtx">ERP request context injected by the framework</param>
        public PcApprovalDashboard([FromServices] ErpRequestContext coreReqCtx)
        {
            ErpRequestContext = coreReqCtx;
        }

        /// <summary>
        /// Configuration options for the Approval Dashboard component.
        /// These options can be set through the page builder Options panel.
        /// </summary>
        public class PcApprovalDashboardOptions
        {
            /// <summary>
            /// Auto-refresh interval in seconds. Set to 0 to disable auto-refresh.
            /// Default: 60 seconds. Minimum: 30 seconds (unless 0 to disable).
            /// </summary>
            [JsonProperty(PropertyName = "refresh_interval")]
            public int RefreshInterval { get; set; } = 60;

            /// <summary>
            /// Default date range in days for calculating time-based metrics.
            /// Default: 30 days. Valid values: 7, 30, 90, or custom positive integer.
            /// </summary>
            [JsonProperty(PropertyName = "default_date_range")]
            public int DefaultDateRange { get; set; } = 30;

            /// <summary>
            /// Whether to display the Pending Approvals metric card.
            /// </summary>
            [JsonProperty(PropertyName = "show_pending")]
            public bool ShowPending { get; set; } = true;

            /// <summary>
            /// Whether to display the Average Approval Time metric card.
            /// </summary>
            [JsonProperty(PropertyName = "show_avg_time")]
            public bool ShowAvgTime { get; set; } = true;

            /// <summary>
            /// Whether to display the Approval Rate metric card.
            /// </summary>
            [JsonProperty(PropertyName = "show_approval_rate")]
            public bool ShowApprovalRate { get; set; } = true;

            /// <summary>
            /// Whether to display the Overdue Requests metric card.
            /// </summary>
            [JsonProperty(PropertyName = "show_overdue")]
            public bool ShowOverdue { get; set; } = true;

            /// <summary>
            /// Whether to display the Recent Activity feed section.
            /// </summary>
            [JsonProperty(PropertyName = "show_activity")]
            public bool ShowActivity { get; set; } = true;

            /// <summary>
            /// Number of recent activities to display in the feed.
            /// Default: 5. Maximum: 20.
            /// </summary>
            [JsonProperty(PropertyName = "activity_count")]
            public int ActivityCount { get; set; } = 5;

            /// <summary>
            /// Data source for conditional visibility. Can be a boolean or string "true"/"false".
            /// </summary>
            [JsonProperty(PropertyName = "is_visible")]
            public string IsVisible { get; set; } = "";
        }

        /// <summary>
        /// Invokes the component and returns the appropriate view based on the render mode.
        /// </summary>
        /// <param name="context">Page component context containing node, options, and data model</param>
        /// <returns>View result for the appropriate render mode</returns>
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

                var options = new PcApprovalDashboardOptions();
                if (context.Options != null)
                {
                    options = JsonConvert.DeserializeObject<PcApprovalDashboardOptions>(context.Options.ToString());
                }

                // Validate and normalize options
                if (options.RefreshInterval > 0 && options.RefreshInterval < 30)
                {
                    options.RefreshInterval = 30; // Minimum 30 seconds
                }
                if (options.DefaultDateRange < 1)
                {
                    options.DefaultDateRange = 30;
                }
                if (options.DefaultDateRange > 365)
                {
                    options.DefaultDateRange = 365;
                }
                if (options.ActivityCount < 1)
                {
                    options.ActivityCount = 1;
                }
                if (options.ActivityCount > 20)
                {
                    options.ActivityCount = 20;
                }

                var componentMeta = new PageComponentLibraryService().GetComponentMeta(context.Node.ComponentName);
                #endregion

                ViewBag.Options = options;
                ViewBag.Node = context.Node;
                ViewBag.ComponentMeta = componentMeta;
                ViewBag.RequestContext = ErpRequestContext;
                ViewBag.AppContext = ErpAppContext.Current;
                ViewBag.ComponentContext = context;

                if (context.Mode != ComponentMode.Options && context.Mode != ComponentMode.Help)
                {
                    // Handle visibility data source
                    var isVisible = true;
                    var isVisibleDS = context.DataModel.GetPropertyValueByDataSource(options.IsVisible);
                    if (isVisibleDS is string && !String.IsNullOrWhiteSpace(isVisibleDS.ToString()))
                    {
                        if (Boolean.TryParse(isVisibleDS.ToString(), out bool outBool))
                        {
                            isVisible = outBool;
                        }
                    }
                    else if (isVisibleDS is Boolean)
                    {
                        isVisible = (bool)isVisibleDS;
                    }
                    ViewBag.IsVisible = isVisible;

                    // Check Manager role for Display mode
                    if (context.Mode == ComponentMode.Display)
                    {
                        var currentUser = SecurityContext.CurrentUser;
                        var hasAccess = false;

                        if (currentUser != null && currentUser.Roles != null)
                        {
                            hasAccess = currentUser.Roles.Any(r =>
                                string.Equals(r.Name, "manager", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(r.Name, "administrator", StringComparison.OrdinalIgnoreCase));
                        }

                        ViewBag.HasAccess = hasAccess;

                        if (hasAccess)
                        {
                            // Load initial metrics for server-side rendering
                            try
                            {
                                var metricsService = new ApprovalMetricsService();
                                var metrics = metricsService.GetDashboardMetrics(
                                    currentUser.Id,
                                    options.DefaultDateRange,
                                    options.ActivityCount);
                                ViewBag.Metrics = metrics;
                            }
                            catch (Exception)
                            {
                                // If metrics fail to load, create empty metrics
                                ViewBag.Metrics = new DashboardMetricsModel
                                {
                                    DateRangeDays = options.DefaultDateRange,
                                    MetricsAsOf = DateTime.UtcNow,
                                    PendingApprovalsCount = 0,
                                    AverageApprovalTimeHours = -1,
                                    ApprovalRatePercent = 0,
                                    OverdueRequestsCount = 0,
                                    RecentActivity = new List<RecentActivityModel>()
                                };
                            }
                        }
                    }

                    // Prepare date range options for dropdown
                    var dateRangeOptions = new List<SelectOption>
                    {
                        new SelectOption { Value = "7", Label = "Last 7 days" },
                        new SelectOption { Value = "30", Label = "Last 30 days" },
                        new SelectOption { Value = "90", Label = "Last 90 days" },
                        new SelectOption { Value = "custom", Label = "Custom range" }
                    };
                    ViewBag.DateRangeOptions = dateRangeOptions;
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
        /// Helper class for select option rendering.
        /// </summary>
        public class SelectOption
        {
            public string Value { get; set; }
            public string Label { get; set; }
        }
    }
}
