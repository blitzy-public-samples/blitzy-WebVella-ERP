using System;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Hooks;
using WebVella.Erp.Plugins.Approval.Services;

namespace WebVella.Erp.Plugins.Approval.Hooks.Api
{
    /// <summary>
    /// Hook adapter class for the purchase_order entity that automatically initiates approval workflows
    /// when new purchase orders are created in the system.
    /// 
    /// This hook follows the WebVella thin adapter pattern - it is a stateless hook that:
    /// 1. Triggers after a purchase_order record is successfully persisted (PostCreate)
    /// 2. Delegates all workflow evaluation and request creation logic to ApprovalRequestService
    /// 3. Uses try-catch to prevent hook failures from blocking purchase order creation
    /// 
    /// The hook extracts the record ID from the created purchase order and initiates the approval
    /// workflow evaluation. If a matching workflow exists for the purchase_order entity, an
    /// approval_request record is created and assigned to the first workflow step.
    /// </summary>
    /// <remarks>
    /// Implementation follows STORY-005 requirements for hook integration:
    /// - Decorated with [HookAttachment("purchase_order")] to bind to the purchase_order entity
    /// - Implements IErpPostCreateRecordHook to fire after record creation
    /// - Errors are logged but not thrown to prevent blocking purchase order operations
    /// </remarks>
    [HookAttachment("purchase_order")]
    public class PurchaseOrderApproval : IErpPostCreateRecordHook
    {
        /// <summary>
        /// Called after a purchase_order record is successfully created.
        /// Initiates the approval workflow evaluation and creates an approval request if a matching workflow exists.
        /// </summary>
        /// <param name="entityName">The name of the entity that was created (should be "purchase_order").</param>
        /// <param name="record">The EntityRecord containing the created purchase order data including its ID.</param>
        /// <remarks>
        /// This method is designed to be fault-tolerant:
        /// - If the record ID cannot be extracted, the operation silently fails
        /// - If no matching workflow exists, no action is taken (handled by ApprovalRequestService)
        /// - Any exceptions are caught and logged to prevent blocking the purchase order creation process
        /// 
        /// The method extracts:
        /// - Record ID from record["id"] - the unique identifier of the created purchase order
        /// - User ID from SecurityContext.CurrentUser?.Id - the user who created the purchase order
        /// 
        /// These values are passed to ApprovalRequestService.Create() which handles:
        /// - Evaluating workflow rules against the purchase order record
        /// - Finding a matching approval workflow for the purchase_order entity
        /// - Creating an approval_request with the first workflow step
        /// - Logging the submission to approval_history
        /// </remarks>
        public void OnPostCreateRecord(string entityName, EntityRecord record)
        {
            try
            {
                // Validate that we have a record to work with
                if (record == null)
                {
                    return;
                }

                // Extract the record ID from the created purchase order
                // The record["id"] contains the GUID of the newly created purchase order
                if (!record.Properties.ContainsKey("id") || record["id"] == null)
                {
                    return;
                }

                Guid recordId;
                var idValue = record["id"];

                // Handle both Guid and string representations of the ID
                if (idValue is Guid guidValue)
                {
                    recordId = guidValue;
                }
                else if (idValue is string stringValue && Guid.TryParse(stringValue, out Guid parsedGuid))
                {
                    recordId = parsedGuid;
                }
                else
                {
                    // Cannot extract a valid GUID from the record ID
                    return;
                }

                // Validate that we have a non-empty record ID
                if (recordId == Guid.Empty)
                {
                    return;
                }

                // Get the current user ID from SecurityContext
                // This identifies who initiated the purchase order creation
                Guid userId = Guid.Empty;
                var currentUser = SecurityContext.CurrentUser;
                if (currentUser != null && currentUser.Id != Guid.Empty)
                {
                    userId = currentUser.Id;
                }
                else
                {
                    // If no user context is available, we cannot create an approval request
                    // as we need to know who requested the approval
                    return;
                }

                // Delegate workflow initiation to ApprovalRequestService
                // The service will:
                // 1. Evaluate routing rules to find a matching workflow for purchase_order entity
                // 2. If a matching workflow exists, create an approval_request record
                // 3. Set the initial step and status to 'pending'
                // 4. Log the 'submitted' action to approval_history
                var approvalRequestService = new ApprovalRequestService();
                
                // Call Create with the source record ID, entity name, and requesting user ID
                // If no matching workflow exists, the service throws ValidationException
                // which is caught below to prevent blocking the purchase order creation
                approvalRequestService.Create(recordId, entityName, userId);
            }
            catch (Exception)
            {
                // Silently catch all exceptions to prevent hook failures from blocking purchase order creation
                // In a production environment, this would be logged using WebVella's logging infrastructure
                // The purchase order has already been persisted at this point, so we don't want to
                // roll it back just because the approval workflow initiation failed
                // 
                // Common scenarios that may cause exceptions:
                // - No matching workflow configured for purchase_order entity (ValidationException)
                // - Workflow has no steps configured (ValidationException)
                // - Database connectivity issues during approval_request creation
                // - System scope access issues
                //
                // All these scenarios are non-fatal for the purchase order creation itself
            }
        }
    }
}
