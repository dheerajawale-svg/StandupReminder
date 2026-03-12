# ADR 0001: WinUI Deployment Channel

- Status: Accepted
- Date: 2026-03-12

## Context

`StandupReminder.WinUI` already has a packaged Windows App SDK setup:

- `Package.appxmanifest` is present
- `EnableMsixTooling` is enabled in the WinUI project
- the WinUI publish profile targets signed x64 MSIX sideload output

The repository also still contains an Inno Setup installer that represents the legacy distribution path used during the WPF era.

The open deployment question was whether to keep investing in the existing Inno-based installer path for the migrated WinUI app or to standardize on the packaged WinUI/MSIX foundation that is already in the repo.

## Decision

Adopt **signed x64 MSIX sideload packages** as the canonical deployment channel for `StandupReminder.WinUI`.

Specific decisions:

- `StandupReminder.WinUI` remains a **packaged** WinUI 3 app.
- The primary release artifact is a **signed x64 MSIX** produced from the existing WinUI publish configuration.
- The current release/update flow is **manual MSIX distribution** rather than App Installer auto-update.
- The existing Inno Setup path is **transitional only** and should not receive new investment for the WinUI app.
- Inno may remain in the repo only as a legacy artifact while WPF retirement is being completed.

## Rationale

This is the most practical choice for the current repository state.

- The WinUI project already has package identity and MSIX tooling configured.
- The publish profile already targets signed x64 sideload packaging.
- The current Inno script is a legacy installer path and is not the native deployment model for the new WinUI package-identity-based app.
- Standardizing on one deployment model reduces migration ambiguity and avoids maintaining two release stories for the same product.
- Deferring App Installer auto-update keeps the decision small and compatible with the current repo, while leaving room to add update automation later.

## Consequences

### Positive

- The deployment model for WinUI is now explicit.
- Package identity remains available for the app.
- Release validation can focus on one real install path instead of splitting effort across Inno and MSIX.

### Negative

- Users install via sideloaded MSIX for now, not a custom EXE installer flow.
- Automatic updates are not part of the current release path.

### Follow-up Work

1. Smoke-test install, launch, tray behavior, lock/unlock handling, settings persistence, and uninstall using the packaged x64 MSIX output.
2. Remove or archive the Inno path once WPF fallback retirement is complete.
3. Revisit App Installer only if the project needs first-party update distribution without changing the packaged deployment model.

## Notes for This Repo

- Canonical WinUI package config: `src/StandupReminder.WinUI/Package.appxmanifest`
- Canonical WinUI publish profile: `src/StandupReminder.WinUI/Properties/PublishProfiles/win10-x64.pubxml`
- Transitional legacy installer artifact: `Installer/StandupReminder.iss`
