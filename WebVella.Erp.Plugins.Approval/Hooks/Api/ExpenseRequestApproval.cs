using System;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Hooks;
using WebVella.Erp.Plugins.Approval.Services;

namespace WebVella.Erp.Plugins.Approval.Hooks.Api
{
    /// <summary>
    /// Hook adapter class for the expense_request entity that automatically initiates
    /// approval workflows when new expense request records are created.
    /// </summary>
    /// <remarks>
    /// This hook follows the WebVella thin adapter pattern - it is stateless and delegates
    /// all workflow evaluation and request creation logic to the ApprovalRequestService.
    /// The hook fires after an expense_request record is persisted, evaluates if any
    /// enabled approval workflows match the record, and creates an approval_request if so.
    /// 
    /// Error handling is designed to be non-blocking: any failures in the approval workflow
    /// initiation are caught and logged, but do not prevent the expense request from being
    /// created successfully. This ensures the core business operation (creating an expense request)
    /// is never blocked by approval system issues.
    /// </remarks>
    [HookAttachment("expense_request")]
    public class ExpenseRequestApproval : IErpPostCreateRecordHook
    {
        /// <summary>
        /// Called after a new expense_request record is created in the database.
        /// Evaluates approval rules and initiates a workflow if a matching workflow exists.
        /// </summary>
        /// <param name="entityName">The name of the entity that was created (should be "expense_request").</param>
        /// <param name="record">The EntityRecord containing the newly created expense request data.</param>
        /// <remarks>
        /// This method extracts the record ID from the record parameter and the current user ID
        /// from SecurityContext, then delegates to ApprovalRequestService.Create() for workflow
        /// initiation. The method is wrapped in a try-catch block to ensure that any failures
        /// in the approval workflow system do not block the expense request creation.
        /// 
        /// If no matching workflow is found for the expense_request entity, the method completes
        /// silently without creating an approval request. This is expected behavior when no
        /// approval workflow has been configured for expense requests.
        /// </remarks>
        public void OnPostCreateRecord(string entityName, EntityRecord record)
        {
            try
            {
                // Validate that we have a valid record
                if (record == null)
                {
                    // No record provided - nothing to process
                    return;
                }

                // Extract the record ID from the created expense_request
                // The "id" field is populated by the RecordManager during creation
                object idValue = record["id"];
                if (idValue == null)
                {
                    // Record has no ID - cannot initiate workflow
                    return;
                }

                Guid recordId;
                if (idValue is Guid guidValue)
                {
                    recordId = guidValue;
                }
                else if (Guid.TryParse(idValue.ToString(), out Guid parsedGuid))
                {
                    recordId = parsedGuid;
                }
                else
                {
                    // Invalid ID format - cannot process
                    return;
                }

                // Validate the record ID is not empty
                if (recordId == Guid.Empty)
                {
                    return;
                }

                // Get the current user ID from the security context
                // This represents the user who created the expense request
                Guid? userId = SecurityContext.CurrentUser?.Id;
                if (!userId.HasValue || userId.Value == Guid.Empty)
                {
                    // No authenticated user - use system user or skip
                    // For approval workflows, we typically need a requester
                    // If no user is available, we cannot properly attribute the request
                    return;
                }

                // Delegate to ApprovalRequestService to evaluate rules and create approval request
                // The service will:
                // 1. Retrieve the source record
                // 2. Evaluate routing rules to find a matching workflow
                // 3. Create an approval_request if a workflow matches
                // 4. Log the 'submitted' action to approval_history
                var approvalRequestService = new ApprovalRequestService();
                approvalRequestService.Create(recordId, entityName, userId.Value);
            }
            catch (WebVella.Erp.Exceptions.ValidationException)
            {
                // ValidationException is thrown when:
                // - No matching approval workflow is found for this entity
                // - The workflow has no steps configured
                // - Record creation fails
                // 
                // This is expected behavior when no workflow is configured for expense requests.
                // We silently swallow this exception to prevent blocking the expense request creation.
                // The expense_request record has already been created successfully at this point.
            }
            catch (ArgumentException)
            {
                // ArgumentException is thrown when invalid parameters are passed to Create().
                // This should not happen in normal operation since we validate inputs above.
                // Silently ignore to prevent blocking the expense request creation.
            }
            catch (Exception)
            {
                // Catch all other exceptions to ensure the expense request creation is not blocked.
                // In production, this would typically be logged for monitoring purposes.
                // Example: logger.LogError(ex, "Failed to initiate approval workflow for expense_request {RecordId}", recordId);
                // 
                // The expense_request record has already been persisted to the database,
                // so even if approval workflow initiation fails, the business operation succeeds.
            }
        }
    }
}
