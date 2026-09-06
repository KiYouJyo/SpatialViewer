using System.ComponentModel;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SpatialViewer.Presentation;
using SpatialViewer.Product.Controls;
using SpatialViewer.ThreeDm.Integration;
using SpatialViewer.ThreeDm.Rendering;

namespace SpatialViewer.Product.Views;

public sealed partial class ThreeDmViewerView : UserControl, IDisposable
{
    private readonly ThreeDmProductSession _session;
    private readonly AppLocalizationService _localization = AppLocalizationService.Default;
    private bool _leftExpanded = true;
    private bool _rightExpanded = true;
    private bool _showingLayers = true;
    private bool _initialViewportPrepared;
    private bool _disposed;
    private ThreeDmLayoutMode _layoutMode = ThreeDmLayoutMode.Large;
    private FileSystemWatcher? _fileWatcher;
    private DateTimeOffset _lastReloadRequestUtc;
    private string? _selectedViewKey;

    internal ThreeDmViewerView(ThreeDmProductSession session)
    {
        _session = session;
        InitializeComponent();
        Viewport.Session = session;
        SetMode(ThreeDmViewerMode.Orbit);
        Viewport.SelectionChanged += Viewport_SelectionChanged;
        ShadedMenuItem.Text = T("ThreeDm_Display_Shaded");
        ShadedEdgesMenuItem.Text = T("ThreeDm_Display_ShadedWithEdges");
        WireframeMenuItem.Text = T("ThreeDm_Display_Wireframe");
        Loaded += ThreeDmViewerView_Loaded;
        Unloaded += ThreeDmViewerView_Unloaded;
        ThreeDmRoot.ActualThemeChanged += ThreeDmRoot_ActualThemeChanged;
        KeyDown += ThreeDmViewerView_KeyDown;
    }

    private void ThreeDmViewerView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_disposed) return;
        _session.PropertyChanged += Session_PropertyChanged;
        AppSettingsStore.Changed += AppSettingsStore_Changed;
        ApplyViewerPreferences();
        ConfigureFileWatcher();
        ApplyLayout();
        RefreshSessionState();
    }

    private void ThreeDmViewerView_Unloaded(object sender, RoutedEventArgs e)
    {
        _session.PropertyChanged -= Session_PropertyChanged;
        AppSettingsStore.Changed -= AppSettingsStore_Changed;
        DisposeFileWatcher();
    }

    private void Session_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(RefreshSessionState);
    }

    private void AppSettingsStore_Changed(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ApplyViewerPreferences();
            ConfigureFileWatcher();
            Viewport.Draw();
        });
    }

    private void ThreeDmRoot_ActualThemeChanged(FrameworkElement sender, object args)
    {
        if (!_disposed) ApplyViewerPreferences();
    }

    private void ApplyViewerPreferences()
    {
        var settings = AppSettingsStore.Current;
        ThreeDmRoot.RequestedTheme = settings.ViewerTheme switch
        {
            ViewerThemePreference.Light => ElementTheme.Light,
            ViewerThemePreference.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
        var lightCanvas = settings.DrawingBackground switch
        {
            DrawingBackgroundPreference.Light => true,
            DrawingBackgroundPreference.Dark => false,
            _ => ThreeDmRoot.ActualTheme == ElementTheme.Light
        };
        Viewport.CanvasColor = lightCanvas ? "#FFFFFF" : "#000000";
    }

    internal void RefreshSessionState()
    {
        if (_disposed) return;
        if (_session.State == ThreeDmProductSessionState.Loading)
        {
            CoordinateText.Text = _session.TotalObjects > 0
                ? string.Format(CultureInfo.CurrentCulture, T("ThreeDm_Status_OpeningProgress"), _session.ProcessedObjects, _session.TotalObjects)
                : string.Format(CultureInfo.CurrentCulture, T("ThreeDm_Status_OpeningFile"), _session.DisplayName);
            return;
        }

        if (_session.State != ThreeDmProductSessionState.Ready)
        {
            CoordinateText.Text = _session.ErrorMessage ?? T("ThreeDm_Status_OpenFailed");
            return;
        }

        var layerRows = new List<ThreeDmLayerRow>();
        foreach (var root in _session.Layers) AddLayerRows(root, 0, layerRows);
        LayerList.ItemsSource = layerRows;

        var views = CreateViewRows();
        ViewList.ItemsSource = views;
        if (_selectedViewKey is null)
            _selectedViewKey = views.FirstOrDefault(item => item.Preset.Key == "standard:perspective")?.Preset.Key;
        ViewList.SelectedItem = views.FirstOrDefault(item => item.Preset.Key == _selectedViewKey);

        var summary = _session.Summary;
        if (summary is not null)
        {
            UnitsText.Text = summary.ModelUnitSystem ?? T("ThreeDm_Unitless");
            ObjectText.Text = string.Format(CultureInfo.CurrentCulture, T("ThreeDm_Status_ObjectCount"), summary.ObjectCount);
            var warningCount = summary.WarningDiagnosticCount + summary.ErrorDiagnosticCount;
            DiagnosticsBar.IsOpen = warningCount > 0;
            DiagnosticsBar.Title = warningCount > 0
                ? string.Format(CultureInfo.CurrentCulture, T("ThreeDm_Diagnostics_Count"), warningCount)
                : string.Empty;
        }

        CoordinateText.Text = T("ThreeDm_Status_Ready");
        ZoomText.Text = ResolveCurrentViewName(views);
        SyncDisplayModeChecks();
        if (!_initialViewportPrepared)
        {
            if (AppSettingsStore.Current.FitToWindowOnOpen) Viewport.Fit();
            _initialViewportPrepared = true;
        }

        if (_session.Selection is { } selection)
            UpdateProperties(_session.GetSelectionProperties(selection));
        else
            UpdateProperties(null);
        Viewport.Draw();
    }

    private ThreeDmViewRow[] CreateViewRows() =>
        _session.ViewPresets.Select(preset => new ThreeDmViewRow(preset, LocalizePresetName(preset))).ToArray();

    private string LocalizePresetName(ThreeDmViewPreset preset) => preset.Key switch
    {
        "standard:perspective" => T("ThreeDm_View_Perspective"),
        "standard:top" => T("ThreeDm_View_Top"),
        "standard:front" => T("ThreeDm_View_Front"),
        "standard:right" => T("ThreeDm_View_Right"),
        _ => preset.Name
    };

    private string ResolveCurrentViewName(IReadOnlyList<ThreeDmViewRow> rows)
    {
        var row = rows.FirstOrDefault(item => item.Preset.Key == _selectedViewKey);
        return row?.Name ?? T("ThreeDm_View_Perspective");
    }

    private static void AddLayerRows(ThreeDmLayerNode node, int depth, List<ThreeDmLayerRow> output)
    {
        output.Add(new ThreeDmLayerRow(
            node.Id,
            node.Name,
            node.EffectiveVisible,
            new Thickness(depth * 14, 0, 0, 0)));
        foreach (var child in node.Children) AddLayerRows(child, depth + 1, output);
    }

    private async void Layer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: Guid id, IsChecked: bool visible }) return;
        await _session.SetLayerVisibilityAsync(id, visible);
        RefreshSessionState();
        Viewport.Draw();
    }

    private void ShowLayers_Click(object sender, RoutedEventArgs e) => SetLeftPaneMode(showLayers: true);
    private void ShowViews_Click(object sender, RoutedEventArgs e) => SetLeftPaneMode(showLayers: false);

    private void SetLeftPaneMode(bool showLayers)
    {
        _showingLayers = showLayers;
        LayerList.Visibility = showLayers ? Visibility.Visible : Visibility.Collapsed;
        ViewList.Visibility = showLayers ? Visibility.Collapsed : Visibility.Visible;
        LayerSegment.Style = (Style)Application.Current.Resources[showLayers ? "PanelSegmentActive" : "PanelSegment"];
        ViewSegment.Style = (Style)Application.Current.Resources[showLayers ? "PanelSegment" : "PanelSegmentActive"];
    }

    private void ViewList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewList.SelectedItem is not ThreeDmViewRow row) return;
        _selectedViewKey = row.Preset.Key;
        ZoomText.Text = row.Name;
        Viewport.SetView(row.Preset.Camera);
    }

    private void Fit_Click(object sender, RoutedEventArgs e) => Viewport.Fit();
    private void SelectTool_Click(object sender, RoutedEventArgs e) => SetMode(ThreeDmViewerMode.Select);
    private void OrbitTool_Click(object sender, RoutedEventArgs e) => SetMode(ThreeDmViewerMode.Orbit);
    private void PanTool_Click(object sender, RoutedEventArgs e) => SetMode(ThreeDmViewerMode.Pan);

    private void SetMode(ThreeDmViewerMode mode)
    {
        Viewport.Mode = mode;
        SelectTool.IsChecked = mode == ThreeDmViewerMode.Select;
        OrbitTool.IsChecked = mode == ThreeDmViewerMode.Orbit;
        PanTool.IsChecked = mode == ThreeDmViewerMode.Pan;
    }

    private async void ShadedMenuItem_Click(object sender, RoutedEventArgs e) =>
        await SetDisplayModeAsync(ThreeDmRenderDisplayMode.Shaded);
    private async void ShadedEdgesMenuItem_Click(object sender, RoutedEventArgs e) =>
        await SetDisplayModeAsync(ThreeDmRenderDisplayMode.ShadedWithEdges);
    private async void WireframeMenuItem_Click(object sender, RoutedEventArgs e) =>
        await SetDisplayModeAsync(ThreeDmRenderDisplayMode.Wireframe);

    private async Task SetDisplayModeAsync(ThreeDmRenderDisplayMode mode)
    {
        await _session.SetDisplayModeAsync(mode);
        SyncDisplayModeChecks();
        Viewport.Draw();
    }

    private void SyncDisplayModeChecks()
    {
        ShadedMenuItem.IsChecked = _session.DisplayMode == ThreeDmRenderDisplayMode.Shaded;
        ShadedEdgesMenuItem.IsChecked = _session.DisplayMode == ThreeDmRenderDisplayMode.ShadedWithEdges;
        WireframeMenuItem.IsChecked = _session.DisplayMode == ThreeDmRenderDisplayMode.Wireframe;
    }

    private void Viewport_SelectionChanged(object? sender, ThreeDmSelectionProperties? properties)
    {
        UpdateProperties(properties);
    }

    private void UpdateProperties(ThreeDmSelectionProperties? properties)
    {
        PropertiesEmpty.Visibility = properties is null ? Visibility.Visible : Visibility.Collapsed;
        if (properties is null)
        {
            PropertiesList.ItemsSource = null;
            return;
        }

        var rows = new List<PropertyRow>
        {
            new(T("ThreeDm_Property_Name"), string.IsNullOrWhiteSpace(properties.Name) ? "—" : properties.Name),
            new(T("ThreeDm_Property_Type"), properties.GeometryKind.ToString()),
            new(T("ThreeDm_Property_Layer"), string.IsNullOrWhiteSpace(properties.LayerName) ? "—" : properties.LayerName),
            new(T("ThreeDm_Property_Material"), string.IsNullOrWhiteSpace(properties.MaterialName) ? "—" : properties.MaterialName),
            new(T("ThreeDm_Property_Visible"), properties.EffectiveVisible ? T("ThreeDm_Yes") : T("ThreeDm_No")),
            new(
                T("ThreeDm_Property_Bounds"),
                string.Format(
                    CultureInfo.CurrentCulture,
                    "{0:G6}, {1:G6}, {2:G6} — {3:G6}, {4:G6}, {5:G6}",
                    properties.Bounds.Min.X,
                    properties.Bounds.Min.Y,
                    properties.Bounds.Min.Z,
                    properties.Bounds.Max.X,
                    properties.Bounds.Max.Y,
                    properties.Bounds.Max.Z)),
            new(
                T("ThreeDm_Property_Instance"),
                properties.InstanceNames.Count > 0 ? string.Join(" › ", properties.InstanceNames) : "—"),
        };

        if (properties.ObjectColorArgb is uint color)
            rows.Add(new PropertyRow(T("ThreeDm_Property_Color"), $"#{color:X8}"));
        if (!string.IsNullOrWhiteSpace(properties.ColorSource))
            rows.Add(new PropertyRow(T("ThreeDm_Property_ColorSource"), properties.ColorSource));
        if (!string.IsNullOrWhiteSpace(properties.MaterialSource))
            rows.Add(new PropertyRow(T("ThreeDm_Property_MaterialSource"), properties.MaterialSource));
        foreach (var pair in properties.GeometryDetails)
            rows.Add(new PropertyRow(pair.Key, pair.Value));

        PropertiesList.ItemsSource = rows;
        ObjectText.Text = $"{properties.GeometryKind} · {properties.LayerName ?? "—"}";
    }

    private void ToggleLeft_Click(object sender, RoutedEventArgs e)
    {
        _leftExpanded = !_leftExpanded;
        LeftPaneHost.IsPaneOpen = _leftExpanded;
    }

    private void ToggleRight_Click(object sender, RoutedEventArgs e)
    {
        _rightExpanded = !_rightExpanded;
        RightPaneHost.IsPaneOpen = _rightExpanded;
    }

    private void ThreeDmRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var mode = e.NewSize.Width >= 1280
            ? ThreeDmLayoutMode.Large
            : e.NewSize.Width >= 800
                ? ThreeDmLayoutMode.Medium
                : ThreeDmLayoutMode.Small;
        if (mode == _layoutMode) return;
        _layoutMode = mode;
        ApplyLayout();
    }

    private void ApplyLayout()
    {
        ViewerToolbar.Visibility = Visibility.Visible;
        ThreeDmRoot.RowDefinitions[0].Height = new GridLength(64);
        var paneLength = _layoutMode switch
        {
            ThreeDmLayoutMode.Large => 300d,
            ThreeDmLayoutMode.Medium => 240d,
            _ => 220d
        };
        LeftPaneHost.OpenPaneLength = paneLength;
        RightPaneHost.OpenPaneLength = paneLength;
        LeftPaneHost.DisplayMode = SplitViewDisplayMode.Inline;
        RightPaneHost.DisplayMode = SplitViewDisplayMode.Inline;
        LeftPaneHost.IsPaneOpen = _leftExpanded;
        RightPaneHost.IsPaneOpen = _rightExpanded;
        SetLeftPaneMode(_showingLayers);
    }

    private void ConfigureFileWatcher()
    {
        DisposeFileWatcher();
        if (_disposed ||
            !AppSettingsStore.Current.AutoCheckFileChanges ||
            !File.Exists(_session.FilePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(_session.FilePath);
        var fileName = Path.GetFileName(_session.FilePath);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName)) return;

        _fileWatcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };
        _fileWatcher.Changed += FileWatcher_Changed;
        _fileWatcher.Renamed += FileWatcher_Changed;
    }

    private void FileWatcher_Changed(object sender, FileSystemEventArgs e)
    {
        if (_disposed) return;
        var now = DateTimeOffset.UtcNow;
        if (now - _lastReloadRequestUtc < TimeSpan.FromMilliseconds(900)) return;
        _lastReloadRequestUtc = now;
        DispatcherQueue.TryEnqueue(async () =>
        {
            if (_disposed ||
                !AppSettingsStore.Current.AutoCheckFileChanges ||
                !File.Exists(_session.FilePath) ||
                _session.State != ThreeDmProductSessionState.Ready)
            {
                return;
            }

            CoordinateText.Text = T("ThreeDm_Status_FileChanged");
            await _session.ReloadAsync();
            if (_session.State == ThreeDmProductSessionState.Ready)
            {
                CoordinateText.Text = T("ThreeDm_Status_FileReloaded");
                RefreshSessionState();
                Viewport.Draw();
            }
        });
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

    private void ThreeDmViewerView_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            _session.Selection = null;
            Viewport.Draw();
            UpdateProperties(null);
        }
        if (e.Key == Windows.System.VirtualKey.F) Viewport.Fit();
    }

    private string T(string key) => _localization.GetString(key);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session.PropertyChanged -= Session_PropertyChanged;
        AppSettingsStore.Changed -= AppSettingsStore_Changed;
        Viewport.SelectionChanged -= Viewport_SelectionChanged;
        Loaded -= ThreeDmViewerView_Loaded;
        Unloaded -= ThreeDmViewerView_Unloaded;
        ThreeDmRoot.ActualThemeChanged -= ThreeDmRoot_ActualThemeChanged;
        KeyDown -= ThreeDmViewerView_KeyDown;
        DisposeFileWatcher();
        Viewport.Dispose();
    }

    private sealed record ThreeDmLayerRow(Guid Id, string Name, bool IsVisible, Thickness Margin);
    private sealed record ThreeDmViewRow(ThreeDmViewPreset Preset, string Name);
}

internal enum ThreeDmLayoutMode { Large, Medium, Small }
