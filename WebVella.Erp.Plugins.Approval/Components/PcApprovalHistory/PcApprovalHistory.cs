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
using WebVella.Erp.Plugins.Approval.Services;
using WebVella.Erp.Plugins.Approval.Model;

namespace WebVella.Erp.Plugins.Approval.Components
{
    /// <summary>
    /// PageComponent for displaying chronological timeline of approval actions.
    /// Shows approval history including approvals, rejections, delegations, escalations, and comments
    /// with timestamps and user information for audit trail purposes.
    /// Part of the WebVella ERP Approval Plugin (STORY-008).
    /// </summary>
    /// <remarks>
    /// This component integrates with the <see cref="ApprovalHistoryService"/> to retrieve
    /// history records for a given approval request. The timeline displays all approval actions
    /// in chronological order with user details and optional comments.
    /// 
    /// Supported action types displayed via <see cref="ApprovalActionType"/>:
    /// - Approve: Request approved at a step
    /// - Reject: Request rejected and workflow terminated
    /// - Delegate: Approval delegated to another user
    /// - Escalate: Request escalated to higher authority
    /// - Comment: Comment added without status change
    /// </remarks>
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
            /// Supports datasource binding expressions (e.g., "{{Record.id}}").
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
            /// Compact mode shows minimal information, detailed mode shows full audit trail.
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
        /// Retrieves approval history records from <see cref="ApprovalHistoryService"/> when in Display mode.
        /// </summary>
        /// <param name="context">The page component context containing node, options, data model, and mode information.</param>
        /// <returns>A task that represents the asynchronous operation, containing the view component result.</returns>
        /// <exception cref="ValidationException">Thrown when the request ID is invalid or history retrieval fails.</exception>
        public async Task<IViewComponentResult> InvokeAsync(PageComponentContext context)
        {
            ErpPage currentPage = null;
            try
            {
                #region << Init >>
                // Validate that the component node is properly configured
                if (context.Node == null)
                {
                    return await Task.FromResult<IViewComponentResult>(Content("Error: The node Id is required to be set as query parameter 'nid', when requesting this component"));
                }

                // Retrieve and validate the current page from the data model
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

                // Deserialize component options from JSON configuration
                var options = new PcApprovalHistoryOptions();
                if (context.Options != null)
                {
                    options = JsonConvert.DeserializeObject<PcApprovalHistoryOptions>(context.Options.ToString());
                }

                // Get component metadata for rendering
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
                
                // Provide action type labels for UI rendering
                ViewBag.ActionTypeApprove = ApprovalActionType.Approve;
                ViewBag.ActionTypeReject = ApprovalActionType.Reject;
                ViewBag.ActionTypeDelegate = ApprovalActionType.Delegate;
                ViewBag.ActionTypeEscalate = ApprovalActionType.Escalate;
                ViewBag.ActionTypeComment = ApprovalActionType.Comment;
                #endregion

                #region << Load history records for Display/Design modes >>
                if (context.Mode != ComponentMode.Options && context.Mode != ComponentMode.Help)
                {
                    var historyRecords = new List<EntityRecord>();
                    
                    // Try to get records from datasource binding first (pre-loaded records)
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
                    
                    // If no records from datasource and request_id is provided, fetch from ApprovalHistoryService
                    if (historyRecords.Count == 0 && !string.IsNullOrWhiteSpace(options.RequestId))
                    {
                        var requestIdValue = context.DataModel.GetPropertyValueByDataSource(options.RequestId);
                        if (requestIdValue != null)
                        {
                            // Parse the request ID and fetch history from the service
                            if (Guid.TryParse(requestIdValue.ToString(), out Guid requestId))
                            {
                                if (requestId != Guid.Empty)
                                {
                                    // Use ApprovalHistoryService to retrieve history records
                                    var historyService = new ApprovalHistoryService();
                                    var historyResult = historyService.GetRequestHistory(requestId);
                                    
                                    if (historyResult != null)
                                    {
                                        historyRecords = historyResult.ToList();
                                    }
                                }
                            }
                        }
                    }

                    // Apply max records limit if specified and greater than zero
                    if (options.MaxRecords.HasValue && options.MaxRecords.Value > 0 && historyRecords.Count > options.MaxRecords.Value)
                    {
                        historyRecords = historyRecords.Take(options.MaxRecords.Value).ToList();
                    }

                    // Order records by performed_on timestamp descending (most recent first)
                    historyRecords = historyRecords
                        .OrderByDescending(r => r.Properties.ContainsKey("performed_on") ? r["performed_on"] : null)
                        .ToList();

                    ViewBag.HistoryRecords = historyRecords;
                    ViewBag.HistoryRecordsJson = JsonConvert.SerializeObject(historyRecords);
                    ViewBag.RecordCount = historyRecords.Count;
                }
                #endregion

                #region << Return appropriate view based on component mode >>
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
                // Handle validation exceptions with user-friendly error message
                ViewBag.Error = ex;
                return await Task.FromResult<IViewComponentResult>(View("Error"));
            }
            catch (Exception ex)
            {
                // Handle general exceptions by wrapping in ValidationException
                ViewBag.Error = new ValidationException()
                {
                    Message = ex.Message
                };
                return await Task.FromResult<IViewComponentResult>(View("Error"));
            }
        }
    }
}
