using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Exceptions;
using WebVella.Erp.Plugins.Approval.Model;
using WebVella.Erp.Plugins.Approval.Services;
using WebVella.Erp.Web;
using WebVella.Erp.Web.Models;
using WebVella.Erp.Web.Services;
using WebVella.TagHelpers.Models;

namespace WebVella.Erp.Plugins.Approval.Components
{
    /// <summary>
    /// Dashboard PageComponent for displaying real-time approval workflow metrics.
    /// Provides managerial oversight with 5 key metric cards: pending count, average approval time,
    /// approval rate percentage, overdue count, and total processed count.
    /// </summary>
    [PageComponent(Label = "Approval Dashboard", Library = "WebVella", Description = "Dashboard displaying real-time approval workflow metrics for manager oversight", Version = "0.0.1", IconClass = "fas fa-tachometer-alt")]
    public class PcApprovalDashboard : PageComponent
    {
        /// <summary>
        /// Gets or sets the ERP request context injected by the DI container.
        /// Provides access to the current user, request information, and security context.
        /// </summary>
        protected ErpRequestContext ErpRequestContext { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PcApprovalDashboard"/> class.
        /// </summary>
        /// <param name="coreReqCtx">The ERP request context injected by ASP.NET Core DI.</param>
        public PcApprovalDashboard([FromServices] ErpRequestContext coreReqCtx)
        {
            ErpRequestContext = coreReqCtx;
        }

        /// <summary>
        /// Options class for configuring the Approval Dashboard component.
        /// Serialized to/from JSON for persistence in page designer.
        /// </summary>
        public class PcApprovalDashboardOptions
        {
            /// <summary>
            /// Gets or sets the number of days to look back for metrics calculation.
            /// Default is 30 days.
            /// </summary>
            [JsonProperty(PropertyName = "days_back")]
            public int DaysBack { get; set; } = 30;

            /// <summary>
            /// Gets or sets a value indicating whether to show charts in the dashboard.
            /// Default is true.
            /// </summary>
            [JsonProperty(PropertyName = "show_charts")]
            public bool ShowCharts { get; set; } = true;

            /// <summary>
            /// Gets or sets the auto-refresh interval in seconds.
            /// Set to 0 to disable auto-refresh. Default is 0 (disabled).
            /// </summary>
            [JsonProperty(PropertyName = "refresh_interval")]
            public int RefreshInterval { get; set; } = 0;
        }

        /// <summary>
        /// Invokes the component asynchronously to render the appropriate view based on the component mode.
        /// </summary>
        /// <param name="context">The page component context containing node, mode, and data model information.</param>
        /// <returns>A task that represents the asynchronous operation, containing the view component result.</returns>
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
                    // Calculate date range for metrics
                    var toDate = DateTime.UtcNow;
                    var fromDate = toDate.AddDays(-options.DaysBack);

                    // Get current user ID for user-specific metrics
                    Guid? userId = null;
                    if (SecurityContext.CurrentUser != null)
                    {
                        userId = SecurityContext.CurrentUser.Id;
                    }

                    // Create metrics model with calculated values
                    var metricsModel = new DashboardMetricsModel
                    {
                        StartDate = fromDate,
                        EndDate = toDate
                    };

                    // Try to get metrics from service if available
                    try
                    {
                        var metricsService = new DashboardMetricsService();
                        
                        // Populate metrics from service
                        metricsModel.PendingCount = metricsService.GetPendingCount(userId);
                        metricsModel.OverdueCount = metricsService.GetOverdueCount(userId);
                        metricsModel.AverageApprovalTimeHours = (decimal)metricsService.GetAverageApprovalTime(fromDate, toDate);
                        metricsModel.ApprovalRate = (decimal)metricsService.GetApprovalRate(fromDate, toDate);
                        metricsModel.TotalActiveWorkflows = metricsService.GetTotalActiveWorkflows();
                        metricsModel.ApprovedTodayCount = metricsService.GetApprovedTodayCount();
                        metricsModel.RejectedTodayCount = metricsService.GetRejectedTodayCount();
                    }
                    catch (Exception)
                    {
                        // If service is not available or fails, use default values
                        // This allows the component to render in design mode or when service is unavailable
                        metricsModel.PendingCount = 0;
                        metricsModel.OverdueCount = 0;
                        metricsModel.AverageApprovalTimeHours = 0;
                        metricsModel.ApprovalRate = 0;
                        metricsModel.TotalActiveWorkflows = 0;
                        metricsModel.ApprovedTodayCount = 0;
                        metricsModel.RejectedTodayCount = 0;
                    }

                    ViewBag.Metrics = metricsModel;
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
    }
}
