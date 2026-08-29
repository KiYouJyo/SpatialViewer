# Stage 2 golden render foundation

The Stage 2 fixture tests create deterministic, fixed-viewport scene command snapshots from `mixed-basic.dxf`, nested blocks, layers, and large coordinates. The snapshots are intentionally source-controlled as normalized text while the Windows-only Win2D backend remains the visual execution path. Stage 3 will add GPU PNG capture/diff with anti-alias tolerance; Stage 2 deliberately does not claim pixel-perfect CAD fidelity.

Run `dotnet test SpatialViewer.Cad.Tests` to validate the same fixed scene content used by the Debug Host and renderer contract.
