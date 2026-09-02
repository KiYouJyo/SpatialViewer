# SpatialViewer v0.3.1

## Kernel compatibility fix

v0.3.1 redesigns the independently updateable CAD kernel compatibility model and removes the incorrect requirement that the SpatialViewer product version must match a kernel-declared 0.x.x product series.

- Kernel eligibility is no longer gated by SpatialViewer 0.2.x / 0.3.x product versions.
- Introduces the independent `SpatialViewer.CadHost 1.0.0` host contract.
- CLR ABI `1.0.0.0` continues to govern assembly binding compatibility; Host Contract independently governs host capability compatibility.
- “Check for updates” now downloads the small standalone `cadcore-release.json` manifest first and validates schema, ABI, Host Contract, version, runtime, and source repository before an update is presented as installable.
- The full ZIP is validated again after download, including archive manifest equality, assembly ABI, and FileVersion checks.
- Release packaging and CI use the same ABI + Host Contract rules and no longer hard-code any SpatialViewer minor product version.
- Safe fallback remains in place: an incompatible or damaged external kernel cannot replace the bundled stable kernel.

v0.3.1 does not change Projects, Favorites, title bar, navigation, theme, or existing viewer interaction design.
