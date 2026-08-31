namespace SpatialViewer.Product;

/// <summary>
/// Process-lifetime update state shared by every AboutView instance.
/// This mirrors UrbanPlanToolbox's default UpdateViewModel session: navigating
/// away from About and returning must render the last check instead of resetting
/// the card to NotChecked.
/// </summary>
internal sealed class AboutUpdateSessionState
{
    private static readonly Lazy<AboutUpdateSessionState> LazyDefault = new(() => new AboutUpdateSessionState());

    private AboutUpdateSessionState()
    {
        CadCoreResult = new CadCoreUpdateResult(CadCoreUpdateState.NotChecked, CadCoreRuntimeBootstrapper.CurrentVersion);
    }

    public static AboutUpdateSessionState Default => LazyDefault.Value;

    public ProductUpdateCheckState ProductState { get; set; } = ProductUpdateCheckState.NotChecked;
    public GitHubReleaseInfo? LatestProductRelease { get; set; }
    public CadCoreUpdateResult CadCoreResult { get; set; }
}

internal enum ProductUpdateCheckState
{
    NotChecked,
    Checking,
    NoRelease,
    NewVersion,
    Latest,
    NetworkFailed,
    Timeout
}
