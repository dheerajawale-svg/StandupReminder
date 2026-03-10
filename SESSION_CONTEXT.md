# Session Context Summary

## User Goals & Intent
- Build a .NET WPF Windows 11 desktop app in `C:\personal\StandupReminder` that detects Windows session return events and runs as a tray-based stand-up reminder.
- Keep the app focused on lock/unlock/logon detection and a tray-first reminder loop rather than broader power-event handling.
- Modernize the UI with `WPF-UI`, theme sync, branded app icons, and installer support.
- Add user-configurable reminder intervals through a settings window and persist them across restarts.
- Preserve Windows session event history across app restarts while limiting the visible history to the last 48 hours.

## Key Technical Context
- Session detection uses `WM_WTSSESSION_CHANGE` with `WTSRegisterSessionNotification`.
- The app currently reacts to:
- `WTS_SESSION_LOCK (0x7)`
- `WTS_SESSION_UNLOCK (0x8)`
- `WTS_SESSION_LOGON (0x5)`
- The reminder loop behavior is:
- initial sitting countdown
- blocking stand-up confirmation window
- standing countdown
- informational sit notification
- pause/resume across lock/unlock
- `MainWindow` is a `Wpf.Ui.Controls.FluentWindow` and uses `SystemThemeWatcher.Watch(this)` for runtime theme tracking.
- `StandUpReminderWindow` is also a `FluentWindow`; a crash caused by `WindowBackdropType="Acrylic"` without `ExtendsContentIntoTitleBar="True"` was fixed by enabling title-bar extension.
- App branding now uses `StandupReminder.App\Assets\reminder_17382582.ico` and `StandupReminder.App\Assets\reminder_17382582.png`.
- The executable icon is embedded through `ApplicationIcon`, the windows use the icon in XAML, the tray icon loads the executable’s associated icon, and the Inno Setup installer uses the same `.ico` as `SetupIconFile`.
- Reminder intervals are configurable through a new WPF-UI `SettingsWindow` opened from the tray icon context menu.
- Settings persistence is per-user in `%LocalAppData%\StandupReminder\settings.json` via `LocalAppDataReminderSettingsStore`.
- Persisted settings are stored as integer minute values, not raw `TimeSpan` JSON.
- Saved reminder settings apply on the next interval boundary; they do not reset the currently active countdown.
- `IPostureReminderScheduler` now supports runtime configuration updates through `UpdateOptions(ReminderScheduleOptions options)`.
- Current reminder defaults in code are:
- `InitialSit = 2 minutes`
- `RecurringSit = 2 minutes`
- `Stand = 2 minutes`
- The main window no longer shows a separate `Reminder Transitions` panel.
- Windows session events are now persisted per user in `%LocalAppData%\StandupReminder\session-events.json` via `LocalAppDataSessionEventLogStore`.
- The dashboard shows only the Windows session event feed, trimmed to the last 48 hours and preserved across app restarts.

## Environment & Configuration Details
- Workspace: `C:\personal\StandupReminder`
- OS/Shell context: Windows PowerShell
- Current session date context for this summary update: `2026-03-10`
- Earlier project setup session date context recorded in the repo: `2026-03-07`
- .NET SDK detected during prior setup: `10.0.103`
- Solution/project:
- `C:\personal\StandupReminder\StandupReminder.slnx`
- `C:\personal\StandupReminder\StandupReminder.App\StandupReminder.App.csproj`
- Project target framework: `net8.0-windows`
- NuGet package in use: `WPF-UI` version `4.2.0`
- Installer/publish artifacts:
- publish profile: `C:\personal\StandupReminder\StandupReminder.App\Properties\PublishProfiles\FolderProfile.pubxml`
- publish output: `C:\personal\StandupReminder\artifacts\publish\StandupReminder\`
- Inno Setup script: `C:\personal\StandupReminder\Installer\StandupReminder.iss`
- installer output: `C:\personal\StandupReminder\artifacts\installer\StandupReminder-Setup.exe`
- Installer characteristics:
- per-user install
- install path under `%LocalAppData%\Programs\StandupReminder`
- Start Menu shortcut
- optional desktop shortcut
- writes `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\StandupReminder`
- removes that value on uninstall

## Discussion Highlights
- Initial scaffold/build work required a few escalated `dotnet` operations because of sandbox restrictions.
- The earlier custom log-on UI was removed and the app was simplified to lock/unlock/logon session monitoring.
- The codebase was refactored toward MVVM:
- view state and event logging moved into `MainWindowViewModel`
- `MainWindow.xaml.cs` remained responsible for Win32 session message hookup and routing
- The UI was modernized with `WPF-UI`, system theme application at startup, and theme watching on windows.
- Installer support was added with deterministic self-contained publishing and an Inno Setup script.
- Runtime auto-start self-registration was removed so Windows Startup Apps remains the source of truth for startup enable/disable behavior.
- A reminder-window crash was reproduced and fixed:
- exception: `System.InvalidOperationException: Cannot apply backdrop effect if ExtendsContentIntoTitleBar is false.`
- fix: set `ExtendsContentIntoTitleBar="True"` on `StandUpReminderWindow`
- App icon/logo support was added using the assets under `StandupReminder.App\Assets`.
- A settings feature was added:
- tray context menu now includes `Settings`
- `SettingsWindow` is single-instance
- users can edit initial sit, recurring sit, and stand durations in whole minutes
- settings validation rejects zero/negative/non-integer minute values
- settings are saved to `%LocalAppData%\StandupReminder\settings.json`
- The Windows session events feed was upgraded from in-memory-only logging to persisted per-user history with 48-hour retention.
- The old transition-log panel and its scheduler log event contract were removed from the dashboard.
- `dotnet build StandupReminder.slnx` succeeded after the icon changes, settings implementation, and 48-hour session-history persistence work.

## Issues, Assumptions & Open Questions
- Resolved issue: legacy `Wpf.Ui`/`WPF.UI` package confusion. The active package remains `WPF-UI` `4.2.0`.
- Resolved issue: reminder prompt window backdrop/title-bar mismatch causing `InvalidOperationException`.
- Assumption: session monitoring remains scoped to the current session (`NOTIFY_FOR_THIS_SESSION = 0`), not all sessions.
- Assumption: startup enable/disable should continue to be controlled by Windows Startup Apps and installer-managed `HKCU\...\Run`.
- Assumption: reminder settings are intentionally per-user in `LocalAppData`, matching the tray app’s current user-scoped behavior.
- Assumption: settings changes should not reset an active countdown; they apply on the next applicable phase transition.
- Current known issue for future debugging:
- the installed build was previously reported to stop or crash around the reminder stage during a manual installed-app test
- the exact root cause in the installed/non-VS scenario remains unconfirmed after the later reminder-window fix
- Open question: whether the current `2 minute` defaults should remain a debug-only choice or be moved behind an explicit development configuration.
- Open question: whether to support all sessions (`NOTIFY_FOR_ALL_SESSIONS`) instead of current-session-only registration.

## References & Contextual Notes
- Files central to the current implementation:
- `C:\personal\StandupReminder\StandupReminder.App\App.xaml`
- `C:\personal\StandupReminder\StandupReminder.App\App.xaml.cs`
- `C:\personal\StandupReminder\StandupReminder.App\MainWindow.xaml`
- `C:\personal\StandupReminder\StandupReminder.App\MainWindow.xaml.cs`
- `C:\personal\StandupReminder\StandupReminder.App\ViewModels\MainWindowViewModel.cs`
- `C:\personal\StandupReminder\StandupReminder.App\SettingsWindow.xaml`
- `C:\personal\StandupReminder\StandupReminder.App\SettingsWindow.xaml.cs`
- `C:\personal\StandupReminder\StandupReminder.App\ViewModels\SettingsWindowViewModel.cs`
- `C:\personal\StandupReminder\StandupReminder.App\Services\PostureReminderScheduler.cs`
- `C:\personal\StandupReminder\StandupReminder.App\Services\LocalAppDataReminderSettingsStore.cs`
- `C:\personal\StandupReminder\StandupReminder.App\Services\LocalAppDataSessionEventLogStore.cs`
- `C:\personal\StandupReminder\StandupReminder.App\Models\ReminderScheduleOptions.cs`
- `C:\personal\StandupReminder\StandupReminder.App\Models\SessionEventLogEntry.cs`
- `C:\personal\StandupReminder\StandupReminder.App\Services\NotifyIconTrayService.cs`
- `C:\personal\StandupReminder\StandupReminder.App\StandUpReminderWindow.xaml`
- Packaging-related files:
- `C:\personal\StandupReminder\StandupReminder.App\Properties\PublishProfiles\FolderProfile.pubxml`
- `C:\personal\StandupReminder\Installer\StandupReminder.iss`
- `C:\personal\StandupReminder\.gitignore`
- Verification completed across sessions:
- `dotnet build StandupReminder.slnx` succeeded after major refactors
- `dotnet publish StandupReminder.App\StandupReminder.App.csproj -c Release -p:PublishProfile=FolderProfile` succeeded
- `C:\Program Files (x86)\Inno Setup 6\ISCC.exe C:\personal\StandupReminder\Installer\StandupReminder.iss` succeeded
- `dotnet build StandupReminder.slnx` also succeeded after the icon, settings, and persisted session-event-history changes
- Output artifact produced:
- `C:\personal\StandupReminder\artifacts\installer\StandupReminder-Setup.exe`
- External references used during the project:
- Microsoft Learn: `WM_WTSSESSION_CHANGE`, `WTSRegisterSessionNotification`, `WM_POWERBROADCAST`, `SystemEvents.SessionSwitch`, `SystemEvents.PowerModeChanged`, `Run and RunOnce Registry Keys`, and Windows startup-app guidance
- Context7: `/lepoco/wpfui` docs for themes, `FluentWindow`, `ApplicationThemeManager`, and `SystemThemeWatcher`
