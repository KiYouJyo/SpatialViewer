using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace SpatialViewer.Product;

public sealed partial class MainWindow
{
    private bool _startupShellLoaded;
    private bool _startupImageReady;
    private bool _startupSplashRenderRequested;
    private bool _startupSplashShown;
    private bool _startupMinimumDurationSatisfied;
    private bool _startupPresentationCompleted;
    private bool _startupWatchdogStarted;
    private readonly Stopwatch _startupSplashVisibleClock = new();

    private void StartupRoot_Loaded(object sender, RoutedEventArgs e)
    {
        StartStartupSafetyNets();
    }

    private void StartupShell_Loaded(object sender, RoutedEventArgs e)
    {
        _startupShellLoaded = true;
        TryCompleteStartupVisual();
    }

    private void StartStartupSafetyNets()
    {
        if (_startupWatchdogStarted) return;
        _startupWatchdogStarted = true;
        _ = EnsureStartupLogoGateAsync();
        _ = WatchStartupAsync();
    }

    private async Task EnsureStartupLogoGateAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(1));
        if (_startupImageReady || _startupPresentationCompleted) return;

        // Packaged image decoding can occasionally complete without raising ImageOpened.
        // Fail open instead of leaving the application behind the startup surface forever.
        _startupImageReady = true;
        StartMinimumSplashDurationAfterFirstRender();
        TryCompleteStartupVisual();
    }

    private async Task WatchStartupAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(5));
        if (_startupPresentationCompleted) return;

        // A startup visual must never become a new failure mode. If initialization is
        // unexpectedly slow or one of the normal readiness signals is missed, reveal
        // the already-created shell and remove the overlay.
        PresentMainContent(skipAnimation: true);
    }

    private void OnStartupLogoImageOpened(object sender, RoutedEventArgs e)
    {
        _startupImageReady = true;
        StartMinimumSplashDurationAfterFirstRender();
        TryCompleteStartupVisual();
    }

    private void OnStartupLogoImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        _startupImageReady = true;
        StartMinimumSplashDurationAfterFirstRender();
        TryCompleteStartupVisual();
    }

    private void StartMinimumSplashDurationAfterFirstRender()
    {
        if (_startupSplashRenderRequested) return;
        _startupSplashRenderRequested = true;
        CompositionTarget.Rendering += OnStartupOverlayRendered;
    }

    private void OnStartupOverlayRendered(object? sender, object e)
    {
        CompositionTarget.Rendering -= OnStartupOverlayRendered;
        StartMinimumSplashDuration();
    }

    private void StartMinimumSplashDuration()
    {
        if (_startupSplashShown) return;
        _startupSplashShown = true;
        _startupSplashVisibleClock.Start();
        _ = CompleteMinimumSplashDurationAsync();
    }

    private async Task CompleteMinimumSplashDurationAsync()
    {
        var remaining = StartupSplashTiming.RemainingMinimumVisibleDuration(_startupSplashVisibleClock);
        if (remaining > TimeSpan.Zero) await Task.Delay(remaining);
        _startupMinimumDurationSatisfied = true;
        TryCompleteStartupVisual();
    }

    private void TryCompleteStartupVisual()
    {
        if (_startupPresentationCompleted ||
            !_startupShellLoaded ||
            !_startupImageReady ||
            !_startupMinimumDurationSatisfied)
        {
            return;
        }

        PresentMainContent(skipAnimation: false);
    }

    private void PresentMainContent(bool skipAnimation)
    {
        if (_startupPresentationCompleted) return;
        _startupPresentationCompleted = true;
        CompositionTarget.Rendering -= OnStartupOverlayRendered;

        // Reveal the fully initialized shell behind the transparent startup layer first,
        // then dissolve only the logo overlay. This preserves one continuous Mica surface.
        ShellContent.Opacity = 1;

        if (skipAnimation)
        {
            StartupOverlay.Visibility = Visibility.Collapsed;
            ShellContent.IsHitTestVisible = true;
            return;
        }

        _ = FadeOutStartupOverlayAsync();
    }

    private async Task FadeOutStartupOverlayAsync()
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var storyboard = new Storyboard();
            var fade = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(StartupSplashTiming.FadeOutDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(fade, StartupOverlay);
            Storyboard.SetTargetProperty(fade, nameof(UIElement.Opacity));
            storyboard.Children.Add(fade);
            storyboard.Completed += (_, _) => completion.TrySetResult(true);
            storyboard.Begin();

            await Task.WhenAny(
                completion.Task,
                Task.Delay(StartupSplashTiming.FadeOutFallbackDuration));
        }
        finally
        {
            StartupOverlay.Visibility = Visibility.Collapsed;
            StartupOverlay.Opacity = 1;
            ShellContent.IsHitTestVisible = true;
        }
    }
}
