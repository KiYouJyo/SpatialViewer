namespace SpatialViewer.Product;

/// <summary>
/// One logical-DIP responsive state for the app shell and its active view.
/// Physical pixels and DPI are intentionally not part of this calculation.
/// </summary>
internal enum ResponsiveLayoutMode
{
    Large,
    Medium,
    Small
}
