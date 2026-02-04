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
	/// Partial class for ApprovalPlugin containing the patch orchestration logic.
	/// This file manages database migration patches by tracking applied versions,
	/// executing patches in sequence, and handling transaction management.
	/// </summary>
	public partial class ApprovalPlugin : ErpPlugin
	{
		/// <summary>
		/// Initial version constant for the Approval plugin.
		/// This represents the base version before any patches are applied.
		/// All patch versions should be greater than this value.
		/// </summary>
		private const int WEBVELLA_APPROVAL_INIT_VERSION = 20240100;

		/// <summary>
		/// Orchestrates the sequential execution of migration patches for the Approval plugin.
		/// This method manages version tracking, transaction handling, and error recovery
		/// to ensure database schema is properly initialized and updated.
		/// </summary>
		/// <remarks>
		/// The method performs the following operations:
		/// 1. Opens a system security context for elevated operations
		/// 2. Creates manager instances for entity, relation, and record operations
		/// 3. Opens a database transaction for atomic patch execution
		/// 4. Loads current plugin settings (version) from storage
		/// 5. Executes patches sequentially based on version checks
		/// 6. Saves updated version after successful patch execution
		/// 7. Commits transaction on success or rolls back on failure
		/// </remarks>
		public void ProcessPatches()
		{
			using (SecurityContext.OpenSystemScope())
			{
				var entMan = new EntityManager();
				var relMan = new EntityRelationManager();
				var recMan = new RecordManager();
				var storeSystemSettings = DbContext.Current.SettingsRepository.Read();
				var systemSettings = new SystemSettings(storeSystemSettings);

				// Create transaction for atomic patch execution
				using (var connection = DbContext.Current.CreateConnection())
				{
					try
					{
						connection.BeginTransaction();

						// Plugin data is stored in the "plugin_data" entity -> "data" text field
						// as stringified JSON containing plugin settings including version

						#region << 1. Validate ERP database version and plugin dependencies >>

						if (systemSettings.Version > 0)
						{
							// Database version validation passed
							// Future: Add checks for specific version requirements if needed
						}

						#endregion

						#region << 2. Load current plugin settings from database >>

						var currentPluginSettings = new PluginSettings() { Version = WEBVELLA_APPROVAL_INIT_VERSION };
						string jsonData = GetPluginData();
						if (!string.IsNullOrWhiteSpace(jsonData))
						{
							currentPluginSettings = JsonConvert.DeserializeObject<PluginSettings>(jsonData);
						}

						#endregion

						#region << 3. Execute patches based on current installed version >>

						// Patch 20240101 - Initial schema creation
						// Creates all approval workflow entities, relations, and base configuration
						{
							var patchVersion = 20240101;
							if (currentPluginSettings.Version < patchVersion)
							{
								try
								{
									currentPluginSettings.Version = patchVersion;
									Patch20240101(entMan, relMan, recMan);
								}
								catch (ValidationException)
								{
									// Preserve validation exception details for debugging
									// Re-throw to maintain stack trace
									throw;
								}
								catch (Exception)
								{
									throw;
								}
							}
						}

						#endregion

						// Persist updated plugin settings with new version
						SavePluginData(JsonConvert.SerializeObject(currentPluginSettings));

						// Commit all changes atomically
						connection.CommitTransaction();
					}
					catch (ValidationException)
					{
						// Roll back on validation errors to maintain data integrity
						connection.RollbackTransaction();
						throw;
					}
					catch (Exception)
					{
						// Roll back on any other errors to maintain data integrity
						connection.RollbackTransaction();
						throw;
					}
				}
			}
		}
	}
}
