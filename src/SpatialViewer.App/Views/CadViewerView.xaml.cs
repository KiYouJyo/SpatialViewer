using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SpatialViewer.Product.Controls;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Presentation;

namespace SpatialViewer.Product.Views;

public sealed partial class CadViewerView : UserControl
{
    private readonly DocumentSession _session;
    private bool _leftExpanded = true;
    private bool _rightExpanded = true;
    private CadLayoutMode _layoutMode = CadLayoutMode.Large;

    public CadViewerView(DocumentSession session)
    {
        _session = session;
        InitializeComponent();
        Viewport.Session = session;
        SetMode(ViewerMode.Pan);
        Viewport.SelectionChanged += Viewport_SelectionChanged;
        Viewport.PointerWorldChanged += (_, point) => CoordinateText.Text = $"X: {point.X:F2}   Y: {point.Y:F2}   Z: 0";
        Loaded += (_, _) => Refresh();
        KeyDown += CadViewerView_KeyDown;
    }

    private void Refresh()
    {
        if (_session.State == DocumentSessionState.Loading)
        {
            PropertiesEmpty.Text = $"正在打开 {_session.DisplayName}…";
            return;
        }
        if (_session.State != DocumentSessionState.Ready || _session.Document is null)
        {
            PropertiesEmpty.Text = _session.ErrorMessage ?? "无法打开此文件。";
            return;
        }
        LayerList.ItemsSource = _session.Layers;
        OverlayLayerList.ItemsSource = _session.Layers;
        ZoomText.Text = $"Zoom {_session.Camera.Zoom:G4}";
        UnitsText.Text = _session.Document is CadDocument cad ? cad.Units.ToString() : "Unitless";
        var groups = DiagnosticsPresenter.Aggregate(_session.Diagnostics.Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
        // Diagnostics remain available to the session, but the property panel is
        // reserved for the selected object rather than a raw internal-code dump.
        DiagnosticsBar.IsOpen = false;
        if (groups.Count > 0)
            ObjectText.Text = $"已跳过 {groups.Sum(group => group.Count)} 个暂不支持的对象";
        Viewport.Fit();
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
        ZoomText.Text = $"Zoom {_session.Camera.Zoom:G4}";
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
}

internal enum CadLayoutMode { Large, Medium, Small }
