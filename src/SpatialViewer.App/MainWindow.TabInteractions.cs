using System.ComponentModel;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using SpatialViewer.Core;
using SpatialViewer.Presentation;
using SpatialViewer.Rendering;
using SpatialViewer.Rendering.Windows;
using Windows.Foundation;
using Windows.Graphics;

namespace SpatialViewer.Product;

public sealed partial class MainWindow
{
    private const double TabPreviewWidth = 320;
    private const double TabPreviewHeight = 180;
    private const double TabPreviewOuterWidth = 344;
    private const double TabPreviewOuterHeight = 252;
    private static readonly Duration TabOpenDuration = new(TimeSpan.FromMilliseconds(190));

    private DispatcherQueueTimer? _tabPreviewTimer;
    private Border? _pendingPreviewTab;
    private DocumentSession? _pendingPreviewSession;
    private string? _pendingPreviewTitle;
    private DocumentTabPreviewWindow? _tabPreviewWindow;

    private void ConfigureTabInteractions(Border container, object tag, string title, double targetWidth)
    {
        // Shell/home tabs intentionally have no hover card. Only real documents
        // expose a preview, matching the browser-like behavior requested for files.
        if (tag is DocumentSession session)
        {
            container.AddHandler(UIElement.PointerEnteredEvent, new PointerEventHandler(DocumentTab_PointerEntered), true);
            container.AddHandler(UIElement.PointerExitedEvent, new PointerEventHandler(DocumentTab_PointerExited), true);
            container.Unloaded += DocumentTab_Unloaded;
            container.Tag = tag;
            container.Resources["DocumentPreviewTitle"] = title;
        }

        AnimateTabOpen(container, targetWidth);
    }

    private void AnimateTabOpen(Border container, double targetWidth)
    {
        // The initial home tab is created before the visual tree is loaded. Keep
        // startup rendering identical and animate only tabs opened by the user.
        if (!RootGrid.IsLoaded)
        {
            container.Width = targetWidth;
            container.Opacity = 1;
            return;
        }

        // Chrome-like tab insertion: grow horizontally in place while fading in.
        // No Y translation or scale is used, so text/icons never jump or blur.
        container.Width = 0;
        container.Opacity = 0;

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var widthAnimation = new DoubleAnimation
        {
            From = 0,
            To = targetWidth,
            Duration = TabOpenDuration,
            EasingFunction = easing,
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(widthAnimation, container);
        Storyboard.SetTargetProperty(widthAnimation, nameof(FrameworkElement.Width));

        var opacityAnimation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(135)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(opacityAnimation, container);
        Storyboard.SetTargetProperty(opacityAnimation, nameof(UIElement.Opacity));

        var storyboard = new Storyboard();
        storyboard.Children.Add(widthAnimation);
        storyboard.Children.Add(opacityAnimation);
        storyboard.Completed += (_, _) =>
        {
            if (container.Parent is null) return;
            container.Width = targetWidth;
            container.Opacity = 1;
        };
        storyboard.Begin();
    }

    private void DocumentTab_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border { Tag: DocumentSession session } container) return;

        CloseDocumentTabPreview();
        _pendingPreviewTab = container;
        _pendingPreviewSession = session;
        _pendingPreviewTitle = container.Resources.TryGetValue("DocumentPreviewTitle", out var value)
            ? value?.ToString() ?? session.DisplayName
            : session.DisplayName;

        _tabPreviewTimer?.Stop();
        _tabPreviewTimer = DispatcherQueue.CreateTimer();
        _tabPreviewTimer.Interval = TimeSpan.FromMilliseconds(450);
        _tabPreviewTimer.IsRepeating = false;
        _tabPreviewTimer.Tick += DocumentTabPreviewTimer_Tick;
        _tabPreviewTimer.Start();
    }

    private void DocumentTab_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border container) return;
        if (ReferenceEquals(_pendingPreviewTab, container)) CancelPendingDocumentTabPreview();
        CloseDocumentTabPreview();
    }

    private void DocumentTab_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is Border container && ReferenceEquals(_pendingPreviewTab, container))
            CancelPendingDocumentTabPreview();
        CloseDocumentTabPreview();
    }

    private void DocumentTabPreviewTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        sender.Tick -= DocumentTabPreviewTimer_Tick;
        if (!ReferenceEquals(_tabPreviewTimer, sender)) return;

        var container = _pendingPreviewTab;
        var session = _pendingPreviewSession;
        var title = _pendingPreviewTitle;
        _tabPreviewTimer = null;
        if (container is null || session is null || string.IsNullOrWhiteSpace(title) || !container.IsLoaded) return;

        var scale = container.XamlRoot?.RasterizationScale ?? 1d;
        var tabBottom = container.TransformToVisual(RootGrid).TransformPoint(new Point(0, container.ActualHeight));
        var x = AppWindow.Position.X + (int)Math.Round(tabBottom.X * scale);
        var y = AppWindow.Position.Y + (int)Math.Round((tabBottom.Y + 6) * scale);

        var preview = new DocumentTabPreviewWindow(session, title, RootGrid.ActualTheme);
        _tabPreviewWindow = preview;
        preview.Closed += (_, _) =>
        {
            if (ReferenceEquals(_tabPreviewWindow, preview)) _tabPreviewWindow = null;
        };
        preview.ShowAt(
            x,
            y,
            Math.Max(1, (int)Math.Round(TabPreviewOuterWidth * scale)),
            Math.Max(1, (int)Math.Round(TabPreviewOuterHeight * scale)));
    }

    private void CancelPendingDocumentTabPreview()
    {
        if (_tabPreviewTimer is not null)
        {
            _tabPreviewTimer.Stop();
            _tabPreviewTimer.Tick -= DocumentTabPreviewTimer_Tick;
            _tabPreviewTimer = null;
        }
        _pendingPreviewTab = null;
        _pendingPreviewSession = null;
        _pendingPreviewTitle = null;
    }

    private void CloseDocumentTabPreview()
    {
        if (_tabPreviewWindow is null) return;
        var preview = _tabPreviewWindow;
        _tabPreviewWindow = null;
        preview.Close();
    }
}

/// <summary>
/// Non-activating document hover card backed by a real WinUI Mica window.
/// A normal ToolTip/Popup can only be made translucent over the parent window;
/// it cannot host a SystemBackdrop. Using a tiny secondary Window keeps the
/// approved preview content while making the surface genuine Mica rather than
/// transparent pseudo-Mica.
/// </summary>
internal sealed class DocumentTabPreviewWindow : Window
{
    public DocumentTabPreviewWindow(DocumentSession session, string title, ElementTheme theme)
    {
        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
        AppWindow.IsShownInSwitchers = false;

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsAlwaysOnTop = true;
            presenter.SetBorderAndTitleBar(true, false);
        }

        var content = new StackPanel
        {
            Width = 320,
            Spacing = 7
        };
        content.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        });
        content.Children.Add(new TextBlock
        {
            Text = session.FilePath,
            FontSize = 11,
            Opacity = 0.68,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        });
        content.Children.Add(new DocumentTabPreviewControl(session));

        Content = new Border
        {
            RequestedTheme = theme,
            Padding = new Thickness(12),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Child = content
        };
    }

    public void ShowAt(int x, int y, int width, int height)
    {
        AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
        AppWindow.Show(false);
    }
}

/// <summary>
/// Lightweight off-screen CAD preview used only while a tab hover card is open.
/// It owns an independent camera so preview fitting never changes the live viewer.
/// </summary>
internal sealed class DocumentTabPreviewControl : UserControl
{
    private readonly DocumentSession _session;
    private readonly Grid _root;
    private readonly CanvasControl _canvas;
    private readonly Camera2D _camera = new(Point2D.Origin);
    private Win2DSceneRenderer? _renderer;
    private bool _eventsAttached;

    public DocumentTabPreviewControl(DocumentSession session)
    {
        _session = session;
        Width = 320;
        Height = 180;
        IsTabStop = false;

        _root = new Grid();
        _canvas = new CanvasControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = false
        };
        _root.Children.Add(_canvas);
        Content = _root;

        Loaded += Preview_Loaded;
        Unloaded += Preview_Unloaded;
        SizeChanged += Preview_SizeChanged;
        ActualThemeChanged += Preview_ActualThemeChanged;
    }

    private void Preview_Loaded(object sender, RoutedEventArgs e)
    {
        _renderer ??= new Win2DSceneRenderer(_canvas);
        AttachEvents();
        ApplyAppearance();
        RenderPreview();
    }

    private void Preview_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachEvents();
        _renderer?.Dispose();
        _renderer = null;
    }

    private void Preview_SizeChanged(object sender, SizeChangedEventArgs e) => RenderPreview();

    private void Preview_ActualThemeChanged(FrameworkElement sender, object args)
    {
        ApplyAppearance();
        RenderPreview();
    }

    private void AttachEvents()
    {
        if (_eventsAttached) return;
        _eventsAttached = true;
        _session.PropertyChanged += Session_PropertyChanged;
        AppSettingsStore.Changed += AppSettingsStore_Changed;
    }

    private void DetachEvents()
    {
        if (!_eventsAttached) return;
        _eventsAttached = false;
        _session.PropertyChanged -= Session_PropertyChanged;
        AppSettingsStore.Changed -= AppSettingsStore_Changed;
    }

    private void Session_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(DocumentSession.State) or nameof(DocumentSession.Document) or nameof(DocumentSession.Layers))) return;
        DispatcherQueue.TryEnqueue(RenderPreview);
    }

    private void AppSettingsStore_Changed(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ApplyAppearance();
            RenderPreview();
        });
    }

    private void ApplyAppearance()
    {
        var settings = AppSettingsStore.Current;
        var lightCanvas = settings.DrawingBackground switch
        {
            DrawingBackgroundPreference.Light => true,
            DrawingBackgroundPreference.Dark => false,
            _ => ActualTheme == ElementTheme.Light
        };
        var canvasColor = lightCanvas ? "#FFFFFF" : "#000000";
        _root.Background = new SolidColorBrush(lightCanvas ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black);
        if (_renderer is not null) _renderer.CanvasColor = canvasColor;
    }

    private void RenderPreview()
    {
        if (_renderer is null || _session.State != DocumentSessionState.Ready || _session.Document is not { } document) return;
        var size = new Size2D(Math.Max(1, ActualWidth), Math.Max(1, ActualHeight));
        _camera.Fit(document.Bounds, size);
        _renderer.Render(RenderPreparation.Prepare(document.Scene, _camera), _camera, size, selectedObject: null);
    }
}
