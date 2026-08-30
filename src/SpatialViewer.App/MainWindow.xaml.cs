using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Input;
using SpatialViewer.Formats.Cad.ACadSharp;
using SpatialViewer.Presentation;
using SpatialViewer.Product.Views;
using Windows.Storage.Pickers;

namespace SpatialViewer.Product;

public sealed partial class MainWindow : Window
{
    private readonly DocumentWorkspace _workspace = new();
    private readonly ACadSharpCadImporter _importer = new();
    private readonly RecentFilesService _recentFiles;

    public MainWindow()
    {
        InitializeComponent();
        Title = "Spatial Viewer Preview";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1600, 1000));
        AppWindow.TitleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
        DocumentTabs.ItemsSource = _workspace.Documents;
        _recentFiles = new RecentFilesService(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpatialViewer", "recent-files.json"));
        Closed += (_, _) => _workspace.CloseAll();
        ShowHome();
    }

    private void ShowHome()
    {
        HomeTab.IsEnabled = false;
        var view = new HomeView(_recentFiles);
        view.OpenRequested += async (_, paths) => await OpenFilesAsync(paths);
        view.FilePickerRequested += async (_, _) => await PickAndOpenAsync();
        MainContent.Content = view;
    }

    private async Task OpenFilesAsync(IEnumerable<string> paths)
    {
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!FormatGate.IsSupported(path))
            {
                await ShowInfoAsync(FormatGate.UnsupportedMessage(path));
                continue;
            }
            var session = _workspace.OpenOrFocus(path, out var existing);
            if (!existing) _ = LoadSessionAsync(session);
            ShowDocument(session);
        }
    }

    private async Task LoadSessionAsync(DocumentSession session)
    {
        var progress = new Progress<SpatialViewer.Core.ImportProgress>(_ => { });
        await session.LoadAsync(_importer, progress);
        if (session.State == DocumentSessionState.Ready) await _recentFiles.RecordAsync(session.FilePath);
        if (ReferenceEquals(_workspace.ActiveDocument, session)) ShowDocument(session);
    }

    private void ShowDocument(DocumentSession session)
    {
        _workspace.Activate(session);
        HomeTab.IsEnabled = true;
        var view = new CadViewerView(session);
        MainContent.Content = view;
    }

    private void CloseSession(DocumentSession session)
    {
        _workspace.Close(session);
        if (_workspace.ActiveDocument is { } active) ShowDocument(active); else ShowHome();
    }

    private async Task PickAndOpenAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".dwg"); picker.FileTypeFilter.Add(".dxf");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        var files = await picker.PickMultipleFilesAsync();
        await OpenFilesAsync(files.Select(file => file.Path));
    }

    private async void OpenFile_Click(object sender, RoutedEventArgs e) => await PickAndOpenAsync();

    private void HomeTab_Click(object sender, RoutedEventArgs e) => ShowHome();
    private void DocumentTab_Click(object sender, RoutedEventArgs e) { if (((FrameworkElement)sender).Tag is DocumentSession session) ShowDocument(session); }
    private void CloseDocument_Click(object sender, RoutedEventArgs e) { if (((FrameworkElement)sender).Tag is DocumentSession session) CloseSession(session); }
    private void Projects_Click(object sender, RoutedEventArgs e) => ShowPlaceholder("项目", "项目工作流将在后续版本提供。");
    private void Favorites_Click(object sender, RoutedEventArgs e) => ShowPlaceholder("收藏", "收藏夹将在后续版本提供。");
    private void ImportFolder_Click(object sender, RoutedEventArgs e) => ShowPlaceholder("导入文件夹", "文件夹导入将在后续版本提供。");
    private void About_Click(object sender, RoutedEventArgs e) => ShowPlaceholder("关于图览", "Spatial Viewer Preview 0.1\nDWG / DXF 查看器");
    private void Settings_Click(object sender, RoutedEventArgs e) => MainContent.Content = new SettingsView();
    private void ShowPlaceholder(string title, string message) { HomeTab.IsEnabled = true; MainContent.Content = new PlaceholderView(title, message); }
    private async Task ShowInfoAsync(string message) { var dialog = new ContentDialog { Title = "Spatial Viewer Preview", Content = message, CloseButtonText = "关闭", XamlRoot = Content.XamlRoot }; await dialog.ShowAsync(); }
    private async void RootGrid_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        var controlDown = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (controlDown && e.Key == Windows.System.VirtualKey.O) { e.Handled = true; await PickAndOpenAsync(); return; }
        if (controlDown && e.Key == Windows.System.VirtualKey.W && _workspace.ActiveDocument is { } active) { e.Handled = true; CloseSession(active); return; }
        if (controlDown && e.Key == Windows.System.VirtualKey.Tab && _workspace.Documents.Count > 1)
        {
            var index = _workspace.Documents.IndexOf(_workspace.ActiveDocument!);
            ShowDocument(_workspace.Documents[(index + 1) % _workspace.Documents.Count]);
            e.Handled = true;
        }
    }
}
