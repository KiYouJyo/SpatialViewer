using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SpatialViewer.Product.Views;

public sealed record FavoriteViewItem(
    string FilePath,
    string DisplayName,
    string ExtensionLabel,
    string Metadata);

public sealed partial class FavoritesView : UserControl
{
    private readonly AppLibraryStore _store;
    private readonly AppLocalizationService _localization = AppLocalizationService.Default;

    public FavoritesView(AppLibraryStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        InitializeComponent();
        Loaded += FavoritesView_Loaded;
        Unloaded += FavoritesView_Unloaded;
        ApplyLocalizedText();
        RefreshItems();
    }

    public ObservableCollection<FavoriteViewItem> VisibleItems { get; } = [];

    public event EventHandler? AddRequested;
    public event EventHandler<IReadOnlyList<string>>? OpenRequested;

    private void FavoritesView_Loaded(object sender, RoutedEventArgs e)
    {
        _store.Changed -= Store_Changed;
        _store.Changed += Store_Changed;
        _localization.LanguageChanged -= Localization_LanguageChanged;
        _localization.LanguageChanged += Localization_LanguageChanged;
        RefreshItems();
    }

    private void FavoritesView_Unloaded(object sender, RoutedEventArgs e)
    {
        _store.Changed -= Store_Changed;
        _localization.LanguageChanged -= Localization_LanguageChanged;
    }

    private void Store_Changed(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(RefreshItems);

    private void Localization_LanguageChanged(object? sender, AppLanguageChangedEventArgs e) => DispatcherQueue.TryEnqueue(() =>
    {
        ApplyLocalizedText();
        RefreshItems();
    });

    private void AddFavoriteButton_Click(object sender, RoutedEventArgs e) => AddRequested?.Invoke(this, EventArgs.Empty);

    private void OpenFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string path } || string.IsNullOrWhiteSpace(path)) return;
        OpenRequested?.Invoke(this, [path]);
    }

    private void RemoveFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string path } || string.IsNullOrWhiteSpace(path)) return;
        _store.RemoveFavorite(path);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshItems();

    private void RefreshItems()
    {
        var query = SearchBox?.Text?.Trim() ?? string.Empty;
        var items = _store.Favorites
            .Where(path => string.IsNullOrEmpty(query) ||
                           Path.GetFileName(path).Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                           path.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .Select(ToViewItem)
            .ToArray();

        VisibleItems.Clear();
        foreach (var item in items) VisibleItems.Add(item);
        if (EmptyState is not null) EmptyState.Visibility = items.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private FavoriteViewItem ToViewItem(string path)
    {
        var displayName = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(displayName)) displayName = Path.GetFileName(path);
        var extension = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(extension)) extension = "FILE";
        var metadata = BuildMetadata(path);
        return new FavoriteViewItem(path, displayName, extension, metadata);
    }

    private static string BuildMetadata(string path)
    {
        try
        {
            var info = new FileInfo(path);
            var size = info.Length switch
            {
                >= 1024L * 1024L * 1024L => $"{info.Length / (1024d * 1024d * 1024d):0.##} GB",
                >= 1024L * 1024L => $"{info.Length / (1024d * 1024d):0.##} MB",
                >= 1024L => $"{info.Length / 1024d:0.##} KB",
                _ => $"{info.Length} B"
            };
            return $"{size} · {info.Directory?.Name ?? string.Empty}".TrimEnd(' ', '·');
        }
        catch (IOException)
        {
            return Path.GetDirectoryName(path) ?? path;
        }
        catch (UnauthorizedAccessException)
        {
            return Path.GetDirectoryName(path) ?? path;
        }
    }

    private void ApplyLocalizedText()
    {
        switch (_localization.CurrentLanguage)
        {
            case "ja-JP":
                TitleText.Text = "お気に入り";
                AddFavoriteButtonText.Text = "お気に入りを追加";
                SummaryText.Text = "よく使う図面や空間データを保存して、すばやく開けます。";
                SearchBox.PlaceholderText = "お気に入りを検索";
                EmptyStateText.Text = "お気に入りはまだありません。「お気に入りを追加」からファイルを選択してください。";
                break;
            case "en-US":
                TitleText.Text = "Favorites";
                AddFavoriteButtonText.Text = "Add favorite";
                SummaryText.Text = "Keep frequently used drawings and spatial data ready for quick access.";
                SearchBox.PlaceholderText = "Search favorites";
                EmptyStateText.Text = "No favorites yet. Choose Add favorite to select files.";
                break;
            default:
                TitleText.Text = "收藏";
                AddFavoriteButtonText.Text = "添加收藏";
                SummaryText.Text = "保存经常使用的图纸与空间数据，随时快速打开。";
                SearchBox.PlaceholderText = "搜索收藏";
                EmptyStateText.Text = "还没有收藏。点击“添加收藏”选择常用文件。";
                break;
        }
    }
}
