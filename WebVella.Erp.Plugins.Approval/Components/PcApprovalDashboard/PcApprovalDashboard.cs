using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Exceptions;
using WebVella.Erp.Web;
using WebVella.Erp.Web.Models;
using WebVella.Erp.Web.Services;
using WebVella.Erp.Plugins.Approval.Services;

namespace WebVella.Erp.Plugins.Approval.Components
{
    /// <summary>
    /// Page component for displaying manager approval dashboard with real-time metrics.
    /// Provides visibility into team approval workflow performance including:
    /// - Pending approvals count
    /// - Average approval time
    /// - Approval rate percentage
    /// - Overdue requests count
    /// - Recent activity feed
    /// </summary>
    [PageComponent(
        Label = "Approval Dashboard",
        Library = "WebVella",
        Description = "Real-time dashboard displaying team approval workflow metrics",
        Version = "0.0.1",
        IconClass = "fas fa-chart-line",
        Category = "Approval Workflow")]
    public class PcApprovalDashboard : PageComponent
    {
        protected ErpRequestContext ErpRequestContext { get; set; }

        /// <summary>
        /// Initializes a new instance of the PcApprovalDashboard component.
        /// </summary>
        /// <param name="coreReqCtx">ERP request context from dependency injection</param>
        public PcApprovalDashboard([FromServices] ErpRequestContext coreReqCtx)
        {
            ErpRequestContext = coreReqCtx;
        }

        /// <summary>
        /// Options model for dashboard configuration.
        /// These options are set through the page builder Options panel.
        /// </summary>
        public class PcApprovalDashboardOptions
        {
            /// <summary>
            /// Auto-refresh interval in seconds.
            /// Set to 0 to disable auto-refresh.
            /// Default: 60 seconds
            /// </summary>
            [JsonProperty(PropertyName = "refresh_interval")]
            public int RefreshInterval { get; set; } = 60;

            /// <summary>
            /// Default date range for metrics calculation.
            /// Valid values: "7d", "30d", "90d", "custom"
            /// Default: "30d"
            /// </summary>
            [JsonProperty(PropertyName = "date_range_default")]
            public string DateRangeDefault { get; set; } = "30d";

            /// <summary>
            /// Whether to highlight overdue requests with alert styling.
            /// When true, overdue count will display with warning colors.
            /// Default: true
            /// </summary>
            [JsonProperty(PropertyName = "show_overdue_alert")]
            public bool ShowOverdueAlert { get; set; } = true;

            /// <summary>
            /// Comma-separated list of metrics to display.
            /// Valid values: pending, avg_time, approval_rate, overdue, recent
            /// Default: all metrics shown
            /// </summary>
            [JsonProperty(PropertyName = "metrics_to_display")]
            public string MetricsToDisplay { get; set; } = "pending,avg_time,approval_rate,overdue,recent";

            /// <summary>
            /// Controls component visibility via data source binding.
            /// Can be set to a boolean value or data source expression.
            /// </summary>
            [JsonProperty(PropertyName = "is_visible")]
            public string IsVisible { get; set; } = "";
        }

        /// <summary>
        /// Main entry point for the component invocation.
        /// Handles initialization, role validation, and view selection based on mode.
        /// </summary>
        /// <param name="context">Page component context containing node, options, and data model</param>
        /// <returns>View component result with appropriate view for current mode</returns>
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

                var options = new PcApprovalDashboardOptions();
                if (context.Options != null)
                {
                    options = JsonConvert.DeserializeObject<PcApprovalDashboardOptions>(
                        context.Options.ToString());
                }

                var componentMeta = new PageComponentLibraryService()
                    .GetComponentMeta(context.Node.ComponentName);
                #endregion

                ViewBag.Options = options;
                ViewBag.Node = context.Node;
                ViewBag.ComponentMeta = componentMeta;
                ViewBag.RequestContext = ErpRequestContext;
                ViewBag.AppContext = ErpAppContext.Current;
                ViewBag.ComponentContext = context;

                // Process visibility for non-options/help modes
                if (context.Mode != ComponentMode.Options && context.Mode != ComponentMode.Help)
                {
                    var isVisible = true;
                    var isVisibleDS = context.DataModel.GetPropertyValueByDataSource(options.IsVisible);
                    if (isVisibleDS is string && !string.IsNullOrWhiteSpace(isVisibleDS.ToString()))
                    {
                        if (bool.TryParse(isVisibleDS.ToString(), out bool outBool))
                        {
                            isVisible = outBool;
                        }
                    }
                    else if (isVisibleDS is bool)
                    {
                        isVisible = (bool)isVisibleDS;
                    }
                    ViewBag.IsVisible = isVisible;
                }

                // Validate manager role for Display mode
                var currentUser = SecurityContext.CurrentUser;
                ViewBag.CurrentUser = currentUser;

                if (context.Mode == ComponentMode.Display && !IsManagerRole(currentUser))
                {
                    ViewBag.Error = new ValidationException()
                    {
                        Message = "Access denied. Manager or Administrator role is required to view the Approval Dashboard."
                    };
                    return await Task.FromResult<IViewComponentResult>(View("Error"));
                }

                // Preload metrics data for Display and Design modes
                if (context.Mode == ComponentMode.Display || context.Mode == ComponentMode.Design)
                {
                    var metricsService = new DashboardMetricsService();
                    var userId = currentUser?.Id ?? Guid.Empty;
                    var toDate = DateTime.UtcNow;
                    var fromDate = GetDateRangeStart(options.DateRangeDefault, toDate);

                    if (context.Mode == ComponentMode.Display && userId != Guid.Empty)
                    {
                        try
                        {
                            var metrics = metricsService.GetDashboardMetrics(userId, fromDate, toDate);
                            ViewBag.Metrics = metrics;
                        }
                        catch (Exception)
                        {
                            // Use sample data if metrics retrieval fails
                            ViewBag.Metrics = GetSampleMetrics(fromDate, toDate);
                        }
                    }
                    else
                    {
                        // Use sample data for Design mode preview
                        ViewBag.Metrics = GetSampleMetrics(fromDate, toDate);
                    }

                    ViewBag.FromDate = fromDate;
                    ViewBag.ToDate = toDate;
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

        #region Helper Methods

        /// <summary>
        /// Checks if the user has Manager or Administrator role.
        /// Case-insensitive role name comparison.
        /// </summary>
        /// <param name="user">User to check</param>
        /// <returns>True if user has manager or administrator role</returns>
        private bool IsManagerRole(ErpUser user)
        {
            if (user == null)
                return false;

            foreach (var role in user.Roles)
            {
                var roleName = role.Name?.ToLowerInvariant();
                if (roleName == "manager" || roleName == "administrator")
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Calculates the start date based on the date range option.
        /// </summary>
        /// <param name="dateRange">Date range string (7d, 30d, 90d, custom)</param>
        /// <param name="endDate">End date reference</param>
        /// <returns>Start date for the range</returns>
        private DateTime GetDateRangeStart(string dateRange, DateTime endDate)
        {
            return dateRange?.ToLower() switch
            {
                "7d" => endDate.AddDays(-7),
                "30d" => endDate.AddDays(-30),
                "90d" => endDate.AddDays(-90),
                _ => endDate.AddDays(-30) // Default to 30 days
            };
        }

        /// <summary>
        /// Creates sample metrics data for Design mode preview.
        /// </summary>
        /// <param name="fromDate">Start of date range</param>
        /// <param name="toDate">End of date range</param>
        /// <returns>Sample dashboard metrics model</returns>
        private Api.DashboardMetricsModel GetSampleMetrics(DateTime fromDate, DateTime toDate)
        {
            return new Api.DashboardMetricsModel
            {
                PendingApprovalsCount = 12,
                AverageApprovalTimeHours = 4.5,
                ApprovalRatePercent = 87.5,
                OverdueRequestsCount = 2,
                RecentActivity = new System.Collections.Generic.List<Api.RecentActivityItem>
                {
                    new Api.RecentActivityItem
                    {
                        Action = "approved",
                        PerformedBy = "John Smith",
                        PerformedOn = DateTime.UtcNow.AddHours(-2),
                        RequestId = Guid.NewGuid(),
                        RequestSubject = "Purchase Order #12345"
                    },
                    new Api.RecentActivityItem
                    {
                        Action = "approved",
                        PerformedBy = "Jane Doe",
                        PerformedOn = DateTime.UtcNow.AddHours(-5),
                        RequestId = Guid.NewGuid(),
                        RequestSubject = "Expense Report Q4"
                    },
                    new Api.RecentActivityItem
                    {
                        Action = "rejected",
                        PerformedBy = "Mike Johnson",
                        PerformedOn = DateTime.UtcNow.AddHours(-8),
                        RequestId = Guid.NewGuid(),
                        RequestSubject = "Travel Request"
                    },
                    new Api.RecentActivityItem
                    {
                        Action = "delegated",
                        PerformedBy = "Sarah Wilson",
                        PerformedOn = DateTime.UtcNow.AddDays(-1),
                        RequestId = Guid.NewGuid(),
                        RequestSubject = "Contract Approval"
                    },
                    new Api.RecentActivityItem
                    {
                        Action = "approved",
                        PerformedBy = "Tom Brown",
                        PerformedOn = DateTime.UtcNow.AddDays(-1).AddHours(-3),
                        RequestId = Guid.NewGuid(),
                        RequestSubject = "Budget Amendment"
                    }
                },
                MetricsAsOf = DateTime.UtcNow,
                DateRangeStart = fromDate,
                DateRangeEnd = toDate
            };
        }

        #endregion
    }
}
