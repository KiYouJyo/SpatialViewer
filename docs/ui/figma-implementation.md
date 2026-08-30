# UI Stage 1 Figma implementation

The formal application reads Figma file `MH6aYeEQ7h7hkRQU4KaS5t` directly. The implemented references are Home Dark `3:17`, Home Light `12:2`, CAD Viewer Dark `5:103`, CAD Viewer Light `12:120`, and the component baseline section `19:34`.

`SpatialViewer.App` maps the design's 48 DIP title bar, 32 DIP tabs, 240 DIP home navigation, 51 DIP viewer toolbar, 300 DIP layer/properties panels, and 24 DIP status bar to WinUI layout rather than absolute-positioned mock UI. The standard Windows caption buttons remain real system caption buttons; the app only supplies their visual integration through the extended title bar.

Figma's generated React/Tailwind references were used only as design context. The committed implementation is C# and XAML on Windows App SDK / WinUI 3.
