using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SpatialViewer.Product.Controls;
using SpatialViewer.Presentation;
using System.Globalization;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace SpatialViewer.Product.Views;

public sealed partial class HomeView : UserControl
{
    private readonly RecentFilesService _recentFiles;
    private readonly AppLocalizationService _localization = AppLocalizationService.Default;
    private IReadOnlyList<RecentFile> _items = Array.Empty<RecentFile>();
    private ResponsiveLayoutMode _responsiveMode = ResponsiveLayoutMode.Large;
    private bool _syncingFilterSelection;
    private int _activeFilterIndex;

    public IReadOnlyList<string> RecentFilterItems { get; }
    public IReadOnlyList<WorkflowItem> WorkflowItems { get; }
    public WorkflowItem CadWorkflow => WorkflowItems[0];
    public WorkflowItem GisWorkflow => WorkflowItems[1];
    public WorkflowItem BimWorkflow => WorkflowItems[2];
    public WorkflowItem RhinoWorkflow => WorkflowItems[3];
    public event EventHandler<IReadOnlyList<string>>? OpenRequested;
    public event EventHandler? FilePickerRequested;

    public HomeView(RecentFilesService recentFiles)
    {
        _recentFiles = recentFiles;
        RecentFilterItems = [T("Home_Filter_All"), "CAD", "GIS", "BIM", "3D"];
        WorkflowItems =
        [
            new("CAD", "DWG / DXF", AppIconKind.Document, true, T("Home_Workflow_Cad_Tooltip")),
            new("GIS", "GPKG / SHP / GeoTIFF", AppIconKind.Area, false, T("Home_Workflow_Planned")),
            new("BIM", "IFC", AppIconKind.Project, false, T("Home_Workflow_Planned")),
            new("Rhino", "3DM", AppIconKind.View, true, T("Home_Workflow_Rhino_Tooltip"))
        ];
        InitializeComponent();
        AutomationProperties.SetName(OpenFileHeaderButton, T("Home_OpenFile_Automation"));
        Loaded += async (_, _) => await ReloadAsync();
    }

    internal void SetResponsiveMode(ResponsiveLayoutMode mode)
    {
        _responsiveMode = mode;
        WorkflowLarge.Visibility = mode == ResponsiveLayoutMode.Large ? Visibility.Visible : Visibility.Collapsed;
        WorkflowMedium.Visibility = mode == ResponsiveLayoutMode.Medium ? Visibility.Visible : Visibility.Collapsed;
        WorkflowSmall.Visibility = mode == ResponsiveLayoutMode.Small ? Visibility.Visible : Visibility.Collapsed;
        RecentToolbarLarge.Visibility = mode == ResponsiveLayoutMode.Large ? Visibility.Visible : Visibility.Collapsed;
        RecentToolbarCompact.Visibility = mode == ResponsiveLayoutMode.Large ? Visibility.Collapsed : Visibility.Visible;
    }

    private async Task ReloadAsync()
    {
#if DEBUG
        if (string.Equals(Environment.GetEnvironmentVariable("SPATIALVIEWER_VISUAL_VERIFICATION"), "1", StringComparison.Ordinal))
        {
            _items = CreateVisualVerificationRecentFiles();
            ApplyFilter();
            return;
        }
#endif
        _items = await _recentFiles.LoadAsync();
        ApplyFilter();
    }

    // Deterministic Debug-only verification data; it never writes to RecentFilesService.
    private static IReadOnlyList<RecentFile> CreateVisualVerificationRecentFiles() =>
    [
        new(@"D:\VisualVerification\A-SITE.dwg", "A-SITE.dwg", ".dwg", SpatialViewer.Core.DocumentKind.Cad, new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero), 1_834_000, true),
        new(@"D:\VisualVerification\A-ROAD.dxf", "A-ROAD.dxf", ".dxf", SpatialViewer.Core.DocumentKind.Cad, new DateTimeOffset(2026, 8, 29, 9, 0, 0, TimeSpan.Zero), 823_000, true),
        new(@"D:\VisualVerification\survey.gpkg", "survey.gpkg", ".gpkg", SpatialViewer.Core.DocumentKind.Gis, new DateTimeOffset(2026, 8, 28, 9, 0, 0, TimeSpan.Zero), 3_241_000, false),
        new(@"D:\VisualVerification\parcels.shp", "parcels.shp", ".shp", SpatialViewer.Core.DocumentKind.Gis, new DateTimeOffset(2026, 8, 27, 9, 0, 0, TimeSpan.Zero), 742_000, false),
        new(@"D:\VisualVerification\building.ifc", "building.ifc", ".ifc", SpatialViewer.Core.DocumentKind.Bim, new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.Zero), 8_100_000, false),
        new(@"D:\VisualVerification\concept.3dm", "concept.3dm", ".3dm", SpatialViewer.Core.DocumentKind.Rhino, new DateTimeOffset(2026, 8, 25, 9, 0, 0, TimeSpan.Zero), 4_220_000, false)
    ];

    private void ApplyFilter()
    {
        var search = (_responsiveMode == ResponsiveLayoutMode.Large ? SearchBox?.Text : CompactSearchBox?.Text)?.Trim() ?? string.Empty;
        var filtered = _items.Where(item => MatchesFilter(item, _activeFilterIndex) &&
            (string.IsNullOrEmpty(search) || item.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) || item.Path.Contains(search, StringComparison.OrdinalIgnoreCase))).ToArray();
        RecentFiles.ItemsSource = filtered.Select(RecentFileTile.From).ToArray();
        EmptyState.Visibility = filtered.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static bool MatchesFilter(RecentFile item, int filterIndex) => filterIndex switch
    {
        1 => item.Extension.Equals(".dwg", StringComparison.OrdinalIgnoreCase) || item.Extension.Equals(".dxf", StringComparison.OrdinalIgnoreCase),
        2 => item.Extension.Equals(".gpkg", StringComparison.OrdinalIgnoreCase) || item.Extension.Equals(".shp", StringComparison.OrdinalIgnoreCase) || item.Extension.Equals(".tif", StringComparison.OrdinalIgnoreCase) || item.Extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase),
        3 => item.Extension.Equals(".ifc", StringComparison.OrdinalIgnoreCase),
        4 => item.Extension.Equals(".3dm", StringComparison.OrdinalIgnoreCase),
        _ => true
    };

    private void Open_Click(object sender, RoutedEventArgs e) => FilePickerRequested?.Invoke(this, EventArgs.Empty);
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void FilterList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingFilterSelection || sender is not ListView list || list.SelectedIndex < 0) return;

        _activeFilterIndex = list.SelectedIndex;
        _syncingFilterSelection = true;
        try
        {
            if (!ReferenceEquals(list, RecentFilterListLarge)) RecentFilterListLarge.SelectedIndex = _activeFilterIndex;
            if (!ReferenceEquals(list, RecentFilterListCompact)) RecentFilterListCompact.SelectedIndex = _activeFilterIndex;
        }
        finally
        {
            _syncingFilterSelection = false;
        }
        ApplyFilter();
    }

    private void RecentFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RecentFileTile item } && item.Source.Exists)
            OpenRequested?.Invoke(this, new[] { item.Source.Path });
    }

    private void DropZone_DragOver(object sender, DragEventArgs e) => e.AcceptedOperation = DataPackageOperation.Copy;

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        var items = await e.DataView.GetStorageItemsAsync();
        OpenRequested?.Invoke(this, items.OfType<StorageFile>().Select(file => file.Path).ToArray());
    }

    private string T(string key) => _localization.GetString(key);
}

public sealed record WorkflowItem(string Title, string Formats, AppIconKind Icon, bool IsEnabled, string ToolTip);

public sealed record RecentFileTile(RecentFile Source, string ExtensionLabel, string DisplayName, string Metadata)
{
    public static RecentFileTile From(RecentFile source) =>
        new(source, source.Extension.TrimStart('.').ToUpperInvariant(), source.DisplayName, $"{FormatSize(source.FileSize)} · {FormatWhen(source.LastOpenedUtc)}");

    private static string FormatSize(long bytes) => bytes >= 1_000_000_000 ? $"{bytes / 1_000_000_000d:0.#} GB" : bytes >= 1_000_000 ? $"{bytes / 1_000_000d:0.#} MB" : bytes >= 1_000 ? $"{bytes / 1_000d:0.#} KB" : $"{bytes} B";

    private static string FormatWhen(DateTimeOffset openedUtc)
    {
        var localization = AppLocalizationService.Default;
        var local = openedUtc.ToLocalTime();
        var today = DateTimeOffset.Now.Date;
        if (local.Date == today)
            return string.Format(CultureInfo.CurrentCulture, localization.GetString("Home_Date_Today"), local);
        if (local.Date == today.AddDays(-1)) return localization.GetString("Home_Date_Yesterday");
        var format = localization.GetString("Home_Date_Format");
        return local.ToString(format, CultureInfo.CurrentCulture);
    }
}
