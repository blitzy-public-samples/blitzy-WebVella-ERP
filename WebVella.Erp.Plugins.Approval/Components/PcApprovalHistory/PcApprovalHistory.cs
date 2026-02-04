using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Exceptions;
using WebVella.Erp.Web;
using WebVella.Erp.Web.Models;
using WebVella.Erp.Web.Services;

namespace WebVella.Erp.Plugins.Approval.Components
{
    /// <summary>
    /// PageComponent for displaying chronological timeline of approval actions.
    /// Shows approval history including approvals, rejections, delegations, and comments
    /// with timestamps and user information for audit trail purposes.
    /// Part of the WebVella ERP Approval Plugin (STORY-008).
    /// </summary>
    [PageComponent(Label = "Approval History", Library = "WebVella", Description = "Displays chronological timeline of approval actions", Version = "0.0.1", IconClass = "fas fa-history")]
    public class PcApprovalHistory : PageComponent
    {
        /// <summary>
        /// Gets or sets the ERP request context injected via dependency injection.
        /// Provides access to the current request context including page, user, and application information.
        /// </summary>
        protected ErpRequestContext ErpRequestContext { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PcApprovalHistory"/> class.
        /// </summary>
        /// <param name="coreReqCtx">The ERP request context injected by the framework.</param>
        public PcApprovalHistory([FromServices] ErpRequestContext coreReqCtx)
        {
            ErpRequestContext = coreReqCtx;
        }

        /// <summary>
        /// Configuration options for the PcApprovalHistory component.
        /// These options are configured through the page builder Options.cshtml view.
        /// </summary>
        public class PcApprovalHistoryOptions
        {
            /// <summary>
            /// Gets or sets the datasource binding for the approval request ID.
            /// This determines which approval request's history is displayed.
            /// </summary>
            [JsonProperty(PropertyName = "request_id")]
            public string RequestId { get; set; } = "";

            /// <summary>
            /// Gets or sets the datasource binding for pre-loaded history records.
            /// Optional - if provided, uses these records instead of fetching from service.
            /// </summary>
            [JsonProperty(PropertyName = "records")]
            public string Records { get; set; } = "";

            /// <summary>
            /// Gets or sets the display mode for the history timeline.
            /// Valid values: "compact" or "detailed". Defaults to "detailed".
            /// </summary>
            [JsonProperty(PropertyName = "display_mode")]
            public string DisplayMode { get; set; } = "detailed";

            /// <summary>
            /// Gets or sets the maximum number of history records to display.
            /// Set to 0 for unlimited records. Defaults to 0.
            /// </summary>
            [JsonProperty(PropertyName = "max_records")]
            public int? MaxRecords { get; set; } = 0;

            /// <summary>
            /// Gets or sets whether to show timestamps on history entries.
            /// Defaults to true.
            /// </summary>
            [JsonProperty(PropertyName = "show_timestamps")]
            public bool ShowTimestamps { get; set; } = true;

            /// <summary>
            /// Gets or sets whether to show comments on history entries.
            /// Defaults to true.
            /// </summary>
            [JsonProperty(PropertyName = "show_comments")]
            public bool ShowComments { get; set; } = true;

            /// <summary>
            /// Gets or sets whether to show user avatars on history entries.
            /// Defaults to false.
            /// </summary>
            [JsonProperty(PropertyName = "show_user_avatars")]
            public bool ShowUserAvatars { get; set; } = false;

            /// <summary>
            /// Gets or sets additional CSS classes to apply to the component container.
            /// </summary>
            [JsonProperty(PropertyName = "class")]
            public string Class { get; set; } = "";

            /// <summary>
            /// Gets or sets the text to display when no history records are found.
            /// Defaults to "No history records found".
            /// </summary>
            [JsonProperty(PropertyName = "empty_text")]
            public string EmptyText { get; set; } = "No history records found";
        }

        /// <summary>
        /// Invokes the component asynchronously to render the appropriate view based on the component mode.
        /// </summary>
        /// <param name="context">The page component context containing node, options, data model, and mode information.</param>
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

                var options = new PcApprovalHistoryOptions();
                if (context.Options != null)
                {
                    options = JsonConvert.DeserializeObject<PcApprovalHistoryOptions>(context.Options.ToString());
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

                if (context.Mode != ComponentMode.Options && context.Mode != ComponentMode.Help)
                {
                    // Load history records based on options
                    var historyRecords = new List<EntityRecord>();
                    
                    // Try to get records from datasource binding first
                    if (!string.IsNullOrWhiteSpace(options.Records))
                    {
                        var recordsFromDataSource = context.DataModel.GetPropertyValueByDataSource(options.Records);
                        if (recordsFromDataSource is List<EntityRecord> recordList)
                        {
                            historyRecords = recordList;
                        }
                        else if (recordsFromDataSource is EntityRecordList entityRecordList)
                        {
                            historyRecords = entityRecordList.ToList();
                        }
                    }
                    
                    // If no records from datasource and request_id is provided, we would fetch from service
                    // Note: ApprovalHistoryService integration would be added when that service is available
                    if (historyRecords.Count == 0 && !string.IsNullOrWhiteSpace(options.RequestId))
                    {
                        var requestIdValue = context.DataModel.GetPropertyValueByDataSource(options.RequestId);
                        if (requestIdValue != null)
                        {
                            // When ApprovalHistoryService is available, fetch records here
                            // For now, initialize empty list - service integration to be completed
                            if (Guid.TryParse(requestIdValue.ToString(), out Guid requestId))
                            {
                                // Integration with ApprovalHistoryService.GetHistoryForRequest(requestId)
                                // This will be connected when the service is implemented
                            }
                        }
                    }

                    // Apply max records limit if specified
                    if (options.MaxRecords.HasValue && options.MaxRecords.Value > 0 && historyRecords.Count > options.MaxRecords.Value)
                    {
                        historyRecords = historyRecords.Take(options.MaxRecords.Value).ToList();
                    }

                    ViewBag.HistoryRecords = historyRecords;
                    ViewBag.HistoryRecordsJson = JsonConvert.SerializeObject(historyRecords);
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
