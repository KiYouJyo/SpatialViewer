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

    public CadViewerView(DocumentSession session)
    {
        _session = session;
        InitializeComponent();
        Viewport.Session = session;
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
        ZoomText.Text = $"Zoom {_session.Camera.Zoom:G4}";
        UnitsText.Text = _session.Document is CadDocument cad ? cad.Units.ToString() : "Unitless";
        var groups = DiagnosticsPresenter.Aggregate(_session.Diagnostics.Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
        if (groups.Count > 0) { DiagnosticsBar.IsOpen = true; DiagnosticsBar.Message = string.Join(" · ", groups.Select(group => $"{group.Code} {group.Count}")); }
        Viewport.Fit();
        Viewport.Draw();
    }

    private void Viewport_SelectionChanged(object? sender, SceneItem? item)
    {
        var sections = PropertyPresenter.Create(item);
        PropertiesEmpty.Visibility = sections.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        PropertiesList.ItemsSource = sections.SelectMany(section => section.Rows.Select(row => new PropertyRow($"{section.Name} · {row.Label}", row.Value))).ToArray();
        ObjectText.Text = item is { } selected ? $"{selected.Metadata.GetValueOrDefault("CadType", selected.Geometry.GetType().Name)} · {selected.Layer.Name}" : string.Empty;
        if (item is { } selectedItem) LayerList.SelectedItem = selectedItem.Layer;
        ZoomText.Text = $"Zoom {_session.Camera.Zoom:G4}";
    }

    private void Layer_Click(object sender, RoutedEventArgs e) => Viewport.Draw();
    private void LayerList_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
    private void Fit_Click(object sender, RoutedEventArgs e) => Viewport.Fit();
    private void SelectTool_Click(object sender, RoutedEventArgs e) => SetMode(ViewerMode.Select);
    private void PanTool_Click(object sender, RoutedEventArgs e) => SetMode(ViewerMode.Pan);
    private void ZoomTool_Click(object sender, RoutedEventArgs e) => SetMode(ViewerMode.Zoom);
    private void SetMode(ViewerMode mode) { Viewport.Mode = mode; SelectTool.Background = mode == ViewerMode.Select ? Application.Current.Resources["BgSelectionBrush"] as Microsoft.UI.Xaml.Media.Brush : null; PanTool.Background = mode == ViewerMode.Pan ? Application.Current.Resources["BgSelectionBrush"] as Microsoft.UI.Xaml.Media.Brush : null; ZoomTool.Background = mode == ViewerMode.Zoom ? Application.Current.Resources["BgSelectionBrush"] as Microsoft.UI.Xaml.Media.Brush : null; }
    private void ToggleLeft_Click(object sender, RoutedEventArgs e) { _leftExpanded = !_leftExpanded; LeftPanel.Visibility = _leftExpanded ? Visibility.Visible : Visibility.Collapsed; WorkspaceGrid.ColumnDefinitions[0].Width = _leftExpanded ? new GridLength(300) : new GridLength(0); }
    private void ToggleRight_Click(object sender, RoutedEventArgs e) { _rightExpanded = !_rightExpanded; RightPanel.Visibility = _rightExpanded ? Visibility.Visible : Visibility.Collapsed; WorkspaceGrid.ColumnDefinitions[2].Width = _rightExpanded ? new GridLength(300) : new GridLength(0); }
    private void CadViewerView_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape) { _session.Selection = null; Viewport.Draw(); PropertiesEmpty.Visibility = Visibility.Visible; PropertiesList.ItemsSource = null; }
        if (e.Key == Windows.System.VirtualKey.F) Viewport.Fit();
    }
}
