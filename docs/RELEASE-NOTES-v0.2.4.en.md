[简体中文](RELEASE-NOTES-v0.2.4.md) | [日本語](RELEASE-NOTES-v0.2.4.ja.md) | English

# Spatial Viewer v0.2.4

This update fixes the startup-layer issue where v0.2.3 could download a newer Cad Core and restart successfully while the real application process still remained bound to the bundled kernel.

## Cad Core startup preload

- Cad Core activation is moved from the WinUI `App` constructor to a CLR `ModuleInitializer`.
- A staged newer kernel is loaded before `Microsoft.UI.Xaml.Application` construction, XAML type-system initialization, and the first product-code access to Cad Core types.
- The bundled Cad Core remains a reliable fallback; only updates that pass version, manifest, and assembly validation can become active.
- Restart-to-update continues to use Windows App SDK `AppInstance.Restart`, with the restarted process binding the selected kernel before WinUI startup.

## Acceptance upgraded for the real packaged-app failure

- A static-binding startup test now compiles the probe against an older Cad Core while staging the latest online release as pending.
- The test requires pending → active promotion before `Main`, then directly accesses the real `ACadSharpCadImporter` type and verifies that its assembly version is the online active version rather than the old compile-time reference.
- The product's real Cad Core gitlink is restored after the probe, so the final MSIX is never downgraded by the test fixture.

## Package version

- Spatial Viewer: 0.2.4
- MSIX: 0.2.4.0
- Architecture: x64
