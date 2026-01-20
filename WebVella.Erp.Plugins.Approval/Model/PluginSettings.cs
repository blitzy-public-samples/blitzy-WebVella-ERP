using Newtonsoft.Json;

namespace WebVella.Erp.Plugins.Approval.Model
{
    /// <summary>
    /// Plugin settings stored in the database to track migration version and configuration.
    /// </summary>
    public class PluginSettings
    {
        /// <summary>
        /// Current plugin version for tracking applied migrations.
        /// Format: YYYYMMDD as integer (e.g., 20260120)
        /// </summary>
        [JsonProperty(PropertyName = "version")]
        public int Version { get; set; } = 0;
    }
}
