# Stage 2 golden render foundation

The Stage 2 fixture tests create deterministic, fixed-viewport PNGs from `mixed-basic.dxf` and `large-coordinate.dxf`. They use a small, platform-independent Scene2D rasterizer so the files can be generated in CI without a desktop GPU. Generated PNGs are ignored by Git and form the input to future image-diff baselines.

The Win2D backend remains the interactive visual execution path. Stage 3 will add GPU capture/diff with anti-alias tolerance; Stage 2 deliberately does not claim pixel-perfect CAD fidelity.

Run `dotnet test SpatialViewer.Cad.Tests` to validate the same fixed scene content used by the Debug Host and renderer contract.
