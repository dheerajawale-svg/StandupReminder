## Action Plan for Remaining WinUI 3 Risks and Open Questions

Below is a prioritized, code-aware plan focused only on items that are still relevant to `StandupReminder.WinUI` production readiness.

## Medium Priority

### 1. Session Event Handling Is Not Yet Full Parity with the Legacy App

- **Risk/Question Summary**  
  WinUI currently uses `SystemEvents.SessionSwitch`, while the legacy WPF app used `WM_WTSSESSION_CHANGE` via `WTSRegisterSessionNotification`. That means WinUI may not yet cover the same set of session transitions.

- **Current Status**  
  **Resolved for current scope**

  Verified current state:
  - WinUI: `SystemEventsReminderSessionEventSource` maps only `Logon`, `Lock`, and `Unlock`
  - WPF: `WindowsSessionEventMonitor` receives raw `WM_WTSSESSION_CHANGE` notifications
  - Microsoft documents additional session events such as console/remote connect/disconnect and desktop-ready
  - Product scope for the WinUI app is now explicitly limited to Windows 11 `Logon`, `Lock`, and `Unlock`

- **Recommended Action**  
  Keep the current adapter and treat richer WTS coverage as out of scope unless the product requirements change.

  **Decision rule**
  - Current requirement: the app only needs `Logon`, `Lock`, and `Unlock` on Windows 11, so `SystemEvents.SessionSwitch` is sufficient.
  - Only if parity with remote desktop / desktop reconnect / console transitions becomes a product requirement should the app replace or supplement the current source with a **WTS-backed Windows adapter**.

  **Recommended implementation direction**
  - Keep raw Win32 isolated in `StandupReminder.Windows`
  - Keep `IReminderSessionEventSource` clean and platform-neutral
  - Keep the core enum small because the app intentionally handles only `Logon`, `Lock`, and `Unlock`
  - Revisit a `WtsReminderSessionEventSource` only if a future requirement adds remote/console session scenarios

  This keeps WinUI modern while preserving clean architecture.

- **Priority**  
  **Low for current scope**  
  Raise to **High** only if remote desktop / fast-user-switching parity becomes a required production scenario.

- **Affected Components**
  - `src/StandupReminder.Windows/SystemEventsReminderSessionEventSource.cs`
  - `src/StandupReminder.WinUI/AppBootstrapper.cs`
  - `src/StandupReminder.Core/Services/ReminderSessionEvent.cs`
  - `src/StandupReminder.Core/Services/IReminderSessionEventSource.cs`
  - possibly a new Windows session adapter file in `src/StandupReminder.Windows`

- **Actionable Next Steps**
  1. Document in code that the Windows 11 app intentionally handles only `Logon`, `Lock`, and `Unlock`.
  2. Leave the current `SystemEventsReminderSessionEventSource` in place.
  3. Reopen the WTS-adapter work only if a future requirement adds remote/console session coverage.

### 2. Threading / Marshalling Is Mostly Good, but the Session Boundary Should Be Hardened

- **Risk/Question Summary**  
  Prompt and settings UI marshalling is already mostly implemented, but session events can still reach runtime code from non-UI threads. That is safe only if all touched runtime paths are truly thread-agnostic.

- **Current Status**  
  **Completed for current scope**

  Verified current state:
  - `StandUpReminderPromptHost` self-marshals to the UI thread
  - `App.xaml.cs` marshals tray-driven UI actions with `DispatcherQueue`
  - `ReminderRuntimeComposition` now queues session-event runtime mutations onto the WinUI dispatcher before calling `_runtime.HandleSessionEvent(...)`
  - `ReminderRuntimeComposition` also routes tray pause/resume mutations through the same dispatcher boundary
  - Runtime mutation ownership is documented in composition code so session and tray callbacks share one entry path

- **Recommended Action**  
  Keep the current single-thread-affine model for runtime mutations.

  **Recommended hardening**
  1. `ReminderSchedulerRuntime` is now treated as single-thread-affine from the UI/composition thread.
  2. Session events and tray pause/resume mutations are marshalled onto the same dispatcher/composition thread before touching runtime state.
  3. The composition code documents that runtime mutations enter through one known thread boundary.
  4. Avoid blocking UI-thread waits where possible in future refactors; current prompt host is acceptable but should remain carefully scoped.

- **Priority**  
  **Completed**

- **Affected Components**
  - `src/StandupReminder.WinUI/ReminderRuntimeComposition.cs`
  - `src/StandupReminder.WinUI/StandUpReminderPromptHost.cs`
  - `src/StandupReminder.WinUI/App.xaml.cs`
  - `src/StandupReminder.Core/Services/ReminderSchedulerRuntime.cs`

- **Actionable Next Steps**
  1. Smoke-test lock/unlock while:
     - prompt is visible
     - settings window is open
     - tray pause/resume is toggled around session changes
  2. If any race symptoms appear, add small targeted tests around runtime state transitions.

## Low Priority / Transitional Only

### 3. CS0108 Warnings in the WPF Tray Service Are Not a WinUI Blocker

- **Risk/Question Summary**  
  The legacy WPF tray interface appears to hide inherited Core members, causing benign CS0108 warnings. The question is whether that should be addressed as part of WinUI migration.

- **Current Status**  
  **Not a current WinUI issue**

  Verified current state:
  - Warning source is in the WPF-only path
  - WinUI uses `IReminderTrayHost` directly through `StandupReminder.Windows`
  - No evidence that the warning blocks WinUI parity or WinUI deployment

- **Recommended Action**  
  Do **not** treat this as active WinUI migration work.

  Only address it if one of these becomes true:
  - the team wants a warning-clean solution before deleting WPF
  - WPF remains longer than expected and warning noise is affecting maintenance
  - CI starts treating warnings as errors for legacy projects

- **Priority**  
  **Low**

- **Affected Components**
  - `src/StandupReminder.App/Services/ITrayService.cs`
  - `src/StandupReminder.App/Services/NotifyIconTrayService.cs`

- **Actionable Next Steps**
  1. Leave unchanged for now.
  2. If desired later, remove member hiding by aligning the WPF interface with `IReminderTrayHost` or deleting the redundant legacy abstraction when WPF is retired.

## Recommended Order of Execution

### Phase 1
1. ~~Finalize the **deployment ADR** on top of the new WinUI MSIX foundation~~ **Done** - standardized on signed x64 MSIX sideload packages for WinUI
2. Build a **WinUI smoke-test checklist**
3. Run manual validation on the current WinUI shell, including the packaged MSIX path

### Phase 2
4. ~~Decide whether current `SystemEvents.SessionSwitch` coverage is enough~~ **Done** for current Windows 11 `Logon` / `Lock` / `Unlock` scope
5. If scope expands, add a **WTS-based session adapter**
6. ~~Normalize runtime/session event marshalling onto one thread boundary~~ **Done**

### Phase 3
7. ~~Add signing for WinUI MSIX~~ **Done** - signed x64 MSIX sideload is the chosen WinUI installer/update channel
8. Re-run smoke validation on the real install/update path
9. Retire WPF only after the validation gate passes

## Bottom Line

The two real blockers to WPF retirement are:

- **desktop smoke validation is still incomplete**
- **the packaged x64 MSIX install/uninstall path still needs full manual validation**

The two meaningful engineering hardening items after that are:

- **revisit WTS-level coverage only if session scope expands beyond `Logon` / `Lock` / `Unlock`**
- **complete smoke validation around the now-explicit runtime thread boundary**

The CS0108 item is real, but it is **legacy-only and not a WinUI readiness blocker**.

If you want, I can turn this into a repo-ready artifact next:
- a concise `docs/winui-risk-action-plan.md`, or
- a checkbox-based validation matrix for WinUI parity.
