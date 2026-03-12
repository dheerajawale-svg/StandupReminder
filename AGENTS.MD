## StandupReminder Workspace Agent Guide

## Solution Purpose
StandupReminder is a tray-first Windows desktop reminder app that helps users alternate between sitting and standing. The repository is in the middle of a migration from a legacy WPF implementation to a WinUI 3 implementation while preserving existing behavior: single-instance lifecycle, tray operation, reminder scheduling, snooze, pause/resume, persisted settings, and session-event awareness.

## Solution Structure
- `src/StandupReminder.Core` — framework-agnostic reminder domain and scheduler logic.
- `src/StandupReminder.Persistence` — shared local storage for settings and session-event history.
- `src/StandupReminder.Windows` — Windows-only adapters for tray, timer, and session events.
- `src/StandupReminder.WinUI` — primary WinUI 3 desktop application shell.
- `src/StandupReminder.App` — legacy WPF fallback/reference implementation.
- `tests/StandupReminder.Core.Tests` — unit tests for Core and Persistence.

## Project: StandupReminder.Core
### Purpose
Framework-agnostic domain library targeting `net8.0`. Contains reminder scheduling logic, domain models, and platform-neutral service contracts.

### Responsibilities
- Owns the scheduler state machine in `ReminderSchedulerEngine`.
- Owns the runtime wrapper in `ReminderSchedulerRuntime`.
- Defines domain models like `ReminderPhase` and `ReminderScheduleOptions`.
- Defines abstractions implemented by platform projects:
  - `IReminderTickSource`
  - `IReminderSessionEventSource`
  - `IReminderSessionEventSink`
  - `IReminderTrayHost`
  - `IReminderTrayNotifier`
  - `IReminderPromptHost`

### Key Files
- `Models/ReminderPhase.cs`
- `Models/ReminderScheduleOptions.cs`
- `Services/ReminderSchedulerEngine.cs`
- `Services/ReminderSchedulerRuntime.cs`
- `Services/ReminderSessionEvent.cs`

### Notes
- No UI or Windows-specific dependencies.
- Uses an injectable `Func<DateTimeOffset>` clock for deterministic testing.

## Project: StandupReminder.Persistence
### Purpose
Shared persistence library targeting `net8.0`. Owns local settings and session-event storage used by both WPF and WinUI applications.

### Responsibilities
- Persists reminder schedule settings.
- Persists appearance settings for the reminder prompt.
- Persists session-event history.
- Manages the shared `%LocalAppData%\StandupReminder\settings.json` document.
- Validates and normalizes ARGB hex color values.

### Key Files
- `IReminderSettingsStore.cs`
- `LocalAppDataReminderSettingsStore.cs`
- `IAppearanceSettingsStore.cs`
- `LocalAppDataAppearanceSettingsStore.cs`
- `ISessionEventLogStore.cs`
- `LocalAppDataSessionEventLogStore.cs`
- `LocalAppDataSettingsDocumentStore.cs`
- `AppearanceSettings.cs`
- `SessionEventLogEntry.cs`
- `ArgbHexColor.cs`

### Notes
- Depends on `StandupReminder.Core` for shared models.
- Preserves settings document sections during round-trips to avoid data loss.

## Project: StandupReminder.Windows
### Purpose
Windows-specific adapter library targeting `net8.0-windows`. Bridges Core contracts to WinForms and Windows session APIs.

### Responsibilities
- Implements the timer abstraction using `System.Threading.Timer` and `SynchronizationContext`.
- Implements session event monitoring using `SystemEvents.SessionSwitch`.
- Implements the system tray host using WinForms `NotifyIcon`.
- Implements notification-only tray balloon support.

### Key Files
- `SynchronizationContextReminderTickSource.cs`
- `SystemEventsReminderSessionEventSource.cs`
- `NotifyIconReminderTrayHost.cs`
- `NotifyIconReminderTrayNotifier.cs`

### Notes
- Depends on `StandupReminder.Core`.
- Tray callbacks fire on WinForms threads; consuming UI layers must marshal to their own UI thread.

## Project: StandupReminder.WinUI
### Purpose
Primary WinUI 3 desktop app targeting `net8.0-windows10.0.22621.0` with Windows App SDK. Runs unpackaged and is the target end-state application.

### Responsibilities
- Owns the custom entry point and single-instance activation flow.
- Owns WinUI app lifecycle and runtime composition.
- Hosts the live scheduler runtime, tray integration, and session-event integration.
- Provides the main shell window with live scheduler state.
- Provides the WinUI stand-up prompt window.
- Provides the WinUI settings window.

### Key Files
- `Program.cs`
- `App.xaml.cs`
- `AppBootstrapper.cs`
- `ReminderRuntimeComposition.cs`
- `ShellViewModel.cs`
- `MainWindow.xaml`
- `StandUpReminderWindow.xaml`
- `StandUpReminderPromptHost.cs`
- `SettingsWindow.xaml`
- `SettingsWindowViewModel.cs`

### Notes
- Depends on Core, Persistence, and Windows adapter projects.
- Uses `DispatcherQueue.TryEnqueue` to marshal tray-driven actions from WinForms threads onto the WinUI UI thread.
- Settings changes persist immediately and apply to the live runtime.

## Project: StandupReminder.App
### Purpose
Legacy WPF desktop application targeting `net8.0-windows`. Acts as the fallback/reference implementation during the WinUI migration.

### Responsibilities
- Owns the original WPF app lifecycle and tray-first runtime behavior.
- Contains the WPF-era scheduler, tray service, session monitor, prompt window, and settings window.
- Contains registry-based auto-start registration.
- Provides reference behavior while WinUI parity is being validated.

### Key Files
- `App.xaml.cs`
- `MainWindow.xaml`
- `StandUpReminderWindow.xaml`
- `SettingsWindow.xaml`
- `Services/PostureReminderScheduler.cs`
- `Services/NotifyIconTrayService.cs`
- `Services/WindowsSessionEventMonitor.cs`
- `Services/RegistryAutoStartRegistrationService.cs`
- `Services/StandUpReminderPromptHost.cs`
- `ViewModels/MainWindowViewModel.cs`
- `ViewModels/SettingsWindowViewModel.cs`

### Notes
- Depends on Core and Persistence.
- Uses `WPF-UI`.
- Intended for retirement after WinUI behavior parity is validated.

## Project: StandupReminder.Core.Tests
### Purpose
Unit test project targeting `net8.0` with xUnit. Validates Core scheduler behavior and Persistence round-trips.

### Responsibilities
- Tests `ReminderSchedulerEngine` state-machine behavior.
- Tests `ReminderSchedulerRuntime` timer lifecycle behavior.
- Tests persistence round-trips for schedule, appearance, and session-event logs.

### Key Files
- `ReminderSchedulerEngineTests.cs`
- `ReminderSchedulerRuntimeTests.cs`
- `ReminderSettingsStoreTests.cs`

### Notes
- Depends on `StandupReminder.Core` and `StandupReminder.Persistence`.
- Uses xUnit, `Microsoft.NET.Test.Sdk`, and `coverlet.collector`.
- Main test command: `dotnet test tests/StandupReminder.Core.Tests/StandupReminder.Core.Tests.csproj`

## Current Migration Context
- WinUI is the primary target architecture.
- Core remains framework-agnostic.
- Windows adapters isolate WinForms/Windows-specific behavior.
- Persistence is shared by both app shells.
- WPF remains only as a fallback/reference path until full WinUI parity is manually validated.

## Implemented Migration Baseline
- Solution/project split is already established across Core, Persistence, Windows, WinUI, and WPF fallback projects.
- Core extraction is complete:
  - `ReminderPhase`
  - `ReminderScheduleOptions`
  - `ReminderSchedulerEngine`
  - `ReminderSchedulerRuntime`
  - framework-agnostic contracts for tick source, session events, tray host/notifier, and prompt host
- WinUI bootstrap is complete:
  - custom `Program.cs`
  - `DISABLE_XAML_GENERATED_MAIN`
  - Windows App SDK single-instance activation/redirection
- Shared persistence migration is largely complete:
  - reminder schedule settings are shared
  - appearance settings are shared
  - session-event log persistence is shared
  - WPF already consumes the shared persistence implementations
- Windows adapter layer is complete:
  - `SynchronizationContextReminderTickSource`
  - `SystemEventsReminderSessionEventSource`
  - `NotifyIconReminderTrayHost`
  - `NotifyIconReminderTrayNotifier`
- WinUI runtime composition is live:
  - scheduler runtime hosted from WinUI
  - tray, timer, and session adapters connected
  - shell shows live phase/remaining time state
  - tray status text and pause menu label are synchronized from runtime state

## Current Ownership Snapshot
- `StandupReminder.WinUI` currently owns:
  - app entry/bootstrap path
  - runtime composition through `AppBootstrapper` and `ReminderRuntimeComposition`
  - tray event handling for `OpenRequested`, `PauseResumeRequested`, `SettingsRequested`, and `ExitRequested`
  - main shell window and `ShellViewModel`
  - WinUI stand-up prompt through `StandUpReminderWindow` and `StandUpReminderPromptHost`
  - WinUI settings UX through `SettingsWindow` and `SettingsWindowViewModel`
- `StandupReminder.Core` owns all domain/runtime abstractions and scheduler behavior.
- `StandupReminder.Persistence` owns settings/history persistence and ARGB normalization.
- `StandupReminder.Windows` owns the concrete Windows-only adapters.
- `StandupReminder.App` still owns the WPF fallback/reference runtime and legacy desktop behavior used for parity comparison.

## Implemented WinUI Behavior
- Tray-driven actions are wired into the live runtime:
  - Open activates the main window
  - Pause/Resume toggles the scheduler runtime
  - Settings opens a single-instance WinUI settings window
  - Exit performs graceful shutdown and composition disposal
- Prompt behavior implemented in WinUI:
  - separate secondary WinUI window
  - single prompt instance at a time
  - reuse/activate existing prompt when already visible
  - confirm and snooze actions
  - dismiss for lock, manual pause, and shutdown
  - live stand-duration and background updates while visible
  - fixed-size, centered, always-on-top behavior with guarded close behavior
- Settings behavior implemented in WinUI:
  - separate single-instance settings window
  - whole-minute validation for sit/stand durations
  - ARGB validation and normalization via `ArgbHexColor`
  - A/R/G/B sliders, preview swatch, and WinUI `ColorPicker`
  - save/cancel flow
  - persistence through shared stores in `StandupReminder.Persistence`
  - immediate application of saved settings to the live `ReminderSchedulerRuntime`
- Threading behavior:
  - tray callbacks arrive on WinForms threads and are marshalled to WinUI with `DispatcherQueue.TryEnqueue`
  - prompt-host operations self-marshal to the WinUI UI thread

## Validation Status
- The WinUI prompt and settings implementation has already passed targeted verification:
  - `dotnet build src/StandupReminder.WinUI/StandupReminder.WinUI.csproj`
  - `dotnet test tests/StandupReminder.Core.Tests/StandupReminder.Core.Tests.csproj`
- Core/Persistence test suite currently contains 18 passing tests covering scheduler and persistence behavior.

## Remaining Work Before WPF Retirement
- Manual parity/smoke validation is still required for the live WinUI tray app, especially:
  - sitting countdown
  - stand prompt show/reuse/confirm/snooze/dismiss flow
  - tray-driven settings open/edit/save/reopen flow
  - runtime updates after settings save while already running
  - manual pause/resume
  - lock/unlock pause and resume behavior
- Packaging/cutover work is still pending:
  - confirm unpackaged/Inno vs MSIX direction
  - verify startup/deployment behavior for the chosen model
  - validate installed-style runs before retiring WPF
- WPF should remain present until WinUI parity is confirmed in realistic desktop runs.

## Migration Constraints That Still Matter
- Keep WPF behavior working unless a slice is explicitly replaced and validated.
- Preserve local data compatibility; avoid unnecessary schema churn.
- Keep ownership moves narrow and abstraction-first.
- Keep WinUI isolated from raw Win32/WinForms details except through adapters/hosts.

## Important Risks And Open Questions
- Packaging choice may affect startup behavior, notification identity, and local data continuity.
- WPF `ITrayService` currently hides inherited Core members and may produce benign CS0108 warnings until WPF tray ownership is retired.
- Session events can reach the prompt host outside timer-posted flows, so prompt operations must continue to self-marshal to the UI thread.
- Prompt/settings compile and pass targeted tests, but still require live desktop smoke validation before the WPF fallback can be removed confidently.
- Open packaging question: remain compatible with the current Inno-based channel or cut over with package identity/MSIX.
- Open session question: current implementation uses `SystemEvents.SessionSwitch`; revisit only if exact low-level `WM_WTSSESSION_CHANGE` parity becomes necessary.

## Most Relevant Files For Ongoing Migration Work
- `src/StandupReminder.WinUI/App.xaml.cs`
- `src/StandupReminder.WinUI/AppBootstrapper.cs`
- `src/StandupReminder.WinUI/ReminderRuntimeComposition.cs`
- `src/StandupReminder.WinUI/ShellViewModel.cs`
- `src/StandupReminder.WinUI/StandUpReminderWindow.xaml`
- `src/StandupReminder.WinUI/StandUpReminderWindow.xaml.cs`
- `src/StandupReminder.WinUI/StandUpReminderPromptHost.cs`
- `src/StandupReminder.WinUI/SettingsWindow.xaml`
- `src/StandupReminder.WinUI/SettingsWindow.xaml.cs`
- `src/StandupReminder.WinUI/SettingsWindowViewModel.cs`
- `src/StandupReminder.Windows/NotifyIconReminderTrayHost.cs`
- `src/StandupReminder.Core/Services/ReminderSchedulerEngine.cs`
- `src/StandupReminder.Core/Services/ReminderSchedulerRuntime.cs`
- `src/StandupReminder.Persistence/LocalAppDataReminderSettingsStore.cs`
- `src/StandupReminder.Persistence/LocalAppDataAppearanceSettingsStore.cs`
- `src/StandupReminder.Persistence/LocalAppDataSessionEventLogStore.cs`