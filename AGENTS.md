# AGENTS.md

## Scope
This file applies to the entire repository at `C:\personal\StandupReminder`.

## Project Summary
- This repository contains a single Windows desktop application: `StandupReminder.App`.
- The app is a `net8.0-windows` WPF application with `UseWPF=true` and `UseWindowsForms=true`.
- `WPF-UI` is used for window chrome, theming, and controls.
- The app runs primarily from the system tray, tracks Windows session events, alternates sitting and standing intervals, and persists settings under Local AppData.

## Repository Layout
- `StandupReminder.slnx`: solution entrypoint with one project.
- `StandupReminder.App/`: main WPF application.
- `StandupReminder.App/Models/`: plain settings/state models and enums.
- `StandupReminder.App/Services/`: scheduler, tray integration, settings persistence, autorun registration, and session monitoring.
- `StandupReminder.App/ViewModels/`: `INotifyPropertyChanged` view models. There is no MVVM framework in use.
- `StandupReminder.App/Controls/`: reusable WPF user controls.
- `Installer/`: Inno Setup installer script and installer artwork.
- `artifacts/`: publish and installer outputs.

## Architecture Notes
- Composition is done manually in [App.xaml.cs](C:\personal\StandupReminder\StandupReminder.App\App.xaml.cs). There is no DI container.
- `PostureReminderScheduler` is the core state machine. Keep scheduling behavior centralized there.
- `MainWindow` owns session monitor hookup and tray-hide behavior.
- `NotifyIconTrayService` owns tray icon, menu actions, and balloon notifications.
- `SettingsWindow` and `SettingsWindowViewModel` handle user-editable reminder intervals and appearance settings.
- Persistence is file-based JSON under `%LocalAppData%\StandupReminder`.

## Persistence and OS Integration
- Reminder and appearance settings are stored in `%LocalAppData%\StandupReminder\settings.json`.
- Session event history is stored in `%LocalAppData%\StandupReminder\session-events.json`.
- Installer startup registration is handled in `Installer/StandupReminder.iss` through `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- There is also a `RegistryAutoStartRegistrationService`; if startup behavior changes, keep installer-time and runtime behavior aligned.

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
- When modifying tray behavior, review both menu actions and tooltip/balloon handling in `NotifyIconTrayService`.
- When modifying settings, keep JSON backward-compatible where practical and preserve fallback behavior on invalid/corrupt files.
- When modifying the stand-up prompt window, keep the blocking behavior intentional:
  - it currently prevents normal close, `Esc`, and `Alt+F4`
  - lock, manual pause, and shutdown dismiss it through explicit methods

## Verification
- Never generate unit tests for this repository.
- Never run unit tests for this repository.
- Prefer build verification and targeted code inspection.
- If you change packaging, verify the publish path still matches `Installer/StandupReminder.iss`.
- If you change persistence, verify the app still tolerates missing or malformed Local AppData JSON files.

## Known Project-Specific Constraints
- The app starts in tray mode by default and uses explicit shutdown flow.
- Theme resources are loaded from `WPF-UI` dictionaries in `App.xaml`.
- The tray tooltip text is constrained by the Windows notify icon text limit and is truncated intentionally.
- Session history shown in the dashboard is trimmed to the last 48 hours.
- Snooze duration is currently fixed in code at 5 minutes.

## Agent Expectations
- Read this file before making changes in this repository.
- Keep changes minimal and aligned with the current structure unless the user asks for broader refactoring.
- Prefer concrete verification steps over speculative claims.
- Call out any mismatch between installer behavior, runtime behavior, and persisted settings when you encounter one.
