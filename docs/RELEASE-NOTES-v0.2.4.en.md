# Spatial Viewer v0.2.4

This release fixes the remaining v0.2.3 issue where a downloaded Cad Core could be staged and the app restarted, yet the restarted process still bound to the bundled kernel.

## Fixes

- Moves Cad Core activation from the `App()` constructor to an assembly `ModuleInitializer`, so the selected external kernel is loaded before WinUI/XAML generated startup code or `MainWindow` can bind any static Cad Core reference.
- Keeps the bundled Cad Core as a safe fallback. Only an external package that passes manifest, architecture, compatibility, and assembly-version validation can be activated.
- Keeps the Windows App SDK `AppInstance.Restart` flow, while ensuring the restarted process consumes `pending.json` and binds the new Cad Core before WinUI initialization.
- Reworks the acceptance probe so it has the same five compile-time Cad Core project references as the real app and actually constructs `ACadSharpCadImporter` after module initialization. This validates real static type binding, not just successful DLL loading.
- Verifies that all five Cad Core assemblies have the expected version and are loaded from the staged version directory rather than the bundled MSIX directory.

## Versions

- SpatialViewer: 0.2.4
- MSIX: 0.2.4.0
- Bundled Cad Core: retained as the fallback baseline
- Online Cad Core: independently updated from the latest compatible `SpatialViewer.CadCore` Release
