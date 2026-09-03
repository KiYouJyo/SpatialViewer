# SpatialViewer v0.3.7 Startup Screen and Theme Fix

[简体中文](RELEASE-NOTES-v0.3.7.md) | [日本語](RELEASE-NOTES-v0.3.7.ja.md) | English

SpatialViewer v0.3.7 now follows the current two-stage startup design used by UrbanPlanToolbox and PageArc: the native Windows splash covers earliest process startup, then the real WinUI window takes over with a transparent in-window startup layer on Mica until the shell is ready. This release also includes the light-theme propagation regression fix discovered after the startup wrapper was introduced.

## Highlights

- Keeps MSIX `uap:SplashScreen` as the Stage 1 cold-start bootstrap with dedicated 100%, 125%, 150%, 200%, and 400% DPI resources and `uap5:Optional="true"`.
- Adds a Stage 2 transparent `StartupOverlay` inside the real main window so the window's own `MicaBackdrop` is visible immediately instead of jumping straight from a fixed `#202020` bitmap to the complete shell.
- Builds the real shell behind the startup logo while keeping it non-interactive until the startup presentation finishes.
- Starts the minimum display timer only after the logo has actually rendered a frame; fast launches keep a complete logo visible for about 500 ms, while slower initialization naturally extends the presentation without an extra blocking delay.
- Once the shell, logo, and minimum display time are ready, reveals the complete shell first and fades the startup layer out over about 200 ms with EaseOut timing.
- Adds a 1-second logo-decode fallback and a 5-second startup watchdog so the startup layer can never become a permanent blocking state.
- Replaces the old native-only startup contract with a hybrid Stage 1 + Mica Stage 2 contract and explicitly locks the accepted title-bar geometry against regression.
- Fixes the theme tree split introduced by the startup wrapper: `WindowRoot` is now the sole theme owner and `RootGrid` inherits it, restoring consistent Light / Dark / System behavior across the whole window.

## Preserved

- The light/dark palette itself is not redesigned; the title bar, hamburger menu, NavigationView, page layout, tabs, CAD rendering, and Cad Core behavior remain unchanged.
- No second standalone WinUI window, text, controls, or fake progress indicator is introduced.
