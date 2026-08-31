namespace SpatialViewer.Product;

/// <summary>
/// Process-lifetime update state shared by every AboutView instance.
/// This mirrors UrbanPlanToolbox's default UpdateViewModel session: navigating
/// away from About and returning must render the last check instead of resetting
/// the card to NotChecked. The CadCore service itself also belongs to this
/// session so an UpdateAvailable result can still be downloaded after navigation.
/// </summary>
internal sealed class AboutUpdateSessionState
{
    private static readonly Lazy<AboutUpdateSessionState> LazyDefault = new(() => new AboutUpdateSessionState());
    private ProductUpdateCheckState _productState = ProductUpdateCheckState.NotChecked;
    private GitHubReleaseInfo? _latestProductRelease;
    private CadCoreUpdateResult _cadCoreResult;

    private AboutUpdateSessionState()
    {
        _cadCoreResult = new CadCoreUpdateResult(CadCoreUpdateState.NotChecked, CadCoreRuntimeBootstrapper.CurrentVersion);
    }

    public static AboutUpdateSessionState Default => LazyDefault.Value;
    public event EventHandler? Changed;

    public ProductUpdateCheckState ProductState
    {
        get => _productState;
        set
        {
            if (_productState == value) return;
            _productState = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public GitHubReleaseInfo? LatestProductRelease
    {
        get => _latestProductRelease;
        set
        {
            if (Equals(_latestProductRelease, value)) return;
            _latestProductRelease = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public CadCoreUpdateService CadCoreUpdateService { get; } = new();

    public CadCoreUpdateResult CadCoreResult
    {
        get => _cadCoreResult;
        set
        {
            if (_cadCoreResult == value) return;
            _cadCoreResult = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
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
