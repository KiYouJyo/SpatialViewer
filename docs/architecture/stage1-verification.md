# Stage 1 verification record

## Automated

Run from the repository root:

```powershell
dotnet restore SpatialViewer.sln
dotnet build SpatialViewer.sln -c Debug --no-restore
dotnet build SpatialViewer.sln -c Release --no-restore
dotnet test SpatialViewer.sln -c Release --no-build
dotnet run --project benchmarks/SpatialViewer.Benchmarks -c Release
dotnet run --project src/SpatialViewer.DebugHost -c Debug
```

The test projects cover double math and inversion, empty/union/transformed bounds, scene identity and visibility, camera conversion/pan/zoom/fit, large-coordinate regression, hit test visibility, transform traversal, render preparation, and 10K/100K scene integration. The benchmark includes deterministic 10K, 100K, and 1M creation/bounds/preparation/hit-test baselines.

## Manual Debug Host checklist

- Select every synthetic scene, including 100K and 1M pressure scenes.
- Use mouse-wheel zoom, left-drag pan, Fit, and Reset.
- Toggle layers; hidden layers must disappear and cannot be selected.
- Click a primitive to see a yellow bounds highlight and its stable object id.
- On the large-coordinate scene, pan/zoom around the survey geometry and confirm its relative geometry stays stable.

The GitHub Actions Windows CI restores, Debug builds, Release builds, and Release tests every main/Stage 1 change and pull request.

## Verified baseline (2026-08-29)

- `dotnet restore`: succeeded.
- Debug and Release solution builds: succeeded with 0 warnings and 0 errors.
- Tests: Core 9, Rendering 3, Integration 3; total 15 passed, 0 failed, 0 skipped.
- Benchmark (Release, milliseconds):
  - 10K: create 21.4, bounds 11.3, prepare 15.1, hit test 15.0.
  - 100K: create 53.7, bounds 79.1, prepare 31.7, hit test 40.0.
  - 1M: create 569.7, bounds 115.3, prepare 166.7, hit test 309.4.
- The unpackaged x64 Debug Host was built and passed a real five-second startup smoke test.
- GitHub Actions Windows CI run `33257428816` passed on the Stage 1 branch.
