# SpatialViewer v0.3.4

[简体中文](RELEASE-NOTES-v0.3.4.md) | [日本語](RELEASE-NOTES-v0.3.4.ja.md) | English

## Adaptive tabs

v0.3.4 improves title-bar tab usability when many documents are open while preserving the existing page rendering and visual language.

- Tabs keep the existing `220 DIP` width while the full tab strip, spacing, and “+” button fit inside the available title-bar width.
- Once the strip becomes crowded, existing and newly opened tabs shrink evenly to a shared target width, similar to Chrome and Firefox.
- Tabs recompute their width while the window is resized, then automatically expand again when space returns or tabs are closed, up to the original `220 DIP` width.
- A `72 DIP` minimum tab width is retained; beyond that limit the existing horizontal scrolling remains available as a fallback rather than compressing controls indefinitely.
- Space for the “+” new-tab button is always reserved so it cannot be pushed outside the title-bar viewport by the tab strip.
- The v0.3.3 horizontal grow-and-fade opening animation is preserved, while existing tabs make room before the new tab expands into its final slot.
- Closing a tab triggers a recalculation after removal so the remaining tabs expand automatically.

## Version and kernel

- Product version is now `0.3.4`; the MSIX package version is `0.3.4.0`.
- Cad Core integration and update behavior are unchanged.

## Unchanged behavior

v0.3.4 does not alter title-bar height, tab corner radius or colors, selected/unselected treatment, CAD hover previews, the hamburger menu, NavigationView, page backgrounds, light/dark themes, or CAD drawing rendering.
