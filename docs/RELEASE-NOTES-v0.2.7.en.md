[简体中文](RELEASE-NOTES-v0.2.7.md) | [日本語](RELEASE-NOTES-v0.2.7.ja.md) | English

# Spatial Viewer v0.2.7

v0.2.7 fixes the content-surface background regression across light and dark themes and formalizes the kernel-bundling policy for official releases. The title bar, tab strip, hamburger menu, navigation pane, and established layout are outside the scope of this visual fix.

## Light and dark theme fix

- Removed the content-background override that could retain a light-theme Brush after switching to dark mode, returning content-surface management to the native WinUI `NavigationView` theme resources.
- Corrected the page surface in both normal and interaction states under light mode while eliminating the translucent white veil that covered the page in dark mode.
- Theme switching continues to use dynamic theme resources so light, dark, and system-following modes no longer retain the previous theme's page background.

## Release kernel policy

- Starting with this release, every official SpatialViewer Release resolves and embeds the latest stable version of each kernel whose application-side feature integration is marked complete. Kernels whose integration is not complete are not bundled.
- CAD is currently the only completed kernel integration, so v0.2.7 bundles only the latest stable `SpatialViewer.CadCore v0.4.0`.
- The release pipeline validates the CadCore source repository, x64 runtime, `SpatialViewer 0.2.x` compatibility range, ABI `1.0.0.0`, published digest, and five core assemblies before placing the verified official Release binaries under `Kernels/Bundled/0.4.0/`.
- The bundled kernel files are compared by SHA-256 against the binaries downloaded from the online CadCore Release, preventing an older submodule build or an incorrect kernel version from entering the official package.

## Acceptance

- Trilingual resources, Debug / Release x64 builds, Release tests, and the DebugHost startup smoke test all pass.
- CadCore online update discovery, ABI validation, download / staging, and restart activation are covered by acceptance checks.
- The release pipeline re-validates the 0.2.7.0 MSIXBundle kernel-isolation layout, latest CadCore injection, digital signature, and one-click installer package.

> Application version: `0.2.7`. MSIX package version: `0.2.7.0`. The official package bundles `SpatialViewer.CadCore v0.4.0` with ABI `1.0.0.0`.