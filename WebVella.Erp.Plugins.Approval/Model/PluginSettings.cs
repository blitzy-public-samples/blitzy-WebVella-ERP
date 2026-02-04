using Newtonsoft.Json;

namespace WebVella.Erp.Plugins.Approval.Model
{
	/// <summary>
	/// Plugin settings DTO for tracking the Approval plugin's migration version state.
	/// Used by ProcessPatches() in ApprovalPlugin._.cs to enable version-gated patch execution.
	/// Persisted via GetPluginData()/SavePluginData() to the plugin_data entity's JSON data field.
	/// </summary>
	internal class PluginSettings
	{
		/// <summary>
		/// Gets or sets the current migration version of the Approval plugin.
		/// This value is compared against patch version numbers to determine which
		/// migrations need to be executed during plugin initialization.
		/// </summary>
		[JsonProperty(PropertyName = "version")]
		public int Version { get; set; }
	}
}
