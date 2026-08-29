# CAD fixtures

All DXF fixtures in this directory were hand-authored for Spatial Viewer and contain no external project data. They use only the Stage 2 entity subset. DWG parity fixtures are generated at test time by ACadSharp from the same in-memory model; this avoids committing opaque binary data while keeping the generated files deterministic and redistributable.

`mixed-basic.dxf` exercises common geometry, layers, colors, text, MText, blocks, nested blocks, and an intentionally unsupported HATCH. `large-coordinate.dxf` protects the double-precision coordinate path. `negative/invalid.dxf` validates failure recovery.
