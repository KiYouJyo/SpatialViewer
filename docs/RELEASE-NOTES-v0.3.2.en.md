# SpatialViewer v0.3.2

[简体中文](RELEASE-NOTES-v0.3.2.md) | [日本語](RELEASE-NOTES-v0.3.2.ja.md) | English

## In-app update

v0.3.2 moves the SpatialViewer application update path to the same verified MSIX workflow used by UrbanPlanToolbox. It no longer stops at checking a release and opening a browser.

- The application-update state on the About page is shared for the process, so navigating away and back does not reset an active or completed check/download state.
- GitHub Releases are checked and only a stable release newer than the installed version is considered an update.
- A release must contain exactly one `SpatialViewer_<version>.0_x64.msixbundle` together with `SHA256SUMS.txt`.
- After download, the updater verifies the GitHub asset SHA-256, the `SHA256SUMS.txt` entry, the WinTrust/MSIX signature, and the publisher certificate Subject and Thumbprint.
- The trusted publisher is fixed to `CN=AppPublisher` with certificate Thumbprint `BD85AD77A651C86CA01A480C8E9BC64952993F98`.
- Once verification succeeds, the update enters a ready-to-install state. After explicit user confirmation, Windows `PackageManager` deploys the new MSIXBundle with `ForceApplicationShutdown`, with application restart registration used for post-update recovery.
- Download, verification, installation, failure, and retry states remain inside the existing Update Management section; no separate launcher is introduced.

## Version display fix

- Display version, internal version, and current version are no longer hard-coded in XAML.
- Version display is centralized through `AppVersionProvider`; packaged builds prefer `Package.Current.Id.Version`.
- v0.3.2 uses product version `0.3.2` and MSIX package version `0.3.2.0`.

## Unchanged areas

v0.3.2 does not modify the title bar, hamburger menu, navigation structure, Projects/Favorites pages, light/dark theme design, or the independent CadCore update mechanism.
