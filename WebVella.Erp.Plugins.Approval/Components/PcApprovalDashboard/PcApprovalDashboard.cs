using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
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
    /// PageComponent class for the Approval Dashboard, providing real-time manager metrics display.
    /// Displays 5 KPIs: pending count, average approval time, approval rate, overdue count, and recent activity.
    /// Requires Manager or Administrator role for secure access.
    /// </summary>
    [PageComponent(
        Label = "Approval Dashboard",
        Library = "WebVella",
        Description = "Real-time dashboard displaying approval workflow metrics",
        Version = "0.0.1",
        IconClass = "fas fa-chart-line",
        Category = "Approval Workflow")]
    public class PcApprovalDashboard : PageComponent
    {
        /// <summary>
        /// Gets or sets the ERP request context.
        /// </summary>
        protected ErpRequestContext ErpRequestContext { get; set; }

        /// <summary>
        /// Initializes a new instance of the PcApprovalDashboard component.
        /// </summary>
        /// <param name="coreReqCtx">The ERP request context injected by the framework.</param>
        public PcApprovalDashboard([FromServices] ErpRequestContext coreReqCtx)
        {
            ErpRequestContext = coreReqCtx;
        }

        /// <summary>
        /// Options class for configuring the Approval Dashboard component.
        /// </summary>
        public class PcApprovalDashboardOptions
        {
            /// <summary>
            /// Gets or sets the auto-refresh interval in seconds.
            /// Default is 60 seconds. Set to 0 to disable auto-refresh.
            /// </summary>
            [JsonProperty(PropertyName = "refresh_interval_seconds")]
            public int RefreshIntervalSeconds { get; set; } = 60;
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

                // For Display mode, validate role and load metrics
                if (context.Mode == ComponentMode.Display)
                {
                    // Validate user has Manager or Administrator role
                    var currentUser = SecurityContext.CurrentUser;
                    if (currentUser != null && !IsManagerRole(currentUser))
                    {
                        ViewBag.Error = new ValidationException()
                        {
                            Message = "Access denied. Manager or Administrator role is required to view the approval dashboard."
                        };
                        return await Task.FromResult<IViewComponentResult>(View("Error"));
                    }

                    // Load dashboard metrics
                    var metricsService = new DashboardMetricsService();
                    var metrics = metricsService.GetDashboardMetrics();
                    ViewBag.Metrics = metrics;
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
        /// Checks if the specified user has Manager or Administrator role.
        /// </summary>
        /// <param name="user">The user to check.</param>
        /// <returns>True if the user has Manager or Administrator role; otherwise, false.</returns>
        private bool IsManagerRole(ErpUser user)
        {
            if (user == null || user.Roles == null)
                return false;

            foreach (var role in user.Roles)
            {
                if (role?.Name != null)
                {
                    var roleName = role.Name.ToLower();
                    if (roleName == "manager" || roleName == "administrator")
                        return true;
                }
            }
            return false;
        }
    }
}
