[简体中文](RELEASE-NOTES-v0.2.3.md) | [日本語](RELEASE-NOTES-v0.2.3.ja.md) | English

# Spatial Viewer v0.2.3

This update fixes the final interaction after a CadCore update has finished downloading.

## Restart-to-update for CadCore

- The CadCore action button no longer becomes disabled after download and verification complete.
- The action changes to “Restart to update” and uses Windows App SDK `AppInstance.Restart` to relaunch SpatialViewer.
- The restarted process reads the staged CadCore before XAML initialization and activates the downloaded kernel immediately. No in-process hot swap is performed.
- If Windows cannot restart the app, the updater remains retryable and retains the concrete restart failure reason for diagnostics.

## Update-management labels

- The kernel-name column is shortened from `SpatialViewer.CadCore` to `CadCore`.
- The update-source column changes from `GitHub Releases` to `SpatialViewer.CadCore`, clearly identifying the independent CadCore repository as the source.

## Acceptance

The signed v0.2.3 acceptance package is generated only after trilingual resources, the restart-to-update UI contract, Debug/Release builds, tests, startup smoke, CadCore Release checks, live download and staging, MSIX generation, and Authenticode signing all pass.
