# SpatialViewer v0.3.7 Startup and CadCore v0.12.3 Integration

SpatialViewer v0.3.7 keeps the current two-stage startup design used by UrbanPlanToolbox and PageArc and also corrects a critical acceptance gap: the Viewer builds being tested were not actually executing the CadCore v0.12.3 real-drawing fixes.

## Startup

- Keeps MSIX `uap:SplashScreen` as the Stage 1 cold-start bootstrap with dedicated 100%, 125%, 150%, 200%, and 400% DPI resources and `uap5:Optional="true"`.
- Uses a transparent `StartupOverlay` inside the real WinUI main window for Stage 2 so the window's own `MicaBackdrop` is visible immediately.
- Builds the real shell behind the startup logo and keeps it non-interactive until startup presentation completes.
- Starts the minimum timer after the logo actually renders, keeps a complete logo visible for about 500 ms on fast launches, then fades the overlay over about 200 ms with EaseOut timing.
- Retains the 1-second logo fallback and 5-second fail-open startup watchdog.

## CadCore delivery correction

Passing CadCore v0.12.3 unit tests did not prove the Viewer defects were fixed because the product was still running older kernels:

- the published SpatialViewer v0.3.6 package bundled CadCore v0.12.2;
- the v0.3.7 source project still declared `CadCoreBundledVersion=0.9.0`;
- its CadCore submodule also still pointed to v0.9.0.

v0.3.7 now pins both the source submodule and bundled source-build version to **CadCore v0.12.3 / commit `2f150fbdcf380fba6f60df7f8a41361322afdd8f`**. Acceptance adds two hard gates:

1. source builds fail unless the declared version and gitlink are exactly v0.12.3;
2. the final MSIXBundle is unpacked and all five assemblies under `Kernels/Bundled/0.12.3` must match the published CadCore v0.12.3 release payload byte-for-byte by SHA-256.

This prevents a kernel-only change from being reported as a Viewer fix while the application still runs an older kernel.

## Real-drawing acceptance boundary

The v0.12.3 changes for the long perpendicular lines, dimension text anchoring/colors/architectural ticks, and legacy CJK SHX fallback will now actually ship inside v0.3.7. Without the original failing DWG, however, they remain **candidate fixes** rather than completed visual fixes.

Final acceptance must compare the original drawing in AutoCAD and SpatialViewer. The three defects are only considered fixed after those real-drawing differences disappear.

## Preserved

- The accepted title bar, hamburger menu, NavigationView, page surfaces, tabs, and existing interaction layout are not redesigned by this integration.
- No second standalone WinUI window, text, controls, or fake progress indicator is introduced.
