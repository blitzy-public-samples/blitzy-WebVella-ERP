using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebVella.Erp.Api;
using WebVella.Erp.Database;

namespace WebVella.Erp.Plugins.Approval.Services
{
    /// <summary>
    /// Foundation base class for the Approval plugin service layer.
    /// Provides centralized access to WebVella ERP manager instances for all derived service classes.
    /// This pattern centralizes manager creation for consistent data access across all approval services.
    /// </summary>
    /// <remarks>
    /// All approval services (ApprovalRouteService, ApprovalRequestService, ApprovalHistoryService, 
    /// DashboardMetricsService, NotificationService) should inherit from this base class to gain
    /// access to the protected manager properties.
    /// 
    /// Properties are eagerly instantiated with private setters to prevent external modification
    /// while allowing derived classes to use them for database operations.
    /// 
    /// Note: This pattern constrains dependency injection and manager lifetimes, but follows
    /// the established WebVella plugin architecture for consistency.
    /// </remarks>
    public class BaseService
    {
        /// <summary>
        /// Gets the RecordManager instance for all CRUD operations on entity records.
        /// Used for creating, reading, updating, and deleting records in approval entities
        /// (approval_workflow, approval_step, approval_rule, approval_request, approval_history).
        /// </summary>
        protected RecordManager RecMan { get; private set; } = new RecordManager();

        /// <summary>
        /// Gets the EntityManager instance for entity schema operations and field metadata.
        /// Used for querying entity definitions, field configurations, and schema validation.
        /// </summary>
        protected EntityManager EntMan { get; private set; } = new EntityManager();

        /// <summary>
        /// Gets the SecurityManager instance for user/role queries and permission validation.
        /// Used for resolving approvers, validating user permissions, and checking role memberships.
        /// </summary>
        protected SecurityManager SecMan { get; private set; } = new SecurityManager();

        /// <summary>
        /// Gets the EntityRelationManager instance for entity relationship operations.
        /// Used for managing relations between approval entities (workflow-steps, step-rules, request-history).
        /// </summary>
        protected EntityRelationManager RelMan { get; private set; } = new EntityRelationManager();

        /// <summary>
        /// Gets the DbFileRepository instance for file storage operations.
        /// Used for handling file attachments associated with approval requests or comments.
        /// </summary>
        protected DbFileRepository Fs { get; private set; } = new DbFileRepository();
    }
}
