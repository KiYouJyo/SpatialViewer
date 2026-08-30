# CAD viewer UI

The formal viewer follows the verified CAD chain:

`DWG/DXF -> ACadSharpCadImporter -> CadDocument -> Scene2D -> RenderPreparation -> Win2DSceneRenderer`.

The layer list controls the real generic `Layer.IsVisible` state, which affects both rendering and hit testing. Selection uses `HitTesting`, appears with an accent bounding box, and is presented from generic `SceneItem` metadata: the WinUI project has no dependency on ACadSharp entity types. Pan, cursor-centred wheel zoom, click zoom mode, and fit all use the session's one `Camera2D`.

Measure, area, and coordinate-pick buttons are deliberately disabled: no result is fabricated. Diagnostics are aggregated into one non-blocking InfoBar rather than one dialog per unsupported entity.
