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
    /// Page component that displays real-time approval workflow metrics for managers.
    /// Provides a dashboard view with pending approvals count, average approval time,
    /// approval rate percentage, overdue requests count, and recent activity feed.
    /// Supports auto-refresh capability and date range filtering.
    /// </summary>
    [PageComponent(
        Label = "Approval Dashboard",
        Library = "WebVella",
        Description = "Real-time dashboard displaying team approval workflow metrics for managers",
        Version = "1.0.0",
        IconClass = "fas fa-chart-line",
        Category = "Approval Workflow")]
    public class PcApprovalDashboard : PageComponent
    {
        /// <summary>
        /// The ERP request context providing access to current user and page information.
        /// </summary>
        protected ErpRequestContext ErpRequestContext { get; set; }

        /// <summary>
        /// List of role names that are authorized to view the dashboard.
        /// </summary>
        private static readonly List<string> AuthorizedRoles = new List<string>
        {
            "manager",
            "administrator",
            "admin"
        };

        /// <summary>
        /// Initializes a new instance of the PcApprovalDashboard component.
        /// </summary>
        /// <param name="coreReqCtx">The ERP request context injected by the framework.</param>
        public PcApprovalDashboard([FromServices] ErpRequestContext coreReqCtx)
        {
            ErpRequestContext = coreReqCtx;
        }

        /// <summary>
        /// Configuration options for the Approval Dashboard component.
        /// These options can be configured through the page builder Options panel.
        /// </summary>
        public class PcApprovalDashboardOptions
        {
            /// <summary>
            /// Interval in seconds between automatic metric refreshes.
            /// Default is 60 seconds. Minimum allowed is 30 seconds.
            /// </summary>
            [JsonProperty(PropertyName = "refresh_interval")]
            public int RefreshInterval { get; set; } = 60;

            /// <summary>
            /// Default date range for metrics display.
            /// Valid values: "7d", "30d", "90d", "custom"
            /// </summary>
            [JsonProperty(PropertyName = "date_range_default")]
            public string DateRangeDefault { get; set; } = "30d";

            /// <summary>
            /// Whether to visually highlight overdue requests with an alert indicator.
            /// </summary>
            [JsonProperty(PropertyName = "show_overdue_alert")]
            public bool ShowOverdueAlert { get; set; } = true;

            /// <summary>
            /// Comma-separated list of metrics to display.
            /// Valid values: "pending", "avg_time", "approval_rate", "overdue", "recent"
            /// </summary>
            [JsonProperty(PropertyName = "metrics_to_display")]
            public string MetricsToDisplay { get; set; } = "pending,avg_time,approval_rate,overdue,recent";

            /// <summary>
            /// Custom title for the dashboard header. If empty, uses default title.
            /// </summary>
            [JsonProperty(PropertyName = "dashboard_title")]
            public string DashboardTitle { get; set; } = "Approval Dashboard";
        }

        /// <summary>
        /// Main entry point for the component. Handles render mode selection
        /// and prepares data for the appropriate view.
        /// </summary>
        /// <param name="context">The page component context containing node and data model information.</param>
        /// <returns>The appropriate view based on the component mode.</returns>
        public async Task<IViewComponentResult> InvokeAsync(PageComponentContext context)
        {
            ErpPage currentPage = null;

            try
            {
                #region Initialization

                // Validate component node
                if (context.Node == null)
                {
                    return await Task.FromResult<IViewComponentResult>(
                        Content("Error: The node Id is required to be set as query parameter 'nid', when requesting this component"));
                }

                // Get current page from data model
                var pageFromModel = context.DataModel.GetProperty("Page");
                if (pageFromModel == null)
                {
                    return await Task.FromResult<IViewComponentResult>(
                        Content("Error: PageModel cannot be null"));
                }
                
                if (pageFromModel is ErpPage page)
                {
                    currentPage = page;
                }
                else
                {
                    return await Task.FromResult<IViewComponentResult>(
                        Content("Error: PageModel does not have Page property or it is not from ErpPage Type"));
                }

                // Parse component options
                var options = new PcApprovalDashboardOptions();
                if (context.Options != null)
                {
                    options = JsonConvert.DeserializeObject<PcApprovalDashboardOptions>(
                        context.Options.ToString()) ?? new PcApprovalDashboardOptions();
                }

                // Ensure refresh interval is at least 30 seconds
                if (options.RefreshInterval < 30)
                {
                    options.RefreshInterval = 30;
                }

                // Get component metadata for page builder
                var componentMeta = new PageComponentLibraryService().GetComponentMeta(context.Node.ComponentName);

                #endregion

                #region Set ViewBag Properties

                ViewBag.Options = options;
                ViewBag.Node = context.Node;
                ViewBag.ComponentMeta = componentMeta;
                ViewBag.RequestContext = ErpRequestContext;
                ViewBag.AppContext = ErpAppContext.Current;
                ViewBag.ComponentContext = context;

                #endregion

                #region Role-Based Access Control

                // For Display and Design modes, validate manager role
                ErpUser currentUser = null;
                if (context.Mode == ComponentMode.Display || context.Mode == ComponentMode.Design)
                {
                    bool hasManagerRole = false;

                    // Get current user from data model
                    var currentUserObj = context.DataModel.GetProperty("CurrentUser");
                    if (currentUserObj != null && currentUserObj is ErpUser)
                    {
                        currentUser = (ErpUser)currentUserObj;
                        
                        // Check if current user has an authorized role
                        if (currentUser.Roles != null)
                        {
                            var userRoles = currentUser.Roles
                                .Select(r => r.Name?.ToLowerInvariant() ?? string.Empty);
                            
                            hasManagerRole = userRoles.Any(role => 
                                AuthorizedRoles.Contains(role));
                        }
                    }

                    // If in Display mode and user doesn't have manager role, show error
                    if (context.Mode == ComponentMode.Display && !hasManagerRole)
                    {
                        ViewBag.Error = new ValidationException
                        {
                            Message = "Access denied. You must have a Manager role to view this dashboard."
                        };
                        return await Task.FromResult<IViewComponentResult>(View("Error"));
                    }

                    ViewBag.HasManagerRole = hasManagerRole;
                    ViewBag.CurrentUser = currentUser;
                }

                #endregion

                #region Load Metrics Data (for Display and Design modes)

                if (context.Mode == ComponentMode.Display || context.Mode == ComponentMode.Design)
                {
                    // Calculate date range based on options
                    DateTime toDate = DateTime.UtcNow;
                    DateTime fromDate = CalculateFromDate(options.DateRangeDefault, toDate);

                    // Get metrics from service
                    var metricsService = new DashboardMetricsService();
                    var currentUserId = currentUser?.Id ?? Guid.Empty;
                    
                    var metrics = metricsService.GetDashboardMetrics(
                        currentUserId,
                        fromDate,
                        toDate);

                    ViewBag.Metrics = metrics;
                    ViewBag.FromDate = fromDate;
                    ViewBag.ToDate = toDate;

                    // Parse which metrics to display
                    var metricsToShow = options.MetricsToDisplay?
                        .Split(',')
                        .Select(m => m.Trim().ToLowerInvariant())
                        .ToList() ?? new List<string>();
                    
                    ViewBag.ShowPending = metricsToShow.Contains("pending");
                    ViewBag.ShowAvgTime = metricsToShow.Contains("avg_time");
                    ViewBag.ShowApprovalRate = metricsToShow.Contains("approval_rate");
                    ViewBag.ShowOverdue = metricsToShow.Contains("overdue");
                    ViewBag.ShowRecent = metricsToShow.Contains("recent");
                }

                #endregion

                #region Return Appropriate View

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
                        ViewBag.Error = new ValidationException
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
                ViewBag.Error = new ValidationException
                {
                    Message = $"An unexpected error occurred: {ex.Message}"
                };
                return await Task.FromResult<IViewComponentResult>(View("Error"));
            }
        }

        /// <summary>
        /// Calculates the start date based on the date range option.
        /// </summary>
        /// <param name="dateRangeOption">The date range option (7d, 30d, 90d, custom).</param>
        /// <param name="toDate">The end date to calculate from.</param>
        /// <returns>The calculated start date.</returns>
        private DateTime CalculateFromDate(string dateRangeOption, DateTime toDate)
        {
            return dateRangeOption?.ToLowerInvariant() switch
            {
                "7d" => toDate.AddDays(-7),
                "30d" => toDate.AddDays(-30),
                "90d" => toDate.AddDays(-90),
                _ => toDate.AddDays(-30) // Default to 30 days
            };
        }
    }
}
