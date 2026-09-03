# CAD v0.12.2 layout scene integration

Real architectural drawing comparison showed that the product viewport renders only `Document.Scene` (model space) even when CadCore has imported Paper Space layouts. This makes model geometry visible while the sheet border/title block can disappear.

The v0.12.2 host integration must select one scene consistently for Fit, Draw, and HitTest:

- model scene remains available and remains the fallback;
- when a CAD document contains a meaningful Paper Space layout, the product may display that layout scene instead of silently discarding it;
- Fit, rendering, and hit testing must all use the same selected scene;
- no title-bar, hamburger-menu, background, tab, or existing page visual changes are part of this patch.

Xref resolution remains explicit and separate; choosing a Paper Space scene must not implicitly follow external paths.
