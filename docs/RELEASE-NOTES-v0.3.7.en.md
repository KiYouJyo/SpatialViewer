# SpatialViewer v0.3.7 Startup Screen

SpatialViewer v0.3.7 adds a native Windows startup screen following the same implementation approach used by UrbanPlanToolbox and PageArc.

## Highlights

- Uses the MSIX `uap:SplashScreen` for cold starts instead of creating a second authored WinUI window only to display branding.
- Uses a stable `#202020` dark surface with the SpatialViewer product mark centered; no extra text, buttons, or fake progress indicator is introduced.
- Generates dedicated 100%, 125%, 150%, 200%, and 400% bitmap resources at a 620×300 logical size so Windows can resolve the correct DPI resource without blurry runtime scaling.
- Marks the splash as `uap5:Optional="true"`, allowing Windows to skip it on fast launches while still covering slower cold-start initialization with the native surface.
- Adds build-time generation and contract validation for splash dimensions, background color, DPI resource qualifiers, and manifest wiring.

## Preserved

- No title bar, hamburger menu, NavigationView, page background, tab, CAD rendering, or Cad Core behavior is changed.
- No extra overlay or startup animation is inserted into the main window, preserving first-frame responsiveness and normal window restoration.
