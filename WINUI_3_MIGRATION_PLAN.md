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
  - scheduler state-machine extraction into `StandupReminder.Core`
  - framework-agnostic reminder contracts for prompt/tray/session-related behavior
- Core validation foundation is in place:
  - `tests/StandupReminder.Core.Tests`
  - scheduler-engine unit coverage
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

### Current ownership map
- `StandupReminder.WinUI`
  - owns the WinUI app entry/bootstrap path and the current shell window
  - currently displays persisted reminder schedule state
  - does **not** yet own the live reminder runtime
- `StandupReminder.Core`
  - owns reminder domain models, contracts, and scheduler engine/state-machine logic
- `StandupReminder.Persistence`
  - owns reminder-settings persistence and the shared settings document for schedule data
- `StandupReminder.Windows`
  - project exists but still needs the concrete Windows adapter implementations for the migrated WinUI runtime
- `StandupReminder.App` (WPF)
  - still owns the live runtime composition root and most active desktop behavior:
    - tray integration
    - session monitoring
    - runtime scheduler hosting
    - reminder prompt window
    - settings window
    - appearance persistence integration
    - session-event history persistence

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

### Stage 1 - Introduce WinUI runtime composition without cutting over behavior
Goal: give the WinUI app a real runtime-composition boundary beyond loading persisted settings.

- Add a small WinUI-owned composition service/coordinator that can host migrated runtime pieces.
- Define how the WinUI shell observes live scheduler state without yet deleting the WPF path.
- Keep the composition seam explicit so future Windows adapters plug into interfaces rather than directly into views.

Exit criteria:
- WinUI bootstrap creates runtime-facing services through a dedicated composition path.
- The new composition path is isolated enough to accept timer/session/tray/prompt adapters next.
- WPF continues to run unchanged as the parity baseline until live WinUI runtime behavior is ready.

### Stage 2 - Implement Windows adapters in `StandupReminder.Windows`
Goal: move Windows-specific infrastructure behind stable adapter interfaces.

Recommended order:
1. timer adapter for WinUI-compatible ticking
2. session event source
3. tray host
4. any owner-window/AppWindow/HWND glue still required for prompt behavior

Specific work:
- Add a WinUI/Windows-compatible timer implementation for the scheduler runtime.
- Implement a session event source with a tiered strategy:
  - first: `SystemEvents.SessionSwitch`
  - fallback: `WTSRegisterSessionNotification` if parity requires it
- Move tray hosting into `StandupReminder.Windows`, keeping the WinUI app dependent only on an abstraction.
- Isolate all Win32 interop needed for top-level window ownership or activation behavior.

Exit criteria:
- WinUI can construct timer/session/tray adapters through `StandupReminder.Windows`.
- No new direct WinForms/raw-Win32 coupling is introduced into `StandupReminder.WinUI`.

### Stage 3 - Move live scheduler behavior into WinUI
Goal: make the WinUI app own the running reminder workflow.

- Host the scheduler engine from the WinUI runtime composition path.
- Connect timer, session, tray, and prompt abstractions to the live WinUI-hosted runtime.
- Surface live phase/remaining-time state in the WinUI shell.
- Keep behavior parity with the current WPF runtime:
  - sitting countdown
  - stand prompt
  - snooze path
  - manual pause/resume
  - pause/resume for session lock/unlock

Exit criteria:
- WinUI can run the reminder lifecycle end-to-end.
- WPF is no longer the only runtime composition root.
- Core behavior remains hosted through abstractions rather than UI-specific logic.

### Stage 4 - Rebuild remaining WPF UX in WinUI
Goal: replace the last WPF-owned user-facing surfaces.

- Rebuild the settings experience in WinUI.
- Rebuild the stand-up prompt in WinUI.
- Replace remaining WPF-specific interaction patterns with WinUI/AppWindow-friendly equivalents.
- Replace any WinForms-based color editing flow with WinUI-native controls if still applicable.

Notes:
- If exact owner/modal semantics are needed, keep that interop isolated to a host/adapter layer.
- Avoid broad UX redesign during parity migration; focus on behavior-preserving replacement first.

Exit criteria:
- Settings and prompt flows are WinUI-owned.
- WPF windows are no longer required for primary reminder UX.

### Stage 5 - Finish the remaining persistence migration
Goal: complete the storage ownership move that is still split between WPF and the new projects.

Remaining likely moves:
- appearance persistence integration
- session-event history persistence
- any local data compatibility/import helpers needed for future packaging changes

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
- Key risk: tray support and some window ownership behavior still require compatibility/interop code.
- Key risk: `SystemEvents.SessionSwitch` may not fully match the fidelity/timing expected from lower-level session notifications.
- Key risk: packaging choice may affect startup behavior, notification identity, and local data continuity.
- Open question: should the final release remain compatible with the current Inno-based channel, or should cutover align with MSIX/package identity?
- Open question: is higher-level observable parity for session behavior sufficient, or is exact low-level `WM_WTSSESSION_CHANGE` parity required?
- Open question: should the first WinUI runtime slice reuse the existing tray technology behind an adapter for speed, then optimize later?

## References & Contextual Notes
- Current files most relevant to the remaining migration work:
  - `StandupReminder.App/App.xaml.cs`
  - `StandupReminder.App/Services/WindowsSessionEventMonitor.cs`
  - `StandupReminder.App/Services/NotifyIconTrayService.cs`
  - `StandupReminder.App/Services/PostureReminderScheduler.cs`
  - `StandupReminder.App/StandUpReminderWindow.xaml.cs`
  - `StandupReminder.App/SettingsWindow.xaml.cs`
  - `src/StandupReminder.WinUI/App.xaml.cs`
  - `src/StandupReminder.WinUI/Program.cs`
  - `src/StandupReminder.WinUI/AppBootstrapper.cs`
  - `src/StandupReminder.WinUI/ShellViewModel.cs`
  - `src/StandupReminder.Persistence/LocalAppDataReminderSettingsStore.cs`
  - `src/StandupReminder.Persistence/LocalAppDataSettingsDocumentStore.cs`
  - `src/StandupReminder.Core/Services/ReminderSchedulerEngine.cs`
- Microsoft documentation relevant to the remaining work:
  - App lifecycle / app instancing: `https://learn.microsoft.com/windows/apps/windows-app-sdk/applifecycle/applifecycle-instancing`
  - Single-instance WinUI app guidance: `https://learn.microsoft.com/windows/apps/windows-app-sdk/applifecycle/applifecycle-single-instance`
  - App window management: `https://learn.microsoft.com/windows/apps/develop/ui/manage-app-windows`
  - `SystemEvents.SessionSwitch`: `https://learn.microsoft.com/dotnet/api/microsoft.win32.systemevents.sessionswitch?view=windowsdesktop-10.0`
  - `WTSRegisterSessionNotification`: `https://learn.microsoft.com/windows/win32/api/wtsapi32/nf-wtsapi32-wtsregistersessionnotification`
  - `WM_WTSSESSION_CHANGE`: `https://learn.microsoft.com/windows/win32/termserv/wm-wtssession-change`
  - Notification Area / `Shell_NotifyIcon`: `https://learn.microsoft.com/windows/win32/shell/notification-area`
  - Windows App SDK unpackaged deployment guidance: `https://learn.microsoft.com/windows/apps/windows-app-sdk/deploy-unpackaged-apps`
  - App notifications quickstart: `https://learn.microsoft.com/windows/apps/windows-app-sdk/notifications/app-notifications/app-notifications-quickstart`
  - `DispatcherQueueTimer`: `https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.dispatching.dispatcherqueuetimer?view=windows-app-sdk-1.8`