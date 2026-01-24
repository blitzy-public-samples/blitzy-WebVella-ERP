using System.Collections.Generic;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Hooks;
using WebVella.Erp.Plugins.Approval.Services;

namespace WebVella.Erp.Plugins.Approval.Hooks.Api
{
    /// <summary>
    /// Hook adapter class for the approval_request entity.
    /// Implements pre-create validation and post-update side effects for approval request lifecycle.
    /// </summary>
    /// <remarks>
    /// This hook class follows the WebVella thin adapter pattern:
    /// - Stateless: No instance state is maintained between calls
    /// - Delegation: All business logic is delegated to the ApprovalRequestService
    /// - Single Responsibility: Only connects entity events to service methods
    /// 
    /// The hook handles two key entity lifecycle events:
    /// 1. PreCreate: Validates required fields and business rules before record persistence
    /// 2. PostUpdate: Triggers side effects when status changes (notifications, cascading updates)
    /// 
    /// Hook bindings are managed by WebVella via the [HookAttachment] attribute which
    /// registers this class to intercept operations on the "approval_request" entity.
    /// </remarks>
    [HookAttachment("approval_request")]
    public class ApprovalRequest : IErpPreCreateRecordHook, IErpPostUpdateRecordHook
    {
        /// <summary>
        /// Pre-create hook handler for approval_request entity validation.
        /// Called by WebVella before a new approval_request record is persisted to the database.
        /// </summary>
        /// <param name="entityName">The name of the entity being created (will be "approval_request").</param>
        /// <param name="record">The entity record being created, containing all field values to be persisted.</param>
        /// <param name="errors">
        /// A list to collect validation errors. If any errors are added, the record creation will be aborted.
        /// Each ErrorModel should include a Key (field name) and Message describing the validation failure.
        /// </param>
        /// <remarks>
        /// This method delegates to ApprovalRequestService.PreCreateApiHookLogic() which:
        /// - Validates required fields: workflow_id, source_entity_name, source_record_id, requested_by
        /// - Checks that the referenced workflow exists and is enabled
        /// - Prevents duplicate pending requests for the same source record
        /// 
        /// Any validation errors are added to the errors collection, which WebVella uses
        /// to prevent record creation and return appropriate error messages to the caller.
        /// </remarks>
        public void OnPreCreateRecord(string entityName, EntityRecord record, List<ErrorModel> errors)
        {
            new ApprovalRequestService().PreCreateApiHookLogic(entityName, record, errors);
        }

        /// <summary>
        /// Post-update hook handler for approval_request entity side effects.
        /// Called by WebVella after an approval_request record has been successfully updated in the database.
        /// </summary>
        /// <param name="entityName">The name of the entity that was updated (will be "approval_request").</param>
        /// <param name="record">The updated entity record containing the new field values after the update.</param>
        /// <remarks>
        /// This method delegates to ApprovalRequestService.PostUpdateApiHookLogic() which:
        /// - Detects when the status field has changed
        /// - Handles status-specific post-processing:
        ///   - APPROVED: Triggers approval completion logic (notifications, source record updates)
        ///   - REJECTED: Triggers rejection completion logic (notifications to requester)
        ///   - ESCALATED: Triggers escalation handling (reassignment, notifications)
        /// - Logs appropriate history entries via ApprovalHistoryService
        /// 
        /// This hook runs after the database transaction has committed, so any failures
        /// in post-processing do not roll back the status change itself.
        /// </remarks>
        public void OnPostUpdateRecord(string entityName, EntityRecord record)
        {
            new ApprovalRequestService().PostUpdateApiHookLogic(entityName, record);
        }
    }
}
