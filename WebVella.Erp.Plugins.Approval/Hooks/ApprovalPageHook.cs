using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Eql;
using WebVella.Erp.Hooks;
using WebVella.Erp.Plugins.Approval.Model;
using WebVella.Erp.Plugins.Approval.Services;
using WebVella.Erp.Web.Hooks;
using WebVella.Erp.Web.Models;

namespace WebVella.Erp.Plugins.Approval.Hooks
{
    /// <summary>
    /// Page lifecycle hook for integrating approval workflow UI elements into WebVella ERP pages.
    /// Handles OnGet and OnPost page lifecycle events to inject approval status information,
    /// action buttons, and history data into page models.
    /// </summary>
    /// <remarks>
    /// This hook class follows the WebVella IPageHook pattern for global page hooks.
    /// It is automatically discovered via the [HookAttachment] attribute and registered
    /// by the WebVella plugin system during application startup.
    /// 
    /// Key responsibilities:
    /// 1. OnGet: Injects approval context data (request, history, status, user authorization)
    ///    into the page model for display in UI components
    /// 2. OnPost: Handles approval action submissions (approve, reject, delegate) and
    ///    delegates processing to the ApprovalRequestService
    /// 
    /// All business logic is delegated to service layer classes:
    /// - ApprovalRequestService: Request lifecycle management
    /// - ApprovalHistoryService: Audit trail retrieval
    /// - ApprovalRouteService: Approver authorization checks
    /// </remarks>
    [HookAttachment]
    public class ApprovalPageHook : IPageHook
    {
        #region Constants

        /// <summary>
        /// Entity name for approval requests used in EQL queries.
        /// </summary>
        private const string ENTITY_APPROVAL_REQUEST = "approval_request";

        /// <summary>
        /// Field name for source record ID in approval_request entity.
        /// </summary>
        private const string FIELD_SOURCE_RECORD_ID = "source_record_id";

        /// <summary>
        /// Field name for source entity name in approval_request entity.
        /// </summary>
        private const string FIELD_SOURCE_ENTITY = "source_entity";

        /// <summary>
        /// Field name for status in approval_request entity.
        /// </summary>
        private const string FIELD_STATUS = "status";

        /// <summary>
        /// DataModel property key for approval request data.
        /// </summary>
        private const string DATAMODEL_APPROVAL_REQUEST = "ApprovalRequest";

        /// <summary>
        /// DataModel property key for approval history data.
        /// </summary>
        private const string DATAMODEL_APPROVAL_HISTORY = "ApprovalHistory";

        /// <summary>
        /// DataModel property key for approval status.
        /// </summary>
        private const string DATAMODEL_APPROVAL_STATUS = "ApprovalStatus";

        /// <summary>
        /// DataModel property key for user authorization flag.
        /// </summary>
        private const string DATAMODEL_IS_AUTHORIZED_APPROVER = "IsAuthorizedApprover";

        /// <summary>
        /// Form field name for approval action type.
        /// </summary>
        private const string FORM_ACTION_TYPE = "approvalActionType";

        /// <summary>
        /// Form field name for approval request ID.
        /// </summary>
        private const string FORM_REQUEST_ID = "approvalRequestId";

        /// <summary>
        /// Form field name for approval comments.
        /// </summary>
        private const string FORM_COMMENTS = "approvalComments";

        /// <summary>
        /// Form field name for delegate user ID.
        /// </summary>
        private const string FORM_DELEGATE_TO = "approvalDelegateTo";

        /// <summary>
        /// Action type string for approve action.
        /// </summary>
        private const string ACTION_APPROVE = "approve";

        /// <summary>
        /// Action type string for reject action.
        /// </summary>
        private const string ACTION_REJECT = "reject";

        /// <summary>
        /// Action type string for delegate action.
        /// </summary>
        private const string ACTION_DELEGATE = "delegate";

        #endregion

        #region IPageHook Implementation

        /// <summary>
        /// Handles the GET request for pages by injecting approval workflow context data
        /// into the page model's DataModel for display in UI components.
        /// </summary>
        /// <param name="pageModel">The base ERP page model containing request context and data model.</param>
        /// <returns>
        /// Returns null to continue normal page flow. The approval data is injected into
        /// the DataModel for access by page components.
        /// </returns>
        /// <remarks>
        /// This method performs the following operations:
        /// 1. Extracts the current record ID and entity name from the page context
        /// 2. Checks if the page displays a record that may have approval workflow
        /// 3. Retrieves pending approval request for the current record (if any)
        /// 4. Retrieves approval history for display in timeline
        /// 5. Determines if the current user is an authorized approver
        /// 6. Injects all approval context data into the DataModel
        /// 
        /// Data injected into DataModel:
        /// - ApprovalRequest: The pending approval request EntityRecord (or null)
        /// - ApprovalHistory: EntityRecordList of approval history entries
        /// - ApprovalStatus: String status of the approval request
        /// - IsAuthorizedApprover: Boolean indicating if user can approve
        /// </remarks>
        public IActionResult OnGet(BaseErpPageModel pageModel)
        {
            if (pageModel == null)
            {
                return null;
            }

            try
            {
                // Get page context to determine if we should inject approval data
                var page = pageModel.ErpRequestContext?.Page;
                if (page == null)
                {
                    return null;
                }

                // Check if this is an approval-related page that should show approval info
                if (!IsApprovalRelatedPage(page))
                {
                    return null;
                }

                // Extract record information from the page model
                var recordInfo = GetRecordFromPageModel(pageModel);
                if (recordInfo == null || recordInfo.Value.recordId == Guid.Empty)
                {
                    return null;
                }

                var recordId = recordInfo.Value.recordId;
                var entityName = recordInfo.Value.entityName;

                // Only inject approval data if we have valid record context
                if (!string.IsNullOrWhiteSpace(entityName))
                {
                    InjectApprovalDataIntoModel(pageModel, recordId, entityName);
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't break page rendering
                // Approval data will simply not be available
                System.Diagnostics.Debug.WriteLine($"ApprovalPageHook.OnGet error: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Handles POST requests for pages by processing approval action submissions.
        /// Supports approve, reject, and delegate actions.
        /// </summary>
        /// <param name="pageModel">The base ERP page model containing request context and form data.</param>
        /// <returns>
        /// Returns null to continue normal page flow after processing the approval action.
        /// In case of errors, the validation messages are added to the page model.
        /// </returns>
        /// <remarks>
        /// This method processes approval actions submitted via form POST:
        /// 1. Checks if the POST contains approval-specific action fields
        /// 2. Validates the action type and required parameters
        /// 3. Delegates action processing to ApprovalRequestService
        /// 4. Updates page model with result status
        /// 
        /// Supported actions:
        /// - approve: Approves the request and advances workflow
        /// - reject: Rejects the request (requires comments)
        /// - delegate: Delegates the request to another user
        /// 
        /// Form fields expected:
        /// - approvalActionType: The action to perform (approve/reject/delegate)
        /// - approvalRequestId: The GUID of the approval request
        /// - approvalComments: Comments for the action (required for reject)
        /// - approvalDelegateTo: Target user ID for delegation
        /// </remarks>
        public IActionResult OnPost(BaseErpPageModel pageModel)
        {
            if (pageModel == null)
            {
                return null;
            }

            try
            {
                // Check if this is an approval action submission
                var formCollection = pageModel.PageContext?.HttpContext?.Request?.Form;
                if (formCollection == null)
                {
                    return null;
                }

                // Check for approval action type in form data
                if (!formCollection.ContainsKey(FORM_ACTION_TYPE))
                {
                    return null;
                }

                var actionType = formCollection[FORM_ACTION_TYPE].ToString()?.ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(actionType))
                {
                    return null;
                }

                // Process the approval action
                ProcessApprovalAction(pageModel, actionType, formCollection);
            }
            catch (Exception ex)
            {
                // Log the error and add validation message
                System.Diagnostics.Debug.WriteLine($"ApprovalPageHook.OnPost error: {ex.Message}");
                
                // Add error to page validation if available
                if (pageModel.Validation != null)
                {
                    pageModel.Validation.AddError("ApprovalAction", $"Error processing approval action: {ex.Message}");
                }
            }

            return null;
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Extracts the current record ID and entity name from the page model context.
        /// </summary>
        /// <param name="pageModel">The page model to extract record information from.</param>
        /// <returns>
        /// A tuple containing the record ID and entity name, or null if no record context exists.
        /// </returns>
        /// <remarks>
        /// This method attempts to get record information from multiple sources:
        /// 1. ErpRequestContext.RecordId and Entity
        /// 2. DataModel "Record" property
        /// 
        /// For record detail pages, the RecordId is typically set in ErpRequestContext.
        /// For other page types, the record may be accessed via the DataModel.
        /// </remarks>
        private (Guid recordId, string entityName)? GetRecordFromPageModel(BaseErpPageModel pageModel)
        {
            // First, try to get from ErpRequestContext (preferred for record pages)
            var reqContext = pageModel.ErpRequestContext;
            if (reqContext != null && reqContext.RecordId.HasValue && reqContext.Entity != null)
            {
                return (reqContext.RecordId.Value, reqContext.Entity.Name);
            }

            // Try to get from DataModel Record property
            if (pageModel.DataModel != null)
            {
                try
                {
                    var record = pageModel.DataModel.GetProperty("Record") as EntityRecord;
                    if (record != null && record.Properties.ContainsKey("id"))
                    {
                        var recordId = record["id"] as Guid?;
                        if (recordId.HasValue && reqContext?.Entity != null)
                        {
                            return (recordId.Value, reqContext.Entity.Name);
                        }
                    }
                }
                catch
                {
                    // Property doesn't exist or other error - continue
                }
            }

            return null;
        }

        /// <summary>
        /// Determines if the page should display approval workflow information.
        /// </summary>
        /// <param name="page">The ERP page to check.</param>
        /// <returns>
        /// True if the page should show approval information, false otherwise.
        /// </returns>
        /// <remarks>
        /// Currently, approval information is shown on record detail pages
        /// where a specific record is being viewed. This excludes:
        /// - List pages (no single record context)
        /// - Create pages (record doesn't exist yet)
        /// - Home/Dashboard pages
        /// 
        /// The method can be extended to check for specific page configurations
        /// or approval-enabled entities.
        /// </remarks>
        private bool IsApprovalRelatedPage(ErpPage page)
        {
            if (page == null)
            {
                return false;
            }

            // Show approval info on record detail and record manage pages
            // These are pages where a single record is being viewed/edited
            switch (page.Type)
            {
                case PageType.RecordDetails:
                case PageType.RecordManage:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Injects approval workflow data into the page model's DataModel.
        /// </summary>
        /// <param name="pageModel">The page model to inject data into.</param>
        /// <param name="recordId">The source record ID to find approval request for.</param>
        /// <param name="entityName">The entity name of the source record.</param>
        /// <remarks>
        /// This method retrieves and injects the following data:
        /// 1. ApprovalRequest: Active/pending approval request for the record
        /// 2. ApprovalHistory: Complete history timeline for the request
        /// 3. ApprovalStatus: Current status string
        /// 4. IsAuthorizedApprover: Whether current user can approve
        /// 
        /// If no approval request exists for the record, null values are injected
        /// to indicate no active workflow.
        /// </remarks>
        private void InjectApprovalDataIntoModel(BaseErpPageModel pageModel, Guid recordId, string entityName)
        {
            // Get the current user ID for authorization checks
            var currentUserId = pageModel.CurrentUser?.Id ?? Guid.Empty;

            // Initialize services
            var requestService = new ApprovalRequestService();
            var historyService = new ApprovalHistoryService();
            var routeService = new ApprovalRouteService();

            // Find approval request for this source record
            EntityRecord approvalRequest = GetApprovalRequestForSourceRecord(recordId, entityName);

            EntityRecordList approvalHistory = null;
            string approvalStatus = null;
            bool isAuthorizedApprover = false;

            if (approvalRequest != null)
            {
                var requestId = (Guid)approvalRequest["id"];

                // Get approval history for the request
                approvalHistory = historyService.GetRequestHistory(requestId);

                // Get current status
                approvalStatus = approvalRequest[FIELD_STATUS] as string;

                // Check if current user is authorized to approve at the current step
                if (currentUserId != Guid.Empty && 
                    string.Equals(approvalStatus, ApprovalStatus.Pending.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    isAuthorizedApprover = CheckUserAuthorization(approvalRequest, currentUserId, routeService);
                }
            }

            // Inject data into DataModel
            // Note: We use reflection or dynamic property setting since DataModel doesn't expose
            // a direct SetProperty method - we'll use the indexer if available
            try
            {
                // The DataModel uses an indexer pattern for property access
                // We can set custom properties that components can then access
                if (pageModel.DataModel != null)
                {
                    // Store approval data as part of the Record if possible
                    // Components can then access via DataModel.GetProperty("ApprovalRequest")
                    var record = pageModel.DataModel.GetProperty("Record") as EntityRecord;
                    if (record != null)
                    {
                        // Add approval data as properties on the record
                        record[DATAMODEL_APPROVAL_REQUEST] = approvalRequest;
                        record[DATAMODEL_APPROVAL_HISTORY] = approvalHistory;
                        record[DATAMODEL_APPROVAL_STATUS] = approvalStatus;
                        record[DATAMODEL_IS_AUTHORIZED_APPROVER] = isAuthorizedApprover;
                    }
                }
            }
            catch
            {
                // If DataModel manipulation fails, the approval data simply won't be available
                // This is acceptable as the page will still render
            }

            // Also store in ViewData for Razor access if HttpContext is available
            try
            {
                var httpContext = pageModel.PageContext?.HttpContext;
                if (httpContext != null)
                {
                    httpContext.Items[DATAMODEL_APPROVAL_REQUEST] = approvalRequest;
                    httpContext.Items[DATAMODEL_APPROVAL_HISTORY] = approvalHistory;
                    httpContext.Items[DATAMODEL_APPROVAL_STATUS] = approvalStatus;
                    httpContext.Items[DATAMODEL_IS_AUTHORIZED_APPROVER] = isAuthorizedApprover;
                }
            }
            catch
            {
                // HttpContext manipulation failed - acceptable fallback
            }
        }

        /// <summary>
        /// Gets the approval request for a given source record.
        /// </summary>
        /// <param name="sourceRecordId">The source record ID.</param>
        /// <param name="entityName">The source entity name.</param>
        /// <returns>The approval request EntityRecord, or null if not found.</returns>
        /// <remarks>
        /// This method queries the approval_request entity for a record matching
        /// the source record ID and entity name. It returns the first matching
        /// request, prioritizing pending requests.
        /// </remarks>
        private EntityRecord GetApprovalRequestForSourceRecord(Guid sourceRecordId, string entityName)
        {
            try
            {
                // Query for approval request matching this source record
                // Prioritize pending requests, but also return completed ones for history display
                var eqlCommand = $@"SELECT * FROM {ENTITY_APPROVAL_REQUEST} 
                                    WHERE {FIELD_SOURCE_RECORD_ID} = @sourceRecordId 
                                    AND {FIELD_SOURCE_ENTITY} = @entityName 
                                    ORDER BY 
                                        CASE WHEN {FIELD_STATUS} = 'pending' THEN 0 ELSE 1 END,
                                        created_on DESC 
                                    PAGE 1 PAGESIZE 1";

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("sourceRecordId", sourceRecordId),
                    new EqlParameter("entityName", entityName)
                };

                var result = new EqlCommand(eqlCommand, eqlParams).Execute();
                return result?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting approval request: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Checks if the specified user is authorized to approve the request.
        /// </summary>
        /// <param name="request">The approval request to check authorization for.</param>
        /// <param name="userId">The user ID to check.</param>
        /// <param name="routeService">The route service for approver resolution.</param>
        /// <returns>True if the user is authorized, false otherwise.</returns>
        /// <remarks>
        /// Authorization is determined by:
        /// 1. Checking if the request was delegated to this user
        /// 2. Checking if the user is in the approvers list for the current step
        /// </remarks>
        private bool CheckUserAuthorization(EntityRecord request, Guid userId, ApprovalRouteService routeService)
        {
            try
            {
                // Check if request was delegated to this user
                var delegatedTo = request["delegated_to"] as Guid?;
                if (delegatedTo.HasValue && delegatedTo.Value == userId)
                {
                    return true;
                }

                // Check if user is in approvers list for current step
                var currentStepId = request["current_step_id"] as Guid?;
                if (currentStepId.HasValue)
                {
                    var approvers = routeService.GetApproversForStep(currentStepId.Value);
                    if (approvers != null && approvers.Contains(userId))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Processes an approval action submitted via form POST.
        /// </summary>
        /// <param name="pageModel">The page model containing form data.</param>
        /// <param name="actionType">The type of action (approve/reject/delegate).</param>
        /// <param name="formCollection">The form data collection.</param>
        /// <remarks>
        /// This method validates the action parameters and delegates processing
        /// to the ApprovalRequestService. Results are stored in HttpContext.Items
        /// for access by page components.
        /// </remarks>
        private void ProcessApprovalAction(BaseErpPageModel pageModel, string actionType, 
            Microsoft.AspNetCore.Http.IFormCollection formCollection)
        {
            // Get current user ID
            var currentUserId = pageModel.CurrentUser?.Id ?? Guid.Empty;
            if (currentUserId == Guid.Empty)
            {
                throw new UnauthorizedAccessException("User must be authenticated to perform approval actions.");
            }

            // Get request ID from form
            if (!formCollection.ContainsKey(FORM_REQUEST_ID))
            {
                throw new ArgumentException("Approval request ID is required.");
            }

            var requestIdStr = formCollection[FORM_REQUEST_ID].ToString();
            if (!Guid.TryParse(requestIdStr, out Guid requestId) || requestId == Guid.Empty)
            {
                throw new ArgumentException("Invalid approval request ID.");
            }

            // Get comments from form
            var comments = formCollection.ContainsKey(FORM_COMMENTS) 
                ? formCollection[FORM_COMMENTS].ToString() 
                : string.Empty;

            // Initialize service
            var requestService = new ApprovalRequestService();

            // Process based on action type
            EntityRecord result = null;
            string resultMessage = string.Empty;

            switch (actionType)
            {
                case ACTION_APPROVE:
                    result = requestService.ApproveRequest(requestId, currentUserId, comments);
                    resultMessage = "Request approved successfully.";
                    break;

                case ACTION_REJECT:
                    // Comments are required for rejection
                    if (string.IsNullOrWhiteSpace(comments))
                    {
                        throw new ArgumentException("Comments are required when rejecting a request.");
                    }
                    result = requestService.RejectRequest(requestId, currentUserId, comments);
                    resultMessage = "Request rejected.";
                    break;

                case ACTION_DELEGATE:
                    // Delegate-to user ID is required
                    if (!formCollection.ContainsKey(FORM_DELEGATE_TO))
                    {
                        throw new ArgumentException("Delegate-to user ID is required for delegation.");
                    }

                    var delegateToStr = formCollection[FORM_DELEGATE_TO].ToString();
                    if (!Guid.TryParse(delegateToStr, out Guid delegateToUserId) || delegateToUserId == Guid.Empty)
                    {
                        throw new ArgumentException("Invalid delegate-to user ID.");
                    }

                    result = requestService.DelegateRequest(requestId, currentUserId, delegateToUserId, comments);
                    resultMessage = "Request delegated successfully.";
                    break;

                default:
                    throw new ArgumentException($"Unknown approval action type: {actionType}");
            }

            // Store result in HttpContext.Items for page access
            try
            {
                var httpContext = pageModel.PageContext?.HttpContext;
                if (httpContext != null)
                {
                    httpContext.Items["ApprovalActionResult"] = result;
                    httpContext.Items["ApprovalActionMessage"] = resultMessage;
                    httpContext.Items["ApprovalActionSuccess"] = true;
                }
            }
            catch
            {
                // HttpContext manipulation failed - acceptable fallback
            }
        }

        #endregion
    }
}
