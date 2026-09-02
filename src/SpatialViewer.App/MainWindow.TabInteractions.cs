using System.ComponentModel;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using SpatialViewer.Core;
using SpatialViewer.Presentation;
using SpatialViewer.Rendering;
using SpatialViewer.Rendering.Windows;
using Windows.UI.Input;

namespace SpatialViewer.Product;

public sealed partial class MainWindow
{
    private const double TabPreviewWidth = 320;
    private const double TabPreviewHeight = 180;
    private static readonly Duration TabOpenDuration = new(TimeSpan.FromMilliseconds(190));

    private void ConfigureTabInteractions(Border container, object tag, string title, double targetWidth)
    {
        // Use both the routed pressed event and a released fallback. Some mouse
        // drivers report the wheel-button transition only through PointerUpdateKind,
        // even though IsMiddleButtonPressed is already false by the time the routed
        // event reaches the detached tab container.
        container.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(ShellTab_PointerPressed), true);
        container.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(ShellTab_PointerReleased), true);
        ToolTipService.SetPlacement(container, PlacementMode.Bottom);
        ToolTipService.SetToolTip(container, CreateTabPreviewToolTip(tag, title));
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

    private void ShellTab_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border { Tag: { } tag } container) return;
        var properties = e.GetCurrentPoint(container).Properties;
        if (!properties.IsMiddleButtonPressed && properties.PointerUpdateKind != PointerUpdateKind.MiddleButtonPressed) return;

        // "Middle click" here explicitly means pressing the mouse wheel button.
        // Match browser semantics and close a background tab without activating it.
        e.Handled = true;
        CloseTabFromMiddleClick(tag);
    }

    private void ShellTab_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border { Tag: { } tag } container) return;
        if (e.GetCurrentPoint(container).Properties.PointerUpdateKind != PointerUpdateKind.MiddleButtonReleased) return;

        // Fallback for mouse/driver combinations that do not expose a reliable
        // middle-button state during PointerPressed. If the pressed path already
        // closed the tab, this detached container no longer receives the release.
        e.Handled = true;
        CloseTabFromMiddleClick(tag);
    }

    private void CloseTabFromMiddleClick(object tag)
    {
        if (tag is DocumentSession session)
        {
            CloseSession(session);
            return;
        }

        if (tag is not string homeId || !_homeTabs.Remove(homeId, out var visual)) return;
        _homeViews.Remove(homeId);
        ShellTabItems.Children.Remove(visual.Container);
        if (!Equals(_selectedTab, homeId)) return;

        if (_documentTabs.Keys.FirstOrDefault() is { } document) ShowDocument(document);
        else if (_homeTabs.Keys.FirstOrDefault() is { } nextHome) ShowHome(nextHome);
        else CreateHomeTab(select: true);
    }

    private static ToolTip CreateTabPreviewToolTip(object tag, string title)
    {
        var content = new StackPanel
        {
            Width = TabPreviewWidth,
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

        var subtitle = tag is DocumentSession session ? session.FilePath : title;
        content.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 11,
            Opacity = 0.68,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        });

        content.Children.Add(tag is DocumentSession document
            ? new DocumentTabPreviewControl(document)
            : CreateHomeTabPreview(title));

        // Keep the existing ToolTip geometry, typography, border and preview
        // content untouched. Only replace its flat fill with the native Mica
        // layering brush used on top of a Mica-backed window.
        return new ToolTip
        {
            Content = content,
            Background = ResolveTabPreviewMicaBrush()
        };
    }

    private static Brush ResolveTabPreviewMicaBrush()
    {
        if (Application.Current.Resources.TryGetValue("LayerOnMicaBaseAltFillColorDefaultBrush", out var micaLayer)
            && micaLayer is Brush micaBrush)
            return micaBrush;

        if (Application.Current.Resources.TryGetValue("LayerFillColorDefaultBrush", out var layer)
            && layer is Brush layerBrush)
            return layerBrush;

        // This path is only a compatibility fallback for runtimes that do not
        // expose the WinUI Mica layer resource. It remains translucent so the
        // MainWindow Mica backdrop is visible rather than becoming flat gray.
        return new SolidColorBrush(ColorHelper.FromArgb(176, 32, 40, 40));
    }

    private static Grid CreateHomeTabPreview(string title)
    {
        var grid = new Grid
        {
            Width = TabPreviewWidth,
            Height = TabPreviewHeight
        };
        grid.Children.Add(new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 8,
            Children =
            {
                new FontIcon { Glyph = "\uE80F", FontSize = 34, Opacity = 0.72 },
                new TextBlock { Text = title, FontSize = 12, Opacity = 0.72, HorizontalAlignment = HorizontalAlignment.Center }
            }
        });
        return grid;
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
        _root.Background = new SolidColorBrush(lightCanvas ? Colors.White : Colors.Black);
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
