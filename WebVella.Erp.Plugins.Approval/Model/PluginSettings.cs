using Newtonsoft.Json;

namespace WebVella.Erp.Plugins.Approval.Model
{
	/// <summary>
	/// Internal class for plugin version tracking and configuration persistence.
	/// Used by ApprovalPlugin._.cs ProcessPatches() method to track which migration
	/// patches have been applied to the plugin. Enables incremental schema updates
	/// by storing the current version number and comparing against available patches.
	/// </summary>
	internal class PluginSettings
	{
		/// <summary>
		/// Gets or sets the current version number of the plugin's applied patches.
		/// This value is incremented as each migration patch is successfully applied,
		/// allowing ProcessPatches() to determine which patches still need to run.
		/// </summary>
		[JsonProperty(PropertyName = "version")]
		public int Version { get; set; }
	}
}
