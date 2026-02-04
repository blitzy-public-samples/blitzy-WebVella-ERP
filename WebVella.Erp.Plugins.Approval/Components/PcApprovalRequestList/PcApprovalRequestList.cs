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
using WebVella.Erp.Web;
using WebVella.Erp.Web.Models;
using WebVella.Erp.Web.Services;
using WebVella.Erp.Plugins.Approval.Services;

namespace WebVella.Erp.Plugins.Approval.Components
{
    /// <summary>
    /// Page component for displaying and filtering pending approval requests.
    /// Provides a filterable, paginated list of approval requests with status-based display,
    /// user filtering, and integration with the approval workflow system.
    /// </summary>
    [PageComponent(
        Label = "Approval Request List",
        Library = "WebVella",
        Description = "Displays and filters pending approval requests",
        Version = "0.0.1",
        IconClass = "fas fa-list-alt",
        Category = "Approval Workflow")]
    public class PcApprovalRequestList : PageComponent
    {
        /// <summary>
        /// The ERP request context injected via dependency injection.
        /// Provides access to the current HTTP context and page context.
        /// </summary>
        protected ErpRequestContext ErpRequestContext { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PcApprovalRequestList"/> class.
        /// </summary>
        /// <param name="coreReqCtx">The ERP request context injected by the framework.</param>
        public PcApprovalRequestList([FromServices] ErpRequestContext coreReqCtx)
        {
            ErpRequestContext = coreReqCtx;
        }

        /// <summary>
        /// Configuration options for the PcApprovalRequestList component.
        /// These options are configured in the page builder and control filtering,
        /// pagination, and display behavior.
        /// </summary>
        public class PcApprovalRequestListOptions
        {
            /// <summary>
            /// Gets or sets the datasource reference for records.
            /// Used to bind to a data source in the page data model.
            /// </summary>
            [JsonProperty(PropertyName = "records")]
            public string Records { get; set; } = "";

            /// <summary>
            /// Gets or sets the status filter value.
            /// Valid values: "pending", "approved", "rejected", "escalated", "expired", or empty for all.
            /// </summary>
            [JsonProperty(PropertyName = "status_filter")]
            public string StatusFilter { get; set; } = "";

            /// <summary>
            /// Gets or sets the page size for pagination.
            /// Default is 10 records per page.
            /// </summary>
            [JsonProperty(PropertyName = "page_size")]
            public int PageSize { get; set; } = 10;

            /// <summary>
            /// Gets or sets whether to show only requests where the current user is the approver.
            /// When true, filters requests to only those assigned to the current user.
            /// </summary>
            [JsonProperty(PropertyName = "show_my_requests_only")]
            public bool ShowMyRequestsOnly { get; set; } = false;

            /// <summary>
            /// Gets or sets whether the component should auto-refresh.
            /// When true, the list will periodically refresh to show new requests.
            /// </summary>
            [JsonProperty(PropertyName = "auto_refresh")]
            public bool AutoRefresh { get; set; } = false;

            /// <summary>
            /// Gets or sets the detail page URL template.
            /// Use {id} as placeholder for request ID. Example: /c/approval/action?requestId={id}
            /// </summary>
            [JsonProperty(PropertyName = "detail_page_url")]
            public string DetailPageUrl { get; set; } = "";
        }

        /// <summary>
        /// Invokes the component and renders the appropriate view based on the component mode.
        /// </summary>
        /// <param name="context">The page component context containing node, options, and data model.</param>
        /// <returns>A task representing the asynchronous operation with the view component result.</returns>
        public async Task<IViewComponentResult> InvokeAsync(PageComponentContext context)
        {
            ErpPage currentPage = null;
            try
            {
                #region << Init >>
                // Validate that we have a valid node context
                if (context.Node == null)
                {
                    return await Task.FromResult<IViewComponentResult>(Content("Error: The node Id is required to be set as query parameter 'nid', when requesting this component"));
                }

                // Get the current page from the data model
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

                // Deserialize component options from context
                var options = new PcApprovalRequestListOptions();
                if (context.Options != null)
                {
                    options = JsonConvert.DeserializeObject<PcApprovalRequestListOptions>(context.Options.ToString());
                }

                // Ensure PageSize has a valid value
                if (options.PageSize <= 0)
                {
                    options.PageSize = 10;
                }

                // Get component metadata from the library service
                var componentMeta = new PageComponentLibraryService().GetComponentMeta(context.Node.ComponentName);
                #endregion

                #region << Populate ViewBag >>
                ViewBag.Options = options;
                ViewBag.Node = context.Node;
                ViewBag.ComponentMeta = componentMeta;
                ViewBag.RequestContext = ErpRequestContext;
                ViewBag.AppContext = ErpAppContext.Current;
                ViewBag.ComponentContext = context;
                ViewBag.CurrentUser = SecurityContext.CurrentUser;
                ViewBag.CurrentUserJson = JsonConvert.SerializeObject(SecurityContext.CurrentUser);
                #endregion

                #region << Data Loading for Display Mode >>
                if (context.Mode != ComponentMode.Options && context.Mode != ComponentMode.Help)
                {
                    // Get query parameters for filtering and pagination
                    int currentPageNumber = 1;
                    HttpContext httpContext = null;
                    if (ErpRequestContext.PageContext != null)
                    {
                        httpContext = ErpRequestContext.PageContext.HttpContext;
                        if (httpContext?.Request?.Query != null)
                        {
                            var query = httpContext.Request.Query;
                            
                            // Get page number from query string
                            var pageParam = query["page"].FirstOrDefault();
                            if (!string.IsNullOrEmpty(pageParam) && int.TryParse(pageParam, out int parsedPage))
                            {
                                currentPageNumber = Math.Max(1, parsedPage);
                            }
                            
                            // Override StatusFilter from query string if present
                            var statusParam = query["status"].FirstOrDefault();
                            if (statusParam != null) // null means not present, empty string means "All Statuses"
                            {
                                options.StatusFilter = statusParam;
                            }
                        }
                    }

                    // Authorization check: enforce ShowMyRequestsOnly for non-manager users
                    var currentUser = SecurityContext.CurrentUser;
                    bool isAuthorizedToViewAll = false;
                    
                    if (currentUser != null)
                    {
                        // Check if user has Manager or Administrator role which grants access to view all requests
                        isAuthorizedToViewAll = currentUser.Roles?.Any(r => 
                            r.Name != null && (
                                r.Name.Equals("manager", StringComparison.OrdinalIgnoreCase) ||
                                r.Name.Equals("administrator", StringComparison.OrdinalIgnoreCase) ||
                                r.Name.Equals("admin", StringComparison.OrdinalIgnoreCase)
                            )
                        ) ?? false;
                    }
                    
                    // If user is NOT authorized to view all, enforce ShowMyRequestsOnly
                    if (!isAuthorizedToViewAll && currentUser != null)
                    {
                        options.ShowMyRequestsOnly = true;
                    }

                    // Set the site root URL for link generation
                    ViewBag.SiteRootUrl = GetFullyQualifiedApplicationPath(httpContext);

                    // Build the EQL query based on options
                    var records = new List<EntityRecord>();
                    int totalCount = 0;

                    // First, try to get records from the configured datasource
                    if (!string.IsNullOrWhiteSpace(options.Records))
                    {
                        var inputRecords = context.DataModel.GetPropertyValueByDataSource(options.Records) as List<EntityRecord>;
                        if (inputRecords != null)
                        {
                            records = inputRecords;
                            totalCount = records.Count;

                            // Apply client-side filtering if records come from datasource
                            if (!string.IsNullOrWhiteSpace(options.StatusFilter))
                            {
                                records = records.Where(r =>
                                    r.Properties.ContainsKey("status") &&
                                    r["status"] != null &&
                                    r["status"].ToString().Equals(options.StatusFilter, StringComparison.OrdinalIgnoreCase)
                                ).ToList();
                                totalCount = records.Count;
                            }

                            // Apply approver filter if ShowMyRequestsOnly is enabled
                            // Filters to show only requests where current user is authorized to approve
                            // per STORY-008 AC7: "displays only requests awaiting current user's action"
                            if (options.ShowMyRequestsOnly && SecurityContext.CurrentUser != null)
                            {
                                var currentUserId = SecurityContext.CurrentUser.Id;
                                var routeService = new ApprovalRouteService();
                                records = records.Where(r =>
                                {
                                    if (!r.Properties.ContainsKey("current_step_id") || r["current_step_id"] == null)
                                        return false;
                                    var stepId = (Guid)r["current_step_id"];
                                    if (stepId == Guid.Empty)
                                        return false;
                                    try { return routeService.IsUserAuthorizedApprover(currentUserId, stepId); }
                                    catch { return false; }
                                }).ToList();
                                totalCount = records.Count;
                            }

                            // Apply pagination
                            int skipCount = (currentPageNumber - 1) * options.PageSize;
                            records = records.Skip(skipCount).Take(options.PageSize).ToList();
                        }
                    }
                    else
                    {
                        // Load records directly via EQL query
                        var eqlParams = new List<EqlParameter>();
                        var whereConditions = new List<string>();

                        // Build status filter condition - normalize to lowercase for case-insensitive matching
                        // Issue 4 fix: Ensure consistent status value comparison (STORY-008 pagination)
                        if (!string.IsNullOrWhiteSpace(options.StatusFilter))
                        {
                            // Normalize status filter to lowercase to match stored values
                            var normalizedStatusFilter = options.StatusFilter.Trim().ToLowerInvariant();
                            whereConditions.Add("status = @statusFilter");
                            eqlParams.Add(new EqlParameter("statusFilter", normalizedStatusFilter));
                        }

                        // Build WHERE clause
                        string whereClause = whereConditions.Any()
                            ? " WHERE " + string.Join(" AND ", whereConditions)
                            : "";

                        // When ShowMyRequestsOnly is enabled, we need to filter in memory using IsUserAuthorizedApprover
                        // because EQL doesn't support complex authorization checks
                        // Per STORY-008 AC7: "displays only requests awaiting current user's action"
                        if (options.ShowMyRequestsOnly && SecurityContext.CurrentUser != null)
                        {
                            var currentUserId = SecurityContext.CurrentUser.Id;
                            var routeService = new ApprovalRouteService();
                            
                            // Load ALL matching records first (no pagination in EQL)
                            string eqlCommand = $"SELECT * FROM approval_request{whereClause} ORDER BY requested_on DESC";
                            var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();
                            
                            if (eqlResult != null && eqlResult.Any())
                            {
                                // Filter in memory using IsUserAuthorizedApprover
                                records = eqlResult.Where(r =>
                                {
                                    if (!r.Properties.ContainsKey("current_step_id") || r["current_step_id"] == null)
                                        return false;
                                    var stepId = (Guid)r["current_step_id"];
                                    if (stepId == Guid.Empty)
                                        return false;
                                    try { return routeService.IsUserAuthorizedApprover(currentUserId, stepId); }
                                    catch { return false; }
                                }).ToList();
                                
                                totalCount = records.Count;
                                
                                // Apply pagination after filtering
                                int skipCount = (currentPageNumber - 1) * options.PageSize;
                                records = records.Skip(skipCount).Take(options.PageSize).ToList();
                            }
                        }
                        else
                        {
                            // Build the main query with pagination
                            // Note: EQL automatically adds ___total_count___ when using PAGE/PAGESIZE
                            string eqlCommand = $"SELECT * FROM approval_request{whereClause} ORDER BY requested_on DESC PAGE {currentPageNumber} PAGESIZE {options.PageSize}";

                            // Execute the query
                            var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();
                            if (eqlResult != null && eqlResult.Any())
                            {
                                records = eqlResult.ToList();
                                
                                // Get total count from the first record (EQL adds ___total_count___ automatically)
                                var firstRecord = records.First();
                                if (firstRecord.Properties.ContainsKey("___total_count___") && firstRecord["___total_count___"] != null)
                                {
                                    totalCount = Convert.ToInt32(firstRecord["___total_count___"]);
                                }
                                else
                                {
                                    // Fallback: just use the count of returned records
                                    totalCount = records.Count;
                                }
                            }
                        }
                    }

                    // Enrich records with related data (workflow name, step name, user info)
                    var enrichedRecords = EnrichRecordsWithRelatedData(records);

                    // Set ViewBag values for the view
                    ViewBag.Records = enrichedRecords;
                    ViewBag.TotalCount = totalCount;
                    ViewBag.PageSize = options.PageSize;
                    ViewBag.CurrentPage = currentPageNumber;

                    // Calculate total pages for pagination
                    int totalPages = (int)Math.Ceiling((double)totalCount / options.PageSize);
                    ViewBag.TotalPages = totalPages;
                    ViewBag.HasPreviousPage = currentPageNumber > 1;
                    ViewBag.HasNextPage = currentPageNumber < totalPages;

                    // Build pager data for the view
                    ViewBag.Pager = BuildPagerData(currentPageNumber, totalPages, options.PageSize);
                }
                #endregion

                #region << Render View Based on Mode >>
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
        /// Enriches approval request records with related data from workflow, step, and user entities.
        /// This method loads additional display information for each request record.
        /// </summary>
        /// <param name="records">The list of approval request records to enrich.</param>
        /// <returns>A list of enriched records with additional display properties.</returns>
        private List<EntityRecord> EnrichRecordsWithRelatedData(List<EntityRecord> records)
        {
            if (records == null || !records.Any())
            {
                return new List<EntityRecord>();
            }

            var enrichedRecords = new List<EntityRecord>();

            // Collect all workflow IDs and step IDs for batch loading
            var workflowIds = new HashSet<Guid>();
            var stepIds = new HashSet<Guid>();
            var userIds = new HashSet<Guid>();

            foreach (var record in records)
            {
                if (record.Properties.ContainsKey("workflow_id") && record["workflow_id"] != null)
                {
                    workflowIds.Add((Guid)record["workflow_id"]);
                }
                if (record.Properties.ContainsKey("current_step_id") && record["current_step_id"] != null)
                {
                    stepIds.Add((Guid)record["current_step_id"]);
                }
                if (record.Properties.ContainsKey("requested_by") && record["requested_by"] != null)
                {
                    userIds.Add((Guid)record["requested_by"]);
                }
            }

            // Load workflows in batch using parameterized OR conditions (EQL compatibility)
            var workflows = new Dictionary<Guid, EntityRecord>();
            if (workflowIds.Any())
            {
                try
                {
                    // Build OR conditions with parameterized queries for EQL compatibility
                    var workflowParams = new List<EqlParameter>();
                    var workflowConditions = new List<string>();
                    int wfIndex = 0;
                    foreach (var wfId in workflowIds)
                    {
                        var paramName = $"wfId{wfIndex}";
                        workflowConditions.Add($"id = @{paramName}");
                        workflowParams.Add(new EqlParameter(paramName, wfId));
                        wfIndex++;
                    }
                    var workflowEql = $"SELECT id, name FROM approval_workflow WHERE ({string.Join(" OR ", workflowConditions)})";
                    
                    var workflowResult = new EqlCommand(workflowEql, workflowParams).Execute();
                    foreach (var wf in workflowResult)
                    {
                        if (wf.Properties.ContainsKey("id") && wf["id"] != null)
                        {
                            workflows[(Guid)wf["id"]] = wf;
                        }
                    }
                }
                catch
                {
                    // If workflow entity doesn't exist yet, continue without enrichment
                }
            }

            // Load steps in batch using parameterized OR conditions (EQL compatibility)
            var steps = new Dictionary<Guid, EntityRecord>();
            if (stepIds.Any())
            {
                try
                {
                    // Build OR conditions with parameterized queries for EQL compatibility
                    var stepParams = new List<EqlParameter>();
                    var stepConditions = new List<string>();
                    int stIndex = 0;
                    foreach (var stId in stepIds)
                    {
                        var paramName = $"stId{stIndex}";
                        stepConditions.Add($"id = @{paramName}");
                        stepParams.Add(new EqlParameter(paramName, stId));
                        stIndex++;
                    }
                    var stepEql = $"SELECT id, name FROM approval_step WHERE ({string.Join(" OR ", stepConditions)})";
                    
                    var stepResult = new EqlCommand(stepEql, stepParams).Execute();
                    foreach (var step in stepResult)
                    {
                        if (step.Properties.ContainsKey("id") && step["id"] != null)
                        {
                            steps[(Guid)step["id"]] = step;
                        }
                    }
                }
                catch
                {
                    // If step entity doesn't exist yet, continue without enrichment
                }
            }

            // Load users in batch using parameterized OR conditions (EQL compatibility)
            var users = new Dictionary<Guid, EntityRecord>();
            if (userIds.Any())
            {
                try
                {
                    // Build OR conditions with parameterized queries for EQL compatibility
                    var userParams = new List<EqlParameter>();
                    var userConditions = new List<string>();
                    int usrIndex = 0;
                    foreach (var usrId in userIds)
                    {
                        var paramName = $"usrId{usrIndex}";
                        userConditions.Add($"id = @{paramName}");
                        userParams.Add(new EqlParameter(paramName, usrId));
                        usrIndex++;
                    }
                    var userEql = $"SELECT id, username, email FROM user WHERE ({string.Join(" OR ", userConditions)})";
                    
                    var userResult = new EqlCommand(userEql, userParams).Execute();
                    foreach (var user in userResult)
                    {
                        if (user.Properties.ContainsKey("id") && user["id"] != null)
                        {
                            users[(Guid)user["id"]] = user;
                        }
                    }
                }
                catch
                {
                    // If user query fails, continue without user enrichment
                }
            }

            // Enrich each record
            foreach (var record in records)
            {
                var enrichedRecord = new EntityRecord();

                // Copy all original properties
                foreach (var prop in record.Properties)
                {
                    enrichedRecord[prop.Key] = prop.Value;
                }

                // Add workflow name
                if (record.Properties.ContainsKey("workflow_id") && record["workflow_id"] != null)
                {
                    var workflowId = (Guid)record["workflow_id"];
                    if (workflows.ContainsKey(workflowId) && workflows[workflowId].Properties.ContainsKey("name"))
                    {
                        enrichedRecord["workflow_name"] = workflows[workflowId]["name"]?.ToString() ?? "Unknown";
                    }
                    else
                    {
                        enrichedRecord["workflow_name"] = "Unknown";
                    }
                }
                else
                {
                    enrichedRecord["workflow_name"] = "Unknown";
                }

                // Add step name
                if (record.Properties.ContainsKey("current_step_id") && record["current_step_id"] != null)
                {
                    var stepId = (Guid)record["current_step_id"];
                    if (steps.ContainsKey(stepId) && steps[stepId].Properties.ContainsKey("name"))
                    {
                        enrichedRecord["current_step_name"] = steps[stepId]["name"]?.ToString() ?? "Unknown";
                    }
                    else
                    {
                        enrichedRecord["current_step_name"] = "Unknown";
                    }
                }
                else
                {
                    enrichedRecord["current_step_name"] = "N/A";
                }

                // Add requester username
                if (record.Properties.ContainsKey("requested_by") && record["requested_by"] != null)
                {
                    var userId = (Guid)record["requested_by"];
                    if (users.ContainsKey(userId) && users[userId].Properties.ContainsKey("username"))
                    {
                        enrichedRecord["requested_by_username"] = users[userId]["username"]?.ToString() ?? "Unknown";
                    }
                    else
                    {
                        enrichedRecord["requested_by_username"] = "Unknown";
                    }
                }
                else
                {
                    enrichedRecord["requested_by_username"] = "Unknown";
                }

                // Add formatted requested_on date
                if (record.Properties.ContainsKey("requested_on") && record["requested_on"] != null)
                {
                    var requestedOn = (DateTime)record["requested_on"];
                    enrichedRecord["requested_on_formatted"] = requestedOn.ToString("yyyy-MM-dd HH:mm");
                }
                else
                {
                    enrichedRecord["requested_on_formatted"] = "N/A";
                }

                // Add status badge class for UI styling
                if (record.Properties.ContainsKey("status") && record["status"] != null)
                {
                    var status = record["status"].ToString().ToLower();
                    enrichedRecord["status_badge_class"] = GetStatusBadgeClass(status);
                }
                else
                {
                    enrichedRecord["status_badge_class"] = "badge-secondary";
                }

                enrichedRecords.Add(enrichedRecord);
            }

            return enrichedRecords;
        }

        /// <summary>
        /// Gets the Bootstrap badge CSS class for a given approval status.
        /// </summary>
        /// <param name="status">The approval status value.</param>
        /// <returns>A CSS class string for styling the status badge.</returns>
        private string GetStatusBadgeClass(string status)
        {
            switch (status?.ToLower())
            {
                case "pending":
                    return "badge-warning";
                case "approved":
                    return "badge-success";
                case "rejected":
                    return "badge-danger";
                case "escalated":
                    return "badge-info";
                case "expired":
                    return "badge-secondary";
                default:
                    return "badge-secondary";
            }
        }

        /// <summary>
        /// Builds pagination data for rendering in the view.
        /// Creates a list of page numbers with ellipsis handling for large page counts.
        /// </summary>
        /// <param name="currentPage">The current page number.</param>
        /// <param name="totalPages">The total number of pages.</param>
        /// <param name="pageSize">The number of records per page.</param>
        /// <returns>An EntityRecord containing pager navigation data.</returns>
        private EntityRecord BuildPagerData(int currentPage, int totalPages, int pageSize)
        {
            var pager = new EntityRecord();
            pager["current_page"] = currentPage;
            pager["total_pages"] = totalPages;
            pager["page_size"] = pageSize;
            pager["has_previous"] = currentPage > 1;
            pager["has_next"] = currentPage < totalPages;
            pager["previous_page"] = Math.Max(1, currentPage - 1);
            pager["next_page"] = Math.Min(totalPages, currentPage + 1);

            // Build visible page numbers (show max 5 pages with current in middle)
            var visiblePages = new List<int>();
            int startPage = Math.Max(1, currentPage - 2);
            int endPage = Math.Min(totalPages, currentPage + 2);

            // Adjust range if we're near the beginning or end
            if (currentPage <= 3)
            {
                endPage = Math.Min(5, totalPages);
            }
            if (currentPage >= totalPages - 2)
            {
                startPage = Math.Max(1, totalPages - 4);
            }

            for (int i = startPage; i <= endPage; i++)
            {
                visiblePages.Add(i);
            }

            pager["visible_pages"] = visiblePages;
            pager["show_first_ellipsis"] = startPage > 1;
            pager["show_last_ellipsis"] = endPage < totalPages;

            return pager;
        }

        /// <summary>
        /// Gets the fully qualified application path including scheme and host.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>The fully qualified application path or empty string if context is null.</returns>
        private string GetFullyQualifiedApplicationPath(HttpContext context)
        {
            if (context == null)
            {
                return string.Empty;
            }

            return string.Format("{0}://{1}",
                context.Request.Scheme,
                context.Request.Host);
        }
    }
}
