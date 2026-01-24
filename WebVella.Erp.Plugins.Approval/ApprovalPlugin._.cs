using Newtonsoft.Json;
using System;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Database;
using WebVella.Erp.Exceptions;
using WebVella.Erp.Plugins.Approval.Model;

namespace WebVella.Erp.Plugins.Approval
{
	/// <summary>
	/// Partial class containing the ProcessPatches orchestration logic for the Approval plugin.
	/// This file implements version-gated patch execution with transaction management,
	/// following the established WebVella plugin pattern.
	/// </summary>
	public partial class ApprovalPlugin : ErpPlugin
	{
		/// <summary>
		/// Initial version constant for the Approval plugin.
		/// This is used as the default version when no prior plugin data exists.
		/// </summary>
		private const int WEBVELLA_APPROVAL_INIT_VERSION = 20260101;

		/// <summary>
		/// Orchestrates the execution of all database migration patches for the Approval plugin.
		/// This method:
		/// - Opens a system security scope to bypass access controls
		/// - Creates necessary manager instances (Entity, EntityRelation, Record)
		/// - Loads current plugin settings from the database
		/// - Executes version-gated patches within a database transaction
		/// - Persists updated plugin settings after successful execution
		/// - Performs automatic rollback on any exception
		/// </summary>
		/// <remarks>
		/// Called from Initialize() in ApprovalPlugin.cs during application startup.
		/// Each patch is executed only once, tracked by the Version property in PluginSettings.
		/// New patches should be added in chronological order with version numbers in YYYYMMDD format.
		/// </remarks>
		public void ProcessPatches()
		{
			using (SecurityContext.OpenSystemScope())
			{
				// Create manager instances for entity and record operations
				var entMan = new EntityManager();
				var relMan = new EntityRelationManager();
				var recMan = new RecordManager();

				// Load system settings to check database version compatibility
				var storeSystemSettings = DbContext.Current.SettingsRepository.Read();
				var systemSettings = new SystemSettings(storeSystemSettings);

				// Create database transaction for atomic patch execution
				using (var connection = DbContext.Current.CreateConnection())
				{
					try
					{
						connection.BeginTransaction();

						// Here we need to initialize or update the environment based on the plugin requirements.
						// The default place for the plugin data is the "plugin_data" entity -> the "data" text field,
						// which is used to store stringified JSON containing the plugin settings or version

						#region << 1. Get the current ERP database version and check for dependencies >>

						if (systemSettings.Version > 0)
						{
							// Database is initialized - plugin can proceed with patches
							// Additional version checks can be added here if specific 
							// minimum versions are required for the approval plugin
						}

						#endregion

						#region << 2. Get the current plugin settings from the database >>

						// Initialize with default version if no prior data exists
						var currentPluginSettings = new PluginSettings() { Version = WEBVELLA_APPROVAL_INIT_VERSION };
						string jsonData = GetPluginData();
						if (!string.IsNullOrWhiteSpace(jsonData))
						{
							currentPluginSettings = JsonConvert.DeserializeObject<PluginSettings>(jsonData);
						}

						#endregion

						#region << 3. Run methods based on the current installed version of the plugin >>

						// Patch 20260123 - Initial entity schema creation
						// Creates: approval_workflow, approval_step, approval_rule, approval_request, approval_history
						{
							var patchVersion = 20260123;
							if (currentPluginSettings.Version < patchVersion)
							{
								try
								{
									currentPluginSettings.Version = patchVersion;
									Patch20260123(entMan, relMan, recMan);
								}
								catch (ValidationException ex)
								{
									// Preserve validation exception details for debugging
									var exception = ex;
									throw ex;
								}
								catch (Exception)
								{
									throw;
								}
							}
						}

						// Additional patches should be added here in chronological order
						// following the same pattern:
						//
						// {
						//     var patchVersion = YYYYMMDD;
						//     if (currentPluginSettings.Version < patchVersion)
						//     {
						//         try
						//         {
						//             currentPluginSettings.Version = patchVersion;
						//             PatchYYYYMMDD(entMan, relMan, recMan);
						//         }
						//         catch (ValidationException ex)
						//         {
						//             var exception = ex;
						//             throw ex;
						//         }
						//         catch (Exception)
						//         {
						//             throw;
						//         }
						//     }
						// }

						#endregion

						// Persist the updated plugin settings with the new version
						SavePluginData(JsonConvert.SerializeObject(currentPluginSettings));

						// Commit all changes if everything succeeded
						connection.CommitTransaction();
					}
					catch (ValidationException ex)
					{
						// Rollback transaction on validation errors
						connection.RollbackTransaction();
						throw ex;
					}
					catch (Exception)
					{
						// Rollback transaction on any other errors
						connection.RollbackTransaction();
						throw;
					}
				}
			}
		}
	}
}
