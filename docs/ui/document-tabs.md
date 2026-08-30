# Document tabs and sessions

`SpatialViewer.Presentation.DocumentWorkspace` owns a collection of `DocumentSession` objects and canonical-path duplicate detection. Each session owns its file path, `IDocument`, `Camera2D`, selection, layer visibility (through its document scene), importer cancellation token, diagnostics, and loading/error state.

`SpatialViewer.App` creates a `CadViewportControl` for the currently active session. The viewport renderer is created on load and disposed on unload; camera and selection stay in the session, so switching tabs does not mix state. Closing a tab cancels an unfinished import and disposes that session without modifying other tabs.
