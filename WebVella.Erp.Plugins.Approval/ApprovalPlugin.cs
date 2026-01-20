using System;
using WebVella.Erp.Api;
using WebVella.Erp.Plugins;

namespace WebVella.Erp.Plugins.Approval
{
    /// <summary>
    /// Approval Workflow Plugin for WebVella ERP.
    /// Provides workflow-based approval functionality for entity records including:
    /// - Multi-step approval workflows with configurable rules
    /// - User and role-based approver assignments
    /// - Request tracking and history auditing
    /// - Manager dashboard with real-time metrics
    /// </summary>
    public partial class ApprovalPlugin : ErpPlugin
    {
        /// <summary>
        /// Plugin identifier name used for registration and discovery
        /// </summary>
        public override string Name { get; protected set; } = "approval";

        /// <summary>
        /// Initializes the Approval plugin and runs database migrations.
        /// Called by the WebVella ERP plugin loader during application startup.
        /// </summary>
        /// <param name="serviceProvider">ASP.NET Core service provider for dependency injection</param>
        public override void Initialize(IServiceProvider serviceProvider)
        {
            // Process any pending database migrations
            ProcessPatches();
        }

        /// <summary>
        /// Processes database schema patches for the approval workflow entities.
        /// Migrations are tracked via plugin version in PluginSettings.
        /// </summary>
        private void ProcessPatches()
        {
            // Patch processing is handled by separate partial class files
            // following the WebVella ERP pattern (ApprovalPlugin.YYYYMMDD.cs)
            // Currently no patches defined - entities will be created by initial setup
        }
    }
}
