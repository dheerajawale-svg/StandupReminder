# WinUI 3 Migration Plan

## User Goals & Intent
- Complete the migration from the current WPF tray-first app to a WinUI 3 desktop app through small, safe, reviewable slices.
- Preserve current reminder behavior while the migration is in progress: single-instance lifecycle, tray-first operation, reminder scheduling, snooze, manual pause/resume, persisted settings, and session-event awareness.
- Keep WPF working until each migrated slice has a validated WinUI equivalent.
- Prefer modern Windows App SDK and WinUI patterns where they exist; isolate unavoidable Win32/compatibility code behind adapters.
- End state success criteria:
  - WinUI owns the app bootstrap, runtime composition, prompt/settings UX, and steady-state desktop experience.
  - `StandupReminder.Core` remains framework-agnostic.
  - `StandupReminder.Windows` owns Windows-only adapters/interactions.
  - `StandupReminder.Persistence` owns local storage concerns.
  - WPF can be retired only after parity is validated on real installed-style runs.

## Key Technical Context

### Current migration baseline
The plan should assume the following foundation is already complete and should **not** be treated as pending work:

- Solution/project structure is in place:
  - `src/StandupReminder.WinUI`
  - `src/StandupReminder.Core`
  - `src/StandupReminder.Windows`
  - `src/StandupReminder.Persistence`
  - solution wiring and WinUI icon asset setup
- Core/domain extraction is complete:
  - `ReminderPhase`
  - `ReminderScheduleOptions`
  - scheduler state-machine extraction into `StandupReminder.Core` (`ReminderSchedulerEngine`)
  - framework-agnostic reminder contracts: `IReminderTickSource`, `IReminderSessionEventSource`, `IReminderTrayHost`, `IReminderTrayNotifier`, `IReminderPromptHost`, `IReminderSessionEventSink`
  - `ReminderSessionEvent` enum (Logon, Lock, Unlock)
- Core validation foundation is in place:
  - `tests/StandupReminder.Core.Tests`
  - scheduler-engine unit coverage (`ReminderSchedulerEngineTests`)
  - scheduler-runtime unit coverage (`ReminderSchedulerRuntimeTests`) — 4 tests covering start, countdown expiry, session logon, and lock/unlock tick-source lifecycle
- WinUI bootstrap slice is complete:
  - custom `Program.cs`
  - `DISABLE_XAML_GENERATED_MAIN`
  - minimal single-instance startup handling with Windows App SDK lifecycle APIs
  - WinUI app/window activation bootstrap
- Reminder-settings persistence slice is complete:
  - portable reminder settings persistence moved into `src/StandupReminder.Persistence`
  - WPF updated to consume the shared reminder-settings persistence implementation
  - WinUI shell now loads and displays persisted reminder schedule values
  - targeted persistence tests added
- WinUI runtime composition slice is complete (Stage 1):
  - `ReminderRuntimeComposition` class owns adapter wiring, runtime start, and disposal
  - `AppBootstrapper` creates the runtime composition with factory-based adapter construction
  - `ReminderSchedulerRuntime` in Core wraps the scheduler engine with tick-source lifecycle management
  - `ShellViewModel` observes live scheduler state (phase, remaining time, timer adapter status) via `INotifyPropertyChanged`
  - WinUI `App.xaml.cs` creates runtime through the composition seam on first activation
- Windows adapter implementations are complete (Stage 2):
  - `SynchronizationContextReminderTickSource` — timer adapter using `System.Threading.Timer` with UI-thread marshalling
  - `SystemEventsReminderSessionEventSource` — session event source using `SystemEvents.SessionSwitch`
  - `NotifyIconReminderTrayHost` — full tray host with context menu (Open, Pause/Resume, Settings, Exit), balloon tips, persistent balloon timer, and icon loading
  - `NotifyIconReminderTrayNotifier` — notification-only tray adapter for balloon tips
  - `StandupReminder.Windows.csproj` references `System.Windows.Forms` via `UseWindowsForms` and `Microsoft.WindowsDesktop.App`
- Live scheduler hosting in WinUI is mostly complete (Stage 3 — tray wiring done):
  - scheduler engine hosted from WinUI runtime composition path ✓
  - timer, session, and tray adapters connected to the live runtime ✓
  - live phase/remaining-time state surfaced in the WinUI shell ✓
  - tray host events wired to WinUI app-level behavior ✓
    - `OpenRequested` → activates main window (via `DispatcherQueue` marshalling)
    - `PauseResumeRequested` → toggles `ReminderSchedulerRuntime.PauseTimer()`/`ResumeTimer()`
    - `SettingsRequested` → opens the WinUI settings window as the primary settings UX (via `DispatcherQueue` marshalling)
    - `ExitRequested` → graceful shutdown with composition disposal
  - tray status text and pause menu label updated from runtime state changes ✓
  - `ReminderRuntimeComposition` exposes `WindowActivationRequested`, `SettingsRequested`, `ShutdownRequested` events for `App.xaml.cs` ✓
  - `App.xaml.cs` uses `DispatcherQueue.TryEnqueue` to marshal tray events from WinForms threads to WinUI UI thread ✓
  - real prompt host wired: `StandUpReminderPromptHost` ✓
  - WinUI settings window wired as a single-instance secondary window ✓
  - remaining: verify full end-to-end behavior parity with the WPF reference implementation
- Persistence migration is mostly complete (Stage 5 — in progress):
  - appearance persistence: `AppearanceSettings`, `IAppearanceSettingsStore`, `LocalAppDataAppearanceSettingsStore`, `ArgbHexColor` moved to `StandupReminder.Persistence`
  - session-event log persistence: `ISessionEventLogStore`, `LocalAppDataSessionEventLogStore`, `SessionEventLogEntry` moved to `StandupReminder.Persistence`
  - `LocalAppDataSettingsDocumentStore` updated with `AppearanceSection` for shared settings document
  - comprehensive persistence tests added (`ReminderSettingsStoreTests`) — 7 tests covering reminder settings, appearance settings, and session event log round-trips
  - WPF already consumes the shared appearance/session-event persistence implementations from `StandupReminder.Persistence`
  - remaining: keep compatibility/import helpers and local-data continuity checks in scope for later packaging/cutover work

### Latest session update (2026-03-11)
- Re-checked the repo against this plan before making further changes and confirmed two stale assumptions:
  - WinUI no longer needed another WPF-persistence migration slice before prompt work.
  - Stage 5 text was outdated because WPF was already consuming the shared appearance/session-event persistence implementations.
- The user explicitly chose the WinUI stand-up prompt pattern as a **separate WinUI window**.
- Completed in this session:
  - implemented `src/StandupReminder.WinUI/StandUpReminderWindow.xaml` and `StandUpReminderWindow.xaml.cs`
  - implemented `src/StandupReminder.WinUI/StandUpReminderPromptHost.cs`
  - wired the real prompt host in `AppBootstrapper`
  - removed the `NullReminderPromptHost` placeholder from the WinUI composition path
  - updated stale WinUI shell status text that still said the prompt UX was pending
- The WinUI prompt now preserves the key WPF-era reminder semantics:
  - one prompt window at a time
  - reuse/activate the existing prompt if the engine re-enters prompt state
  - confirm and snooze actions
  - dismiss for lock, manual pause, and shutdown
  - live stand-duration and background updates while visible
  - fixed-size, centered, always-on-top window with casual close blocked unless the app explicitly allows it
- Targeted verification completed successfully after the implementation:
  - `dotnet build src/StandupReminder.WinUI/StandupReminder.WinUI.csproj`
  - `dotnet test tests/StandupReminder.Core.Tests/StandupReminder.Core.Tests.csproj` → 18 tests passed
- The next logical Stage 4 slice in this session was the **WinUI settings experience**.
- Completed in this session:
  - implemented `src/StandupReminder.WinUI/SettingsWindow.xaml` and `SettingsWindow.xaml.cs`
  - implemented `src/StandupReminder.WinUI/SettingsWindowViewModel.cs`
  - updated `App.xaml.cs` so tray-driven `SettingsRequested` activation opens a single-instance WinUI settings window and marshals back to the UI thread
  - updated `AppBootstrapper` and `ReminderRuntimeComposition` so settings save/load uses the shared stores from `StandupReminder.Persistence`
  - updated `ReminderRuntimeComposition` to persist settings, apply them to the live `ReminderSchedulerRuntime`, and refresh `ShellViewModel`
  - updated `ShellViewModel` so saved schedule text refreshes after settings changes and stale Stage 4 messaging is removed
- The WinUI settings window now preserves the key WPF-era settings behavior:
  - whole-minute validation for initial sit / recurring sit / stand values
  - ARGB hex validation and normalization through `ArgbHexColor`
  - A/R/G/B sliders, preview swatch, and WinUI `ColorPicker`
  - save/cancel flow with single-instance settings window behavior
  - shared persistence through `LocalAppDataReminderSettingsStore` and `LocalAppDataAppearanceSettingsStore`
  - immediate runtime updates through `ReminderSchedulerRuntime.UpdateOptions(...)` and `UpdatePromptBackground(...)`
- Targeted verification for the settings slice completed successfully:
  - `dotnet build src/StandupReminder.WinUI/StandupReminder.WinUI.csproj` → passed
  - `dotnet test tests/StandupReminder.Core.Tests/StandupReminder.Core.Tests.csproj` → 18/18 passed
  - existing persistence tests continue to cover settings round-trip behavior, including:
    - `Save_PreservesAppearanceSectionAndRoundTripsReminderSchedule`
    - `AppearanceStore_Save_PreservesReminderScheduleAndNormalizesAppearance`
- One small compile issue surfaced during verification and was fixed immediately:
  - `DispatcherQueue.TryEnqueue(...)` needed a `DispatcherQueueHandler` lambda rather than a raw `Action`

### Current ownership map
- `StandupReminder.WinUI`
  - owns the WinUI app entry/bootstrap path, shell window, and runtime composition
  - hosts the live `ReminderSchedulerRuntime` through `ReminderRuntimeComposition` and `AppBootstrapper`
  - displays live scheduler state (phase, remaining time, timer adapter status) via `ShellViewModel`
  - tray host events are wired: Open → window activation, PauseResume → runtime toggle, Settings → single-instance WinUI settings window activation, Exit → graceful shutdown
  - `ReminderRuntimeComposition` owns tray status updates and pause menu label synchronization
  - `App.xaml.cs` marshals tray events to the WinUI UI thread via `DispatcherQueue.TryEnqueue`
  - owns the real WinUI stand-up prompt UX through `StandUpReminderWindow` and `StandUpReminderPromptHost`
  - owns the real WinUI settings UX through `SettingsWindow` and `SettingsWindowViewModel`
  - prompt-host operations self-marshal to the WinUI UI thread and reuse a single prompt window instance when possible
  - settings persistence/application now flows through `ReminderRuntimeComposition` using shared schedule/appearance stores
- `StandupReminder.Core`
  - owns reminder domain models, contracts, scheduler engine (`ReminderSchedulerEngine`), and runtime wrapper (`ReminderSchedulerRuntime`)
  - owns all framework-agnostic service interfaces: `IReminderTickSource`, `IReminderSessionEventSource`, `IReminderTrayHost`, `IReminderTrayNotifier`, `IReminderPromptHost`, `IReminderSessionEventSink`
- `StandupReminder.Persistence`
  - owns reminder-settings persistence, appearance-settings persistence, and session-event log persistence
  - owns the shared settings document store (`LocalAppDataSettingsDocumentStore`) with schedule and appearance sections
  - owns `ArgbHexColor` normalization utility
- `StandupReminder.Windows`
  - owns concrete Windows adapter implementations: `SynchronizationContextReminderTickSource`, `SystemEventsReminderSessionEventSource`, `NotifyIconReminderTrayHost`, `NotifyIconReminderTrayNotifier`
- `StandupReminder.App` (WPF)
  - still owns the live WPF runtime composition root and WPF-specific desktop behavior:
    - WPF reminder prompt window (`StandUpReminderWindow`)
    - WPF settings window (`SettingsWindow`)
    - legacy WPF prompt/settings UX kept only as the fallback/reference implementation while WinUI parity is validated
    - now consumes shared appearance/session-event persistence from `StandupReminder.Persistence`

### Migration constraints that still matter
- Keep WPF behavior working unless a slice explicitly replaces it and is validated.
- Preserve current local data compatibility where practical; avoid unnecessary schema churn.
- Keep slices narrow: one ownership move or one runtime integration seam at a time.
- Prefer abstraction-first moves so WinUI does not take direct dependencies on WinForms or raw Win32 APIs.

### Current target architecture
- `src/StandupReminder.WinUI`
  - WinUI executable shell, app bootstrap, shell viewmodels, WinUI prompt/settings surfaces, and runtime composition once migrated.
- `src/StandupReminder.Core`
  - reminder domain, scheduler engine, options/models, and framework-agnostic service contracts.
- `src/StandupReminder.Windows`
  - Windows-only adapters such as tray host, session event source, timer glue, and any required HWND/AppWindow interop.
- `src/StandupReminder.Persistence`
  - local settings/history storage, document stores, path resolution, and data compatibility helpers.

## Environment & Configuration Details
- Workspace/repo: `C:\personal\StandupReminder`
- Current production app shape: WPF desktop app targeting `net8.0-windows`
- New shell under migration: WinUI 3 desktop app using Windows App SDK
- Current local data shape:
  - reminder settings and appearance settings share `%LocalAppData%\StandupReminder\settings.json`
  - session-event history lives separately under local app data
- Current lifecycle expectation: tray-first, single-instance, current-user desktop app
- Packaging direction remains open, but the migration should remain compatible with either:
  - unpackaged/Inno-based transition periods, or
  - a later packaged/MSIX target

## Remaining Migration Roadmap

### Stage 3 (continued) - Complete live scheduler behavior in WinUI
Goal: make the WinUI app own the running reminder workflow end-to-end.

Already done:
- Scheduler engine hosted from WinUI runtime composition path.
- Timer, session, and tray adapters connected to the live runtime.
- Live phase/remaining-time state surfaced in the WinUI shell.
- Tray host events wired to WinUI app-level behavior:
  - `OpenRequested` → show/activate the main window via `DispatcherQueue` marshalling.
  - `PauseResumeRequested` → toggles `ReminderSchedulerRuntime.PauseTimer()`/`ResumeTimer()` based on `IsManuallyPaused`.
  - `SettingsRequested` → opens the single-instance WinUI settings window via `DispatcherQueue` marshalling.
  - `ExitRequested` → graceful shutdown: unsubscribes events, disposes composition, closes window, exits process.
- Tray status text and pause menu label updated from runtime state changes via `ReminderRuntimeComposition.UpdateTrayState()`.
- `ReminderRuntimeComposition` exposes `WindowActivationRequested`, `SettingsRequested`, `ShutdownRequested` events.
- `App.xaml.cs` uses `DispatcherQueue.TryEnqueue` to safely marshal tray events (firing on WinForms threads) to the WinUI UI thread.
- `ShellViewModel` status messages updated to be user-facing rather than developer-diagnostic.
- Real WinUI prompt host wired through `AppBootstrapper` (`StandUpReminderPromptHost` replaces `NullReminderPromptHost`).
- WinUI stand-up prompt implemented as a separate WinUI window (`StandUpReminderWindow`).
- WinUI settings window implemented as a separate secondary WinUI window (`SettingsWindow`).
- `ReminderRuntimeComposition` now owns the live settings save/apply path for schedule and appearance updates.
- Prompt behavior parity slice completed for the core interaction path:
  - single prompt window instance at a time
  - reuse/activate existing prompt when already visible
  - confirm and snooze actions
  - dismiss for lock, pause, and shutdown
  - live stand-duration/background updates while the prompt is visible
- Settings behavior parity slice completed for the core interaction path:
  - single settings window instance at a time
  - whole-minute validation for schedule fields
  - ARGB validation/normalization plus color sliders, preview, and WinUI color picker
  - save persists through shared stores and applies immediately to the live runtime
- Prompt host marshals prompt work to the WinUI UI thread so session-event driven dismiss/show paths remain safe.
- Targeted verification completed: WinUI project build passed and the Core test suite passed (18/18).

Remaining work:
- Verify behavior parity with the current WPF runtime:
  - sitting countdown
  - stand prompt show/reuse/confirm/snooze/dismiss flow
  - tray-driven settings open/edit/save/reopen flow
  - runtime updates after settings save while the app is already running
  - snooze path
  - manual pause/resume
  - pause/resume for session lock/unlock
  - manual smoke validation of prompt/settings behavior on installed-style or real desktop runs before WPF retirement

Exit criteria:
- WinUI can run the reminder lifecycle end-to-end, including the stand-up prompt path.
- WPF is no longer the only runtime composition root.
- Core behavior remains hosted through abstractions rather than UI-specific logic.

### Stage 4 - Rebuild remaining WPF UX in WinUI
Goal: replace the last WPF-owned user-facing surfaces.

Completed in this session:
- Rebuilt the stand-up prompt in WinUI as a separate secondary window (`StandUpReminderWindow`) backed by a real `IReminderPromptHost` implementation (`StandUpReminderPromptHost`).
- Used WinUI/AppWindow-friendly window management for the prompt: fixed-size, centered, always-on-top, and guarded close behavior.
- Rebuilt the settings experience in WinUI through `SettingsWindow`, `SettingsWindowViewModel`, and composition-driven save/apply wiring.
- Preserved the WPF settings behavior in WinUI for the primary editing path:
  - reminder interval editing in whole minutes
  - popup appearance editing with ARGB hex validation/normalization
  - A/R/G/B sliders, preview swatch, and WinUI color picker
  - single-instance settings window behavior
  - persistence through `StandupReminder.Persistence`
- Updated tray-to-settings activation so WinUI is now the primary settings UX.
- Updated runtime composition so settings changes persist, apply to the live runtime, and refresh shell state.
- Completed targeted verification for the implementation:
  - `dotnet build src/StandupReminder.WinUI/StandupReminder.WinUI.csproj` ✓
  - `dotnet test tests/StandupReminder.Core.Tests/StandupReminder.Core.Tests.csproj` ✓

Remaining work:
- Perform manual parity/smoke validation of the full prompt + settings workflow from the running tray app.
- Decide when WPF prompt/settings surfaces can be downgraded further from fallback/reference to retirement candidates.
- Keep any remaining owner/modal or desktop-integration quirks isolated to adapters/hosts if follow-up fixes are needed.

Notes:
- If exact owner/modal semantics are needed, keep that interop isolated to a host/adapter layer.
- Avoid broad UX redesign during parity migration; focus on behavior-preserving replacement first.

Exit criteria:
- Settings and prompt flows are WinUI-owned for the primary reminder UX.
- WPF windows are no longer required for primary reminder UX, but remain the fallback/reference path until parity is manually validated.

### Stage 5 (continued) - Finish the remaining persistence migration
Goal: complete the storage ownership move that is still split between WPF and the new projects.

Already done:
- Appearance persistence (`AppearanceSettings`, `IAppearanceSettingsStore`, `LocalAppDataAppearanceSettingsStore`, `ArgbHexColor`) moved to `StandupReminder.Persistence`.
- Session-event log persistence (`ISessionEventLogStore`, `LocalAppDataSessionEventLogStore`, `SessionEventLogEntry`) moved to `StandupReminder.Persistence`.
- `LocalAppDataSettingsDocumentStore` updated with `AppearanceSection`.
- Comprehensive persistence tests added.
- WPF startup/runtime wiring already consumes the shared appearance persistence implementation.
- WPF session-event history flow already consumes the shared session-event log persistence implementation.

Remaining work:
- Add any local data compatibility/import helpers needed for future packaging changes.
- Keep auditing for any remaining WPF-local persistence code during later cleanup/cutover work.

Specific checks:
- preserve the existing shared `settings.json` behavior unless a migration is intentional
- confirm local app data continuity across any deployment changes
- keep storage contracts portable and testable

Exit criteria:
- `StandupReminder.Persistence` owns all local settings/history persistence still needed by the WinUI app.
- WPF-local persistence ownership is minimized or removed.

### Stage 6 - Packaging, startup strategy, validation, and cutover
Goal: finish the migration only after the WinUI path is proven outside the development loop.

- Reconfirm packaging direction:
  - temporary unpackaged/Inno bridge, or
  - packaged/MSIX-first cutover
- Revisit startup registration and deployment behavior for the chosen model.
- Validate the WinUI app in realistic installed-style runs, not just local debugging.
- Retire WPF only after acceptance criteria are met.

Exit criteria:
- WinUI feature parity is validated.
- Deployment/runtime behavior is understood for the chosen packaging model.
- WPF retirement can happen without losing production behavior.

## Validation Guidance For Remaining Slices
- Continue validating every migration slice with the smallest useful scope first:
  - targeted unit tests
  - project/solution build
  - solution test pass
- Add tests whenever logic or persistence ownership moves.
- Once live WinUI runtime behavior is introduced, expand validation to cover:
  - first launch / second launch activation
  - tray menu actions
  - manual pause/resume
  - snooze flow
  - lock/unlock behavior
  - persisted settings reload
  - session-event history persistence
- Before final cutover, validate on installed-style runs rather than only `F5` debugging sessions.

## Issues, Assumptions & Open Questions
- Assumption: tray-first behavior remains mandatory.
- Assumption: current-session monitoring is sufficient unless a broader requirement appears.
- Assumption: behavior parity remains higher priority than UX redesign during migration.
- Assumption: WPF remains the safe fallback until WinUI runtime parity is demonstrated.
- Resolved: the first WinUI runtime slice reuses the existing `NotifyIcon`-based tray technology behind the `IReminderTrayHost` adapter. This was implemented in Stage 2.
- Resolved: the WinUI stand-up prompt pattern is a separate WinUI window. This was chosen explicitly for behavior-preserving parity and is now implemented.
- Resolved: the WinUI settings experience is now implemented and tray-driven settings activation is no longer a placeholder.
- Resolved: Stage 5 text previously overstated pending work; WPF was already consuming the shared appearance/session-event persistence implementations.
- Key risk: packaging choice may affect startup behavior, notification identity, and local data continuity.
- Key risk: WPF `ITrayService` now inherits from `IReminderTrayHost` (from Core), producing CS0108 hiding warnings — these are benign during migration but should be cleaned up when WPF tray ownership is retired.
- Key risk: session events can reach the prompt host outside the timer-posted flow, so WinUI prompt operations must continue to self-marshal to the UI thread.
- Key risk: prompt/settings slices now compile and pass targeted tests, but they still need live desktop smoke validation before WPF fallback can be retired confidently.
- Open question: should the final release remain compatible with the current Inno-based channel, or should cutover align with MSIX/package identity?
- Open question: is higher-level observable parity for session behavior sufficient, or is exact low-level `WM_WTSSESSION_CHANGE` parity required? (The current `SystemEventsReminderSessionEventSource` uses `SystemEvents.SessionSwitch` which covers Logon/Lock/Unlock.)

## References & Contextual Notes
- Current files most relevant to the remaining migration work:
  - `StandupReminder.App/StandUpReminderWindow.xaml.cs` (WPF prompt parity/reference implementation)
  - `StandupReminder.App/SettingsWindow.xaml.cs` (WPF settings parity/reference implementation for the WinUI settings slice)
  - `StandupReminder.App/Services/PostureReminderScheduler.cs` (WPF scheduler — to be retired after Stage 3)
  - `StandupReminder.App/Services/NotifyIconTrayService.cs` (WPF tray — to be retired after WinUI tray wiring)
  - `src/StandupReminder.WinUI/App.xaml.cs` (tray event wiring complete — marshals via DispatcherQueue and owns the single-instance settings window activation path)
  - `src/StandupReminder.WinUI/ReminderRuntimeComposition.cs` (composition root — tray events/status updates plus live settings save/apply path)
  - `src/StandupReminder.WinUI/AppBootstrapper.cs` (adapter/store factory — wires prompt host plus shared settings/appearance stores)
  - `src/StandupReminder.WinUI/ShellViewModel.cs` (live state display — shell messaging and schedule text now refresh after settings saves)
  - `src/StandupReminder.WinUI/StandUpReminderWindow.xaml` (WinUI stand-up prompt surface)
  - `src/StandupReminder.WinUI/StandUpReminderWindow.xaml.cs` (prompt window behavior: AppWindow setup, centering, guarded close, live updates)
  - `src/StandupReminder.WinUI/StandUpReminderPromptHost.cs` (real WinUI `IReminderPromptHost` implementation with single-window reuse and UI-thread marshalling)
  - `src/StandupReminder.WinUI/SettingsWindow.xaml` (WinUI settings surface)
  - `src/StandupReminder.WinUI/SettingsWindow.xaml.cs` (settings window behavior: AppWindow setup, color editing, validation, save/cancel)
  - `src/StandupReminder.WinUI/SettingsWindowViewModel.cs` (whole-minute validation and settings-form state)
  - `src/StandupReminder.Windows/NotifyIconReminderTrayHost.cs` (tray adapter — events wired through composition)
  - `src/StandupReminder.Core/Services/ReminderSchedulerRuntime.cs` (runtime wrapper)
  - `src/StandupReminder.Core/Services/ReminderSchedulerEngine.cs` (scheduler state machine)
  - `src/StandupReminder.Persistence/ArgbHexColor.cs` (shared ARGB validation/normalization used by the WinUI settings slice)
  - `src/StandupReminder.Persistence/LocalAppDataReminderSettingsStore.cs` (shared reminder schedule persistence)
  - `src/StandupReminder.Persistence/LocalAppDataAppearanceSettingsStore.cs` (shared appearance persistence)
  - `src/StandupReminder.Persistence/LocalAppDataSessionEventLogStore.cs` (shared session-event log persistence)
- Microsoft documentation relevant to the remaining work:
  - App lifecycle / app instancing: `https://learn.microsoft.com/windows/apps/windows-app-sdk/applifecycle/applifecycle-instancing`
  - Single-instance WinUI app guidance: `https://learn.microsoft.com/windows/apps/windows-app-sdk/applifecycle/applifecycle-single-instance`
  - App window management / AppWindow patterns for secondary windows: `https://learn.microsoft.com/windows/apps/develop/ui/manage-app-windows`
  - Notification Area / `Shell_NotifyIcon`: `https://learn.microsoft.com/windows/win32/shell/notification-area`
  - Windows App SDK unpackaged deployment guidance: `https://learn.microsoft.com/windows/apps/windows-app-sdk/deploy-unpackaged-apps`
  - App notifications quickstart: `https://learn.microsoft.com/windows/apps/windows-app-sdk/notifications/app-notifications/app-notifications-quickstart`
  - `DispatcherQueueTimer`: `https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.dispatching.dispatcherqueuetimer?view=windows-app-sdk-1.8`