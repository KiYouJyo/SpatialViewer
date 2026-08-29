# CAD model

`CadDocument` implements the generic `IDocument` contract while keeping CAD-only state in `SpatialViewer.Formats.Cad`: version, drawing units, CAD layers, block definitions, model-space entities, and source metadata.

Stage 2 entities are POINT, LINE, CIRCLE, ARC, ELLIPSE, LWPOLYLINE/POLYLINE, TEXT, MTEXT, INSERT, and an explicit unsupported-entity record. Each preserves handle, layer, color mode, visibility, linetype name, lineweight, and metadata.

Block definitions remain logical CAD objects. INSERT becomes a hierarchy node with translation, rotation, uniform/non-uniform scaling, inherited color context, and recursion protection. The translator resolves ByLayer, ByBlock, ACI, and true-color values to generic scene styles while retaining CAD source metadata for selection display.
