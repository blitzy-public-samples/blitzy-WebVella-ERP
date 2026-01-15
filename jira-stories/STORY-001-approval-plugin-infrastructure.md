# STORY-001: Approval Plugin Infrastructure

## Description

Create the foundational plugin infrastructure for the WebVella ERP Approval Workflow system. This story establishes the plugin scaffold following the established WebVella ERP plugin architecture, including the main plugin class inheriting from `ErpPlugin`, the project file configuration, migration orchestration setup, job scheduling infrastructure, and the plugin settings model.

The Approval plugin will serve as the foundation for implementing a multi-level approval workflow automation system that handles purchase orders, expense requests, and other business documents requiring hierarchical approval chains. This infrastructure story creates the structural foundation upon which all subsequent approval workflow functionality will be built.

The plugin follows the proven patterns established in existing WebVella plugins such as `WebVella.Erp.Plugins.Project`, ensuring consistency with the platform's architecture and enabling seamless integration with the ERP ecosystem.

## Business Value

- **Extensible Modular Architecture**: Establishes a clean plugin boundary that allows the approval workflow functionality to be developed, tested, and deployed independently of the core ERP system
- **Separation of Concerns**: Isolates approval-specific code from other ERP functionality, improving maintainability and reducing the risk of regressions
- **Independent Deployment Capability**: Enables the approval workflow feature to be enabled/disabled without affecting other ERP modules
- **Future-Proof Foundation**: Creates a standardized structure that supports incremental feature additions through dated migration patches
- **Consistent Developer Experience**: Follows established WebVella patterns, reducing the learning curve for developers familiar with the platform

## Acceptance Criteria

- [ ] **AC1**: The `WebVella.Erp.Plugins.Approval` plugin loads successfully when the ERP application starts, appearing in the list of registered plugins
- [ ] **AC2**: The `ProcessPatches()` method executes successfully during initialization, creating/reading the plugin data entry in the `plugin_data` database table with the correct plugin name "approval"
- [ ] **AC3**: The `SetSchedulePlans()` method registers a SchedulePlan for the approval notification job (to be implemented in STORY-006) with a 5-minute interval
- [ ] **AC4**: The `PluginSettings` object is properly serialized to JSON and persisted via `SavePluginData()`, and can be retrieved and deserialized via `GetPluginData()`
- [ ] **AC5**: No runtime errors occur during plugin initialization, with all operations completing within the `SecurityContext.OpenSystemScope()` and database transaction scope

## Technical Implementation Details

### Files/Modules to Create

| File Path | Description |
|-----------|-------------|
| `WebVella.Erp.Plugins.Approval/ApprovalPlugin.cs` | Main plugin class with `Initialize()` and `SetSchedulePlans()` methods |
| `WebVella.Erp.Plugins.Approval/ApprovalPlugin._.cs` | Partial class containing `ProcessPatches()` migration orchestration logic |
| `WebVella.Erp.Plugins.Approval/WebVella.Erp.Plugins.Approval.csproj` | Razor SDK project file with net9.0 target framework |
| `WebVella.Erp.Plugins.Approval/Model/PluginSettings.cs` | JSON DTO for plugin version tracking |

### Folder Structure to Create

```
WebVella.Erp.Plugins.Approval/
├── ApprovalPlugin.cs                    # Main plugin class
├── ApprovalPlugin._.cs                  # ProcessPatches() orchestrator
├── WebVella.Erp.Plugins.Approval.csproj # Project file
├── Components/                          # Future: Page components (STORY-008)
├── Controllers/                         # Future: REST API controllers (STORY-007)
├── DataSource/                          # Future: CodeDataSource implementations
├── Hooks/                               # Future: Api/ and Page/ hook adapters (STORY-005)
│   └── Api/                             # Record hooks
├── Jobs/                                # Future: ErpJob implementations (STORY-006)
├── Model/                               # Plugin-specific models
│   └── PluginSettings.cs                # Version tracking DTO
├── Services/                            # Future: Business logic services (STORY-004)
├── Theme/                               # Future: Custom styles
├── Utils/                               # Future: Utility classes
└── wwwroot/                             # Future: Static assets
```

### Key Classes and Functions

#### ApprovalPlugin.cs

```csharp
public partial class ApprovalPlugin : ErpPlugin
{
    [JsonProperty(PropertyName = "name")]
    public override string Name { get; protected set; } = "approval";

    public override void Initialize(IServiceProvider serviceProvider)
    {
        using (var ctx = SecurityContext.OpenSystemScope())
        {
            ProcessPatches();
            SetSchedulePlans();
        }
    }

    public void SetSchedulePlans()
    {
        // Registers SchedulePlan for ProcessApprovalNotificationsJob
        // GUID-based ID, Daily type, 5-minute interval
    }
}
```

**Source Pattern**: `WebVella.Erp.Plugins.Project/ProjectPlugin.cs`

#### ApprovalPlugin._.cs

```csharp
public partial class ApprovalPlugin : ErpPlugin
{
    private const int WEBVELLA_APPROVAL_INIT_VERSION = 20260115;

    public void ProcessPatches()
    {
        using (SecurityContext.OpenSystemScope())
        {
            var entMan = new EntityManager();
            var relMan = new EntityRelationManager();
            var recMan = new RecordManager();

            using (var connection = DbContext.Current.CreateConnection())
            {
                try
                {
                    connection.BeginTransaction();

                    // Load current plugin settings
                    var currentPluginSettings = new PluginSettings() { Version = WEBVELLA_APPROVAL_INIT_VERSION };
                    string jsonData = GetPluginData();
                    if (!string.IsNullOrWhiteSpace(jsonData))
                        currentPluginSettings = JsonConvert.DeserializeObject<PluginSettings>(jsonData);

                    // Version-gated patch execution (future patches added here)

                    SavePluginData(JsonConvert.SerializeObject(currentPluginSettings));
                    connection.CommitTransaction();
                }
                catch (Exception)
                {
                    connection.RollbackTransaction();
                    throw;
                }
            }
        }
    }
}
```

**Source Pattern**: `WebVella.Erp.Plugins.Project/ProjectPlugin._.cs`

#### Model/PluginSettings.cs

```csharp
internal class PluginSettings
{
    [JsonProperty(PropertyName = "version")]
    public int Version { get; set; }
}
```

**Source Pattern**: `WebVella.Erp.Plugins.Project/Model/PluginSettings.cs`

#### WebVella.Erp.Plugins.Approval.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
    <PropertyGroup>
        <TargetFramework>net9.0</TargetFramework>
        <AddRazorSupportForMvc>true</AddRazorSupportForMvc>
        <SatelliteResourceLanguages>en</SatelliteResourceLanguages>
    </PropertyGroup>

    <ItemGroup>
        <FrameworkReference Include="Microsoft.AspNetCore.App" />
    </ItemGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.AspNetCore.Mvc.NewtonsoftJson" Version="9.0.10" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\WebVella.Erp.Web\WebVella.Erp.Web.csproj" />
        <ProjectReference Include="..\WebVella.ERP\WebVella.Erp.csproj" />
    </ItemGroup>
</Project>
```

**Source Pattern**: `WebVella.Erp.Plugins.Project/WebVella.Erp.Plugins.Project.csproj`

### Integration Points

| Integration | Description |
|-------------|-------------|
| `ErpPlugin` base class | Inherits from `WebVella.Erp.ErpPlugin` for plugin registration and lifecycle |
| `SecurityContext.OpenSystemScope()` | Elevates permissions for system-level operations during initialization |
| `DbContext.Current.CreateConnection()` | Obtains database connection for transaction management |
| `ScheduleManager.Current` | Registers background job schedule plans |
| `GetPluginData()` / `SavePluginData()` | Persists plugin settings to `plugin_data` table |

### Technical Approach

1. **Create Project Structure**: Initialize the `WebVella.Erp.Plugins.Approval` project using the Razor SDK with net9.0 target framework, referencing `WebVella.Erp` and `WebVella.Erp.Web` projects

2. **Implement Main Plugin Class**: Create `ApprovalPlugin.cs` as a partial class inheriting from `ErpPlugin`, overriding the `Name` property with value "approval" and implementing `Initialize()` method

3. **Implement Migration Orchestrator**: Create `ApprovalPlugin._.cs` with `ProcessPatches()` method following the version-gated migration pattern:
   - Initialize managers (`EntityManager`, `EntityRelationManager`, `RecordManager`)
   - Begin database transaction
   - Load current plugin version from `plugin_data`
   - Execute patches based on version comparison (future patches)
   - Save updated version and commit transaction
   - Rollback on any exception

4. **Implement Schedule Plan Registration**: In `SetSchedulePlans()`, register a `SchedulePlan` for the notification job:
   - Use a fixed GUID for the schedule plan ID
   - Set type to `SchedulePlanType.Daily`
   - Configure 5-minute interval (`IntervalInMinutes = 5`)
   - Set `StartTimespan = 0` and `EndTimespan = 1440` for all-day execution
   - Enable all days of the week

5. **Create Plugin Settings Model**: Implement `Model/PluginSettings.cs` as an internal class with a `Version` property decorated with `[JsonProperty]` for JSON serialization

6. **Create Folder Structure**: Establish the standard plugin folder hierarchy (Components, Controllers, Hooks, Jobs, Model, Services, etc.) for future story implementations

### SchedulePlan Configuration Details

```csharp
Guid notificationSchedulePlanId = new Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890");
SchedulePlan notificationPlan = new SchedulePlan
{
    Id = notificationSchedulePlanId,
    Name = "Process Approval Notifications",
    Type = SchedulePlanType.Daily,
    StartDate = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, DateTimeKind.Utc),
    EndDate = null,
    ScheduledDays = new SchedulePlanDaysOfWeek
    {
        ScheduledOnMonday = true,
        ScheduledOnTuesday = true,
        ScheduledOnWednesday = true,
        ScheduledOnThursday = true,
        ScheduledOnFriday = true,
        ScheduledOnSaturday = true,
        ScheduledOnSunday = true
    },
    IntervalInMinutes = 5,
    StartTimespan = 0,
    EndTimespan = 1440,
    JobTypeId = new Guid("XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX"), // ProcessApprovalNotificationsJob ID
    Enabled = true
};
```

## Dependencies

**None** - This is the foundational story that all subsequent approval workflow stories depend upon.

## Effort Estimate

**3 Story Points**

This is a relatively straightforward infrastructure setup story that primarily involves creating boilerplate plugin structure following well-established patterns in the WebVella ERP codebase. The complexity is low because:
- Clear patterns exist in `WebVella.Erp.Plugins.Project`
- No complex business logic required
- Standard folder structure creation
- Minimal integration points

## Labels

`workflow` `approval` `backend` `infrastructure` `plugin`

---

## Additional Notes

### Source Code References

| Reference | Purpose |
|-----------|---------|
| `WebVella.Erp/ErpPlugin.cs` | Base class providing `GetPluginData()`, `SavePluginData()`, and `Initialize()` contract |
| `WebVella.Erp.Plugins.Project/ProjectPlugin.cs` | Reference implementation for plugin initialization and schedule plan registration |
| `WebVella.Erp.Plugins.Project/ProjectPlugin._.cs` | Reference implementation for `ProcessPatches()` migration orchestration |
| `WebVella.Erp.Plugins.Project/WebVella.Erp.Plugins.Project.csproj` | Reference project file structure |
| `WebVella.Erp.Plugins.Project/Model/PluginSettings.cs` | Reference settings model implementation |

### Future Patch Files

As the approval workflow evolves, migration patches will be added following the naming convention:
- `ApprovalPlugin.20260115.cs` - Initial entity creation (STORY-002)
- `ApprovalPlugin.YYYYMMDD.cs` - Future migrations

### Testing Considerations

- Verify plugin appears in `ErpService.Plugins` collection after startup
- Confirm `plugin_data` table entry exists with `name = 'approval'`
- Validate SchedulePlan registration via `ScheduleManager.Current.GetSchedulePlan()`
- Test transaction rollback on simulated failure
