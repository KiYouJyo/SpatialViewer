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
