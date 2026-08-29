# Stage 2 import pipeline

```text
file path -> IDocumentImporter -> reader adapter -> CadDocument -> CadSceneTranslator -> Scene2D -> renderer
```

`IDocumentImporter` is owned by Core and accepts `ImportRequest`, `ImportOptions`, progress reporting, and `CancellationToken`. It returns `ImportResult`, keeping partial success explicit through shared diagnostics.

`ACadSharpCadImporter` is the only production component that references ACadSharp. It copies reader results into Spatial Viewer records on a background task, reports reader warnings, and never returns an ACadSharp object. DXF and DWG use the same adapter, `CadDocument`, translator, and Stage 1 renderer path.
