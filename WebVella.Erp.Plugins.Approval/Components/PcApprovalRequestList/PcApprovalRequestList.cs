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
using WebVella.Erp.Plugins.Approval.Services;
using WebVella.Erp.Web;
using WebVella.Erp.Web.Models;
using WebVella.Erp.Web.Services;
using WebVella.Erp.Web.Utils;

namespace WebVella.Erp.Plugins.Approval.Components
{
    /// <summary>
    /// PageComponent for displaying pending approval requests in a filterable, sortable, paginated list.
    /// Displays approval requests for the current user with status indicators, filters, and pagination support.
    /// Inherits from PageComponent base class following WebVella page component patterns.
    /// </summary>
    /// <remarks>
    /// This component provides the following features:
    /// - Retrieves pending approval requests via ApprovalRequestService.GetPendingRequestsForUser()
    /// - Supports filtering by status, workflow, and date range via URL query parameters
    /// - Supports pagination with configurable page size
    /// - Supports sorting by multiple columns (created_on, due_date, status, workflow_name)
    /// - Displays status indicators using ApprovalStatus enum values
    /// 
    /// Component views:
    /// - Display: Main list view with filters and pagination
    /// - Design: Design-time preview for page builder
    /// - Options: Component configuration editor
    /// - Help: Help documentation view
    /// - Error: Error display view
    /// </remarks>
    [PageComponent(Label = "Approval Request List", Library = "WebVella", Description = "Displays pending approval requests for the current user", Version = "0.0.1", IconClass = "fas fa-list-check")]
    public class PcApprovalRequestList : PageComponent
    {
        #region Properties

        /// <summary>
        /// Gets or sets the ERP request context for accessing page and HTTP context.
        /// Injected via constructor using FromServices attribute.
        /// </summary>
        protected ErpRequestContext ErpRequestContext { get; set; }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="PcApprovalRequestList"/> class.
        /// </summary>
        /// <param name="coreReqCtx">The ERP request context injected by the framework via dependency injection.</param>
        public PcApprovalRequestList([FromServices] ErpRequestContext coreReqCtx)
        {
            ErpRequestContext = coreReqCtx;
        }

        #endregion

        #region Options Class

        /// <summary>
        /// Options class for the PcApprovalRequestList component configuration.
        /// Contains settings for datasource binding, filtering, pagination, and sorting.
        /// All properties use JsonProperty attribute for serialization/deserialization.
        /// </summary>
        public class PcApprovalRequestListOptions
        {
            /// <summary>
            /// Gets or sets the datasource name for approval requests.
            /// If empty, requests are loaded directly from the database via ApprovalRequestService.
            /// </summary>
            [JsonProperty(PropertyName = "records")]
            public string Records { get; set; } = "";

            /// <summary>
            /// Gets or sets the status filter value.
            /// Empty string means all statuses are displayed.
            /// Valid values: pending, approved, rejected, delegated, escalated, cancelled
            /// </summary>
            [JsonProperty(PropertyName = "status_filter")]
            public string StatusFilter { get; set; } = "";

            /// <summary>
            /// Gets or sets the page size for pagination.
            /// Default is 10 items per page. Minimum is 1, maximum recommended is 50.
            /// </summary>
            [JsonProperty(PropertyName = "page_size")]
            public int PageSize { get; set; } = 10;

            /// <summary>
            /// Gets or sets the column to sort by.
            /// Valid values: created_on, due_date, status, workflow_name, entity_name
            /// Default is 'created_on' for chronological order.
            /// </summary>
            [JsonProperty(PropertyName = "sort_column")]
            public string SortColumn { get; set; } = "created_on";

            /// <summary>
            /// Gets or sets the sort direction.
            /// Valid values are 'asc' for ascending or 'desc' for descending.
            /// Default is 'desc' to show newest requests first.
            /// </summary>
            [JsonProperty(PropertyName = "sort_direction")]
            public string SortDirection { get; set; } = "desc";
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Invokes the component asynchronously and returns the appropriate view based on the component mode.
        /// Loads approval requests via ApprovalRequestService, applies filters, sorting, and pagination,
        /// then populates ViewBag with data for Razor view rendering.
        /// </summary>
        /// <param name="context">The page component context containing node, options, mode, and data model.</param>
        /// <returns>
        /// A Task containing the IViewComponentResult for rendering:
        /// - Display view for runtime display mode
        /// - Design view for page builder design mode
        /// - Options view for component configuration
        /// - Help view for documentation
        /// - Error view when exceptions occur
        /// </returns>
        /// <exception cref="ValidationException">Thrown when component configuration is invalid or data loading fails.</exception>
        public async Task<IViewComponentResult> InvokeAsync(PageComponentContext context)
        {
            ErpPage currentPage = null;
            try
            {
                #region << Init >>
                // Validate context node is provided
                if (context.Node == null)
                {
                    return await Task.FromResult<IViewComponentResult>(Content("Error: The node Id is required to be set as query parameter 'nid', when requesting this component"));
                }

                // Validate page model exists and is correct type
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

                // Deserialize component options
                var options = new PcApprovalRequestListOptions();
                if (context.Options != null)
                {
                    options = JsonConvert.DeserializeObject<PcApprovalRequestListOptions>(context.Options.ToString());
                }

                // Get component metadata from library service
                var componentMeta = new PageComponentLibraryService().GetComponentMeta(context.Node.ComponentName);
                #endregion

                #region << ViewBag Setup >>
                // Populate standard ViewBag properties for all view modes
                ViewBag.Options = options;
                ViewBag.Node = context.Node;
                ViewBag.ComponentMeta = componentMeta;
                ViewBag.RequestContext = ErpRequestContext;
                ViewBag.AppContext = ErpAppContext.Current;
                ViewBag.ComponentContext = context;
                ViewBag.CurrentUser = SecurityContext.CurrentUser;
                ViewBag.CurrentUserJson = JsonConvert.SerializeObject(SecurityContext.CurrentUser);
                #endregion

                // Only load data for Display and Design modes, not Options or Help
                if (context.Mode != ComponentMode.Options && context.Mode != ComponentMode.Help)
                {
                    #region << Data Loading >>
                    // Get current user ID for filtering requests
                    var currentUser = SecurityContext.CurrentUser;
                    var currentUserId = currentUser?.Id ?? Guid.Empty;

                    // Try to get records from datasource first
                    var inputRecords = context.DataModel.GetPropertyValueByDataSource(options.Records) as List<EntityRecord>;

                    if (inputRecords == null || !inputRecords.Any())
                    {
                        // Load approval requests from database via ApprovalRequestService
                        inputRecords = LoadApprovalRequestsFromService(currentUserId);
                    }

                    // Apply status filter from options if specified
                    if (!string.IsNullOrEmpty(options.StatusFilter))
                    {
                        inputRecords = inputRecords.Where(r =>
                            string.Equals(r["status"]?.ToString(), options.StatusFilter, StringComparison.OrdinalIgnoreCase)
                        ).ToList();
                    }

                    // Get HTTP context for URL parameter handling
                    HttpContext httpContext = null;
                    if (ErpRequestContext?.PageContext != null)
                    {
                        httpContext = ErpRequestContext.PageContext.HttpContext;
                    }

                    // Apply URL-based filters
                    inputRecords = ApplyUrlFilters(inputRecords, httpContext, options);

                    // Calculate totals before pagination
                    var totalCount = inputRecords.Count;

                    // Apply sorting
                    inputRecords = ApplySorting(inputRecords, options.SortColumn, options.SortDirection);

                    // Get current page from URL query parameter
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
                    ViewBag.SiteRootUrl = UrlUtils.FullyQualifiedApplicationPath(httpContext);

                    // Load workflows for filter dropdown
                    ViewBag.Workflows = LoadWorkflows();

                    // Set status options for filter dropdown using ApprovalStatus enum
                    ViewBag.StatusOptions = GetStatusOptionsAsList();
                    #endregion
                }

                // Return appropriate view based on component mode
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

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Loads approval requests from the database using ApprovalRequestService.
        /// Returns pending requests where the specified user is an authorized approver.
        /// </summary>
        /// <param name="userId">The ID of the user to load requests for.</param>
        /// <returns>A list of EntityRecord objects representing approval requests for the user.</returns>
        /// <remarks>
        /// Uses ApprovalRequestService.GetPendingRequestsForUser() as the primary data source.
        /// This method filters requests where:
        /// - The user is an authorized approver for the current step
        /// - The request was delegated to this user
        /// Returns sorted by due_date ascending (most urgent first).
        /// </remarks>
        private List<EntityRecord> LoadApprovalRequestsFromService(Guid userId)
        {
            var result = new List<EntityRecord>();

            try
            {
                using (var securityScope = SecurityContext.OpenSystemScope())
                {
                    // Use ApprovalRequestService to get pending requests for user
                    var requestService = new ApprovalRequestService();
                    var pendingRequests = requestService.GetPendingRequestsForUser(userId);

                    if (pendingRequests != null && pendingRequests.Any())
                    {
                        // Convert EntityRecordList to List<EntityRecord>
                        result = pendingRequests.ToList();

                        // Enrich records with workflow name for display
                        EnrichRecordsWithWorkflowData(result);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but return empty list to prevent component failure
                System.Diagnostics.Debug.WriteLine($"Error loading approval requests from service: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Enriches approval request records with workflow name data for display purposes.
        /// </summary>
        /// <param name="records">The list of records to enrich.</param>
        private void EnrichRecordsWithWorkflowData(List<EntityRecord> records)
        {
            try
            {
                // Get unique workflow IDs from records
                var workflowIds = records
                    .Where(r => r["workflow_id"] != null)
                    .Select(r => (Guid)r["workflow_id"])
                    .Distinct()
                    .ToList();

                if (!workflowIds.Any())
                    return;

                // Build workflow lookup dictionary
                var workflowLookup = new Dictionary<Guid, string>();

                foreach (var workflowId in workflowIds)
                {
                    var eqlCommand = new EqlCommand(
                        "SELECT id, name FROM approval_workflow WHERE id = @workflowId",
                        new EqlParameter("workflowId", workflowId));
                    var workflow = eqlCommand.Execute()?.FirstOrDefault();

                    if (workflow != null)
                    {
                        workflowLookup[(Guid)workflow["id"]] = workflow["name"]?.ToString() ?? "Unknown";
                    }
                }

                // Enrich each record with workflow name
                foreach (var record in records)
                {
                    var recordWorkflowId = record["workflow_id"] as Guid?;
                    if (recordWorkflowId.HasValue && workflowLookup.ContainsKey(recordWorkflowId.Value))
                    {
                        record["workflow_name"] = workflowLookup[recordWorkflowId.Value];
                    }
                    else
                    {
                        record["workflow_name"] = "Unknown";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error enriching records with workflow data: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies URL query parameter filters to the records list.
        /// Supports filtering by status, workflow, and date range.
        /// </summary>
        /// <param name="records">The records to filter.</param>
        /// <param name="httpContext">The HTTP context containing query parameters.</param>
        /// <param name="options">The component options for sort overrides.</param>
        /// <returns>The filtered list of records.</returns>
        private List<EntityRecord> ApplyUrlFilters(List<EntityRecord> records, HttpContext httpContext, PcApprovalRequestListOptions options)
        {
            if (httpContext == null || records == null || !records.Any())
                return records;

            var queryParams = httpContext.Request.Query;

            // Status filter from URL (overrides options.StatusFilter)
            if (queryParams.ContainsKey("statusFilter") && !string.IsNullOrEmpty(queryParams["statusFilter"]))
            {
                var urlStatusFilter = queryParams["statusFilter"].ToString();
                records = records.Where(r =>
                    string.Equals(r["status"]?.ToString(), urlStatusFilter, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            // Workflow filter from URL
            if (queryParams.ContainsKey("workflowFilter") && !string.IsNullOrEmpty(queryParams["workflowFilter"]))
            {
                var workflowFilter = queryParams["workflowFilter"].ToString();
                if (Guid.TryParse(workflowFilter, out var workflowId))
                {
                    records = records.Where(r =>
                        r["workflow_id"] != null && (Guid)r["workflow_id"] == workflowId
                    ).ToList();
                }
            }

            // Date range filter - dateFrom
            if (queryParams.ContainsKey("dateFrom") && !string.IsNullOrEmpty(queryParams["dateFrom"]))
            {
                if (DateTime.TryParse(queryParams["dateFrom"].ToString(), out var dateFrom))
                {
                    records = records.Where(r =>
                        r["created_on"] != null && (DateTime)r["created_on"] >= dateFrom
                    ).ToList();
                }
            }

            // Date range filter - dateTo
            if (queryParams.ContainsKey("dateTo") && !string.IsNullOrEmpty(queryParams["dateTo"]))
            {
                if (DateTime.TryParse(queryParams["dateTo"].ToString(), out var dateTo))
                {
                    // Add 1 day to include the entire end date
                    records = records.Where(r =>
                        r["created_on"] != null && (DateTime)r["created_on"] <= dateTo.AddDays(1)
                    ).ToList();
                }
            }

            // Override sorting options from URL parameters
            if (queryParams.ContainsKey("sortColumn"))
            {
                options.SortColumn = queryParams["sortColumn"].ToString();
            }
            if (queryParams.ContainsKey("sortDirection"))
            {
                options.SortDirection = queryParams["sortDirection"].ToString();
            }

            return records;
        }

        /// <summary>
        /// Loads available workflows for the filter dropdown.
        /// Only returns active workflows ordered alphabetically by name.
        /// </summary>
        /// <returns>A list of EntityRecord objects representing active workflows.</returns>
        private List<EntityRecord> LoadWorkflows()
        {
            var result = new List<EntityRecord>();

            try
            {
                using (var securityScope = SecurityContext.OpenSystemScope())
                {
                    var eqlCommand = new EqlCommand(
                        "SELECT id, name FROM approval_workflow WHERE is_active = @is_active ORDER BY name ASC",
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
        /// Uses ApprovalStatus enum values to generate options.
        /// </summary>
        /// <returns>
        /// A list of anonymous objects with Value and Label properties for each status option.
        /// Includes an "All Statuses" option with empty value at the start.
        /// </returns>
        private List<object> GetStatusOptionsAsList()
        {
            var options = new List<object>
            {
                new { Value = "", Label = "All Statuses" }
            };

            // Add options for each ApprovalStatus enum value
            options.Add(new { Value = ApprovalStatus.Pending.ToString().ToLowerInvariant(), Label = "Pending" });
            options.Add(new { Value = ApprovalStatus.Approved.ToString().ToLowerInvariant(), Label = "Approved" });
            options.Add(new { Value = ApprovalStatus.Rejected.ToString().ToLowerInvariant(), Label = "Rejected" });
            options.Add(new { Value = ApprovalStatus.Delegated.ToString().ToLowerInvariant(), Label = "Delegated" });
            options.Add(new { Value = ApprovalStatus.Escalated.ToString().ToLowerInvariant(), Label = "Escalated" });
            options.Add(new { Value = ApprovalStatus.Cancelled.ToString().ToLowerInvariant(), Label = "Cancelled" });

            return options;
        }

        /// <summary>
        /// Applies sorting to the records based on the specified column and direction.
        /// Supports sorting by created_on, due_date, status, workflow_name, and entity_name columns.
        /// </summary>
        /// <param name="records">The list of records to sort.</param>
        /// <param name="sortColumn">The column name to sort by.</param>
        /// <param name="sortDirection">The sort direction ('asc' for ascending, 'desc' for descending).</param>
        /// <returns>The sorted list of records.</returns>
        private List<EntityRecord> ApplySorting(List<EntityRecord> records, string sortColumn, string sortDirection)
        {
            if (records == null || !records.Any())
                return records;

            var isDescending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

            // Determine which property to sort by using pattern matching
            Func<EntityRecord, object> sortKey = sortColumn?.ToLowerInvariant() switch
            {
                "id" => r => r["id"]?.ToString() ?? "",
                "workflow_name" => r => r["workflow_name"]?.ToString() ?? r["$approval_workflow.name"]?.ToString() ?? "",
                "source_entity" => r => r["source_entity"]?.ToString() ?? "",
                "status" => r => r["status"]?.ToString() ?? "",
                "due_date" => r => r["due_date"] ?? DateTime.MaxValue,
                "created_on" => r => r["created_on"] ?? DateTime.MinValue,
                _ => r => r["created_on"] ?? DateTime.MinValue, // Default to created_on
            };

            return isDescending
                ? records.OrderByDescending(sortKey).ToList()
                : records.OrderBy(sortKey).ToList();
        }

        #endregion
    }
}
