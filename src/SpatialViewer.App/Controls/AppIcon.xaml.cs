using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SpatialViewer.Product.Controls;

public enum AppIconKind
{
    Home, Menu, Project, Favorite, Folder, Settings, Help, OpenFile, Document,
    Add, Close, Forward, Select, Pan, Zoom, Fit, Measure, Area, Coordinates, View, Panel
}

/// <summary>Single Fluent icon map. Pages choose semantic icons, never text glyphs.</summary>
public sealed partial class AppIcon : UserControl
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(AppIconKind), typeof(AppIcon), new PropertyMetadata(AppIconKind.Document, OnAppearanceChanged));
    public static readonly DependencyProperty IconSizeProperty = DependencyProperty.Register(
        nameof(IconSize), typeof(double), typeof(AppIcon), new PropertyMetadata(16d, OnAppearanceChanged));

    public AppIconKind Kind { get => (AppIconKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public double IconSize { get => (double)GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }

    public AppIcon()
    {
        InitializeComponent();
        UpdateAppearance();
    }

    private static void OnAppearanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((AppIcon)d).UpdateAppearance();

    private void UpdateAppearance()
    {
        Glyph.Symbol = Kind switch
        {
            AppIconKind.Home => Symbol.Home,
            AppIconKind.Menu => Symbol.GlobalNavigationButton,
            AppIconKind.Project => Symbol.Library,
            AppIconKind.Favorite => Symbol.Favorite,
            AppIconKind.Folder => Symbol.Folder,
            AppIconKind.Settings => Symbol.Setting,
            AppIconKind.Help => Symbol.Help,
            AppIconKind.OpenFile => Symbol.OpenFile,
            AppIconKind.Document => Symbol.Page,
            AppIconKind.Add => Symbol.Add,
            AppIconKind.Close => Symbol.Cancel,
            AppIconKind.Forward => Symbol.Forward,
            AppIconKind.Select => Symbol.TouchPointer,
            AppIconKind.Pan => Symbol.Map,
            AppIconKind.Zoom => Symbol.Zoom,
            AppIconKind.Fit => Symbol.FullScreen,
            AppIconKind.Measure => Symbol.Preview,
            AppIconKind.Area => Symbol.Map,
            AppIconKind.Coordinates => Symbol.Target,
            AppIconKind.View => Symbol.View,
            AppIconKind.Panel => Symbol.OpenPane,
            _ => Symbol.Page
        };
        Glyph.Width = IconSize;
        Glyph.Height = IconSize;
        Width = IconSize;
        Height = IconSize;
    }
}
