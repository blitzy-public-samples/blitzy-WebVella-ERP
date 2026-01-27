# STORY-008 Testing Steps - UI Components

## Prerequisites
- Application running (`dotnet run` from WebVella.Erp.Site directory)
- Database migrated with approval entities
- Valid admin login credentials

## Steps to Test

### 1. Verify Component Files Exist

#### 1.1 PcApprovalWorkflowConfig Component
```bash
ls -la WebVella.Erp.Plugins.Approval/Components/PcApprovalWorkflowConfig/
```
**Expected Files:**
- `PcApprovalWorkflowConfig.cs` (component class)
- `Design.cshtml` (page builder preview)
- `Display.cshtml` (runtime display)
- `Options.cshtml` (configuration panel)
- `Help.cshtml` (documentation)
- `Error.cshtml` (error display)
- `service.js` (client-side logic)

#### 1.2 PcApprovalRequestList Component
```bash
ls -la WebVella.Erp.Plugins.Approval/Components/PcApprovalRequestList/
```
**Expected Files:** Same structure as above

#### 1.3 PcApprovalAction Component
```bash
ls -la WebVella.Erp.Plugins.Approval/Components/PcApprovalAction/
```
**Expected Files:** Same structure as above

#### 1.4 PcApprovalHistory Component
```bash
ls -la WebVella.Erp.Plugins.Approval/Components/PcApprovalHistory/
```
**Expected Files:** Same structure as above

### 2. Verify Component Registration

#### 2.1 Check PageComponent Attributes
```bash
grep -n "\[PageComponent\]" WebVella.Erp.Plugins.Approval/Components/*/*.cs
```
**Expected:** Each component has `[PageComponent]` attribute with:
- `Label`
- `Library = "WebVella"`
- `Description`
- `Version = "0.0.1"`
- `IconClass`
- `Category = "Approval Workflow"`

#### 2.2 Check Component Base Class
```bash
grep -n "class.*:.*PageComponent" WebVella.Erp.Plugins.Approval/Components/*/*.cs
```
**Expected:** Each component extends `PageComponent`

### 3. Verify Embedded Resources

#### 3.1 Check csproj Configuration
```bash
grep -A 2 "EmbeddedResource" WebVella.Erp.Plugins.Approval/WebVella.Erp.Plugins.Approval.csproj
```
**Expected:** All service.js files listed as embedded resources

### 4. UI Testing (BLOCKED - Pre-existing SDK Bug)

**⚠️ BLOCKER: UI component testing cannot be performed**

**Root Cause:** Pre-existing bug in `WebVella.Erp.Web/Controllers/WebApiController.cs`
```csharp
[AcceptVerbs(new[] { "DELETE" }, Route = "{*filepath}")]
public IActionResult DeleteFile(string filepath)
```

**Impact:**
- Static files under `/_content/` return HTTP 405
- jQuery, Moment.js, Bootstrap fail to load
- Page Builder (`wv-pb-manager`) non-functional
- Cannot add/configure/test components through UI

**Evidence:**
```bash
curl -I http://localhost:5000/_content/WebVella.TagHelpers/lib/jquery/jquery.min.js
# Returns: HTTP/1.1 405 Method Not Allowed, Allow: DELETE
```

### 5. Component Logic Verification (Unit Tests)

#### PcApprovalWorkflowConfig Tests
- ✅ `InvokeAsync_DisplayMode_ReturnsWorkflowList`
- ✅ `InvokeAsync_DesignMode_ReturnsPreview`
- ✅ `InvokeAsync_OptionsMode_ReturnsConfigForm`
- ✅ `InvokeAsync_WithError_ReturnsErrorView`

#### PcApprovalRequestList Tests
- ✅ `InvokeAsync_DisplayMode_ReturnsPendingRequests`
- ✅ `InvokeAsync_WithFilters_AppliesFilters`
- ✅ `InvokeAsync_WithPagination_PaginatesResults`

#### PcApprovalAction Tests
- ✅ `InvokeAsync_DisplayMode_ReturnsActionButtons`
- ✅ `InvokeAsync_WithPendingRequest_ShowsApproveReject`
- ✅ `InvokeAsync_WithCompletedRequest_HidesButtons`

#### PcApprovalHistory Tests
- ✅ `InvokeAsync_DisplayMode_ReturnsTimeline`
- ✅ `InvokeAsync_WithHistory_ShowsAllEntries`
- ✅ `InvokeAsync_OrdersByDate_Descending`

### 6. Manual Component Verification

#### 6.1 Add Component to Page (When UI Fixed)
1. Navigate to SDK → Pages
2. Create or edit a page
3. Click "Add Component"
4. Search for "Approval"
5. **Expected:** All 4 components visible in "Approval Workflow" category:
   - Approval Workflow Config
   - Approval Request List
   - Approval Action
   - Approval History

#### 6.2 Configure Component
1. Add component to page body
2. Click Options tab
3. **Expected:** Configuration options render correctly

#### 6.3 Preview Component
1. Switch to Design mode
2. **Expected:** Preview content renders

#### 6.4 Test Display Mode
1. Navigate to page in browser
2. **Expected:** Component displays with real data

## Test Data Used
- Test workflows and requests created via API
- Sample approval data

## Screenshots
- `component-files-structure.png` - Component directory structure
- (UI screenshots blocked due to SDK bug)

## Result
⚠️ PARTIAL PASS - Component implementation verified:
- ✅ All 4 component classes created
- ✅ All view files (Design, Display, Options, Help, Error) created
- ✅ All service.js files created
- ✅ Components properly decorated with `[PageComponent]`
- ✅ Components extend `PageComponent` base class
- ✅ Embedded resources configured in csproj
- ✅ All unit tests pass (437/437)
- ⚠️ UI visual testing BLOCKED by pre-existing SDK bug

## Component Summary
| Component | Files | Category | Icon |
|-----------|-------|----------|------|
| PcApprovalWorkflowConfig | 7 | Approval Workflow | fa-cogs |
| PcApprovalRequestList | 7 | Approval Workflow | fa-list-alt |
| PcApprovalAction | 7 | Approval Workflow | fa-check-circle |
| PcApprovalHistory | 7 | Approval Workflow | fa-history |

## Notes for Future Testing
When the SDK bug is fixed:
1. Test each component in Page Builder
2. Verify Options configuration works
3. Verify Design preview renders
4. Verify Display mode with real data
5. Test client-side JavaScript (AJAX, refresh, etc.)
