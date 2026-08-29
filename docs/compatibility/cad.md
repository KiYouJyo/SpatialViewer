# CAD compatibility matrix — Stage 2

| Entity/capability | Read | Scene | Render | Notes |
| --- | --- | --- | --- | --- |
| POINT, LINE, CIRCLE, ARC, ELLIPSE | Yes | Yes | Yes | Double precision |
| LWPOLYLINE, POLYLINE | Yes | Yes | Yes | Bulge arc fidelity deferred |
| TEXT, MTEXT | Yes | Yes | Basic | MTEXT formatting normalized to plain text |
| BLOCK, INSERT, nested blocks | Yes | Yes | Yes | Cycle protected, non-uniform scale |
| Layers, visibility, ACI/true-color | Yes | Yes | Yes | ByLayer/ByBlock resolution |
| Lineweight | Yes | Metadata/basic width | Basic | Not print-scale accurate |
| HATCH, DIMENSION, LEADER, MLEADER | Detected | No | No | Diagnostic and safe skip |
| XREF, raster, paper space/layout, proxy/custom objects | Detected/partial | No | No | Deferred |

“Read” does not claim visual fidelity; only the Render column states actual viewer output.
