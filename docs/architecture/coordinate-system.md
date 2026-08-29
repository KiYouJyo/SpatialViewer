# Coordinate system and precision

All persisted model-space and viewport math uses `double`: points, vectors, bounding boxes, transforms, camera target, zoom conversion, hit testing, and scene bounds. No WinUI `Point` or GPU-facing `float` type appears in Core.

The rendering conversion is deliberately late:

```text
world double -> hierarchy transform double -> camera-relative screen double -> backend float
```

The Win2D renderer applies a node's world transform and the camera projection before converting final display positions to `float`. It never casts raw CAD/GIS coordinates such as `(500000, 3400000)` directly to a GPU coordinate. `RenderFrame.LocalOrigin` records the camera target so Direct3D-based future backends can batch in a camera-relative local coordinate system.

The regression suite round-trips large coordinates and verifies millimetre-scale deltas at high zoom. The large-coordinate synthetic scene is part of the Debug Host so this behavior can also be manually inspected while panning and zooming.
