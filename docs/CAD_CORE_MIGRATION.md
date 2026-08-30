# CAD kernel repository split

The CAD viewing kernel is developed in `KiYouJyo/SpatialViewer.CadCore`.

## Ownership

`SpatialViewer.CadCore` owns double-precision scene/camera primitives used by the CAD pipeline, CAD semantic model and translation, the ACadSharp DWG/DXF adapter, backend-neutral render preparation, Win2D backend, and kernel regression tests.

`SpatialViewer` owns the WinUI 3 application shell, tabs, command surfaces, panels, product presentation/localization, application integration tests, and packaging.

## Development dependency

During active kernel development, `SpatialViewer` pins `external/SpatialViewer.CadCore` as a Git submodule at an explicit commit. UI project references point directly into that checkout. A kernel update therefore appears in the product repository as a reviewable gitlink revision change.

After CadCore starts publishing stable packages, the UI may switch to versioned NuGet dependencies without changing public namespaces during the migration window.

## Update procedure

1. Make and validate the kernel change in `SpatialViewer.CadCore`.
2. Merge/tag the CadCore change.
3. Advance the `external/SpatialViewer.CadCore` gitlink in a SpatialViewer PR.
4. Require both CadCore CI and SpatialViewer integration CI to pass before release.
