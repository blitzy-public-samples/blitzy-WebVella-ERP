using Newtonsoft.Json;
using System;
using WebVella.Erp.Api;

namespace WebVella.Erp.Plugins.Approval
{
    /// <summary>
    /// Approval Workflow Plugin for WebVella ERP.
    /// Provides manager approval dashboard with real-time metrics,
    /// approval request processing, and workflow management capabilities.
    /// </summary>
    public class ApprovalPlugin : ErpPlugin
    {
        /// <summary>
        /// Plugin identifier name used for registration and data storage.
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public override string Name { get; protected set; } = "approval";

        /// <summary>
        /// Plugin version number for migration tracking.
        /// </summary>
        [JsonProperty(PropertyName = "version")]
        public override int Version { get; protected set; } = 1;

        /// <summary>
        /// Human-readable description of the plugin.
        /// </summary>
        [JsonProperty(PropertyName = "description")]
        public override string Description { get; protected set; } = 
            "Approval Workflow Plugin providing manager dashboard with real-time metrics, " +
            "approval request processing, and workflow management.";

        /// <summary>
        /// Company name for the plugin author.
        /// </summary>
        [JsonProperty(PropertyName = "company")]
        public override string Company { get; protected set; } = "WebVella";

        /// <summary>
        /// Company website URL.
        /// </summary>
        [JsonProperty(PropertyName = "company_url")]
        public override string CompanyUrl { get; protected set; } = "https://webvella.com";

        /// <summary>
        /// Plugin author name.
        /// </summary>
        [JsonProperty(PropertyName = "author")]
        public override string Author { get; protected set; } = "WebVella";

        /// <summary>
        /// License type for the plugin.
        /// </summary>
        [JsonProperty(PropertyName = "license")]
        public override string License { get; protected set; } = "Apache-2.0";

        /// <summary>
        /// Repository URL for the plugin source code.
        /// </summary>
        [JsonProperty(PropertyName = "repository")]
        public override string Repository { get; protected set; } = 
            "https://github.com/WebVella/WebVella-ERP";

        /// <summary>
        /// Initializes the plugin when the ERP system starts.
        /// Opens a system security scope to ensure proper permissions for initialization.
        /// </summary>
        /// <param name="serviceProvider">Service provider for dependency injection</param>
        public override void Initialize(IServiceProvider serviceProvider)
        {
            using (var ctx = SecurityContext.OpenSystemScope())
            {
                // Plugin initialization logic
                // Future: Add database migrations, schedule plans, etc.
            }
        }
    }
}
