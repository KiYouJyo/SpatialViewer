# Stage 2 verification record

Run `dotnet build SpatialViewer.sln -c Release`, `dotnet test SpatialViewer.sln -c Release`, and `dotnet run --project benchmarks/SpatialViewer.Benchmarks -c Release` from the repository root.

The CAD regression suite imports self-authored DXF fixtures, creates a real DWG fixture through the ACadSharp adapter, validates DXF/DWG semantic parity, negative-file recovery, cancellation, CAD-to-Scene color/metadata translation, nested non-uniform block transforms, and large coordinates. The Stage 1 tests and synthetic benchmarks remain part of the same solution gate.

The Debug Host opens `.dxf` and `.dwg` with an unpackaged WinUI picker, imports asynchronously, fits the resulting generic scene, exposes CAD layers, shows import/document diagnostics, and displays source metadata after selection.

## Verified baseline (2026-08-29)

- Debug and Release solution builds succeeded with 0 warnings and 0 errors.
- Tests: Core 9, Rendering 3, Integration 3, CAD 10; total 25 passed, 0 failed, 0 skipped.
- Stage 1 synthetic benchmark (ms): 10K create/bounds/prepare/hit = 22.4/11.1/15.5/16.2; 100K = 43.1/81.4/35.9/41.9; 1M = 588.3/104.6/167.0/320.3.
- CAD importer benchmark (ms): 10K import/prepare = 256.3/3.2; 100K = 1654.6/44.1.
- The CAD fixture test generated deterministic PNG files at `artifacts/stage2/render/` for mixed-basic and large-coordinate scenes.
