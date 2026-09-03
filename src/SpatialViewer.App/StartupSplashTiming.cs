using System.Diagnostics;

namespace SpatialViewer.Product;

/// <summary>
/// Non-blocking timing contract for the authored Mica startup overlay.
/// Mirrors the proven UrbanPlanToolbox/PageArc behavior without adding a second window.
/// </summary>
internal static class StartupSplashTiming
{
    public static readonly TimeSpan MinimumVisibleDuration = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan FadeOutDuration = TimeSpan.FromMilliseconds(200);
    public static readonly TimeSpan FadeOutFallbackDuration = TimeSpan.FromMilliseconds(300);

    public static TimeSpan RemainingMinimumVisibleDuration(Stopwatch visibleClock) =>
        MinimumVisibleDuration - visibleClock.Elapsed > TimeSpan.Zero
            ? MinimumVisibleDuration - visibleClock.Elapsed
            : TimeSpan.Zero;
}
