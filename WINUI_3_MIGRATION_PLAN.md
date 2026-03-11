# WinUI 3 Migration Plan

## User Goals & Intent
- Fully revamp the current WPF tray-first reminder app into a modern WinUI 3 desktop app rather than attempt an in-place framework conversion.
- Preserve current business behavior: single-instance desktop lifecycle, tray-first operation, persisted reminder settings, persisted session-event history, snooze support, manual pause/resume, and the stand-up prompt workflow.
- Replace WPF-specific window/message plumbing with WinUI 3 and Windows App SDK patterns wherever a modern equivalent exists.
- Keep Win32 interop only where Microsoft still exposes no first-party WinUI-native equivalent.
- Success criteria for the migration:
  - feature parity with the existing WPF app,
  - packaged WinUI 3 app recommended as the default target,
  - explicit compatibility layer for tray and any remaining session/owner-window interop,
  - a validation plan that proves behavior outside Visual Studio-installed runs.

## Key Technical Context
- The current app is feasible to migrate, but not by direct file-for-file conversion, because the hard coupling is to WPF window/message infrastructure rather than to Win32 session APIs themselves.
- Current repo areas that define the migration surface:
  - `StandupReminder.App/App.xaml.cs` = startup/composition root.
  - `StandupReminder.App/Services/WindowsSessionEventMonitor.cs` = WPF `HwndSource` + `WindowInteropHelper` + `WM_WTSSESSION_CHANGE` integration.
  - `StandupReminder.App/Services/NotifyIconTrayService.cs` = WinForms tray/menu/timer compatibility layer.
  - `StandupReminder.App/Services/PostureReminderScheduler.cs` = core reminder state machine.
  - `StandupReminder.App/StandUpReminderWindow.xaml.cs` and `SettingsWindow.xaml.cs` = WPF-specific window UX.
- Recommended target architecture:
  - WinUI 3 desktop app for UI shell and windows.
  - Windows App SDK app lifecycle APIs for single-instance orchestration.
  - Extracted framework-agnostic reminder domain/services where possible.
  - Thin compatibility layer for notification-area tray support and any remaining HWND-only behavior.
  - Keep persistence contracts and models portable so they survive the UI rewrite.

### Current-to-target API/platform mapping

| Current mechanism | Current role | Recommended target | Decision |
| --- | --- | --- | --- |
| `App.xaml.cs` WPF startup/composition | Bootstrap, tray, scheduler, main window wiring | WinUI `App.xaml.cs` + custom `Program.cs` + `Microsoft.Windows.AppLifecycle.AppInstance` | Replace with modern Windows App SDK lifecycle |
| WPF window model (`Window`, `WindowInteropHelper`, `HwndSource.AddHook`) | Native handle access and message hook plumbing | WinUI `Window` + `AppWindow` for normal window management; use HWND interop only where still required | Mostly replace |
| `WM_WTSSESSION_CHANGE` + `WTSRegisterSessionNotification` | Lock/unlock/logon/logoff session monitoring | First evaluate `Microsoft.Win32.SystemEvents.SessionSwitch`; retain raw WTS path if exact low-level fidelity is needed | Partial modern alternative |
| `DispatcherTimer` | Scheduler loop timing | `Microsoft.UI.Dispatching.DispatcherQueueTimer` or timer abstraction backed by `DispatcherQueue` | Replace |
| WinForms `NotifyIcon` / tray menu | Tray icon, context menu, pause/resume/open/settings/exit | Keep compatibility/interop layer (`NotifyIcon` or direct `Shell_NotifyIcon`) because no first-party WinUI tray API was identified | Keep interop |
| WPF reminder/settings windows | Primary UX surfaces | Rebuild as WinUI windows/pages/dialogs; use `AppWindow`/`OverlappedPresenter` where appropriate | Replace |
| WinForms `ColorDialog` | Popup appearance editing | Prefer WinUI-native color editing UX (`ColorPicker` or custom editor) | Replace |
| Ad-hoc top-level window state control | Size/position/presenter behavior | `AppWindow`, `OverlappedPresenter`, `CompactOverlayPresenter`, etc. | Replace where applicable |
| Tray balloon/system notification assumptions | Reminder/status notifications | Use Windows App SDK app notifications for toast-style notifications where useful, but not as a tray replacement | Supplement only |

### Documentation-grounded replacement notes
- WinUI apps are multi-instanced by default, so single-instance behavior must be implemented intentionally with `AppInstance.FindOrRegisterForKey`, `IsCurrent`, and `RedirectActivationToAsync`.
- `AppWindow` is the high-level abstraction over the top-level HWND and should be the default path for size, presenter, title bar, and window state behavior.
- `SystemEvents.SessionSwitch` is a viable higher-level session event source for lock/unlock/logon/logoff, but it only works with a running message pump and requires explicit unsubscription because it is a static event.
- `WTSRegisterSessionNotification` remains the authoritative low-level fallback when exact `WM_WTSSESSION_CHANGE` semantics are required.
- Notification-area tray behavior still roots back to `Shell_NotifyIcon`; no first-party pure WinUI tray primitive was identified in the Microsoft documentation reviewed.
- Windows App SDK app notifications are useful for toast/notification UX, but they do not replace a tray icon.
- For modal owner-window scenarios, `AppWindow` helps with top-level windowing, but owner/modality can still require Win32 interop.

## Environment & Configuration Details
- Workspace/repo: `C:\personal\StandupReminder`
- Current app type: WPF desktop app targeting `net8.0-windows`
- Current installer flow: publish output + Inno Setup packaging
- Current persistence model: per-user local app data for reminder settings, appearance settings, and session-event history
- Current lifecycle model: tray-first, single-user desktop app, current-session monitoring
- Recommended migration target:
  - WinUI 3 desktop app using the Windows App SDK
  - packaged deployment as the default recommendation
  - latest stable Windows App SDK version available at implementation start
- Why packaged is the default recommendation:
  - cleaner Windows integration through package identity,
  - simpler notification integration,
  - fewer runtime-bootstrap concerns than unpackaged deployment,
  - easier long-term support posture for a modern desktop app.
- When unpackaged remains acceptable:
  - if the existing installer/channel must stay MSI/Inno-first,
  - if package identity is intentionally avoided,
  - and if the team accepts Windows App SDK bootstrapper/runtime distribution requirements.

## Discussion Highlights

### Phase 0 - Confirm architecture direction and success criteria
- Freeze current behavior as the parity baseline before any rewrite begins.
- Decide and document:
  - packaged vs unpackaged target,
  - whether current-session-only monitoring remains sufficient,
  - whether the stand-up prompt must remain blocking/owner-bound in the same way,
  - whether tray-first behavior remains mandatory at launch.
- Deliverables:
  - migration RFC/checklist,
  - feature parity matrix,
  - risk register.

### Phase 1 - Extract framework-agnostic core logic from WPF assumptions
- Separate pure reminder domain logic from UI/window concerns.
- Move scheduler state transitions, settings models, persistence contracts, and session-event translation contracts behind framework-neutral interfaces.
- Define new boundaries such as:
  - app lifecycle service,
  - session event source,
  - tray host,
  - reminder prompt host,
  - notification service.
- Goal: make only the shell and interop layers UI-framework-specific.

### Phase 2 - Create the WinUI 3 shell and lifecycle bootstrap
- Stand up a new WinUI 3 app project rather than trying to mutate the WPF project in place.
- Add custom `Program.cs` and disable generated main if single-instance redirection is handled before XAML startup.
- Implement single-instance behavior using `AppInstance` and activation redirection.
- Recreate the composition root so services are initialized in WinUI rather than WPF.
- Exit criteria:
  - app launches cleanly,
  - second launch activates existing instance,
  - no reminder logic yet required beyond shell startup.

### Phase 3 - Rebuild tray-first hosting and background interaction model
- Port `NotifyIconTrayService` into a shell/interop layer owned by the WinUI app.
- Keep tray behavior behind an interface so the WinUI shell does not depend directly on WinForms or shell APIs.
- Recreate current menu commands:
  - Open
  - Pause timer / Resume timer
  - Settings
  - Exit
- Decide whether to keep WinForms `NotifyIcon` for speed or replace it with a direct `Shell_NotifyIcon` wrapper for tighter control.
- Recommendation: keep the compatibility layer first, optimize later only if it becomes a maintenance issue.

### Phase 4 - Replace session monitoring with a tiered strategy
- First implementation target: `SystemEvents.SessionSwitch` for lock/unlock/logon/logoff handling in the WinUI desktop process.
- Build a translation layer from raw session events into the app's existing session event model.
- Validate parity for:
  - `SessionLock`
  - `SessionUnlock`
  - `SessionLogon`
  - optionally `SessionLogoff` if useful for persistence/state cleanup.
- Keep a fallback implementation using `WTSRegisterSessionNotification` if testing shows gaps in timing, fidelity, or activation behavior.
- Recommendation: design `ISessionEventSource` with at least two implementations:
  - `SystemEventsSessionEventSource`
  - `WtsSessionEventSource`

### Phase 5 - Port the reminder scheduler to WinUI-compatible timing
- Replace WPF `DispatcherTimer` usage with `DispatcherQueueTimer` or a timer abstraction using `DispatcherQueue`.
- Preserve the existing state machine semantics:
  - `Idle`
  - `SittingCountdown`
  - `SnoozedCountdown`
  - `StandingCountdown`
  - `StandPromptPending`
  - `PausedForLock`
  - `PausedManually`
- Keep the scheduler independent from window classes so prompt display becomes a hosted action rather than direct WPF window manipulation.
- Exit criteria:
  - countdowns survive session pause/resume logic,
  - manual tray pause/resume works,
  - snooze flow still reopens the prompt correctly.

### Phase 6 - Rebuild the reminder and settings UX in WinUI 3
- Recreate the settings surface using WinUI controls and modern theming.
- Rebuild the stand-up prompt as a WinUI window/dialog experience.
- Use `AppWindow` and `OverlappedPresenter` for supported window behavior such as size, placement, and presenter configuration.
- If true modal owner-window behavior is still required, isolate any HWND owner interop to the window host layer only.
- Replace the WinForms color picker dependency with WinUI-native UI.

### Phase 7 - Reconnect persistence, appearance, and event history
- Reuse the existing persistence model and file formats where possible to avoid data migration risk.
- Port settings/event-history stores with minimal schema change.
- Verify that existing local app data survives the migration path or create a one-time import path if the app identity/storage location changes.
- Pay special attention to packaged deployment if local paths or startup behavior differ from today's installer-driven model.

### Phase 8 - Notifications, packaging, startup, and deployment
- Keep tray icon behavior separate from toast/app notifications.
- Use Windows App SDK app notifications only where they improve reminder/status UX.
- Revisit startup enablement strategy:
  - keep Windows Startup Apps / installer-managed startup behavior, or
  - adopt packaged-app startup capabilities if they fit the chosen deployment model.
- Recommendation: if moving to packaged WinUI, reevaluate the entire installer story instead of blindly reusing the current Inno flow.

### Phase 9 - Validation, cutover, and retirement of WPF app
- Validate on installed builds, not only F5 debugging sessions.
- Required validation matrix:
  - first launch and second-launch single-instance activation,
  - lock/unlock/logon behavior,
  - tray menu actions,
  - snooze flow,
  - manual pause/resume,
  - persisted settings reload,
  - session-event history persistence,
  - packaged/unpackaged deployment-specific runtime behavior.
- Final cutover approach:
  - run WinUI build as side-by-side prerelease,
  - close parity gaps,
  - then retire the WPF app after acceptance.

## Issues, Assumptions & Open Questions
- Assumption: tray-first behavior remains a hard requirement after migration.
- Assumption: current-session-only monitoring is still acceptable unless multi-session support is explicitly requested.
- Assumption: the existing reminder state machine behavior should be preserved before any UX redesign changes are introduced.
- Assumption: packaged WinUI 3 is preferred unless installer/distribution constraints force unpackaged deployment.
- Key risk: `SystemEvents.SessionSwitch` is higher-level and simpler, but must be proven equivalent enough for this app's timing and behavior expectations.
- Key risk: tray support is still interop territory, so the final WinUI app will not be 100% free of Win32/compatibility code.
- Key risk: if the stand-up prompt depends on exact owner/modal semantics, some HWND interop may remain necessary even after adopting `AppWindow`.
- Open question: should the migration preserve the current local data files as-is, or intentionally version/migrate them?
- Open question: should the new WinUI app remain Inno-installed, or should packaging be reworked around MSIX/package identity?
- Open question: is exact parity for `WM_WTSSESSION_CHANGE` required, or is parity for the app's observable lock/unlock/logon behavior sufficient?
- Open question: should the migration keep the existing tray icon implementation technology initially for delivery speed?

## References & Contextual Notes
- Current implementation files most relevant to migration:
  - `StandupReminder.App/App.xaml.cs`
  - `StandupReminder.App/Services/WindowsSessionEventMonitor.cs`
  - `StandupReminder.App/Services/NotifyIconTrayService.cs`
  - `StandupReminder.App/Services/PostureReminderScheduler.cs`
  - `StandupReminder.App/StandUpReminderWindow.xaml.cs`
  - `StandupReminder.App/SettingsWindow.xaml.cs`
- Microsoft documentation used to ground this plan:
  - App instancing with the app lifecycle API: `https://learn.microsoft.com/windows/apps/windows-app-sdk/applifecycle/applifecycle-instancing`
  - Create a single-instanced WinUI 3 app with C#: `https://learn.microsoft.com/windows/apps/windows-app-sdk/applifecycle/applifecycle-single-instance`
  - Manage app windows / `AppWindow`: `https://learn.microsoft.com/windows/apps/develop/ui/manage-app-windows`
  - `SystemEvents.SessionSwitch`: `https://learn.microsoft.com/dotnet/api/microsoft.win32.systemevents.sessionswitch?view=windowsdesktop-10.0`
  - `WTSRegisterSessionNotification`: `https://learn.microsoft.com/windows/win32/api/wtsapi32/nf-wtsapi32-wtsregistersessionnotification`
  - `WTSUnRegisterSessionNotification`: `https://learn.microsoft.com/windows/win32/api/wtsapi32/nf-wtsapi32-wtsunregistersessionnotification`
  - `WM_WTSSESSION_CHANGE`: `https://learn.microsoft.com/windows/win32/termserv/wm-wtssession-change`
  - Notification Area guidance / `Shell_NotifyIcon`: `https://learn.microsoft.com/windows/win32/shell/notification-area`
  - Windows App SDK deployment guide for packaged-with-external-location or unpackaged apps: `https://learn.microsoft.com/windows/apps/windows-app-sdk/deploy-unpackaged-apps`
  - Windows App SDK app notifications quickstart: `https://learn.microsoft.com/windows/apps/windows-app-sdk/notifications/app-notifications/app-notifications-quickstart`
  - `DispatcherQueueTimer`: `https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.dispatching.dispatcherqueuetimer?view=windows-app-sdk-1.8`
- This file is a planning artifact only; no source migration has been performed yet.