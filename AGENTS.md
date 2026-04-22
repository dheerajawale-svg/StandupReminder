# AGENTS.md

## Scope
This file applies to the entire repository at `C:\personal\StandupReminder`.

## Project Summary
- This repository contains a Windows desktop application project, `StandupReminder.App`, plus a small supporting Windows interop library, `StandupReminder.WindowsInterop`.
- `StandupReminder.App` targets `net10.0-windows10.0.17763.0` with `UseWPF=true` and `UseWindowsForms=true`.
- `StandupReminder.WindowsInterop` targets `net10.0-windows` and contains Windows-specific session-monitoring and autorun registration helpers shared by the app.
- `WPF-UI` is used for window chrome, theming, and controls.
- `Microsoft.Toolkit.Uwp.Notifications` is used for Windows app notification toasts.
- `Microsoft.Extensions.Logging` and `Microsoft.Extensions.Logging.Console` are referenced by the app.
- The app runs primarily from the system tray, tracks Windows session events, alternates sitting and standing intervals, and persists settings under Local AppData.

## Repository Layout
- `StandupReminder.slnx`: solution entrypoint with two projects.
- `StandupReminder.App/`: main WPF application.
- `StandupReminder.App/Models/`: plain settings/state models and enums.
- `StandupReminder.App/Services/`: scheduler, tray integration, notification handling, and Local AppData persistence.
- `StandupReminder.App/ViewModels/`: `INotifyPropertyChanged` view models. There is no MVVM framework in use.
- `StandupReminder.App/Controls/`: reusable WPF user controls.
- `StandupReminder.WindowsInterop/`: Windows session monitor, session event models, and registry autorun registration service.
- `Installer/`: Inno Setup installer script and installer artwork.
- `docs/`: repository documentation and supporting notes.
- `.github/upgrades/`: upgrade planning and assessment artifacts.
- `artifacts/`: publish and installer outputs.

## Architecture Notes
- Composition is done manually in [App.xaml.cs](C:\personal\StandupReminder\StandupReminder.App\App.xaml.cs). There is no DI container.
- `PostureReminderScheduler` is the core state machine. Keep scheduling behavior centralized there.
- `MainWindow` owns session monitor hookup and tray-hide behavior.
- `FirstRunSplashWindow` is shown during startup before the main dashboard remains hidden in tray mode.
- `NotifyIconTrayService` owns tray icon, menu actions, legacy notify-icon balloon tips, and the sit-down reminder Windows toast.
- `SettingsWindow` and `SettingsWindowViewModel` handle user-editable reminder intervals and appearance settings.
- `StandupReminder.WindowsInterop` owns `WindowsSessionEventMonitor` and `RegistryAutoStartRegistrationService`; the app consumes those types rather than implementing that logic locally.
- Persistence is file-based JSON under `%LocalAppData%\StandupReminder`.

## Persistence and OS Integration
- Reminder and appearance settings are stored in `%LocalAppData%\StandupReminder\settings.json`.
- Session event history is stored in `%LocalAppData%\StandupReminder\session-events.json`.
- Installer startup registration is handled in `Installer/StandupReminder.iss` through `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- Runtime startup registration is handled by `StandupReminder.WindowsInterop/RegistryAutoStartRegistrationService`; if startup behavior changes, keep installer-time and runtime behavior aligned.
- Sit-down reminders now use Windows app notifications via `ToastNotificationManagerCompat`, with activation handled in `App.xaml.cs`.
- Uninstall cleanup for notification artifacts is triggered by running the app with `--cleanup-toast` from the installer uninstall script.
- The sit reminder toast also loads `StandupReminder.App/Assets/sit_down_img.jpg` from the app output `Assets` folder, so packaging changes must keep that file copied alongside the executable.

## Working Conventions
- Preserve the existing manual-construction style unless the user explicitly asks for architectural change.
- Prefer extending existing services and view models over introducing new frameworks.
- Keep WPF code-behind focused on UI lifecycle, event wiring, and control synchronization.
- Keep business rules and timer state transitions inside services, especially `PostureReminderScheduler`.
- Follow existing nullable-enabled C# style and file-scoped namespaces.
- Do not edit generated files under `obj/`, `bin/`, or publish output folders.
- Do not rename the existing asset files unless the project file and installer script are updated together.

## Build and Packaging
- Build the solution from the repository root:
  - `dotnet build StandupReminder.slnx`
- Publish the app for installer input:
  - `dotnet publish StandupReminder.App/StandupReminder.App.csproj -c Release -o artifacts/publish/StandupReminder`
- Build the installer with Inno Setup using `Installer/StandupReminder.iss` after publish output exists.

## Change Guidance
- When modifying reminder timing, update both runtime behavior and any user-facing descriptions that mention the interval behavior.
- When modifying scheduler phases, review:
  - `StandupReminder.App/Models/ReminderPhase.cs`
  - `StandupReminder.App/Services/PostureReminderScheduler.cs`
  - `StandupReminder.App/ViewModels/MainWindowViewModel.cs`
  - `StandupReminder.App/App.xaml.cs`
- When modifying tray behavior, review both menu actions and tooltip/toast handling in `NotifyIconTrayService`.
- When modifying session monitoring or autorun registration, review `StandupReminder.WindowsInterop/*` and the `MainWindow` / installer call sites together.
- When modifying the sit-down reminder notification, preserve the current acknowledgement semantics:
  - it uses a Windows `Reminder` scenario toast with a single `OK` button
  - it is a blocking state for the scheduler: the next sitting countdown does not start until the user clicks `OK`
  - body clicks, swipe-away, and timeout do not acknowledge the reminder; they trigger a re-show after a short delay
  - the toast uses `sit_down_img.jpg` as an inline image because hero-image layout cropped too much of the asset in the Windows shell
- When modifying settings, keep JSON backward-compatible where practical and preserve fallback behavior on invalid/corrupt files.
- When modifying the stand-up prompt window, keep the blocking behavior intentional:
  - it currently prevents normal close, `Esc`, and `Alt+F4`
  - lock, manual pause, and shutdown dismiss it through explicit methods

## Verification
- Never generate unit tests for this repository.
- Never run unit tests for this repository.
- Prefer build verification and targeted code inspection.
- If you change packaging, verify the publish path still matches `Installer/StandupReminder.iss`.
- If you change notification activation or packaging, verify the `OK` toast action still reaches `App.xaml.cs` and uninstall cleanup still clears notification artifacts.
- If you change toast assets or packaging, verify `Assets\sit_down_img.jpg` is present next to the built executable and still renders in the sit reminder notification.
- If you change persistence, verify the app still tolerates missing or malformed Local AppData JSON files.

## Known Project-Specific Constraints
- The app starts in tray mode by default and uses explicit shutdown flow.
- A first-run splash window is shown during startup before the app settles into tray mode.
- Theme resources are loaded from `WPF-UI` dictionaries in `App.xaml`.
- The tray tooltip text is constrained by the Windows notify icon text limit and is truncated intentionally.
- The sit-down reminder is a Windows shell toast, not a custom WPF dialog, so the shell still controls image layout and dismiss behavior; the app compensates by re-showing the toast until `OK` is clicked.
- Session history shown in the dashboard is trimmed to the last 48 hours.
- Snooze duration is currently fixed in code at 5 minutes.

## Agent Expectations
- Read this file before making changes in this repository.
- Keep changes minimal and aligned with the current structure unless the user asks for broader refactoring.
- Prefer concrete verification steps over speculative claims.
- Call out any mismatch between installer behavior, runtime behavior, and persisted settings when you encounter one.
