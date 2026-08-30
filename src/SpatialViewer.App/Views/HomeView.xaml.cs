using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SpatialViewer.Presentation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace SpatialViewer.Product.Views;

public sealed partial class HomeView : UserControl
{
    private readonly RecentFilesService _recentFiles;
    private IReadOnlyList<RecentFile> _items = Array.Empty<RecentFile>();
    public event EventHandler<IReadOnlyList<string>>? OpenRequested;
    public event EventHandler? FilePickerRequested;
    public HomeView(RecentFilesService recentFiles) { _recentFiles = recentFiles; InitializeComponent(); Loaded += async (_, _) => await ReloadAsync(); }

    private async Task ReloadAsync()
    {
        _items = await _recentFiles.LoadAsync();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var search = SearchBox?.Text?.Trim() ?? string.Empty;
        var filtered = _items.Where(item => string.IsNullOrEmpty(search) || item.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) || item.Path.Contains(search, StringComparison.OrdinalIgnoreCase)).ToArray();
        RecentFiles.ItemsSource = filtered;
        EmptyState.Visibility = filtered.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Open_Click(object sender, RoutedEventArgs e) => FilePickerRequested?.Invoke(this, EventArgs.Empty);
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
    private void RecentFiles_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (RecentFiles.SelectedItem is RecentFile item && item.Exists) OpenRequested?.Invoke(this, new[] { item.Path }); }
    private void DropZone_DragOver(object sender, DragEventArgs e) => e.AcceptedOperation = DataPackageOperation.Copy;
    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        var items = await e.DataView.GetStorageItemsAsync();
        OpenRequested?.Invoke(this, items.OfType<StorageFile>().Select(file => file.Path).ToArray());
    }
}
