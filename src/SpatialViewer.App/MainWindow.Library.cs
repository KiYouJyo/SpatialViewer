using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SpatialViewer.Product.Views;
using Windows.Storage.Pickers;

namespace SpatialViewer.Product;

public sealed partial class MainWindow
{
    private readonly AppLibraryStore _libraryStore = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SpatialViewer",
        "library.json"));

    private static readonly string[] LibraryFileExtensions =
    [
        ".dwg", ".dxf",
        ".gpkg", ".shp", ".tif", ".tiff", ".geojson", ".json",
        ".ifc", ".3dm"
    ];

    private async void ShellNavigation_V03ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is not NavigationViewItem item || item.Tag is not string tag) return;
        switch (tag)
        {
            case "Home":
                ShowHome();
                break;
            case "Projects":
                ShowProjects();
                break;
            case "Favorites":
                ShowFavorites();
                break;
            case "ImportFolder":
                await ImportFolderAsProjectAsync();
                break;
            case "Settings":
                ShowSettings();
                break;
            case "About":
                ShowAbout();
                break;
        }
    }

    private void ShowProjects()
    {
        ShowNavigationChrome();
        SelectShellItem(ProjectsNav);
        var view = new ProjectsView(_libraryStore);
        view.NewProjectRequested += async (_, _) => await CreateProjectAsync();
        view.OpenRequested += async (_, paths) => await OpenFilesAsync(paths);
        MainContent.Content = view;
    }

    private void ShowFavorites()
    {
        ShowNavigationChrome();
        SelectShellItem(FavoritesNav);
        var view = new FavoritesView(_libraryStore);
        view.AddRequested += async (_, _) => await AddFavoritesAsync();
        view.OpenRequested += async (_, paths) => await OpenFilesAsync(paths);
        MainContent.Content = view;
    }

    private async Task CreateProjectAsync()
    {
        var nameBox = new TextBox
        {
            PlaceholderText = _localizationLanguage switch
            {
                "ja-JP" => "プロジェクト名",
                "en-US" => "Project name",
                _ => "项目名称"
            },
            MinWidth = 320
        };
        var dialog = new ContentDialog
        {
            Title = _localizationLanguage switch
            {
                "ja-JP" => "新規プロジェクト",
                "en-US" => "New project",
                _ => "新建项目"
            },
            Content = nameBox,
            PrimaryButtonText = _localizationLanguage switch
            {
                "ja-JP" => "作成",
                "en-US" => "Create",
                _ => "创建"
            },
            CloseButtonText = _localizationLanguage switch
            {
                "ja-JP" => "キャンセル",
                "en-US" => "Cancel",
                _ => "取消"
            },
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = RootGrid.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        var name = string.IsNullOrWhiteSpace(nameBox.Text)
            ? DefaultProjectName()
            : nameBox.Text.Trim();
        var files = await PickLibraryFilesAsync();
        _libraryStore.CreateProject(name, files);
        ShowProjects();
    }

    private async Task AddFavoritesAsync()
    {
        var files = await PickLibraryFilesAsync();
        foreach (var path in files) _libraryStore.AddFavorite(path);
        ShowFavorites();
    }

    private async Task ImportFolderAsProjectAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return;

        IReadOnlyList<string> files;
        try
        {
            files = Directory
                .EnumerateFiles(folder.Path, "*", SearchOption.AllDirectories)
                .Where(IsLibraryFile)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (UnauthorizedAccessException)
        {
            files = [];
        }
        catch (IOException)
        {
            files = [];
        }

        _libraryStore.CreateProject(folder.Name, files);
        ShowProjects();
    }

    private async Task<IReadOnlyList<string>> PickLibraryFilesAsync()
    {
        var picker = new FileOpenPicker();
        foreach (var extension in LibraryFileExtensions) picker.FileTypeFilter.Add(extension);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        var files = await picker.PickMultipleFilesAsync();
        return files.Select(file => file.Path).Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
    }

    private static bool IsLibraryFile(string path) => LibraryFileExtensions.Contains(
        Path.GetExtension(path),
        StringComparer.OrdinalIgnoreCase);

    private string DefaultProjectName() => _localizationLanguage switch
    {
        "ja-JP" => $"プロジェクト {DateTime.Now:yyyy-MM-dd}",
        "en-US" => $"Project {DateTime.Now:yyyy-MM-dd}",
        _ => $"项目 {DateTime.Now:yyyy-MM-dd}"
    };

    private string _localizationLanguage => AppLocalizationService.Default.CurrentLanguage;
}
