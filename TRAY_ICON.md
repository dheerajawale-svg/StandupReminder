# Session Context Summary

## User Goals & Intent
- Modernize the tray icon and tray menu for the WinUI app only.
- Keep the WPF fallback on the existing WinForms tray implementation.
- Upgrade the solution from `.NET 8` to `.NET 10`.
- Preserve current tray behaviors: left click opens the app, right click opens a command surface, pause/resume label stays in sync, settings remains single-instance, exit shuts down cleanly, tooltip/status updates continue, and scheduler notifications still work.
- Do not add unit tests.

## Key Technical Context
- Repository: `C:\personal\StandupReminder`
- Primary target: `src/StandupReminder.WinUI`
- Shared tray abstraction remains `IReminderTrayHost` in `src/StandupReminder.Core\Services\IReminderTrayHost.cs`.
- Legacy tray implementation remains `NotifyIconReminderTrayHost` in `src/StandupReminder.Windows\NotifyIconReminderTrayHost.cs`.
- WinUI runtime composition continues to depend on `IReminderTrayHost` in `src/StandupReminder.WinUI\ReminderRuntimeComposition.cs`.
- The WinUI app originally used the shared WinForms tray host through `AppBootstrapper`.
- The WinUI project now uses `H.NotifyIcon.WinUI` for the tray icon shell integration, but not for the final menu surface.

## Environment & Configuration Details
- Current workspace date during session: `2026-03-12`
- SDK pin added at repo root: `global.json`
  - SDK version: `10.0.104`
  - `rollForward`: `latestPatch`
- Project retargeting performed:
  - Core / Persistence / tests: `net10.0`
  - Windows adapter / WPF fallback: `net10.0-windows`
  - WinUI app: `net10.0-windows10.0.22621.0`
- WinUI package changes:
  - Kept `Microsoft.WindowsAppSDK` at `1.8.260209005`
  - Added `H.NotifyIcon.WinUI` `2.4.1`
- WinUI build architecture settings now explicitly include:
  - `PlatformTarget`: `x64`
  - `RuntimeIdentifier`: `win-x64`
- Solution configuration fix in `StandupReminder.slnx`:
  - Added explicit mapping from `Any CPU` solution configuration to `x64` project configuration for `StandupReminder.WinUI.csproj`

## Discussion Highlights
- Research/findings:
  - WinUI 3 / Windows App SDK still does not provide a first-party tray icon + tray context menu API.
  - WinUI has built-in in-window menu surfaces like `MenuFlyout`, `MenuBar`, and `CommandBarFlyout`, but those are not tray APIs.
  - `H.NotifyIcon.WinUI` was selected as the WinUI-only tray integration library after the move to `.NET 10`.
- First implementation attempt:
  - Used `H.NotifyIcon` `TaskbarIcon` with `ContextFlyout` and `ContextMenuMode="SecondWindow"` in `src/StandupReminder.WinUI\TrayIconResources.xaml`.
  - Result:
    - Tray icon was missing.
    - Menu placement was wrong near the Windows 11 tray overflow area.
    - The menu appeared bottom-right and partially off-screen.
- Second implementation attempt:
  - Switched `ContextMenuMode` to `PopupMenu`.
  - Result:
    - Placement improved, but the menu regressed to the old shell-style context menu.
    - This was not acceptable because it lost the modern WinUI look.
- Third implementation attempt:
  - Switched to `TaskbarIcon.TrayPopup`.
  - Result:
    - The app crashed when opening the tray menu.
    - The menu was not reliably visible before the crash.
    - Conclusion: the library popup/context-menu surfaces were not stable enough in this tray-first hidden-window setup.
- Final implementation:
  - Keep `H.NotifyIcon` only for:
    - tray icon creation
    - left click command
    - right click command
    - tooltip/status updates
    - balloon/persistent notifications
  - Replace library-rendered tray menus with an owned WinUI window:
    - `src/StandupReminder.WinUI\TrayMenuWindow.xaml`
    - `src/StandupReminder.WinUI\TrayMenuWindow.xaml.cs`
  - The tray icon now raises a custom WinUI menu window on right click, positioned from the cursor and clamped to the current display work area.
  - The tray icon image is now loaded from the built executable via `Icon.ExtractAssociatedIcon(Environment.ProcessPath)` in `src/StandupReminder.WinUI\HNotifyIconReminderTrayHost.cs`.

## Issues, Assumptions & Open Questions
- Known runtime issue before tray work:
  - Launch verification for the WinUI app previously failed on this machine with:
    - `System.Runtime.InteropServices.COMException (0x80040154): Class not registered`
    - failure path: `Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent()` in `src/StandupReminder.WinUI\Program.cs`
  - That issue blocks full launch-based verification in this environment and may be machine/runtime registration related rather than tray-code related.
- Current assumption:
  - The owned WinUI menu window is safer than using `H.NotifyIcon` `SecondWindow`, `PopupMenu`, or `TrayPopup` for the menu surface.
- Open validation items that still need live desktop confirmation:
  - Right click opens `TrayMenuWindow` without crashing.
  - Menu appears fully visible near the tray icon, including in the Windows 11 overflow flyout.
  - Menu position behaves acceptably on multi-monitor setups.
  - Menu hides correctly on deactivation.
  - Tray icon remains visible after the icon-loading change.
- If the menu still lands slightly off:
  - Tune the cursor-to-window positioning logic in `TrayMenuWindow.ShowAt`.
  - Do not revert to `PopupMenu`, because that returns the old shell context menu.
- Pre-existing warnings still present:
  - WPF fallback `ITrayService` still emits `CS0108` member hiding warnings.

## References & Contextual Notes
- Main files changed for tray work:
  - `C:\personal\StandupReminder\src\StandupReminder.WinUI\AppBootstrapper.cs`
  - `C:\personal\StandupReminder\src\StandupReminder.WinUI\HNotifyIconReminderTrayHost.cs`
  - `C:\personal\StandupReminder\src\StandupReminder.WinUI\TrayIconResources.xaml`
  - `C:\personal\StandupReminder\src\StandupReminder.WinUI\TrayMenuWindow.xaml`
  - `C:\personal\StandupReminder\src\StandupReminder.WinUI\TrayMenuWindow.xaml.cs`
  - `C:\personal\StandupReminder\src\StandupReminder.WinUI\StandupReminder.WinUI.csproj`
  - `C:\personal\StandupReminder\StandupReminder.slnx`
  - `C:\personal\StandupReminder\global.json`
- Important commands run during the session:
  - `dotnet build src/StandupReminder.WinUI/StandupReminder.WinUI.csproj`
  - `dotnet build src/StandupReminder.App/StandupReminder.App.csproj`
  - `dotnet build StandupReminder.slnx`
  - `dotnet test tests/StandupReminder.Core.Tests/StandupReminder.Core.Tests.csproj`
- Verification status at the point this handoff was written:
  - WinUI build: passing
  - WPF fallback build: passing, with existing `CS0108` warnings
  - Core/Persistence tests: `18` passing
  - Live tray UI verification: still pending because the session hit environment/runtime launch issues and repeated tray-surface regressions
