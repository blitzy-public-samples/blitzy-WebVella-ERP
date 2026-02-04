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

namespace WebVella.Erp.Plugins.Approval.Components
{
    /// <summary>
    /// PageComponent for displaying approval request audit trail as a timeline.
    /// Shows all actions taken on an approval request including submitted, approved,
    /// rejected, delegated, and escalated events with user, timestamp, and comments.
    /// </summary>
    [PageComponent(
        Label = "Approval History",
        Library = "WebVella",
        Description = "Displays approval request audit trail as timeline",
        Version = "0.0.1",
        IconClass = "fas fa-history",
        Category = "Approval Workflow")]
    public class PcApprovalHistory : PageComponent
    {
        /// <summary>
        /// The ERP request context injected via constructor.
        /// Provides access to the current HTTP request context and page information.
        /// </summary>
        protected ErpRequestContext ErpRequestContext { get; set; }

        /// <summary>
        /// Constructor with dependency injection for ErpRequestContext.
        /// </summary>
        /// <param name="coreReqCtx">The ERP request context provided by the DI container.</param>
        public PcApprovalHistory([FromServices] ErpRequestContext coreReqCtx)
        {
            ErpRequestContext = coreReqCtx;
        }

        /// <summary>
        /// Options class for configuring the PcApprovalHistory component.
        /// Contains the request ID binding for specifying which approval request's
        /// history should be displayed.
        /// </summary>
        public class PcApprovalHistoryOptions
        {
            /// <summary>
            /// The data source binding for the approval request ID.
            /// Can be a static GUID string or a data source expression like {{Record.id}}.
            /// </summary>
            [JsonProperty(PropertyName = "request_id")]
            public string RequestId { get; set; } = "";
        }

        /// <summary>
        /// Asynchronously invokes the component to render the appropriate view based on context mode.
        /// Handles Display, Design, Options, Help, and Error modes.
        /// In Display mode, loads approval history records for the specified request.
        /// </summary>
        /// <param name="context">The page component context containing node, options, and mode information.</param>
        /// <returns>A task that resolves to the appropriate view component result.</returns>
        public async Task<IViewComponentResult> InvokeAsync(PageComponentContext context)
        {
            ErpPage currentPage = null;
            try
            {
                #region << Init >>
                // Validate that the node ID is provided
                if (context.Node == null)
                {
                    return await Task.FromResult<IViewComponentResult>(Content("Error: The node Id is required to be set as query parameter 'nid', when requesting this component"));
                }

                // Validate that the page model is available
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

                // Deserialize component options from the context
                var options = new PcApprovalHistoryOptions();
                if (context.Options != null)
                {
                    options = JsonConvert.DeserializeObject<PcApprovalHistoryOptions>(context.Options.ToString());
                }

                // Get component metadata for rendering
                var componentMeta = new PageComponentLibraryService().GetComponentMeta(context.Node.ComponentName);
                #endregion

                #region << Set ViewBag Values >>
                ViewBag.Options = options;
                ViewBag.Node = context.Node;
                ViewBag.ComponentMeta = componentMeta;
                ViewBag.RequestContext = ErpRequestContext;
                ViewBag.AppContext = ErpAppContext.Current;
                ViewBag.ComponentContext = context;
                ViewBag.CurrentUser = SecurityContext.CurrentUser;
                #endregion

                #region << Load History Data for Display Mode >>
                if (context.Mode != ComponentMode.Options && context.Mode != ComponentMode.Help)
                {
                    // Resolve the request ID from the data source binding or query parameter
                    Guid requestId = Guid.Empty;
                    
                    // First, try to get request ID from query parameter
                    HttpContext httpContext = null;
                    if (ErpRequestContext.PageContext != null)
                    {
                        httpContext = ErpRequestContext.PageContext.HttpContext;
                        if (httpContext?.Request?.Query != null)
                        {
                            var requestIdParam = httpContext.Request.Query["requestId"].FirstOrDefault();
                            if (!string.IsNullOrEmpty(requestIdParam) && Guid.TryParse(requestIdParam, out Guid parsedId))
                            {
                                requestId = parsedId;
                            }
                        }
                    }
                    
                    // If not in query params, try the options
                    if (requestId == Guid.Empty && !string.IsNullOrWhiteSpace(options.RequestId))
                    {
                        // Try to get the value from data source binding
                        var requestIdValue = context.DataModel.GetPropertyValueByDataSource(options.RequestId);
                        
                        if (requestIdValue != null)
                        {
                            if (requestIdValue is Guid guidValue)
                            {
                                requestId = guidValue;
                            }
                            else if (requestIdValue is string stringValue && Guid.TryParse(stringValue, out Guid parsedGuid))
                            {
                                requestId = parsedGuid;
                            }
                        }
                        else if (Guid.TryParse(options.RequestId, out Guid directGuid))
                        {
                            // If data source binding returns null, try parsing the option directly as a GUID
                            requestId = directGuid;
                        }
                    }

                    // Store the request ID in ViewBag for use in views
                    ViewBag.RequestId = requestId;

                    // Load history records if we have a valid request ID
                    var historyRecords = new List<EntityRecord>();
                    
                    if (requestId != Guid.Empty)
                    {
                        try
                        {
                            // Query approval_history records using EQL with parameterized query for security
                            var eqlCommand = new EqlCommand(
                                "SELECT * FROM approval_history WHERE request_id = @requestId ORDER BY performed_on DESC",
                                new List<EqlParameter>
                                {
                                    new EqlParameter("requestId", requestId)
                                });

                            var queryResult = eqlCommand.Execute();
                            
                            if (queryResult != null && queryResult.Count > 0)
                            {
                                historyRecords.AddRange(queryResult);
                            }
                        }
                        catch (Exception queryEx)
                        {
                            // Log the error but don't fail the component - just show empty history
                            ViewBag.QueryError = queryEx.Message;
                        }
                    }

                    ViewBag.HistoryRecords = historyRecords;
                }
                #endregion

                #region << Return Appropriate View >>
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
    }
}
