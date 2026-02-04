# STORY-001 Testing Steps - Plugin Infrastructure

## Prerequisites
- .NET 9.0 SDK installed
- PostgreSQL database available and configured
- Application source code available
- **Set environment variable before testing:** `export ASPNETCORE_ENVIRONMENT=Development` (Linux/Mac) or `set ASPNETCORE_ENVIRONMENT=Development` (Windows)

## Steps to Test

### 1. Verify Plugin Project Structure
1. Open file explorer or terminal
2. Navigate to `WebVella.Erp.Plugins.Approval/` directory
3. **Expected Files:**
   - `WebVella.Erp.Plugins.Approval.csproj` (project file)
   - `ApprovalPlugin.cs` (main plugin entry point)
   - `ApprovalPlugin._.cs` (ProcessPatches orchestration)
   - `ApprovalPlugin.20260123.cs` (entity migration)
   - `Model/PluginSettings.cs` (version tracking)

### 2. Verify Plugin Registration in Solution
1. Open `WebVella.ERP3.sln`
2. Search for "Approval" project
3. **Expected Result:** `WebVella.Erp.Plugins.Approval` project is listed
4. Verify project references include:
   - `WebVella.Erp.Web`
   - `WebVella.Erp`

### 3. Verify Plugin Configuration in Site
1. Open `WebVella.Erp.Site/Startup.cs`
2. Search for "ApprovalPlugin"
3. **Expected Result:** Plugin registered in ConfigureServices/UseErpPlugin chain
4. Open `WebVella.Erp.Site/WebVella.Erp.Site.csproj`
5. **Expected Result:** Project reference to `WebVella.Erp.Plugins.Approval`

### 4. Build Solution
```bash
cd /tmp/blitzy/blitzy-WebVella-ERP/blitzy145b21cba
dotnet restore WebVella.ERP3.sln
dotnet build WebVella.ERP3.sln --configuration Debug
```
**Expected Result:** Build succeeds with 0 errors

### 5. Run Unit Tests
```bash
export ASPNETCORE_ENVIRONMENT=Development
dotnet test WebVella.Erp.Plugins.Approval.Tests/WebVella.Erp.Plugins.Approval.Tests.csproj \
    --configuration Release --verbosity normal
```
**Expected Result:** All 566 tests pass (437 unit tests + 129 integration tests)

### 6. Start Application and Verify Plugin Loads
```bash
cd WebVella.Erp.Site
dotnet run
```
**Expected Result:** 
- Application starts without errors
- Console shows plugin initialization messages
- No migration errors in logs

### 7. Verify Plugin via Admin Interface
1. Navigate to http://localhost:5000
2. Login as administrator
3. Navigate to SDK → Plugins (if available)
4. **Expected Result:** "approval" plugin listed

## Test Data Used
- No manual test data required

## Screenshots
- `plugin-project-structure.png` (file tree showing plugin files)
- `plugin-build-success.png` (terminal showing successful build)
- `plugin-tests-passed.png` (unit test results)

## Result
✅ PASS - Plugin infrastructure verified:
- Project structure correct
- Plugin registered in solution
- Site references plugin
- Build succeeds (0 errors)
- 566/566 tests pass (unit + integration)
- Application starts successfully
- Plugin initializes and runs migrations
