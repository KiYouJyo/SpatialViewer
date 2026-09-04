using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
    private bool _initialViewportPrepared;
    private bool _disposed;
    private ThreeDmLayoutMode _layoutMode = ThreeDmLayoutMode.Large;

    internal ThreeDmViewerView(ThreeDmProductSession session)
    {
        _session = session;
        InitializeComponent();
        Viewport.Session = session;
        DisplayModePicker.ItemsSource = CreateDisplayModes();
        DisplayModePicker.DisplayMemberPath = nameof(ThreeDmDisplayModeRow.Name);
        DisplayModePicker.SelectedIndex = 1;
        Loaded += ThreeDmViewerView_Loaded;
        Unloaded += ThreeDmViewerView_Unloaded;
        ThreeDmRoot.ActualThemeChanged += ThreeDmRoot_ActualThemeChanged;
    }

    private void ThreeDmViewerView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_disposed) return;
        _session.PropertyChanged += Session_PropertyChanged;
        AppSettingsStore.Changed += AppSettingsStore_Changed;
        ApplyViewerPreferences();
        ApplyLayout();
        RefreshSessionState();
    }

    private void ThreeDmViewerView_Unloaded(object sender, RoutedEventArgs e)
    {
        _session.PropertyChanged -= Session_PropertyChanged;
        AppSettingsStore.Changed -= AppSettingsStore_Changed;
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
            Viewport.Draw();
        });
    }

    private void ThreeDmRoot_ActualThemeChanged(FrameworkElement sender, object args)
    {
        ApplyViewerPreferences();
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
            ProgressText.Text = _session.TotalObjects > 0
                ? string.Format(T("ThreeDm_Status_OpeningProgress"), _session.ProcessedObjects, _session.TotalObjects)
                : string.Format(T("ThreeDm_Status_OpeningFile"), _session.DisplayName);
            return;
        }

        if (_session.State != ThreeDmProductSessionState.Ready)
        {
            ProgressText.Text = _session.ErrorMessage ?? T("ThreeDm_Status_OpenFailed");
            return;
        }

        var layers = new List<ThreeDmLayerRow>();
        foreach (var root in _session.Layers) AddLayerRows(root, 0, layers);
        LayerList.ItemsSource = layers;

        if (ViewPicker.ItemsSource is null)
        {
            ViewPicker.ItemsSource = _session.ViewPresets;
            ViewPicker.SelectedItem = _session.ViewPresets.FirstOrDefault(item => item.Key == "standard:perspective");
        }

        var summary = _session.Summary;
        if (summary is not null)
        {
            SummaryText.Text = string.Format(
                T("ThreeDm_Summary"),
                summary.ObjectCount,
                summary.LayerCount,
                summary.MaterialCount,
                summary.NamedViewCount,
                summary.InstanceDefinitionCount);
            UnitsText.Text = summary.ModelUnitSystem ?? T("ThreeDm_Unitless");
            ObjectText.Text = string.Format(T("ThreeDm_Status_ObjectCount"), summary.ObjectCount);
            var warningCount = summary.WarningDiagnosticCount + summary.ErrorDiagnosticCount;
            DiagnosticsBar.IsOpen = warningCount > 0;
            DiagnosticsBar.Title = warningCount > 0
                ? string.Format(T("ThreeDm_Diagnostics_Count"), warningCount)
                : string.Empty;
        }

        ProgressText.Text = T("ThreeDm_Status_Ready");
        if (!_initialViewportPrepared)
        {
            if (AppSettingsStore.Current.FitToWindowOnOpen) Viewport.Fit();
            _initialViewportPrepared = true;
        }

        Viewport.Draw();
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

    private void Layer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: Guid id, IsChecked: bool visible }) return;
        _session.SetLayerVisibility(id, visible);
        RefreshSessionState();
    }

    private void Fit_Click(object sender, RoutedEventArgs e) => Viewport.Fit();

    private void ViewPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewPicker.SelectedItem is ThreeDmViewPreset preset) Viewport.SetView(preset.Camera);
    }

    private void DisplayModePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DisplayModePicker.SelectedItem is not ThreeDmDisplayModeRow row) return;
        _session.SetDisplayMode(row.Mode);
        Viewport.Draw();
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
    }

    private ThreeDmDisplayModeRow[] CreateDisplayModes() =>
    [
        new(ThreeDmRenderDisplayMode.Shaded, T("ThreeDm_Display_Shaded")),
        new(ThreeDmRenderDisplayMode.ShadedWithEdges, T("ThreeDm_Display_ShadedWithEdges")),
        new(ThreeDmRenderDisplayMode.Wireframe, T("ThreeDm_Display_Wireframe")),
    ];

    private string T(string key) => _localization.GetString(key);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session.PropertyChanged -= Session_PropertyChanged;
        AppSettingsStore.Changed -= AppSettingsStore_Changed;
        Viewport.Dispose();
    }

    private sealed record ThreeDmLayerRow(Guid Id, string Name, bool IsVisible, Thickness Margin);
    private sealed record ThreeDmDisplayModeRow(ThreeDmRenderDisplayMode Mode, string Name);
}

internal enum ThreeDmLayoutMode
{
    Large,
    Medium,
    Small,
}
