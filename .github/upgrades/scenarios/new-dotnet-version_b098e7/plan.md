# .NET 10.0 Upgrade Plan

## Table of Contents

- [Executive Summary](#executive-summary)
- [Migration Strategy](#migration-strategy)
- [Detailed Dependency Analysis](#detailed-dependency-analysis)
- [Project-by-Project Plans](#project-by-project-plans)
  - [StandupReminder.WindowsInterop](#standupreminderwindowsinterop)
  - [StandupReminder.App](#standupreminderapp)
- [Package Update Reference](#package-update-reference)
- [Breaking Changes Catalog](#breaking-changes-catalog)
- [Risk Management](#risk-management)
- [Testing & Validation Strategy](#testing--validation-strategy)
- [Complexity & Effort Assessment](#complexity--effort-assessment)
- [Source Control Strategy](#source-control-strategy)
- [Success Criteria](#success-criteria)

---

## Executive Summary

### Scenario Description

This plan outlines the upgrade of the StandupReminder solution from **.NET 8.0** to **.NET 10.0 LTS**. The solution consists of 2 WPF-based Windows desktop projects with a clear dependency structure.

### Scope

**Projects Affected:** 2 projects
- `StandupReminder.WindowsInterop` (Foundation library - no dependencies)
- `StandupReminder.App` (Main WPF application - depends on WindowsInterop)

**Current State:**
- Target Framework: `net8.0-windows10.0.17763.0`
- Total LOC: 2,533
- NuGet Packages: 2 (1 requires update)

**Target State:**
- Target Framework: `net10.0-windows`
- All projects building successfully
- All packages compatible with .NET 10.0

### Selected Strategy

**All-At-Once Strategy** - All projects upgraded simultaneously in a single coordinated operation.

**Rationale:**
- **Small Solution**: 2 projects (well below 5-project threshold)
- **Simple Dependency Structure**: Single linear dependency (App → WindowsInterop)
- **Current Framework**: Both projects already on modern .NET (net8.0)
- **Package Compatibility**: Only 1 package requires update (WPF-UI)
- **No Security Vulnerabilities**: All packages are secure
- **Homogeneous Targets**: Both projects targeting same framework (net10.0-windows)

This approach enables the fastest completion time with minimal complexity, as all changes can be validated together in a single build-test cycle.

### Complexity Assessment

**Discovered Metrics:**
- **Total Projects**: 2
- **Dependency Depth**: 2 levels (0 and 1)
- **Total Issues**: 468 API compatibility issues
- **Risk Indicators**: 
  - Security Vulnerabilities: 0 ✓
  - High LOC Projects: 0 (largest project: 2,326 LOC) ✓
  - Circular Dependencies: 0 ✓
  - Package Updates Required: 1

**Complexity Classification: SIMPLE**

The high issue count (468) is expected for .NET framework upgrades and primarily consists of:
- **432 Binary Incompatible APIs** (92.3%) - WPF/WinForms types requiring recompilation
- **19 Source Incompatible APIs** (4.1%) - Require recompilation, potential minor fixes
- **14 Behavioral Changes** (3.0%) - Runtime behavior differences to validate
- **3 Undefined Issues** (0.6%) - Minor edge cases

Most binary incompatible issues are WPF framework types (System.Windows.*) that don't require code changes, just recompilation with the new framework.

### Critical Issues

**Package Compatibility:**
- **WPF-UI 4.2.0**: Marked as incompatible - requires investigation
  - Assessment suggests downgrade to 2.0.3 (unusual - needs validation)
  - May need to find compatible version or replace library

**No Security Vulnerabilities** - All packages are secure ✓

### Recommended Approach

**All-At-Once Strategy** with single atomic upgrade operation:
1. Update both project files to `net10.0-windows` simultaneously
2. Update WPF-UI package (investigate compatible version)
3. Restore dependencies
4. Build solution and address any compilation errors
5. Validate application functionality

### Iteration Strategy

**Fast Batch Approach** (8 iterations total):
- **Phase 1**: Discovery & Classification (3 iterations) ✓
- **Phase 2**: Foundation (3 iterations)
- **Phase 3**: Detail Generation (1 iteration - batch both projects)
- **Final**: Success Criteria & Source Control (1 iteration)

---

## Migration Strategy

### Approach Selection

**Selected: All-At-Once Strategy**

All projects in the solution will be upgraded simultaneously in a single coordinated operation. This approach is optimal for this solution based on the following factors:

### Justification

#### Why All-At-Once is Appropriate

**Solution Characteristics:**
- ✓ **Small Solution**: 2 projects (well below 5-project threshold for incremental approach)
- ✓ **Simple Dependencies**: Single linear dependency chain (App → WindowsInterop)
- ✓ **Modern Starting Point**: Both projects already on .NET 8.0 (modern .NET)
- ✓ **Limited Package Updates**: Only 1 package requires update
- ✓ **No Security Risks**: Zero security vulnerabilities
- ✓ **Homogeneous Architecture**: Both WPF Windows desktop projects with same target

**Risk Assessment:**
- **Low Complexity**: Clear dependency structure with no cycles
- **Predictable Issues**: 468 API issues are mostly WPF/WinForms binary incompatibilities (expected, require recompilation not code changes)
- **Fast Validation**: Can test entire solution in single build-test cycle
- **No Intermediate States**: No need to maintain multi-targeting or compatibility layers

#### All-At-Once Strategy Rationale

**Advantages for This Solution:**
1. **Fastest Completion**: Single upgrade operation vs. multiple phases
2. **No Multi-Targeting Complexity**: No need for conditional compilation or compatibility code
3. **Clean Dependency Resolution**: All projects on same framework version immediately
4. **Simpler Testing**: Validate entire solution together
5. **Single Commit**: All changes in one atomic source control operation

**Minimal Risks:**
- Small codebase (2,533 LOC) limits blast radius
- WPF framework changes are well-documented (.NET 8 → 10)
- Package update scope is minimal (1 package)
- No production dependencies to coordinate

### Dependency-Based Ordering

Within the All-At-Once approach, the framework and package updates will be applied to projects in dependency order to ensure a clean build:

**Update Order:**
1. **StandupReminder.WindowsInterop** - Foundation library (no dependencies)
   - Update TargetFramework to `net10.0-windows`
   - No package updates required

2. **StandupReminder.App** - Main application (depends on WindowsInterop)
   - Update TargetFramework to `net10.0-windows`
   - Update WPF-UI package

**Build Order:**
- MSBuild will automatically build WindowsInterop first (dependency), then App
- This ensures App builds against the upgraded WindowsInterop assembly

### Execution Approach

**Atomic Operation:**
All project file modifications and package updates will be performed as a single batch, followed by:
1. Dependency restoration (`dotnet restore`)
2. Solution build (`dotnet build`)
3. Compilation error resolution (if any)
4. Rebuild verification
5. Application validation

**No Parallel Execution Needed:**
- Only 2 projects; sequential processing is fast
- Dependency constraint requires WindowsInterop before App
- Coordinated update ensures consistency

### Phase Definition

**Phase 1: Atomic Upgrade**

**Scope:** All projects

**Operations:**
- Update TargetFramework in both .csproj files
- Update WPF-UI package in StandupReminder.App.csproj
- Restore all dependencies
- Build solution
- Fix any compilation errors
- Verify solution builds with 0 errors

**Success Criteria:**
- Both projects target `net10.0-windows`
- All packages restored successfully
- Solution builds without errors
- Solution builds without warnings (stretch goal)

**Deliverables:**
- Modified StandupReminder.WindowsInterop.csproj
- Modified StandupReminder.App.csproj
- Successful build output

### Rollback Strategy

If critical issues are discovered during upgrade:

**Rollback Options:**
1. **Git Revert**: Revert commit on upgrade branch (fastest)
2. **Branch Switch**: Switch back to source branch (clean slate)
3. **Selective Revert**: Revert specific file changes if partial rollback needed

**Rollback Triggers:**
- Build errors that cannot be resolved within reasonable timeframe
- WPF-UI package compatibility issues blocking progress
- Unexpected behavioral changes affecting core functionality

---

## Detailed Dependency Analysis

### Dependency Graph Summary

The solution has a simple, linear dependency structure with no circular dependencies:

```
Level 0 (Foundation):
  └─ StandupReminder.WindowsInterop
     - Dependencies: 0
     - Dependants: 1 (StandupReminder.App)
     - Issues: 17 (16 binary incompatible, 0 source incompatible, 0 behavioral)

Level 1 (Application):
  └─ StandupReminder.App
     - Dependencies: 1 (StandupReminder.WindowsInterop)
     - Dependants: 0 (top-level application)
     - Issues: 451 (416 binary incompatible, 19 source incompatible, 14 behavioral)
```

### Project Groupings by Migration Phase

**All-At-Once Strategy: Single Phase**

Since this is a small solution with a clear dependency structure, all projects will be upgraded in a single coordinated operation:

**Phase 1: Atomic Upgrade** (All Projects)
- `StandupReminder.WindowsInterop` (Foundation library)
- `StandupReminder.App` (Main application)

Both projects will have their target frameworks updated simultaneously, followed by package updates, dependency restoration, and build verification in a single atomic operation.

### Critical Path Identification

**Dependency Order:**
1. `StandupReminder.WindowsInterop` (no dependencies - can be modified first)
2. `StandupReminder.App` (depends on WindowsInterop)

However, since we're using the All-At-Once strategy, both projects will be updated together. The build order will naturally respect dependencies (WindowsInterop builds before App), but the framework and package updates happen simultaneously.

**Critical Path:**
- Update both .csproj files → Restore packages → Build solution → Fix any compilation errors → Validate

### Circular Dependency Details

**None** - The solution has no circular dependencies. This is ideal for a clean upgrade.

---

## Project-by-Project Plans

### StandupReminder.WindowsInterop

**Project Type:** Class Library (Windows Interop)

**Current State:**
- Target Framework: `net8.0-windows10.0.17763.0`
- Dependencies: 0 (foundation library)
- NuGet Packages: 0
- Lines of Code: 207
- Files: 5
- Files with Incidents: 2
- Risk Level: 🟢 Low

**Target State:**
- Target Framework: `net10.0-windows`
- No package updates required

**Purpose:**
Foundation library providing Windows session monitoring and interop services. Used by StandupReminder.App for detecting system session changes (lock, unlock, logoff, etc.).

---

#### Migration Steps

**1. Prerequisites**
- None (foundation library has no dependencies)
- Ensure .NET 10 SDK installed

**2. Framework Update**
- Update `TargetFramework` in `StandupReminder.WindowsInterop.csproj`:
  ```xml
  <TargetFramework>net10.0-windows</TargetFramework>
  ```
- Note: Remove specific Windows version (`10.0.17763.0`) - .NET 10 uses simplified targeting

**3. Package/Dependency Updates**

No packages to update (this project has no NuGet dependencies).

**4. Expected Breaking Changes**

| Category | Count | Impact |
|----------|-------|--------|
| Binary Incompatible | 16 | Recompilation required |
| Source Incompatible | 0 | None |
| Behavioral Changes | 0 | None |

**Affected Areas:**
- `WindowsSessionEventMonitor.cs` - 16 binary incompatible APIs (all WPF types)

**Expected Changes:**
- **None** - Binary incompatible APIs require recompilation only, not code changes
- All 16 issues are WPF framework types that remain in .NET 10
- No API removals or signature changes expected

**5. Code Modifications**

**Expected:** None

The 16 binary incompatible APIs are all WPF types (System.Windows.*) that are available in .NET 10 with the same signatures. These issues indicate the assemblies need to be recompiled against .NET 10 framework references, not that code changes are needed.

**Files Affected:**
- `WindowsSessionEventMonitor.cs` - No changes expected (recompilation only)

**6. Testing Strategy**

**Unit Testing:**
- No test project currently exists for WindowsInterop
- Consider adding tests in future for session monitoring logic

**Integration Testing:**
- Will be tested via StandupReminder.App (consumer)
- Verify session event monitoring works after App upgrade

**Manual Testing:**
- Test through StandupReminder.App functionality
- Verify Windows session events (lock, unlock) are detected correctly

**7. Validation Checklist**

- [ ] Project builds without errors
- [ ] Project builds without warnings
- [ ] No NuGet restore errors
- [ ] Assembly references resolve correctly
- [ ] StandupReminder.App can reference upgraded assembly
- [ ] No breaking API changes in public interface

### StandupReminder.App

**Project Type:** WPF Application (Windows Desktop)

**Current State:**
- Target Framework: `net8.0-windows10.0.17763.0`
- Dependencies: 1 (StandupReminder.WindowsInterop)
- NuGet Packages: 2 (Microsoft.Toolkit.Uwp.Notifications, WPF-UI)
- Lines of Code: 2,326
- Files: 29
- Files with Incidents: 17
- Risk Level: 🟡 Medium

**Target State:**
- Target Framework: `net10.0-windows`
- WPF-UI package requires update (investigate compatible version)
- Microsoft.Toolkit.Uwp.Notifications - no update required (compatible)

**Purpose:**
Main WPF desktop application providing posture reminders and standup notifications for Windows users. Includes system tray integration, settings UI, and session monitoring.

---

#### Migration Steps

**1. Prerequisites**
- StandupReminder.WindowsInterop must be upgraded to net10.0-windows first (dependency)
- Ensure .NET 10 SDK installed
- Research WPF-UI package compatibility with .NET 10

**2. Framework Update**
- Update `TargetFramework` in `StandupReminder.App.csproj`:
  ```xml
  <TargetFramework>net10.0-windows</TargetFramework>
  ```
- Note: Remove specific Windows version (`10.0.17763.0`) - .NET 10 uses simplified targeting

**3. Package/Dependency Updates**

| Package | Current Version | Action | Target Version | Reason |
|---------|-----------------|--------|----------------|--------|
| Microsoft.Toolkit.Uwp.Notifications | 7.1.3 | Keep | 7.1.3 | ✅ Compatible with .NET 10 |
| WPF-UI | 4.2.0 | Investigate | TBD | ⚠️ Marked incompatible - research needed |

**WPF-UI Update Strategy:**
1. **First:** Check NuGet for latest WPF-UI version (may be 5.x, 6.x, or newer)
2. **Second:** Try building with current 4.2.0 first (assessment may be conservative)
3. **Third:** If incompatible, search for "WPF-UI .NET 10" compatibility
4. **Fallback:** Remove package if not critical or find alternative

**4. Expected Breaking Changes**

| Category | Count | Impact |
|----------|-------|--------|
| Binary Incompatible | 416 | Recompilation required |
| Source Incompatible | 19 | Minor code fixes needed |
| Behavioral Changes | 14 | Testing required |

**Technology Breakdown:**
- **WPF APIs**: 173 issues (38.5%) - Mostly recompilation
- **Windows Forms**: 119 issues (26.5%) - NotifyIcon tray integration
- **System.Drawing**: 11 issues (2.4%) - Icon handling
- **Legacy Controls**: 2 issues (0.4%) - Check usage

**Most Affected Files:**
1. `NotifyIconTrayService.cs` - 124 issues (mostly NotifyIcon/tray APIs)
2. `SettingsWindow.xaml.cs` - 68 issues (WPF controls)
3. `StandUpReminderWindow.xaml.cs` - 49 issues (WPF controls)
4. `PostureReminderScheduler.cs` - 41 issues (DispatcherTimer)
5. `App.xaml.cs` - 31 issues (WPF Application APIs)

**5. Code Modifications**

**High Priority - Source Incompatible (19 issues):**

**A. TimeSpan.FromMinutes (6 occurrences)**
- **File:** `PostureReminderScheduler.cs`, `SettingsWindowViewModel.cs`, `LocalAppDataReminderSettingsStore.cs`
- **Issue:** Potential source incompatibility
- **Expected Fix:** May need explicit type casting or method call adjustment
- **Example:** Verify `TimeSpan.FromMinutes(double)` calls compile correctly

**B. System.Drawing.Icon (8 occurrences)**
- **File:** `NotifyIconTrayService.cs`
- **Issue:** Source incompatible
- **Expected Fix:** May need System.Drawing.Common NuGet package explicitly referenced
- **Action:** If compilation errors occur, add:
  ```xml
  <PackageReference Include="System.Drawing.Common" Version="9.0.0" />
  ```

**C. ToolStripItemCollection.Add (5 occurrences)**
- **File:** `NotifyIconTrayService.cs`
- **Issue:** Source incompatible (ContextMenuStrip tray icon menu)
- **Expected Fix:** Verify method overload resolution; may need explicit parameter types

**Medium Priority - Behavioral Changes (14 issues):**

**D. System.Uri Constructor (8 occurrences)**
- **Files:** `App.xaml.cs`, `MainWindow.xaml.cs`, `SettingsWindow.xaml.cs`, `StandUpReminderWindow.xaml.cs`, generated files
- **Issue:** Behavioral change in Uri parsing or validation
- **Expected Impact:** Low (XAML resource loading)
- **Action:** Test XAML resource loading works correctly

**E. Uri Behavior (Total 8 behavioral issues)**
- **Impact:** Mostly XAML component loading (Application.LoadComponent)
- **Testing:** Verify all windows/controls load correctly

**Low Priority - Binary Incompatible (416 issues):**

Most binary incompatible APIs are WPF/WinForms framework types that require only recompilation:
- System.Windows.Controls.* (Slider, TextBox, Border, etc.)
- System.Windows.Forms.* (NotifyIcon, ContextMenuStrip, etc.)
- System.Windows.Threading.DispatcherTimer
- System.Windows.Window, Application, etc.

**No code changes expected** - these compile and run correctly in .NET 10.

**6. Testing Strategy**

**Build Testing:**
- Clean build to verify all projects compile
- Check for warnings related to deprecated APIs
- Verify NuGet package restore success

**Unit Testing:**
- No test project currently exists
- Consider adding tests for core business logic (scheduler, settings store)

**Integration Testing:**
- Test full application workflow:
  - Launch application
  - Verify system tray icon appears
  - Test tray menu functionality
  - Open Settings window
  - Configure reminder intervals
  - Test reminder notifications appear
  - Test session monitoring (lock/unlock detection)

**Manual Testing:**
- **Tray Icon:** Right-click system tray, verify context menu
- **Settings UI:** Open settings, verify all controls render correctly
- **Reminder Window:** Wait for reminder, verify window appears
- **Session Events:** Lock/unlock Windows session, verify detection
- **UWP Notifications:** Verify Windows 10/11 toast notifications work

**Performance Testing:**
- Monitor memory usage over time (DispatcherTimer memory leaks?)
- Verify timers fire at expected intervals

**Behavioral Change Testing:**
- Focus on Uri-related functionality (XAML resource loading)
- Test DispatcherTimer behavior (intervals, priority)
- Test NotifyIcon behavior (tray icon visibility, tooltips, balloon tips)

**7. Validation Checklist**

- [ ] Project builds without errors
- [ ] Project builds without warnings
- [ ] All NuGet packages restored successfully
- [ ] WPF-UI package resolved (compatible version found or removed)
- [ ] StandupReminder.WindowsInterop reference works correctly
- [ ] Application launches successfully
- [ ] System tray icon appears and is functional
- [ ] Tray context menu works (right-click)
- [ ] Settings window opens and displays correctly
- [ ] All WPF controls render properly
- [ ] Reminder timer fires correctly
- [ ] Reminder window appears with correct content
- [ ] UWP toast notifications work (Windows 10/11)
- [ ] Session monitoring works (lock/unlock detection)
- [ ] Application exits cleanly
- [ ] No memory leaks after extended use (30+ minutes)

---

## Package Update Reference

### Common Package Updates (Affecting Multiple Projects)

**None** - No packages are shared between projects.

### Project-Specific Package Updates

#### StandupReminder.WindowsInterop

**No package updates required** - This project has no NuGet dependencies.

#### StandupReminder.App

| Package | Current | Target | Update Reason | Priority | Notes |
|---------|---------|--------|---------------|----------|-------|
| Microsoft.Toolkit.Uwp.Notifications | 7.1.3 | 7.1.3 (keep) | ✅ Compatible with .NET 10 | N/A | No update needed |
| WPF-UI | 4.2.0 | **TBD** | ⚠️ Marked incompatible | 🔴 High | **Requires investigation** - see strategy below |

### WPF-UI Package Strategy

**Current Situation:**
- Assessment marks WPF-UI 4.2.0 as incompatible with .NET 10
- Suggested version 2.0.3 (downgrade) is unusual and likely incorrect
- WPF-UI is a popular UI library for modern WPF applications

**Investigation Steps:**
1. **Check NuGet.org**: Search for latest WPF-UI package version
   - Look for versions 5.x, 6.x, or newer
   - Check release notes for .NET 10 compatibility

2. **Check GitHub**: Visit WPF-UI repository
   - Look for .NET 10 support in issues/PRs
   - Check if library has been renamed or replaced

3. **Try Current Version First**: Attempt build with 4.2.0
   - Assessment tools can be overly conservative
   - Many packages work without explicit .NET 10 targeting

**Decision Matrix:**

| Scenario | Action | Impact |
|----------|--------|--------|
| Latest version (5.x+) supports .NET 10 | Update to latest | ✅ Best outcome |
| Current 4.2.0 builds successfully | Keep 4.2.0 | ✅ No changes needed |
| No compatible version available | Identify UI features used | ⚠️ Investigate alternatives |
| Package is non-critical | Remove package | ⚠️ May need UI adjustments |

**Potential Alternatives** (if WPF-UI incompatible):
- **ModernWpfUI** - Modern WPF UI library
- **MaterialDesignInXamlToolkit** - Material Design for WPF
- **MahApps.Metro** - Modern WPF styles and controls
- **Native WPF** - Use standard WPF controls with custom styling

### Package Update Execution Order

Since both projects are upgraded atomically (All-At-Once strategy):

1. **Update project files** (both TargetFramework changes)
2. **Update WPF-UI** in StandupReminder.App.csproj (after investigation)
3. **Restore packages** (`dotnet restore`)
4. **Build solution** (`dotnet build`)

If WPF-UI investigation is not complete before upgrade, can:
- Keep 4.2.0 and try building
- Add WPF-UI update as follow-up task if needed

---

## Breaking Changes Catalog

### Overview

Total issues: 468 (432 binary incompatible, 19 source incompatible, 14 behavioral changes)

**Key Insight:** Most issues (92.3%) are binary incompatible WPF/WinForms types that require recompilation only, not code changes.

### Critical Breaking Changes (Source Incompatible)

These may require code modifications:

#### 1. TimeSpan.FromMinutes - Source Incompatible (6 occurrences)

**Files Affected:**
- `PostureReminderScheduler.cs` (2 occurrences)
- `SettingsWindowViewModel.cs` (3 occurrences)
- `LocalAppDataReminderSettingsStore.cs` (3 occurrences)

**Issue:** Method overload resolution or implicit conversion changes

**Potential Fix:**
```csharp
// If this fails to compile:
var interval = TimeSpan.FromMinutes(reminderInterval);

// Try explicit type:
var interval = TimeSpan.FromMinutes((double)reminderInterval);
```

**Likelihood:** Low - TimeSpan.FromMinutes(double) exists in .NET 10
**Action:** Compile and check for errors; fix if needed

---

#### 2. System.Drawing.Icon - Source Incompatible (8 occurrences)

**Files Affected:**
- `NotifyIconTrayService.cs` (system tray icon functionality)

**Issue:** System.Drawing.Common may need explicit package reference in .NET 10

**Potential Fix:**
If compilation errors occur, add to `StandupReminder.App.csproj`:
```xml
<PackageReference Include="System.Drawing.Common" Version="9.0.0" />
```

**Likelihood:** Medium - System.Drawing.Common is often needed explicitly in .NET 5+
**Action:** Try build first; add package if needed

---

#### 3. ToolStripItemCollection.Add - Source Incompatible (5 occurrences)

**Files Affected:**
- `NotifyIconTrayService.cs` (tray context menu items)

**Issue:** Method overload resolution changes

**Potential Fix:**
```csharp
// If this fails:
items.Add("Menu Item", icon, handler);

// Try explicit:
items.Add("Menu Item", (Image)icon, handler);
// OR
items.Add(new ToolStripMenuItem("Menu Item", icon, handler));
```

**Likelihood:** Low - Common overload exists in .NET 10
**Action:** Compile and check for errors; fix if needed

---

### Behavioral Changes (Testing Required)

These APIs work but may behave differently:

#### 4. System.Uri Constructor (8 occurrences)

**Files Affected:**
- `App.xaml.cs`, `MainWindow.xaml.cs`, `SettingsWindow.xaml.cs`, `StandUpReminderWindow.xaml.cs`
- Generated XAML files (.g.i.cs)

**Issue:** Uri parsing or validation behavior changes

**Usage Context:**
```csharp
Application.LoadComponent(this, new Uri("/StandupReminder;component/MainWindow.xaml", UriKind.Relative));
```

**Potential Impact:**
- Uri validation may be stricter or more lenient
- Relative path resolution may differ
- Exception behavior may change

**Testing:**
- Verify all XAML windows/controls load correctly
- Check for Uri-related exceptions at startup
- Test resource loading (images, styles, etc.)

**Likelihood:** Low - XAML component loading is stable
**Action:** Manual testing during validation

---

### Expected Breaking Changes (Binary Incompatible - Recompilation Only)

These types require recompilation but no code changes:

#### WPF Framework Types (184 issues, 39.6%)

**Most Frequent:**
- `System.Windows.Controls.Slider` (20)
- `System.Windows.Threading.DispatcherTimer` (15)
- `System.Windows.RoutedEventHandler` (10)
- `System.Windows.Media.Color` (10)
- `System.Windows.Controls.Border` (7)
- `System.Windows.Controls.TextBox` (7)
- `System.Windows.Application` (6)
- `System.Windows.Window` (6)
- `System.Windows.WindowState` (6)
- `System.Windows.Interop.HwndSource` (6)

**Files Affected:** All XAML code-behind files and generated files

**Expected Impact:** None - Recompilation only

**Action:** Verify build succeeds; these APIs exist in .NET 10 unchanged

---

#### Windows Forms Types (119 issues, 25.6%)

**Most Frequent:**
- `System.Windows.Forms.ContextMenuStrip` (18)
- `System.Windows.Forms.NotifyIcon` (15)
- `System.Windows.Forms.ToolStripMenuItem` (8)
- `System.Windows.Forms.ToolStripItemCollection` (8)
- `System.Windows.Forms.ToolStripItem` (6)
- `System.Windows.Forms.MouseButtons` (3)

**Files Affected:** Primarily `NotifyIconTrayService.cs` (system tray integration)

**Expected Impact:** None - Recompilation only

**Action:** Verify tray icon functionality works correctly

---

#### System.Drawing Types (11 issues, 2.4%)

**Most Frequent:**
- `System.Drawing.Icon` (8) - Already covered in source incompatible
- `System.Drawing.Image` (3)

**Files Affected:** `NotifyIconTrayService.cs`, `ColorUtil.cs`

**Expected Impact:** None if System.Drawing.Common package added

**Action:** Add System.Drawing.Common package if compilation errors occur

---

#### Windows Forms Legacy Controls (2 issues, 0.4%)

**Types:** StatusBar, DataGrid, ContextMenu, MainMenu, MenuItem, ToolBar

**Expected Impact:** None if not used; check if any legacy controls present

**Action:** Verify no legacy controls in use (unlikely given assessment)

---

### Breaking Changes by File

**Highest Risk Files:**

| File | Issues | Risk | Notes |
|------|--------|------|-------|
| `NotifyIconTrayService.cs` | 124 | 🟡 Medium | Source incompatible issues (Icon, ToolStripItemCollection) |
| `SettingsWindow.xaml.cs` | 68 | 🟢 Low | Mostly binary incompatible (recompilation) |
| `StandUpReminderWindow.xaml.cs` | 49 | 🟢 Low | Mostly binary incompatible (recompilation) |
| `PostureReminderScheduler.cs` | 41 | 🟡 Medium | TimeSpan.FromMinutes source incompatible |
| `App.xaml.cs` | 31 | 🟢 Low | Binary incompatible + Uri behavioral change |
| `MainWindow.xaml.cs` | 32 | 🟢 Low | Binary incompatible + Uri behavioral change |

---

### Summary of Expected Code Changes

**High Probability (Expect to Fix):**
1. Add `System.Drawing.Common` package reference (if Icon errors occur)

**Medium Probability (May Need to Fix):**
2. Adjust `TimeSpan.FromMinutes` calls (explicit type casting)
3. Adjust `ToolStripItemCollection.Add` calls (explicit types or ToolStripMenuItem)

**Low Probability (Unlikely to Need Fixes):**
4. Uri constructor calls (behavioral testing only)
5. WPF/WinForms framework types (recompilation only)

**Total Expected Code Modifications:** 0-20 lines (likely under 5 lines)

---

## Risk Management

### High-Risk Changes

| Project | Risk Level | Description | Mitigation |
|---------|------------|-------------|------------|
| StandupReminder.App | 🟡 Medium | WPF-UI package incompatibility - assessment suggests downgrade to 2.0.3 which is unusual | Investigate latest compatible version first; check WPF-UI GitHub/NuGet for .NET 10 compatibility; consider removing if incompatible or finding alternative |
| StandupReminder.App | 🟢 Low | 416 binary incompatible APIs (mostly WPF types) | Expected for framework upgrade; most require only recompilation, not code changes |
| StandupReminder.App | 🟢 Low | 19 source incompatible APIs (TimeSpan, Drawing.Icon) | Minor compilation fixes expected; well-documented breaking changes |
| StandupReminder.WindowsInterop | 🟢 Low | 16 binary incompatible APIs (WPF types) | Expected for framework upgrade; recompilation only |

### All-At-Once Strategy Risk Factors

**Atomic Upgrade Risks:**
- **Both Projects Affected Simultaneously**: If WPF-UI issue blocks upgrade, entire solution is affected
- **Single Testing Surface**: Must validate both projects together
- **No Incremental Validation**: Cannot test WindowsInterop independently with .NET 10

**Mitigations:**
- Small solution size limits blast radius (2,533 LOC total)
- Clear dependency structure simplifies troubleshooting
- Can rollback entire upgrade via Git revert if needed
- WPF-UI is the only package requiring attention

### Security Vulnerabilities

**None** ✓

All packages are secure with no known CVEs. This is excellent and removes a major risk factor from the upgrade.

### Contingency Plans

#### WPF-UI Package Incompatibility

**Problem**: Assessment suggests WPF-UI 4.2.0 is incompatible with .NET 10 and recommends 2.0.3 (downgrade is unusual)

**Alternatives:**
1. **Research Latest Version**: Check WPF-UI NuGet page for latest version (may be newer than 4.2.0 with .NET 10 support)
2. **Try Current Version**: Attempt upgrade with WPF-UI 4.2.0 first - assessment tools can be overly conservative
3. **Remove Package**: If WPF-UI provides only UI enhancements, consider removing if incompatible
4. **Find Alternative**: Research alternative WPF UI libraries compatible with .NET 10

**Decision Tree:**
- Try WPF-UI 4.2.0 with .NET 10 → If builds successfully, keep it
- If build fails → Check for WPF-UI updates (5.x, 6.x, etc.)
- If no compatible version → Assess functionality provided by WPF-UI
- If non-critical → Remove package
- If critical → Find alternative library

#### Compilation Errors

**Problem**: Unexpected compilation errors after framework upgrade

**Alternatives:**
1. **Check Breaking Changes**: Consult .NET 10 breaking changes documentation
2. **API Replacements**: Use Microsoft Learn to find replacement APIs
3. **Community Resources**: Search GitHub issues, Stack Overflow for similar WPF upgrade issues
4. **Incremental Fix**: Address errors file by file, starting with WindowsInterop (smaller surface)

#### Behavioral Changes

**Problem**: Application runs but behaves differently (14 behavioral change APIs detected)

**Alternatives:**
1. **Focused Testing**: Test areas using Uri, DispatcherTimer, and other flagged APIs
2. **Compare Behavior**: Run .NET 8 and .NET 10 versions side-by-side to compare
3. **Consult Documentation**: Review .NET 10 behavioral change documentation for each API
4. **Code Adjustments**: Update code to explicitly handle new behavior if needed

---

## Testing & Validation Strategy

### Overall Testing Approach

**All-At-Once Strategy Testing:**
Since both projects are upgraded simultaneously, testing happens in a single phase after the atomic upgrade completes.

**Testing Levels:**
1. **Build Validation** - Verify compilation succeeds
2. **Smoke Testing** - Quick functional validation
3. **Comprehensive Validation** - Full application testing

---

### Phase-by-Phase Testing Requirements

#### Phase 1: Atomic Upgrade (All Projects)

**Build Validation:**

1. **Clean Build Test**
   - Run `dotnet clean` to remove old binaries
   - Run `dotnet restore` to restore packages
   - Run `dotnet build` to compile solution
   - **Success Criteria:** 0 build errors

2. **Warning Analysis**
   - Review all build warnings
   - Identify deprecation warnings
   - Document warnings for future fixes
   - **Success Criteria:** No critical warnings (errors promoted to warnings)

3. **Dependency Resolution**
   - Verify all NuGet packages restored
   - Verify WPF-UI package resolved (compatible version or removed)
   - Verify project references work (App → WindowsInterop)
   - **Success Criteria:** No package conflicts, all references resolved

**Smoke Testing:**

4. **Application Launch**
   - Run StandupReminder.App
   - **Success Criteria:** Application starts without exceptions

5. **System Tray Icon**
   - Verify tray icon appears in system tray
   - **Success Criteria:** Icon visible, tooltip displays

6. **Basic Interaction**
   - Right-click tray icon, verify menu appears
   - Select "Settings" or "Exit" menu item
   - **Success Criteria:** Menu responds, commands work

**Comprehensive Validation:**

7. **Settings Window**
   - Open Settings window
   - Verify all controls render correctly (sliders, textboxes, buttons)
   - Modify settings (reminder interval, colors)
   - Save settings
   - Reopen Settings to verify persistence
   - **Success Criteria:** All controls functional, settings persist

8. **Reminder Functionality**
   - Configure short reminder interval (e.g., 1 minute)
   - Wait for reminder to trigger
   - Verify reminder window appears
   - Verify reminder content displays correctly
   - Test "Dismiss" and "Snooze" actions (if present)
   - **Success Criteria:** Reminders fire on time, UI displays correctly

9. **UWP Notifications**
   - Verify Windows toast notifications appear (if enabled)
   - Test notification actions (if any)
   - **Success Criteria:** Notifications display using Windows 10/11 Action Center

10. **Session Monitoring**
    - Lock Windows session (Win+L)
    - Unlock session
    - Verify application detects lock/unlock events (check logs or behavior)
    - **Success Criteria:** Session events detected correctly

11. **Tray Context Menu**
    - Right-click tray icon
    - Test all menu items:
      - Show Reminder
      - Settings
      - Exit
    - **Success Criteria:** All menu items functional

12. **Color Customization** (if applicable)
    - Open Settings
    - Change reminder window colors
    - Trigger reminder
    - Verify colors applied
    - **Success Criteria:** Color changes reflected in UI

13. **Application Exit**
    - Exit via tray menu
    - Verify clean shutdown (no exceptions)
    - Verify tray icon removed
    - **Success Criteria:** Clean exit, no lingering processes

**Behavioral Change Testing:**

14. **Uri-Related Functionality**
    - Verify all XAML windows load (MainWindow, SettingsWindow, StandUpReminderWindow)
    - Check for Uri parsing exceptions in logs
    - **Success Criteria:** All windows load without Uri exceptions

15. **DispatcherTimer Behavior**
    - Verify reminder timers fire at expected intervals
    - Test multiple timer cycles (3-4 reminders)
    - Check memory usage doesn't grow excessively
    - **Success Criteria:** Timers accurate, no memory leaks

16. **NotifyIcon Behavior**
    - Verify tray icon visibility
    - Verify tooltip text
    - Verify balloon tips (if used)
    - **Success Criteria:** All NotifyIcon features work as expected

**Performance & Stability Testing:**

17. **Extended Run Test**
    - Run application for 30-60 minutes
    - Monitor memory usage (Task Manager)
    - Verify no memory leaks
    - Verify timers continue firing
    - **Success Criteria:** Stable memory, consistent behavior

18. **Stress Test**
    - Rapidly open/close Settings window (10-20 times)
    - Rapidly trigger reminders (short interval)
    - **Success Criteria:** No crashes, no exceptions

---

### Smoke Tests (Quick Validation After Each Phase)

**Phase 1 Smoke Test** (5 minutes):
1. ✓ Solution builds (0 errors)
2. ✓ Application launches
3. ✓ Tray icon appears
4. ✓ Settings window opens
5. ✓ Exit works cleanly

---

### Comprehensive Validation (Before Phase Completion)

**Phase 1 Comprehensive Validation** (30 minutes):

All 18 tests listed above must pass before upgrade is considered complete.

**Critical Path Tests** (cannot fail):
- Application launches ✓
- Tray icon appears ✓
- Settings window opens ✓
- Reminder triggers ✓
- Application exits cleanly ✓

**Important Tests** (should pass, can be deferred if needed):
- Session monitoring ✓
- UWP notifications ✓
- Color customization ✓
- Extended run test ✓

**Nice-to-Have Tests** (can be deferred):
- Stress test
- Performance profiling

---

### Testing Tools & Methods

**Build Tools:**
- `dotnet clean` - Clean previous build artifacts
- `dotnet restore` - Restore NuGet packages
- `dotnet build` - Compile solution
- `dotnet run --project StandupReminder.App` - Launch application

**Manual Testing:**
- Windows 10/11 desktop environment
- System tray interaction
- Windows session lock/unlock (Win+L)
- Task Manager (monitor memory usage)

**Logging:**
- Enable verbose logging if application has logging framework
- Check Windows Event Viewer for application errors
- Monitor console output if available

---

### Test Environment Requirements

**Operating System:**
- Windows 10 version 1809+ (build 17763+) OR
- Windows 11 (any version)

**Prerequisites:**
- .NET 10 SDK installed
- Windows desktop session (not Server Core)
- System tray accessible (not hidden)

---

### Success Criteria Summary

**Build Success:**
- ✅ Solution builds with 0 errors
- ✅ No critical warnings
- ✅ All packages restored

**Functional Success:**
- ✅ Application launches
- ✅ Tray icon functional
- ✅ Settings UI works
- ✅ Reminders fire correctly
- ✅ Session monitoring works
- ✅ Application exits cleanly

**Quality Success:**
- ✅ No memory leaks (stable over 30+ minutes)
- ✅ No unhandled exceptions
- ✅ Performance comparable to .NET 8 version

---

### Failure Response

**If Build Fails:**
1. Review compilation errors
2. Check Breaking Changes Catalog for known issues
3. Apply fixes (TimeSpan, Icon, ToolStripItemCollection)
4. Retry build

**If Functional Tests Fail:**
1. Compare behavior to .NET 8 version
2. Check .NET 10 breaking changes documentation
3. Search for specific error messages
4. Consult WPF/WinForms migration guides

**If WPF-UI Package Blocks Progress:**
1. Try removing WPF-UI package
2. Build and test without it
3. Identify UI features lost
4. Find alternative package or implement manually

---

### Regression Testing

**Baseline Comparison:**
Before starting upgrade, document:
- Application launch time
- Memory usage after 10 minutes
- Reminder trigger accuracy
- Settings window load time

**Post-Upgrade:**
Compare same metrics to ensure no regression.

**Acceptable Variance:**
- Launch time: ±20%
- Memory usage: ±15%
- Timer accuracy: ±5 seconds
- UI load time: ±20%

---

## Complexity & Effort Assessment

### Overall Solution Complexity: LOW

**Justification:**
- Small solution (2 projects, 2,533 LOC)
- Simple linear dependency (no cycles)
- Modern starting framework (.NET 8)
- Minimal package updates (1 package)
- No security vulnerabilities

### Per-Project Complexity

| Project | Complexity | Dependencies | Risk | Justification |
|---------|------------|--------------|------|---------------|
| **StandupReminder.WindowsInterop** | 🟢 Low | 0 | 🟢 Low | Small library (207 LOC), no packages, 16 API issues (WPF types, recompilation only) |
| **StandupReminder.App** | 🟡 Medium | 1 | 🟡 Medium | Main application (2,326 LOC), 1 package requires investigation, 449 API issues (mostly recompilation) |

### Phase Complexity Assessment

**Phase 1: Atomic Upgrade (All Projects)**

**Complexity: Medium**
- **Framework Update**: Low complexity (straightforward TargetFramework change)
- **Package Update**: Medium complexity (WPF-UI requires investigation)
- **Build & Fix**: Medium complexity (449 API issues, most recompilation-only)
- **Validation**: Low complexity (small application, easy to test)

**Dependency Ordering:**
- WindowsInterop updated first (no dependencies)
- App updated second (depends on WindowsInterop)
- Both build together in single operation

### Relative Effort Distribution

**Estimated Distribution:**
- **Project File Updates**: 5% (quick edits to 2 .csproj files)
- **Package Investigation**: 20% (research WPF-UI .NET 10 compatibility)
- **Dependency Restoration**: 5% (automated restore)
- **Build & Compilation Fix**: 50% (address any compilation errors from 468 API issues)
- **Testing & Validation**: 20% (validate application functionality)

### Resource Requirements

**Skill Levels Required:**
- **Framework Migration**: Intermediate .NET knowledge
- **WPF Development**: Intermediate to Advanced (understanding of WPF architecture)
- **Package Management**: Basic to Intermediate (NuGet, dependency resolution)
- **Testing**: Basic (manual application testing)

**Parallel Capacity:**
- Not applicable (single developer can complete; 2 projects too few for parallelization)

### Complexity Factors

**Simplifying Factors:**
- ✓ Small codebase (2,533 LOC)
- ✓ Clear dependency structure
- ✓ Modern starting framework (.NET 8)
- ✓ No security vulnerabilities
- ✓ SDK-style projects (modern format)
- ✓ No legacy dependencies

**Complicating Factors:**
- ⚠ High API issue count (468) - but mostly binary incompatible (recompilation)
- ⚠ WPF-UI package compatibility uncertain
- ⚠ WPF-specific APIs (Windows-only, desktop-only)

### All-At-Once Strategy Methodology

**Complexity Impact:**
- **Lower Procedural Complexity**: Single upgrade operation vs. multiple phases
- **Higher Technical Concentration**: All issues must be resolved in one go
- **Faster Iteration**: No intermediate states to maintain
- **Simpler Testing**: Validate entire solution together (no compatibility matrix)

---

## Source Control Strategy

### All-At-Once Strategy Source Control Guidance

**Approach:** Single atomic commit containing all upgrade changes.

**Rationale:**
- Small solution with clear scope (2 projects)
- All changes are logically related (framework upgrade)
- Easier to review as single changeset
- Simpler to rollback if needed (one revert)
- No intermediate states to maintain

---

### Branching Strategy

**Current Setup:**
- **Source Branch:** `toast-new` (starting point)
- **Upgrade Branch:** `upgrade-to-NET10` (current branch, already created)
- **Main Branch:** (TBD - likely `main` or `master`)

**Workflow:**

1. **Preparation** ✅ (Already Complete)
   - Created upgrade branch: `upgrade-to-NET10`
   - Committed pending changes
   - Switched to upgrade branch

2. **Development** (During Upgrade)
   - All changes committed to `upgrade-to-NET10`
   - Regular commits as work progresses
   - OR single commit at end (see Commit Strategy below)

3. **Review**
   - Create Pull Request: `upgrade-to-NET10` → `toast-new` OR `main`
   - Code review by team (if applicable)
   - Testing validation on upgrade branch

4. **Merge**
   - Merge upgrade branch after all tests pass
   - Use merge commit or squash merge (team preference)
   - Tag with version: `v1.0-net10` or similar

5. **Cleanup**
   - Delete upgrade branch after successful merge (optional)
   - Deploy from main branch

---

### Commit Strategy

**Option 1: Single Atomic Commit (Recommended for All-At-Once)**

**Approach:**
- Make all changes (project files, package updates, code fixes)
- Test thoroughly
- Create single comprehensive commit

**Commit Message Template:**
```
Upgrade to .NET 10.0 LTS

- Updated StandupReminder.WindowsInterop to net10.0-windows
- Updated StandupReminder.App to net10.0-windows
- Investigated WPF-UI package compatibility [outcome]
- Fixed TimeSpan.FromMinutes source incompatibility (if needed)
- Added System.Drawing.Common package reference (if needed)
- Verified all functionality working correctly

Tested:
- Build: Success (0 errors)
- Application launch: Success
- Tray icon: Functional
- Settings UI: Functional
- Reminders: Working correctly
- Session monitoring: Working correctly

Breaking changes addressed:
- [List any code changes made]

Closes #[issue-number] (if applicable)
```

**Advantages:**
- Clean, atomic history
- Easy to understand what changed
- Simple to revert if needed
- Clear before/after state

**Disadvantages:**
- Large changeset (but only 2 projects)
- Lose incremental history

---

**Option 2: Incremental Commits (Alternative)**

**Approach:**
- Commit after each logical step
- Allows tracking progress
- Easier to identify when issues were introduced

**Commit Sequence:**
1. "Update project files to net10.0-windows"
2. "Update WPF-UI package to [version]"
3. "Add System.Drawing.Common package reference"
4. "Fix TimeSpan.FromMinutes source incompatibility"
5. "Fix ToolStripItemCollection.Add overload"
6. "Verify and test upgraded application"

**Advantages:**
- Incremental progress tracking
- Easier to identify specific change that caused issue
- Can cherry-pick specific fixes if needed

**Disadvantages:**
- More complex history
- Intermediate commits may not build/pass tests
- More noise in commit log

---

### Recommended Approach

**For This Upgrade: Option 1 (Single Atomic Commit)**

**Justification:**
- Small solution (2 projects)
- All-At-Once strategy emphasizes atomic operation
- Limited scope (framework + 1 package)
- Easier to review as single changeset
- Clean history for future reference

---

### Review and Merge Process

**Pull Request Requirements:**

**Title:**
```
[Upgrade] .NET 10.0 Migration
```

**Description Template:**
```markdown
## Overview
Upgrades StandupReminder solution from .NET 8.0 to .NET 10.0 LTS.

## Changes
- ✅ StandupReminder.WindowsInterop → net10.0-windows
- ✅ StandupReminder.App → net10.0-windows
- ✅ WPF-UI package: [kept at 4.2.0 / updated to X.Y.Z / removed]
- ✅ System.Drawing.Common: [added / not needed]
- ✅ Code fixes: [list if any]

## Testing
- [x] Solution builds successfully (0 errors)
- [x] Application launches
- [x] System tray icon functional
- [x] Settings window functional
- [x] Reminders trigger correctly
- [x] Session monitoring works
- [x] Application exits cleanly
- [x] Extended run test (30+ minutes)

## Breaking Changes
[List any behavioral changes or code modifications]

## API Compatibility
- 432 binary incompatible APIs (recompilation only)
- 19 source incompatible APIs (addressed: [yes/no])
- 14 behavioral changes (tested: [yes/no])

## Rollback Plan
If issues discovered after merge:
1. Revert this PR commit
2. Switch back to toast-new branch
3. Investigate issues
4. Retry upgrade

## Deployment Notes
- Requires .NET 10 Runtime on user machines
- No database migrations required
- No configuration changes required
```

**Review Checklist:**

Code Reviewer Should Verify:
- [ ] Both project files updated to net10.0-windows
- [ ] Package references updated appropriately
- [ ] No hardcoded version numbers remain (.NET 8 references)
- [ ] Code changes are minimal and justified
- [ ] No unrelated changes included
- [ ] Commit message is clear and complete

Testing Reviewer Should Verify:
- [ ] Application launches successfully
- [ ] Core functionality works (reminders, settings, tray)
- [ ] No obvious regressions
- [ ] Performance acceptable

---

### Merge Criteria

**Must Pass Before Merging:**
1. ✅ All build tests pass (0 errors)
2. ✅ All critical functional tests pass (see Testing Strategy)
3. ✅ Code review approved (if applicable)
4. ✅ No unresolved blocking issues
5. ✅ Documentation updated (README, changelog)

**Nice-to-Have Before Merging:**
- Extended run test completed (30+ minutes)
- Performance benchmarks comparable
- All warnings addressed

---

### Post-Merge Actions

After successful merge:

1. **Tag Release**
   ```bash
   git tag v1.0.0-net10 -a -m "Release: .NET 10.0 upgrade"
   git push origin v1.0.0-net10
   ```

2. **Update Documentation**
   - Update README.md with .NET 10 requirement
   - Update CHANGELOG.md with upgrade notes
   - Update any build/deployment documentation

3. **Announce**
   - Notify team of successful upgrade
   - Document any breaking changes for users
   - Update deployment pipelines (if applicable)

4. **Monitor**
   - Monitor for issues in first few days
   - Check crash reports (if telemetry exists)
   - Gather user feedback

---

### Rollback Procedure

**If Critical Issues Found After Merge:**

**Immediate Rollback:**
```bash
# Find merge commit hash
git log --oneline

# Revert merge commit
git revert -m 1 <merge-commit-hash>

# Push revert
git push origin main
```

**Alternative: Branch Revert**
```bash
# Switch to previous branch
git checkout toast-new

# Create hotfix branch if needed
git checkout -b hotfix/net8-restore
```

**When to Rollback:**
- Application crashes on startup
- Data loss or corruption
- Critical functionality broken
- Security vulnerabilities introduced
- Performance degradation >50%

**When to Fix Forward:**
- Minor UI glitches
- Non-critical warnings
- Small behavioral changes
- Performance degradation <20%

---

### Git Configuration

**Recommended Settings:**

```bash
# Use meaningful merge commit messages
git config merge.log true

# Show more context in diffs
git config diff.context 5

# Use branch-specific ignore (if needed)
# .git/info/exclude
obj/
bin/
*.user
```

---

### Branch Protection (If Applicable)

**Recommended Rules for Main Branch:**
- Require pull request before merging
- Require 1 approval (if team)
- Require status checks to pass (CI build)
- Do not allow force pushes
- Do not allow deletions

---

## Success Criteria

### Technical Criteria

**Framework Migration:**
- ✅ All projects target `net10.0-windows` (no `net8.0-windows` references remain)
- ✅ No projects use deprecated target framework monikers
- ✅ All projects use simplified Windows targeting (no specific Windows build versions)

**Package Updates:**
- ✅ All packages restored successfully
- ✅ WPF-UI package resolved (compatible version found, or removed with mitigation)
- ✅ Microsoft.Toolkit.Uwp.Notifications remains at 7.1.3 (compatible)
- ✅ System.Drawing.Common added if needed (for Icon support)
- ✅ No package version conflicts
- ✅ No deprecated packages in use

**Build Success:**
- ✅ `dotnet restore` succeeds for entire solution
- ✅ `dotnet build` succeeds with 0 errors
- ✅ Both projects compile successfully
- ✅ StandupReminder.WindowsInterop builds independently
- ✅ StandupReminder.App builds with WindowsInterop reference

**Warnings:**
- ✅ No critical warnings (CS0618 deprecation warnings documented)
- ✅ No package downgrade warnings
- ✅ No binding redirect warnings (not applicable to SDK-style projects)

**Dependencies:**
- ✅ Project reference works (App → WindowsInterop)
- ✅ No circular dependencies
- ✅ All assembly references resolve correctly

---

### Quality Criteria

**Code Quality:**
- ✅ All source incompatible APIs addressed:
  - TimeSpan.FromMinutes compiles correctly
  - System.Drawing.Icon compiles correctly (with System.Drawing.Common if needed)
  - ToolStripItemCollection.Add compiles correctly
- ✅ No unsafe code modifications made (only necessary fixes)
- ✅ Code remains maintainable and readable
- ✅ No workarounds that compromise quality

**Test Coverage:**
- ✅ All existing functionality still works (manual testing)
- ✅ No test projects broken (none exist currently)
- ✅ Smoke tests pass (see Testing Strategy)
- ✅ Comprehensive validation tests pass (see Testing Strategy)

**Documentation:**
- ✅ README.md updated with .NET 10 requirement
- ✅ CHANGELOG.md updated with upgrade notes
- ✅ Breaking changes documented (if any)
- ✅ Build instructions updated (if needed)

---

### Process Criteria

**All-At-Once Strategy Followed:**
- ✅ Both projects upgraded simultaneously
- ✅ Single atomic operation completed
- ✅ No intermediate multi-targeting required
- ✅ All changes validated together
- ✅ Single commit or logically grouped commits

**All-At-Once Strategy Principles Applied:**
- ✅ Maximum consolidation achieved (both projects in single operation)
- ✅ No artificial checkpoints created
- ✅ Dependency order respected (WindowsInterop before App)
- ✅ Single build-test cycle performed

**Source Control:**
- ✅ All changes committed to upgrade branch (`upgrade-to-NET10`)
- ✅ Commit message(s) clear and descriptive
- ✅ Pull request created (if applicable)
- ✅ Code review completed (if applicable)
- ✅ Merge criteria met

**Testing:**
- ✅ Build validation completed
- ✅ Smoke tests completed
- ✅ Comprehensive validation completed
- ✅ Behavioral change testing completed
- ✅ Extended run test completed (30+ minutes)

---

### Functional Criteria

**Core Functionality:**
- ✅ **Application Launch:** Application starts without errors or exceptions
- ✅ **System Tray Icon:** Icon appears in system tray with correct tooltip
- ✅ **Tray Context Menu:** Right-click menu displays and all items work
- ✅ **Settings Window:** Opens correctly, all controls render and function
- ✅ **Settings Persistence:** Settings save and load correctly
- ✅ **Reminder Timer:** Reminders fire at configured intervals
- ✅ **Reminder Window:** Reminder UI displays correctly
- ✅ **UWP Notifications:** Windows toast notifications work (if enabled)
- ✅ **Session Monitoring:** Lock/unlock events detected correctly
- ✅ **Application Exit:** Exits cleanly without errors or lingering processes

**Data Integrity:**
- ✅ Existing settings preserved after upgrade
- ✅ User preferences retained
- ✅ No configuration migration required

**Performance:**
- ✅ Application launch time comparable to .NET 8 version (±20%)
- ✅ Memory usage stable over extended period (no leaks)
- ✅ Timer accuracy maintained (±5 seconds)
- ✅ UI responsiveness unchanged

---

### Compatibility Criteria

**API Compatibility:**
- ✅ All 432 binary incompatible APIs addressed (recompilation)
- ✅ All 19 source incompatible APIs addressed (code fixes if needed)
- ✅ All 14 behavioral changes tested and validated
- ✅ No runtime exceptions from API changes

**Platform Compatibility:**
- ✅ Runs on Windows 10 version 1809+ (build 17763+)
- ✅ Runs on Windows 11 (any version)
- ✅ System tray functionality works on all tested OS versions
- ✅ UWP notifications work on Windows 10/11

**Runtime Requirements:**
- ✅ .NET 10 Runtime requirement documented
- ✅ No .NET 8 Runtime dependencies remain
- ✅ Application runs on clean .NET 10 installation

---

### Risk Mitigation Criteria

**Security:**
- ✅ No security vulnerabilities introduced
- ✅ All packages remain secure (no new CVEs)
- ✅ No deprecated security-sensitive APIs in use

**Stability:**
- ✅ No unhandled exceptions during normal operation
- ✅ No memory leaks detected (30+ minute run)
- ✅ No race conditions or timing issues introduced

**Rollback Readiness:**
- ✅ Source branch (`toast-new`) preserved and accessible
- ✅ Rollback procedure documented
- ✅ Rollback can be executed in <5 minutes if needed

---

### Definition of Done

**The upgrade is COMPLETE when ALL of the following are true:**

**Essential (Must Have):**
1. ✅ Both projects target `net10.0-windows`
2. ✅ Solution builds with 0 errors
3. ✅ All packages restored successfully
4. ✅ Application launches successfully
5. ✅ Core functionality works (tray, settings, reminders)
6. ✅ No critical regressions identified
7. ✅ Changes committed to source control
8. ✅ All tests pass (see Testing Strategy)

**Important (Should Have):**
9. ✅ Extended run test completed (30+ minutes stable)
10. ✅ Documentation updated (README, CHANGELOG)
11. ✅ Code review completed (if applicable)
12. ✅ Pull request merged (if applicable)

**Nice-to-Have (Stretch Goals):**
13. ⚪ Build warnings reduced or eliminated
14. ⚪ Performance benchmarks show improvement
15. ⚪ Test project added for future testing

---

### Acceptance Testing

**Final Acceptance Test Checklist:**

Before declaring upgrade complete, perform final validation:

1. **Clean Environment Test**
   - [ ] Fresh clone from upgrade branch
   - [ ] `dotnet restore`
   - [ ] `dotnet build`
   - [ ] `dotnet run --project StandupReminder.App`
   - [ ] All functionality works

2. **Regression Test**
   - [ ] Compare behavior to .NET 8 version
   - [ ] All features work identically
   - [ ] Performance acceptable

3. **Documentation Test**
   - [ ] README.md accurate
   - [ ] Build instructions work
   - [ ] Requirements clearly stated

4. **Handoff Test** (if applicable)
   - [ ] Another developer can build from instructions
   - [ ] No tribal knowledge required
   - [ ] All tools/dependencies documented

---

### Post-Upgrade Success Metrics

**Monitor for First 7 Days:**
- ✅ Application crash rate (should be 0)
- ✅ User-reported issues (track in issue tracker)
- ✅ Performance metrics (compare to baseline)
- ✅ Memory usage trends (no gradual increase)

**Long-Term Success:**
- Application remains stable over weeks/months
- No .NET 10-specific issues emerge
- Future updates easier (on modern framework)
- Security patches available (LTS support)

---

### Sign-Off

**Upgrade Approved When:**
- Technical Lead reviews and approves (if applicable)
- QA validates all tests pass (if applicable)
- Product Owner accepts functionality (if applicable)
- All "Essential" criteria met
- All "Important" criteria met (or documented exceptions)

**Upgrade Rejected If:**
- Critical functionality broken
- Data loss or corruption occurs
- Performance degradation >50%
- Security vulnerabilities introduced
- Cannot rollback safely

---

**END OF PLAN**
