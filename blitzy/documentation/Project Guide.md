# Blitzy Project Guide — WebApiController Single-Responsibility Refactor

> **Project:** Behavior-preserving readability & single-responsibility refactor of `WebVella.Erp.Web/Controllers/WebApiController.cs`
> **Branch:** `blitzy-aaa1e363-7856-44a8-8839-11d2fa456d52` · **HEAD:** `92000c36` · **Baseline:** `bfe15661`
> **Status:** 85.0% complete — all autonomous AAP-scoped work delivered & validated; remaining work is human path-to-production.

---

## 1. Executive Summary

### 1.1 Project Overview

This project is a **behavior-preserving readability refactor** of a single ASP.NET Core MVC controller — `WebApiController.cs` — within the **WebVella ERP** platform (.NET 9). The original 4,313-line class declared 70 long public action methods. The work decomposed those methods so each expresses a **single responsibility** (≤40-line target, 60-line hard ceiling), using Extract-Method and DRY de-duplication into ~100 **private helpers in the same file**. Target users are WebVella ERP developers and maintainers. Business impact: substantially improved maintainability and reviewability with **zero behavioral change**. Technical scope is strictly single-file and in-place: the public API surface, response shapes, and every message string are preserved byte-for-byte, with zero new compile errors or warnings.

### 1.2 Completion Status

```mermaid
%%{init: {'theme':'base', 'themeVariables':{'pie1':'#5B39F3','pie2':'#FFFFFF','pieStrokeColor':'#B23AF2','pieStrokeWidth':'2px','pieOuterStrokeWidth':'2px','pieTitleTextSize':'16px','pieSectionTextColor':'#111111'}}}%%
pie showData title Completion — 85.0% Complete
    "Completed Work (h)" : 85
    "Remaining Work (h)" : 15
```

| Metric | Hours |
|--------|-------|
| **Total Hours** | **100** |
| Completed Hours (AI) | 85 |
| Completed Hours (Manual) | 0 |
| **Completed Hours (AI + Manual)** | **85** |
| **Remaining Hours** | **15** |
| **Percent Complete** | **85.0%** |

> Completion is computed with the PA1 AAP-scoped methodology: `Completed ÷ (Completed + Remaining) = 85 ÷ 100 = 85.0%`. All remaining hours are standard human path-to-production (review + merge + manual verification), not new AAP scope.

### 1.3 Key Accomplishments

- ✅ All **six confirmed de-duplication / decomposition targets (T1–T6)** consolidated exactly once into named private helpers.
- ✅ All **19 methods that exceeded 60 lines were decomposed** — 0 methods now exceed 60 lines (longest is 57).
- ✅ **Public surface byte-identical** — 144 signature/attribute lines, empty diff between baseline and HEAD.
- ✅ **Response primitives preserved** — `NotFound` 21=21, `BadRequest` 6=6, `ViewComponent` 4=4; only the expected `Json` 40→38 and `ContentResult` 7→1 consolidation deltas.
- ✅ **All message strings verbatim**, including the **two intentional copy-paste artifacts** that must not be "corrected."
- ✅ **Single-file scope** — only `WebApiController.cs` changed (`git diff --name-status`).
- ✅ **Clean compile** — full solution builds with **0 errors / 28 pre-existing warnings**; the in-scope file's only warning is the pre-existing `ASP0019`.
- ✅ **Runtime validated** — host boots ("Application started" on `:5080`); three controller routes return **HTTP 302** (auth enforced, controller registered).

### 1.4 Critical Unresolved Issues

The refactor itself has **no unresolved defects**. The items below gate final release/validation but are either process steps or pre-existing out-of-scope environment conditions.

| Issue | Impact | Owner | ETA |
|-------|--------|-------|-----|
| Mandatory human full-diff review not yet performed | Gate 8 (Reviewability) requires human sign-off within the AAP's ≤6h window before merge | Reviewer | Within review window (≤6h) |
| No automated test suite exists anywhere in the solution | No regression safety net; behavior parity rests on differential analysis + manual smoke | Reviewer / QA | During review |
| *(Out of scope)* `Startup.cs` loads lowercase `"config.json"` while disk file is `Config.json` | Linux case-sensitive startup crash — blocks runtime boot (not caused by the refactor) | Platform / Host team | Separate PR |
| *(Out of scope)* Hardcoded unreachable DB connection string (`192.168.0.190:5436`) | Blocks DB connectivity for local/CI runtime (not caused by the refactor) | Platform / Host team | Separate PR |

### 1.5 Access Issues

No access issues prevent build validation: the repository is accessible, the solution restores and builds **offline** from cached NuGet packages, and all source is present.

| System / Resource | Type of Access | Issue Description | Resolution Status | Owner |
|-------------------|----------------|-------------------|-------------------|-------|
| Source repository | Read/Write | None — branch checked out, working tree clean, build succeeds | ✅ No issue | — |
| NuGet feed | Package restore | None — `dotnet restore` exits 0 from offline cache (122 packages) | ✅ No issue | — |
| PostgreSQL database | Network / DB | `Config.json` hardcodes `Server=192.168.0.190;Port=5436`, unreachable from build/CI — runtime only (out of scope) | ⚠ Open — provision a reachable DB for runtime | Platform / Host team |
| SMB file share | Network / file | `\\192.168.0.2\Share\erp3-files` unreachable; `EnableFileSystemStorage=false`, so not exercised | ⚠ Deferred (low) | Platform / Host team |

### 1.6 Recommended Next Steps

1. **[High]** Perform the human full-diff review of `WebApiController.cs` (+2,286 / −1,958) within the **≤6h reviewability window**, confirming every hunk is a pure structural move and all strings are byte-identical.
2. **[High]** Approve and merge the PR; verify the post-merge CI build is green.
3. **[Medium]** Stand up a runtime environment (work around the out-of-scope config-path case mismatch and provision a reachable PostgreSQL), then re-run the route-probing smoke test.
4. **[Medium]** Execute a manual regression smoke test of the six affected endpoint families (no automated tests exist).
5. **[Low]** Schedule **separate, out-of-scope efforts**: fix the config-path case sensitivity, externalize the DB connection string, upgrade vulnerable NuGet packages, and introduce a characterization test suite.

---

## 2. Project Hours Breakdown

### 2.1 Completed Work Detail

All completed work is autonomous (AI) and traces to a specific AAP requirement.

| Component | Hours | Description |
|-----------|------:|-------------|
| Codebase Analysis & Decomposition Planning | 12 | Reading the 4,313-line controller; identifying the 6 targets and all 19 methods >60 lines; designing the helper topology under a byte-identical-behavior constraint [AAP §0.1–0.4] |
| T1 — "Init SubmitObj" Parser Consolidation | 2 | `BuildEqlDataSourceQueryFromSubmit`; invoked *before* the `try` at both sites so the uncaught `throw` path is preserved [AAP T1] |
| T2 — EQL Error-Mapping Helpers | 3 | `JsonFromEqlException` + `JsonFromException`; used by `EqlQueryAction` + `DataSourceQueryAction`; the 2 `DataSourceAction` overloads correctly left byte-identical (different error-accumulation pattern) [AAP T2] |
| T3 — HTTP-500 `ContentResult` Consolidation | 3 | `LogErrorAndReturn500` across 7 sites with the per-method log label parameterized; non-targets correctly excluded [AAP T3] |
| T4 — `PageComponentRenderViews` Decomposition | 5 | 5 ordered helpers (request validation, type resolution, route-context simulation, model assembly, view dispatch); 170→43 lines; `ViewComponent` dispatch preserved [AAP T4] |
| T5 — `ToggleSection` Node-ID Resolver | 2 | `ResolveNodeIdsFromComponentData`; key-parameterized, 3 type-branches order-preserved, 4 messages reconstructed byte-for-byte [AAP T5] |
| T6 — Select2 Projection Helper | 2 | `MapRecordsToSelect2Items` `{id,text}` projection with the original fallback chains [AAP T6] |
| Method-Size Mandate — 15 Additional Decompositions | 34 | `PatchField` 302→33, `GetQuickSearch` 225→44, `UpdateSchedulePlan` 219→49, two relation methods 193→53, `CreateEntityRecordWithRelation` 168→33, and 10 more; ~90 additional private helpers (commit `0b9de60e`, +2,040 / −1,669) [AAP method-size rule] |
| 8-Gate Behavior-Preservation Verification | 11 | Differential analysis: public-surface diff, response-primitive counts, message parity, method-size scan, token-multiset comparison [AAP gates 1–8] |
| Runtime Smoke Validation | 6 | Solution build + host boot + 3-route HTTP-302 probing (with out-of-scope env workarounds) [path-to-production] |
| Iteration & Clean-Landing Rework | 5 | Two-commit landing (`1374cd94`, `0b9de60e`) achieving zero new errors/warnings [AAP gate 1] |
| **Total Completed** | **85** | |

### 2.2 Remaining Work Detail

All remaining work is human path-to-production; none is new AAP scope.

| Category | Hours | Priority |
|----------|------:|----------|
| Human full-diff review of `WebApiController.cs` within the ≤6h reviewability window (Gate 8 sign-off) | 6 | High |
| Manual endpoint regression smoke test of the 6 affected action families (no automated tests exist) | 4 | Medium |
| Runtime environment standup for independent re-verification (config-path workaround + reachable PostgreSQL) | 3 | Medium |
| PR approval, merge to mainline & post-merge CI build verification | 2 | High |
| **Total Remaining** | **15** | |

### 2.3 Hours Reconciliation

| Check | Result |
|-------|--------|
| Section 2.1 total (Completed) | **85h** |
| Section 2.2 total (Remaining) | **15h** |
| 2.1 + 2.2 = Total Project Hours (Section 1.2) | 85 + 15 = **100h** ✅ |
| Section 1.2 Remaining = Section 2.2 total = Section 7 pie "Remaining Work" | 15 = 15 = 15 ✅ |
| Completion % = 85 ÷ 100 | **85.0%** (consistent in §1.2, §7, §8) ✅ |

---

## 3. Test Results

**No automated test suite exists anywhere in the solution** — there are zero test projects, frameworks, or test attributes (independently confirmed: 0 csproj reference xUnit/NUnit/MSTest/`Microsoft.NET.Test`). The AAP scope is a single-file structural refactor that **explicitly mandates no new test files**. Therefore there are no unit/integration/E2E tests to execute or report.

| Test Category | Framework | Total Tests | Passed | Failed | Coverage % | Notes |
|---------------|-----------|------------:|-------:|-------:|-----------:|-------|
| Unit | — | 0 | 0 | 0 | N/A | No test project in solution |
| Integration | — | 0 | 0 | 0 | N/A | No test project in solution |
| UI / E2E | — | 0 | 0 | 0 | N/A | Backend API controller; no UI surface |
| API | — | 0 | 0 | 0 | N/A | No automated API tests; see runtime smoke below |

**Autonomous verification performed in lieu of a test suite** (from Blitzy's validation logs, independently reproduced):

| Verification Method | Result |
|---------------------|--------|
| Solution compile (`dotnet build WebVella.ERP3.sln -c Debug`) | ✅ 0 errors, 28 pre-existing warnings |
| In-scope file compile (`WebVella.Erp.Web`) | ✅ 0 errors; only pre-existing `ASP0019` warning |
| Public-surface differential (baseline ↔ HEAD) | ✅ Byte-identical (144 lines, empty diff) |
| Response-primitive counts | ✅ `NotFound`/`BadRequest`/`ViewComponent` unchanged; `Json` 40→38, `ContentResult` 7→1 (expected) |
| Method-size scan (brace-matched) | ✅ 0 methods >60 lines (max 57) |
| Runtime route smoke (3 routes) | ✅ HTTP 302 (auth enforced, controller registered) |

> **Integrity:** every entry above originates from Blitzy's autonomous validation logs for this project and was re-verified during this assessment. No external or fabricated test data is included.

---

## 4. Runtime Validation & UI Verification

**Runtime health** (host = `WebVella.Erp.Site`):

- ✅ **Operational** — Solution build succeeds (0 errors).
- ✅ **Operational** — `WebVella.Erp.Web` project compiles (0 errors); refactored controller present (4,641 lines).
- ✅ **Operational** — Web host reaches **"Application started. Now listening on http://127.0.0.1:5080"** (~4s).
- ✅ **Operational** — Controller registration & authorization: `api/v3/en_US/eql`, `api/v3.0/user/preferences/toggle-sidebar-size`, `api/v3.0/datasource/code-compile` each return **HTTP 302** (redirect to login → `[Authorize]` enforced, route resolved, not 404).
- ⚠ **Partial** — Authenticated request/response *parity* was not exercised against a live database (out-of-scope env blockers); registration + auth are proven, full payload parity rests on the differential analysis.
- ⚠ **Partial** — Runtime boot required out-of-scope workarounds (config-path symlink + provisioned PostgreSQL).

**API integration:**

- ✅ **Operational** — Route table intact; HTTP-verb/route/auth attributes byte-identical to baseline.
- ✅ **Operational** — View-component-rendering endpoints retain their `ViewComponent(...)` dispatch (count 4=4 preserved).

**UI verification:**

- **N/A** — `WebApiController` is a server-side API controller (`ApiControllerBase : Controller`). Per AAP §0.3.4 there is no user-facing UI, component library, or Figma material; the refactor alters no visual output.

---

## 5. Compliance & Quality Review

The AAP defines an **eight-gate** acceptance framework. Each gate was independently re-verified during this assessment (baseline `bfe15661` ↔ HEAD `92000c36`).

| # | Gate | Status | Evidence | Progress |
|---|------|--------|----------|:--------:|
| 1 | **Compile** — 0 new errors / 0 new warnings | ✅ Pass | Solution: 0 errors / 28 pre-existing warnings. In-scope file's only warning is `ASP0019` at L1785 — same `Headers.Add` line as baseline L3297 (verbatim-preserved) | 100% |
| 2 | **Public surface** | ✅ Pass | 144 public/`[Route]`/`[Http*]`/`[Authorize]`/`[AllowAnonymous]` lines; baseline↔HEAD diff empty | 100% |
| 3 | **Response parity** | ✅ Pass | `NotFound` 21=21, `BadRequest` 6=6, `ViewComponent` 4=4; `Json` 40→38 & `ContentResult` 7→1 explained by T2/T3 | 100% |
| 4 | **Message parity** | ✅ Pass | All literals preserved; the 2 intentional copy-paste artifacts present (count 2 each); T5 reconstructs messages via key interpolation | 100% |
| 5 | **Method size** | ✅ Pass | Brace-matched scan: 0 methods >60 lines; longest = `DataSourceAction` (57) | 100% |
| 6 | **De-duplication (T1–T6)** | ✅ Pass | All 11 helpers present with correct call-site counts; near-duplicates parameterized; non-targets excluded | 100% |
| 7 | **Scope** | ✅ Pass | `git diff --name-status` = only `M WebApiController.cs`; screenshot artifacts removed (commit `92000c36`) | 100% |
| 8 | **Reviewability** | 🟦 Agent-complete; awaiting human sign-off | Token-multiset diff shows only consolidation removed / scaffolding added; 45/72 public methods byte-identical | 95% |

**Fixes applied during autonomous validation:** **None required.** The refactor was complete and correct as committed; all eight gates passed on the as-committed state with a clean working tree.

**Outstanding compliance items:**

- Human reviewer sign-off on Gate 8 (the full diff must be validated within the AAP's ≤6h window).
- Manual runtime regression of authenticated endpoints (recommended given the absence of automated tests).

---

## 6. Risk Assessment

| Risk | Category | Severity | Probability | Mitigation | Status |
|------|----------|----------|-------------|------------|--------|
| No automated test suite anywhere — parity relies on differential analysis + manual smoke; no future regression net | Technical | Medium | Medium | Differential gate analysis complete; manual endpoint smoke before merge; consider characterization tests (separate effort) | Mitigated |
| Large single-file diff (+2,286 / −1,958) increases reviewer burden | Technical | Medium | Low | Every hunk verified as a pure structural move; review guided by the 8-gate evidence; AAP budgets ≤6h | Open (pending human review) |
| Pre-existing `ASP0019` warning deliberately preserved (behavior-preservation) | Technical | Low | N/A | Out of scope to "fix"; verbatim-preserved by design | Accepted |
| Zero security-surface change (`[Authorize]`/`[AllowAnonymous]` byte-identical; HTTP 302 proves enforcement) | Security | Low | Low | No action needed | No new risk |
| Pre-existing NuGet advisories — `NU1903` AutoMapper (high), `NU1902` MailKit — in out-of-scope manifests | Security | Medium | Medium | Upgrade in a separate scoped effort | Open (out of scope, documented) |
| Out-of-scope: `Startup.cs` loads `"config.json"` vs disk `Config.json` → Linux startup crash | Operational | High | High (Linux) | Rename/symlink or fix load path (separate PR); not caused by the refactor | Open (out of scope, documented) |
| Out-of-scope: hardcoded unreachable DB connection string (`192.168.0.190`) | Operational | High | High | Externalize config / env-specific connection string (separate PR) | Open (out of scope, documented) |
| No monitoring/health-check change (behavior-preserving refactor) | Operational | Low | Low | No action needed | No new risk |
| Authenticated request/response parity not exercised against a live DB (env blockers) | Integration | Medium | Low | Manual authenticated smoke of the 6 target endpoints post-env-standup | Open (recommended pre-merge) |
| External callers unaffected (public signatures byte-identical) | Integration | Low | Low | No call-site changes required anywhere in the solution | No risk |

---

## 7. Visual Project Status

**Project hours breakdown** (Completed = Dark Blue `#5B39F3`, Remaining = White `#FFFFFF`):

```mermaid
%%{init: {'theme':'base', 'themeVariables':{'pie1':'#5B39F3','pie2':'#FFFFFF','pieStrokeColor':'#B23AF2','pieStrokeWidth':'2px','pieOuterStrokeWidth':'2px','pieSectionTextColor':'#111111'}}}%%
pie showData title Project Hours — Completed vs Remaining
    "Completed Work" : 85
    "Remaining Work" : 15
```

**Remaining hours by category** (from Section 2.2, sums to 15h):

```mermaid
%%{init: {'theme':'base', 'themeVariables':{'pie1':'#5B39F3','pie2':'#7C5CF5','pie3':'#A78BF8','pie4':'#CBBDFB','pieStrokeColor':'#B23AF2','pieSectionTextColor':'#111111'}}}%%
pie showData title Remaining Work by Category (15h)
    "Full-diff review (High)" : 6
    "Manual regression smoke (Medium)" : 4
    "Runtime env standup (Medium)" : 3
    "PR approval & merge (High)" : 2
```

> **Integrity:** the pie "Remaining Work" value (15) equals Section 1.2 Remaining Hours (15) and the Section 2.2 Hours total (15). "Completed Work" (85) equals Section 1.2 Completed Hours (85).

---

## 8. Summary & Recommendations

**Achievements.** The refactor delivers the full AAP scope: all six confirmed targets (T1–T6) were consolidated exactly once, and **every one of the 19 methods that exceeded 60 lines was decomposed** (the file now has zero methods over 60 lines, down from a 302-line maximum). The transformation is faithfully behavior-preserving — the public surface is byte-identical, response primitives are unchanged except for the expected consolidation deltas, and all message strings (including two intentional copy-paste artifacts) are verbatim. The solution compiles with **0 errors** and **no new warnings**, and the application boots with the refactored controller serving authenticated routes (HTTP 302).

**Remaining gaps.** The project is **85.0% complete**. The remaining 15 hours are entirely **human path-to-production**: the mandatory full-diff review within the ≤6h reviewability window, a manual regression smoke of the affected endpoints (necessary because no automated tests exist), a runtime environment standup for independent re-verification, and PR approval/merge with CI confirmation. None of this is new AAP scope.

**Critical path to production.** (1) Human review → (2) manual endpoint smoke → (3) merge + CI. The two out-of-scope environment blockers (config-path case sensitivity, hardcoded DB) do not affect the refactor's correctness but must be addressed — in **separate efforts** — for a clean deployment.

**Success metrics.**

| Metric | Target | Actual |
|--------|--------|--------|
| New compile errors | 0 | ✅ 0 |
| New compile warnings | 0 | ✅ 0 (only pre-existing `ASP0019` in scope) |
| Methods > 60 lines | 0 | ✅ 0 (was 19) |
| Files changed | 1 | ✅ 1 (`WebApiController.cs`) |
| AAP gates passed | 8 | ✅ 8 (Gate 8 awaiting human sign-off) |

**Production-readiness assessment.** The in-scope refactor is **production-ready pending human review**: it is structurally sound, behavior-preserving, single-file, and clean-compiling. Recommended posture: approve after the ≤6h diff review and the manual endpoint smoke; track the out-of-scope environment items as separate work.

---

## 9. Development Guide

### 9.1 System Prerequisites

- **.NET SDK 9.0** (verified `9.0.315`). `global.json` pins no version, so the latest installed 9.x is used.
- **PostgreSQL** (the ERP persists via Npgsql; connection string in `WebVella.Erp.Site/Config.json`).
- **OS:** Linux, macOS, or Windows. ⚠ On case-sensitive filesystems (Linux), see Troubleshooting for the `Config.json` load-path caveat.
- **Optional:** Docker (to run a local PostgreSQL container).

### 9.2 Environment Setup

```bash
# Load the .NET toolchain onto PATH (container convenience script)
source /etc/profile.d/dotnet.sh

# Confirm the SDK
dotnet --version        # => 9.0.315
dotnet --list-sdks      # => 9.0.315 [/usr/share/dotnet/sdk]
```

### 9.3 Dependency Installation

```bash
# From the repository root
dotnet restore WebVella.ERP3.sln        # => exit 0 ("All projects up-to-date" / restored from cache)
```

> A pre-existing advisory `NU1903` (AutoMapper) surfaces here from an out-of-scope manifest; it does not affect restore success.

### 9.4 Build

```bash
# Full solution (Debug)
dotnet build WebVella.ERP3.sln -c Debug --no-restore
# => Build succeeded.  0 Error(s)  28 Warning(s)   (all pre-existing / out-of-scope)

# Or just the project that contains the in-scope file
dotnet build WebVella.Erp.Web/WebVella.Erp.Web.csproj -c Debug
# => 0 Error(s); the only in-scope warning is the pre-existing ASP0019 in WebApiController.cs
```

### 9.5 Run the Application (host = `WebVella.Erp.Site`)

```bash
# 1) Provide a reachable PostgreSQL (example via Docker; matches Config.json port 5436)
docker run -d --name erp-pg \
  -e POSTGRES_USER=test -e POSTGRES_PASSWORD=test -e POSTGRES_DB=erp3 \
  -p 5436:5432 postgres:16

# 2) (Linux only) Work around the out-of-scope config-path case mismatch
cd WebVella.Erp.Site
ln -sf Config.json config.json     # Startup.cs loads lowercase "config.json"

# 3) Start the host
ASPNETCORE_URLS=http://127.0.0.1:5080 dotnet run --no-build -c Debug
# => "Application started. Now listening on http://127.0.0.1:5080"
```

### 9.6 Verification Steps

```bash
# Each refactored route should answer 302 (redirect to login = [Authorize] enforced, route resolved)
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:5080/api/v3/en_US/eql
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:5080/api/v3.0/user/preferences/toggle-sidebar-size
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:5080/api/v3.0/datasource/code-compile
# Expected: 302 for each
```

### 9.7 Reviewing the Refactor Diff

```bash
# Scope check — should list only WebApiController.cs
git diff bfe15661 HEAD --name-status

# Per-hunk structural review with extra context
git diff bfe15661 HEAD -U10 -- WebVella.Erp.Web/Controllers/WebApiController.cs | less

# Confirm the public surface is unchanged
diff \
  <(git show bfe15661:WebVella.Erp.Web/Controllers/WebApiController.cs | grep -E '^\s*(public |\[Route|\[Http|\[Authorize|\[AllowAnonymous)' | sed 's/^[[:space:]]*//' | sort) \
  <(grep -E '^\s*(public |\[Route|\[Http|\[Authorize|\[AllowAnonymous)' WebVella.Erp.Web/Controllers/WebApiController.cs | sed 's/^[[:space:]]*//' | sort)
# Expected: no output (identical)
```

### 9.8 Troubleshooting

- **Startup error "could not find / open `config.json`" on Linux** — `Startup.cs:42` loads lowercase `config.json` but the file on disk is `Config.json`. Symlink or rename (see §9.5 step 2). *Out of scope for this refactor.*
- **DB connection refused / timeout** — `Config.json` hardcodes `Server=192.168.0.190;Port=5436`. Point it at a reachable PostgreSQL (see §9.5 step 1). *Out of scope.*
- **Build shows `ASP0019` / `CS0618` / `CA2200` / `NU190x`** — these are **pre-existing** and out of scope; they are not introduced by the refactor. The in-scope `ASP0019` is verbatim-preserved by design.
- **`dotnet test` finds nothing** — expected; the solution contains no test projects.

---

## 10. Appendices

### Appendix A — Command Reference

| Purpose | Command |
|---------|---------|
| Load toolchain | `source /etc/profile.d/dotnet.sh` |
| SDK version | `dotnet --version` |
| Restore | `dotnet restore WebVella.ERP3.sln` |
| Build (solution) | `dotnet build WebVella.ERP3.sln -c Debug --no-restore` |
| Build (Web project) | `dotnet build WebVella.Erp.Web/WebVella.Erp.Web.csproj -c Debug` |
| Run host | `cd WebVella.Erp.Site && ASPNETCORE_URLS=http://127.0.0.1:5080 dotnet run -c Debug` |
| Scope diff | `git diff bfe15661 HEAD --name-status` |
| Full diff (context) | `git diff bfe15661 HEAD -U10 -- WebVella.Erp.Web/Controllers/WebApiController.cs` |

### Appendix B — Port Reference

| Port | Service |
|------|---------|
| `5080` | WebVella ERP web host (`ASPNETCORE_URLS`) |
| `5436` | PostgreSQL (per `Config.json`; container maps `5436→5432`) |

### Appendix C — Key File Locations

| Path | Role |
|------|------|
| `WebVella.Erp.Web/Controllers/WebApiController.cs` | **In-scope file** (4,641 lines after refactor) |
| `WebVella.Erp.Web/WebVella.Erp.Web.csproj` | In-scope project manifest |
| `WebVella.Erp.Site/Startup.cs` | Host startup (config load path — out of scope) |
| `WebVella.Erp.Site/Config.json` | Runtime config / DB connection (out of scope) |
| `WebVella.ERP3.sln` | Solution (20 projects) |
| `global.json` | SDK selection (no pinned version) |

### Appendix D — Technology Versions

| Component | Version |
|-----------|---------|
| .NET SDK | 9.0.315 |
| Target framework | `net9.0` (`Microsoft.NET.Sdk.Razor`) |
| Microsoft.AspNetCore.Mvc.NewtonsoftJson | 9.0.10 |
| Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation | 9.0.10 |
| Newtonsoft.Json | 13.0.4 |
| Wangkanai.Detection | 8.20.0 |
| System.IdentityModel.Tokens.Jwt | 8.14.0 |
| Microsoft.CodeAnalysis.* | 4.14.0 |
| WebVella.TagHelpers | 1.7.2 |

### Appendix E — Environment Variable Reference

| Variable | Value (example) | Purpose |
|----------|-----------------|---------|
| `ASPNETCORE_URLS` | `http://127.0.0.1:5080` | Bind address/port for the host |
| `ASPNETCORE_ENVIRONMENT` | `Development` | ASP.NET Core environment (optional) |
| `DOTNET_CLI_TELEMETRY_OPTOUT` | `1` | Disable SDK telemetry (optional) |

### Appendix F — Developer Tools / Review Guide

| Tool | Usage in this project |
|------|-----------------------|
| `git diff --name-status` | Confirm single-file scope (Gate 7) |
| `git diff -U10` | Hunk-by-hunk structural review (Gate 8) |
| `diff <(...) <(...)` | Public-surface parity check (Gate 2) |
| `dotnet build` | Clean-compile gate (Gate 1) |
| `curl -w "%{http_code}"` | Route/auth smoke (HTTP 302) |
| brace-matched method scan | Method-size gate (Gate 5) |

### Appendix G — Glossary

| Term | Meaning |
|------|---------|
| **AAP** | Agent Action Plan — the authoritative project requirements |
| **T1–T6** | The six confirmed de-duplication/decomposition targets |
| **Extract Method** | Refactoring that lifts a code fragment into a named helper |
| **EQL** | WebVella's Entity Query Language (queried by several endpoints) |
| **ContentResult** | ASP.NET Core MVC result returning raw content with a status code |
| **ViewComponent** | MVC mechanism for rendering a reusable view fragment |
| **ASP0019** | Analyzer warning: prefer `IHeaderDictionary.Append`/indexer over `IDictionary.Add` for headers (pre-existing, preserved) |
| **Byte-identical** | A string/signature reproduced exactly, character-for-character |

---

*Generated by the Blitzy Platform · Completion 85.0% (85h of 100h) · Branch `blitzy-aaa1e363-7856-44a8-8839-11d2fa456d52` @ `92000c36`*