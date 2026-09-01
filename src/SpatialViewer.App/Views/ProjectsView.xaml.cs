using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SpatialViewer.Product.Views;

public sealed record ProjectViewItem(
    Guid Id,
    string Name,
    IReadOnlyList<string> Files,
    string FileCountText,
    string FormatSummary,
    string LastOpenedText);

public sealed partial class ProjectsView : UserControl
{
    private readonly AppLibraryStore _store;
    private readonly AppLocalizationService _localization = AppLocalizationService.Default;

    public ProjectsView(AppLibraryStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        InitializeComponent();
        Loaded += ProjectsView_Loaded;
        Unloaded += ProjectsView_Unloaded;
        ApplyLocalizedText();
        RefreshItems();
    }

    public ObservableCollection<ProjectViewItem> VisibleItems { get; } = [];

    public event EventHandler? NewProjectRequested;
    public event EventHandler<IReadOnlyList<string>>? OpenRequested;

    private void ProjectsView_Loaded(object sender, RoutedEventArgs e)
    {
        _store.Changed -= Store_Changed;
        _store.Changed += Store_Changed;
        _localization.LanguageChanged -= Localization_LanguageChanged;
        _localization.LanguageChanged += Localization_LanguageChanged;
        RefreshItems();
    }

    private void ProjectsView_Unloaded(object sender, RoutedEventArgs e)
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

    private void NewProjectButton_Click(object sender, RoutedEventArgs e) => NewProjectRequested?.Invoke(this, EventArgs.Empty);

    private void ProjectCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Guid id }) return;
        var project = _store.Projects.FirstOrDefault(candidate => candidate.Id == id);
        if (project is null) return;
        _store.TouchProject(id);
        if (project.Files.Count > 0) OpenRequested?.Invoke(this, project.Files);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshItems();

    private void RefreshItems()
    {
        var query = SearchBox?.Text?.Trim() ?? string.Empty;
        var items = _store.Projects
            .Where(project => string.IsNullOrEmpty(query) ||
                              project.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                              project.Files.Any(path => Path.GetFileName(path).Contains(query, StringComparison.CurrentCultureIgnoreCase)))
            .Select(ToViewItem)
            .ToArray();

        VisibleItems.Clear();
        foreach (var item in items) VisibleItems.Add(item);
        if (EmptyState is not null) EmptyState.Visibility = items.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private ProjectViewItem ToViewItem(ProjectLibraryItem project)
    {
        var extensions = project.Files
            .Select(path => Path.GetExtension(path).TrimStart('.').ToUpperInvariant())
            .Where(extension => extension.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
        var formatSummary = extensions.Length == 0 ? "—" : string.Join(" · ", extensions);
        var language = _localization.CurrentLanguage;
        var fileCount = language switch
        {
            "ja-JP" => $"{project.Files.Count} ファイル",
            "en-US" => project.Files.Count == 1 ? "1 file" : $"{project.Files.Count} files",
            _ => $"{project.Files.Count} 个文件"
        };
        var localTime = project.LastOpenedAt.ToLocalTime();
        var lastOpened = language switch
        {
            "ja-JP" => $"最終オープン  {localTime:g}",
            "en-US" => $"Last opened  {localTime:g}",
            _ => $"最近打开  {localTime:g}"
        };
        return new ProjectViewItem(project.Id, project.Name, project.Files, fileCount, formatSummary, lastOpened);
    }

    private void ApplyLocalizedText()
    {
        switch (_localization.CurrentLanguage)
        {
            case "ja-JP":
                TitleText.Text = "プロジェクト";
                NewProjectButtonText.Text = "新規プロジェクト";
                SummaryText.Text = "関連する図面や空間データを、繰り返し開けるプロジェクトとしてまとめます。";
                SearchBox.PlaceholderText = "プロジェクトを検索";
                EmptyStateText.Text = "プロジェクトはまだありません。「新規プロジェクト」または「フォルダーをインポート」から開始してください。";
                break;
            case "en-US":
                TitleText.Text = "Projects";
                NewProjectButtonText.Text = "New project";
                SummaryText.Text = "Group related drawings and spatial data into reusable projects.";
                SearchBox.PlaceholderText = "Search projects";
                EmptyStateText.Text = "No projects yet. Start with New project or Import folder.";
                break;
            default:
                TitleText.Text = "项目";
                NewProjectButtonText.Text = "新建项目";
                SummaryText.Text = "将相关图纸与空间数据组织为可重复打开的项目。";
                SearchBox.PlaceholderText = "搜索项目";
                EmptyStateText.Text = "还没有项目。使用“新建项目”或“导入文件夹”开始。";
                break;
        }
    }
}
