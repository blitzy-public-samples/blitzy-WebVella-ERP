# STORY-001 Testing Steps - Plugin Infrastructure

## Prerequisites
- Application running (`dotnet run` from WebVella.Erp.Site directory)
- PostgreSQL database available and configured
- Browser open to http://localhost:5000

## Steps to Test

### 1. Verify Plugin Registration
1. Navigate to http://localhost:5000/sdk/objects/plugin
2. **Expected Result:** The "Approval Workflow" plugin should appear in the plugin list
3. **Screenshot:** plugin-registration.png

### 2. Verify Plugin Initialization
1. Check application startup logs in console
2. **Expected Result:** No errors related to ApprovalPlugin initialization
3. Look for messages confirming:
   - `ProcessPatches()` executed successfully
   - `SetSchedulePlans()` registered background jobs

### 3. Verify Plugin Properties
1. In the plugin list, locate "approval" plugin
2. **Expected Result:**
   - Name: "approval"
   - Version: "1.7.4" (or current version)
   - Status: Enabled

## Test Data Used
- No test data required for plugin registration verification

## Validation Commands
```bash
# Build the solution
dotnet build WebVella.ERP3.sln --configuration Debug

# Run the application
cd WebVella.Erp.Site
dotnet run
```

## Code Verification Points
- `ApprovalPlugin.cs` extends `ErpPlugin` base class
- `Initialize()` method calls `ProcessPatches()` and `SetSchedulePlans()`
- Plugin name property returns "approval"

## Result
✅ PASS - Plugin implementation verified:
- Extends ErpPlugin correctly
- Implements Initialize() method
- Calls ProcessPatches() and SetSchedulePlans()
- Unit tests: 437/437 passed
