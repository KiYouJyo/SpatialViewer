# Stage 2 verification record

Run `dotnet build SpatialViewer.sln -c Release`, `dotnet test SpatialViewer.sln -c Release`, and `dotnet run --project benchmarks/SpatialViewer.Benchmarks -c Release` from the repository root.

The CAD regression suite imports self-authored DXF fixtures, creates a real DWG fixture through the ACadSharp adapter, validates DXF/DWG semantic parity, negative-file recovery, cancellation, CAD-to-Scene color/metadata translation, nested non-uniform block transforms, and large coordinates. The Stage 1 tests and synthetic benchmarks remain part of the same solution gate.

The Debug Host opens `.dxf` and `.dwg` with an unpackaged WinUI picker, imports asynchronously, fits the resulting generic scene, exposes CAD layers, shows import/document diagnostics, and displays source metadata after selection.

## Verified baseline (2026-08-29)

- Debug and Release solution builds succeeded with 0 warnings and 0 errors.
- Tests: Core 9, Rendering 3, Integration 3, CAD 11; total 26 passed, 0 failed, 0 skipped.
- Stage 1 synthetic benchmark (ms): 10K create/bounds/prepare/hit = 20.0/11.2/15.7/15.9; 100K = 40.2/90.0/21.9/45.2; 1M = 586.9/110.5/155.1/302.7.
- CAD importer benchmark (ms): 10K import/prepare = 275.1/16.0; 100K = 2036.8/82.3. A 100K nested-block scene builds/translates in 660.3 ms and prepares 100K commands in 79.7 ms.
- The CAD fixture test generated deterministic PNG files at `artifacts/stage2/render/` for mixed-basic and large-coordinate scenes.
