using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SpatialViewer.Product.Controls;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;
using SpatialViewer.Presentation;
using System.Globalization;

namespace SpatialViewer.Product.Views;

public sealed partial class CadViewerView : UserControl, IDisposable
{
    private readonly DocumentSession _session;
    private readonly ACadSharpCadImporter _importer = new();
    private readonly AppLocalizationService _localization = AppLocalizationService.Default;
    private bool _leftExpanded = true;
    private bool _rightExpanded = true;
    private bool _initialViewportPrepared;
    private bool _disposed;
    private CadLayoutMode _layoutMode = CadLayoutMode.Large;
    private FileSystemWatcher? _fileWatcher;
    private DateTimeOffset _lastReloadRequestUtc;

    public CadViewerView(DocumentSession session)
    {
        _session = session;
        InitializeComponent();
        Viewport.Session = session;
        SetMode(ViewerMode.Pan);
        Viewport.SelectionChanged += Viewport_SelectionChanged;
        Viewport.PointerWorldChanged += (_, point) => CoordinateText.Text = $"X: {point.X:F2}   Y: {point.Y:F2}   Z: 0";
        Loaded += CadViewerView_Loaded;
        Unloaded += CadViewerView_Unloaded;
        CadRoot.ActualThemeChanged += CadRoot_ActualThemeChanged;
        KeyDown += CadViewerView_KeyDown;
    }

    private void CadViewerView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_disposed) return;
        AppSettingsStore.Changed += AppSettingsStore_Changed;
        ApplyViewerPreferences();
        ConfigureFileWatcher();
        Refresh();
    }

    private void CadViewerView_Unloaded(object sender, RoutedEventArgs e)
    {
        AppSettingsStore.Changed -= AppSettingsStore_Changed;
        DisposeFileWatcher();
    }

    private void CadRoot_ActualThemeChanged(FrameworkElement sender, object args)
    {
        if (!_disposed) ApplyViewerPreferences();
    }

    private void AppSettingsStore_Changed(object? sender, EventArgs e)
    {
        if (_disposed) return;
        ApplyViewerPreferences();
        ConfigureFileWatcher();
    }

    private void ApplyViewerPreferences()
    {
        var settings = AppSettingsStore.Current;
        CadRoot.RequestedTheme = settings.ViewerTheme switch
        {
            ViewerThemePreference.Light => ElementTheme.Light,
            ViewerThemePreference.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
        var lightCanvas = settings.DrawingBackground switch
        {
            DrawingBackgroundPreference.Light => true,
            DrawingBackgroundPreference.Dark => false,
            _ => CadRoot.ActualTheme == ElementTheme.Light
        };
        Viewport.CanvasColor = lightCanvas ? "#FFFFFF" : "#000000";
    }

    private void ConfigureFileWatcher()
    {
        DisposeFileWatcher();
        if (_disposed || !AppSettingsStore.Current.AutoCheckFileChanges || !File.Exists(_session.FilePath)) return;
        var directory = Path.GetDirectoryName(_session.FilePath);
        var fileName = Path.GetFileName(_session.FilePath);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName)) return;
        _fileWatcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };
        _fileWatcher.Changed += FileWatcher_Changed;
        _fileWatcher.Renamed += FileWatcher_Changed;
    }

    private void DisposeFileWatcher()
    {
        if (_fileWatcher is null) return;
        _fileWatcher.EnableRaisingEvents = false;
        _fileWatcher.Changed -= FileWatcher_Changed;
        _fileWatcher.Renamed -= FileWatcher_Changed;
        _fileWatcher.Dispose();
        _fileWatcher = null;
    }

    private void FileWatcher_Changed(object sender, FileSystemEventArgs e)
    {
        if (_disposed) return;
        var now = DateTimeOffset.UtcNow;
        if (now - _lastReloadRequestUtc < TimeSpan.FromMilliseconds(900)) return;
        _lastReloadRequestUtc = now;
        DispatcherQueue.TryEnqueue(async () =>
        {
            if (_disposed || !AppSettingsStore.Current.AutoCheckFileChanges || !File.Exists(_session.FilePath)) return;
            ObjectText.Text = T("Cad_Status_FileChanged");
            await _session.LoadAsync(_importer, new Progress<ImportProgress>(_ => { }));
            Refresh(forceDraw: true);
        });
    }

    private void Refresh(bool forceDraw = false)
    {
        if (_disposed) return;
        if (_session.State == DocumentSessionState.Loading)
        {
            PropertiesEmpty.Text = string.Format(CultureInfo.CurrentCulture, T("Cad_Status_OpeningFile"), _session.DisplayName);
            return;
        }
        if (_session.State != DocumentSessionState.Ready || _session.Document is null)
        {
            PropertiesEmpty.Text = _session.ErrorMessage ?? T("Cad_Status_OpenFailed");
            return;
        }
        LayerList.ItemsSource = _session.Layers;
        OverlayLayerList.ItemsSource = _session.Layers;
        ZoomText.Text = string.Format(CultureInfo.CurrentCulture, T("Cad_Status_Zoom"), _session.Camera.Zoom);
        UnitsText.Text = _session.Document is CadDocument cad ? cad.Units.ToString() : T("Cad_Unitless");
        var groups = DiagnosticsPresenter.Aggregate(_session.Diagnostics.Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
        DiagnosticsBar.IsOpen = false;
        if (groups.Count > 0)
            ObjectText.Text = string.Format(CultureInfo.CurrentCulture, T("Cad_Status_SkippedObjects"), groups.Sum(group => group.Count));
        else if (forceDraw)
            ObjectText.Text = T("Cad_Status_FileReloaded");
        if (!_initialViewportPrepared)
        {
            if (AppSettingsStore.Current.FitToWindowOnOpen) Viewport.Fit();
            _initialViewportPrepared = true;
        }
        Viewport.Draw();
    }

    private void Viewport_SelectionChanged(object? sender, SceneItem? item)
    {
        var sections = PropertyPresenter.Create(item);
        PropertiesEmpty.Visibility = sections.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var rows = sections.SelectMany(section => section.Rows.Select(row => new PropertyRow($"{section.Name} · {row.Label}", row.Value))).ToArray();
        PropertiesList.ItemsSource = rows;
        OverlayPropertiesList.ItemsSource = rows;
        ObjectText.Text = item is { } selected ? $"{selected.Metadata.GetValueOrDefault("CadType", selected.Geometry.GetType().Name)} · {selected.Layer.Name}" : string.Empty;
        OverlayPropertiesEmpty.Visibility = PropertiesEmpty.Visibility;
        if (item is { } selectedItem) LayerList.SelectedItem = selectedItem.Layer;
        ZoomText.Text = string.Format(CultureInfo.CurrentCulture, T("Cad_Status_Zoom"), _session.Camera.Zoom);
    }

    private void Layer_Click(object sender, RoutedEventArgs e) => Viewport.Draw();
    private void LayerList_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
    private void Fit_Click(object sender, RoutedEventArgs e) => Viewport.Fit();
    private void SelectTool_Click(object sender, RoutedEventArgs e) => SetMode(ViewerMode.Select);
    private void PanTool_Click(object sender, RoutedEventArgs e) => SetMode(ViewerMode.Pan);

    private void SetMode(ViewerMode mode)
    {
        Viewport.Mode = mode;
        SelectTool.IsChecked = mode == ViewerMode.Select;
        PanTool.IsChecked = mode == ViewerMode.Pan;
    }

    private void ToggleLeft_Click(object sender, RoutedEventArgs e)
    {
        if (_layoutMode == CadLayoutMode.Small) { LayersFlyout.ShowAt((FrameworkElement)sender); return; }
        _leftExpanded = !_leftExpanded;
        LeftPaneHost.IsPaneOpen = _leftExpanded;
    }

    private void ToggleRight_Click(object sender, RoutedEventArgs e)
    {
        if (_layoutMode != CadLayoutMode.Large) { PropertiesFlyout.ShowAt((FrameworkElement)sender); return; }
        _rightExpanded = !_rightExpanded;
        RightPaneHost.IsPaneOpen = _rightExpanded;
    }

    private void CadRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var mode = e.NewSize.Width >= 1280 ? CadLayoutMode.Large : e.NewSize.Width >= 800 ? CadLayoutMode.Medium : CadLayoutMode.Small;
        if (mode == _layoutMode) return;
        _layoutMode = mode;
        ApplyLayout();
    }

    private void ApplyLayout()
    {
        CadRoot.RowDefinitions[0].Height = new GridLength(64);
        ViewerToolbar.Visibility = Visibility.Visible;
        if (_layoutMode == CadLayoutMode.Large)
        {
            LeftPaneHost.OpenPaneLength = 300;
            LeftPaneHost.IsPaneOpen = _leftExpanded;
            RightPaneHost.IsPaneOpen = _rightExpanded;
        }
        else if (_layoutMode == CadLayoutMode.Medium)
        {
            LeftPaneHost.OpenPaneLength = 240;
            LeftPaneHost.IsPaneOpen = _leftExpanded;
            RightPaneHost.IsPaneOpen = false;
        }
        else
        {
            CadRoot.RowDefinitions[0].Height = new GridLength(0);
            ViewerToolbar.Visibility = Visibility.Collapsed;
            LeftPaneHost.IsPaneOpen = false;
            RightPaneHost.IsPaneOpen = false;
        }
    }

    private void CadViewerView_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape) { _session.Selection = null; Viewport.Draw(); PropertiesEmpty.Visibility = Visibility.Visible; PropertiesList.ItemsSource = null; }
        if (e.Key == Windows.System.VirtualKey.F) Viewport.Fit();
    }

    private string T(string key) => _localization.GetString(key);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        AppSettingsStore.Changed -= AppSettingsStore_Changed;
        Loaded -= CadViewerView_Loaded;
        Unloaded -= CadViewerView_Unloaded;
        CadRoot.ActualThemeChanged -= CadRoot_ActualThemeChanged;
        KeyDown -= CadViewerView_KeyDown;
        DisposeFileWatcher();
        Viewport.Dispose();
    }
}

internal enum CadLayoutMode { Large, Medium, Small }
