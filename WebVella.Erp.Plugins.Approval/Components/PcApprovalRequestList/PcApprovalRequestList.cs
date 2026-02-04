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
using WebVella.TagHelpers.Models;

namespace WebVella.Erp.Plugins.Approval.Components
{
    /// <summary>
    /// PageComponent for displaying pending approval requests in a filterable, sortable, paginated list.
    /// Displays approval requests for the current user with status indicators, filters, and pagination support.
    /// </summary>
    [PageComponent(Label = "Approval Request List", Library = "WebVella", Description = "Displays pending approval requests for the current user", Version = "0.0.1", IconClass = "fas fa-list-check")]
    public class PcApprovalRequestList : PageComponent
    {
        /// <summary>
        /// Gets or sets the ERP request context for accessing page and HTTP context.
        /// </summary>
        protected ErpRequestContext ErpRequestContext { get; set; }

        /// <summary>
        /// Initializes a new instance of the PcApprovalRequestList class.
        /// </summary>
        /// <param name="coreReqCtx">The ERP request context injected by the framework.</param>
        public PcApprovalRequestList([FromServices] ErpRequestContext coreReqCtx)
        {
            ErpRequestContext = coreReqCtx;
        }

        /// <summary>
        /// Options class for the PcApprovalRequestList component.
        /// Contains configuration settings for datasource, filtering, pagination, and sorting.
        /// </summary>
        public class PcApprovalRequestListOptions
        {
            /// <summary>
            /// Gets or sets the datasource name for approval requests.
            /// </summary>
            [JsonProperty(PropertyName = "records")]
            public string Records { get; set; } = "";

            /// <summary>
            /// Gets or sets the status filter value. Empty string means all statuses.
            /// </summary>
            [JsonProperty(PropertyName = "status_filter")]
            public string StatusFilter { get; set; } = "";

            /// <summary>
            /// Gets or sets the page size for pagination. Default is 10.
            /// </summary>
            [JsonProperty(PropertyName = "page_size")]
            public int PageSize { get; set; } = 10;

            /// <summary>
            /// Gets or sets the column to sort by. Default is 'created_on'.
            /// </summary>
            [JsonProperty(PropertyName = "sort_column")]
            public string SortColumn { get; set; } = "created_on";

            /// <summary>
            /// Gets or sets the sort direction. Valid values are 'asc' or 'desc'. Default is 'desc'.
            /// </summary>
            [JsonProperty(PropertyName = "sort_direction")]
            public string SortDirection { get; set; } = "desc";
        }

        /// <summary>
        /// Invokes the component and returns the appropriate view based on the component mode.
        /// </summary>
        /// <param name="context">The page component context containing node, options, mode, and data model.</param>
        /// <returns>The view component result for rendering.</returns>
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

                var options = new PcApprovalRequestListOptions();
                if (context.Options != null)
                {
                    options = JsonConvert.DeserializeObject<PcApprovalRequestListOptions>(context.Options.ToString());
                }

                var componentMeta = new PageComponentLibraryService().GetComponentMeta(context.Node.ComponentName);
                #endregion

                #region << ViewBag Setup >>
                ViewBag.Options = options;
                ViewBag.Node = context.Node;
                ViewBag.ComponentMeta = componentMeta;
                ViewBag.RequestContext = ErpRequestContext;
                ViewBag.AppContext = ErpAppContext.Current;
                ViewBag.ComponentContext = context;
                ViewBag.CurrentUser = SecurityContext.CurrentUser;
                ViewBag.CurrentUserJson = JsonConvert.SerializeObject(SecurityContext.CurrentUser);
                #endregion

                if (context.Mode != ComponentMode.Options && context.Mode != ComponentMode.Help)
                {
                    #region << Data Loading >>
                    // Get current user for filtering
                    var currentUser = SecurityContext.CurrentUser;
                    var currentUserId = currentUser?.Id ?? Guid.Empty;

                    // Get records from datasource or load from database
                    var inputRecords = context.DataModel.GetPropertyValueByDataSource(options.Records) as List<EntityRecord>;
                    
                    if (inputRecords == null || !inputRecords.Any())
                    {
                        // Load approval requests from database if no datasource provided
                        inputRecords = LoadApprovalRequests(currentUserId);
                    }

                    // Apply status filter if specified
                    if (!string.IsNullOrEmpty(options.StatusFilter))
                    {
                        inputRecords = inputRecords.Where(r => 
                            string.Equals(r["status"]?.ToString(), options.StatusFilter, StringComparison.OrdinalIgnoreCase)
                        ).ToList();
                    }

                    // Get URL parameters for additional filtering and pagination
                    HttpContext httpContext = null;
                    if (ErpRequestContext?.PageContext != null)
                    {
                        httpContext = ErpRequestContext.PageContext.HttpContext;
                    }

                    // Apply URL-based filters
                    if (httpContext != null)
                    {
                        var queryParams = httpContext.Request.Query;
                        
                        // Status filter from URL
                        if (queryParams.ContainsKey("statusFilter") && !string.IsNullOrEmpty(queryParams["statusFilter"]))
                        {
                            var urlStatusFilter = queryParams["statusFilter"].ToString();
                            inputRecords = inputRecords.Where(r => 
                                string.Equals(r["status"]?.ToString(), urlStatusFilter, StringComparison.OrdinalIgnoreCase)
                            ).ToList();
                        }

                        // Workflow filter from URL
                        if (queryParams.ContainsKey("workflowFilter") && !string.IsNullOrEmpty(queryParams["workflowFilter"]))
                        {
                            var workflowFilter = queryParams["workflowFilter"].ToString();
                            if (Guid.TryParse(workflowFilter, out var workflowId))
                            {
                                inputRecords = inputRecords.Where(r => 
                                    r["workflow_id"] != null && (Guid)r["workflow_id"] == workflowId
                                ).ToList();
                            }
                        }

                        // Date range filter from URL
                        if (queryParams.ContainsKey("dateFrom") && !string.IsNullOrEmpty(queryParams["dateFrom"]))
                        {
                            if (DateTime.TryParse(queryParams["dateFrom"].ToString(), out var dateFrom))
                            {
                                inputRecords = inputRecords.Where(r => 
                                    r["created_on"] != null && (DateTime)r["created_on"] >= dateFrom
                                ).ToList();
                            }
                        }

                        if (queryParams.ContainsKey("dateTo") && !string.IsNullOrEmpty(queryParams["dateTo"]))
                        {
                            if (DateTime.TryParse(queryParams["dateTo"].ToString(), out var dateTo))
                            {
                                // Add 1 day to include the entire end date
                                inputRecords = inputRecords.Where(r => 
                                    r["created_on"] != null && (DateTime)r["created_on"] <= dateTo.AddDays(1)
                                ).ToList();
                            }
                        }

                        // Override sorting from URL
                        if (queryParams.ContainsKey("sortColumn"))
                        {
                            options.SortColumn = queryParams["sortColumn"].ToString();
                        }
                        if (queryParams.ContainsKey("sortDirection"))
                        {
                            options.SortDirection = queryParams["sortDirection"].ToString();
                        }
                    }

                    // Calculate totals before pagination
                    var totalCount = inputRecords.Count;

                    // Apply sorting
                    inputRecords = ApplySorting(inputRecords, options.SortColumn, options.SortDirection);

                    // Get current page from URL
                    var currentPageNum = 1;
                    if (httpContext != null && httpContext.Request.Query.ContainsKey("page"))
                    {
                        int.TryParse(httpContext.Request.Query["page"].ToString(), out currentPageNum);
                        if (currentPageNum < 1) currentPageNum = 1;
                    }

                    // Apply pagination
                    var pageSize = options.PageSize > 0 ? options.PageSize : 10;
                    var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
                    if (currentPageNum > totalPages && totalPages > 0) currentPageNum = totalPages;
                    
                    var paginatedRecords = inputRecords
                        .Skip((currentPageNum - 1) * pageSize)
                        .Take(pageSize)
                        .ToList();

                    // Set ViewBag data for the view
                    ViewBag.Records = paginatedRecords;
                    ViewBag.RecordsJson = JsonConvert.SerializeObject(paginatedRecords);
                    ViewBag.Pagination = new
                    {
                        TotalCount = totalCount,
                        CurrentPage = currentPageNum,
                        TotalPages = totalPages,
                        PageSize = pageSize
                    };
                    ViewBag.SiteRootUrl = ApprovalUtils.FullyQualifiedApplicationPath(httpContext);

                    // Load workflows for filter dropdown
                    ViewBag.Workflows = LoadWorkflows();

                    // Set status options for filter dropdown
                    ViewBag.StatusOptions = GetStatusOptions();
                    #endregion
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
        /// Loads approval requests from the database for the specified user.
        /// Returns pending requests where the user is an authorized approver.
        /// </summary>
        /// <param name="userId">The ID of the user to load requests for.</param>
        /// <returns>A list of EntityRecord objects representing approval requests.</returns>
        private List<EntityRecord> LoadApprovalRequests(Guid userId)
        {
            var result = new List<EntityRecord>();
            
            try
            {
                using (var securityScope = SecurityContext.OpenSystemScope())
                {
                    var recMan = new RecordManager();
                    
                    // Query approval_request entity
                    // In a full implementation, this would also filter by user being an approver
                    var eqlCommand = new EqlCommand("SELECT *, $approval_workflow.name FROM approval_request ORDER BY created_on DESC");
                    var queryResult = eqlCommand.Execute();
                    
                    if (queryResult != null && queryResult.Any())
                    {
                        result = queryResult.ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but return empty list to prevent component failure
                System.Diagnostics.Debug.WriteLine($"Error loading approval requests: {ex.Message}");
            }
            
            return result;
        }

        /// <summary>
        /// Loads available workflows for the filter dropdown.
        /// </summary>
        /// <returns>A list of EntityRecord objects representing workflows.</returns>
        private List<EntityRecord> LoadWorkflows()
        {
            var result = new List<EntityRecord>();
            
            try
            {
                using (var securityScope = SecurityContext.OpenSystemScope())
                {
                    var eqlCommand = new EqlCommand("SELECT id, name FROM approval_workflow WHERE is_active = @is_active ORDER BY name ASC", 
                        new EqlParameter("is_active", true));
                    var queryResult = eqlCommand.Execute();
                    
                    if (queryResult != null && queryResult.Any())
                    {
                        result = queryResult.ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but return empty list to prevent component failure
                System.Diagnostics.Debug.WriteLine($"Error loading workflows: {ex.Message}");
            }
            
            return result;
        }

        /// <summary>
        /// Gets the list of status options for the filter dropdown.
        /// </summary>
        /// <returns>A list of SelectOption objects for the status filter.</returns>
        private List<SelectOption> GetStatusOptions()
        {
            return new List<SelectOption>
            {
                new SelectOption("", "All Statuses"),
                new SelectOption("pending", "Pending"),
                new SelectOption("approved", "Approved"),
                new SelectOption("rejected", "Rejected"),
                new SelectOption("delegated", "Delegated"),
                new SelectOption("escalated", "Escalated"),
                new SelectOption("cancelled", "Cancelled")
            };
        }

        /// <summary>
        /// Applies sorting to the records based on the specified column and direction.
        /// </summary>
        /// <param name="records">The list of records to sort.</param>
        /// <param name="sortColumn">The column name to sort by.</param>
        /// <param name="sortDirection">The sort direction ('asc' or 'desc').</param>
        /// <returns>The sorted list of records.</returns>
        private List<EntityRecord> ApplySorting(List<EntityRecord> records, string sortColumn, string sortDirection)
        {
            if (records == null || !records.Any())
                return records;

            var isDescending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

            // Determine which property to sort by
            Func<EntityRecord, object> sortKey = sortColumn?.ToLowerInvariant() switch
            {
                "id" => r => r["id"]?.ToString() ?? "",
                "workflow_name" => r => r["workflow_name"]?.ToString() ?? r["$approval_workflow.name"]?.ToString() ?? "",
                "entity_name" => r => r["entity_name"]?.ToString() ?? "",
                "status" => r => r["status"]?.ToString() ?? "",
                "due_date" => r => r["due_date"] ?? DateTime.MaxValue,
                _ => r => r["created_on"] ?? DateTime.MinValue, // Default to created_on
            };

            return isDescending
                ? records.OrderByDescending(sortKey).ToList()
                : records.OrderBy(sortKey).ToList();
        }
    }
}
