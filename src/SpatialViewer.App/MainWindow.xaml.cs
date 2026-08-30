using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using SpatialViewer.Formats.Cad.ACadSharp;
using SpatialViewer.Presentation;
using SpatialViewer.Product.Views;
using Windows.Storage.Pickers;
using Windows.UI;
using System.Text.Json;

namespace SpatialViewer.Product;

public sealed partial class MainWindow : Window
{
    private readonly DocumentWorkspace _workspace = new();
    private readonly ACadSharpCadImporter _importer = new();
    private readonly RecentFilesService _recentFiles;
    private readonly Dictionary<string, ShellTabVisual> _homeTabs = new(StringComparer.Ordinal);
    private readonly Dictionary<DocumentSession, ShellTabVisual> _documentTabs = new();
    private object? _selectedTab;
    private bool _suppressShellSelection;
    private ResponsiveLayoutMode _responsiveMode = ResponsiveLayoutMode.Large;
    private bool _responsiveLayoutApplied;
    private bool _restoringWindowState;
    private static readonly string WindowStatePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpatialViewer", "window-state.json");

    public MainWindow()
    {
        InitializeComponent();
        Title = "Spatial Viewer Preview";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.TitleBar.ButtonBackgroundColor = Color.FromArgb(0, 0, 0, 0);
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Color.FromArgb(0, 0, 0, 0);
        AppWindow.TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(32, 255, 255, 255);
        AppWindow.TitleBar.ButtonPressedBackgroundColor = Color.FromArgb(48, 255, 255, 255);
        AppWindow.TitleBar.ButtonHoverForegroundColor = Color.FromArgb(255, 255, 255, 255);
        RestoreWindowSize();
        AppWindow.Changed += AppWindow_Changed;
        _recentFiles = new RecentFilesService(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpatialViewer", "recent-files.json"));
        Closed += (_, _) => { PersistWindowSize(); _workspace.CloseAll(); };
        RootGrid.Loaded += RootGrid_Loaded;
        CreateHomeTab(select: true);
    }

    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyResponsiveLayout(force: true);
        DispatcherQueue.TryEnqueue(() =>
        {
            ApplyResponsiveLayout(force: true);
            ShellNavigation.UpdateLayout();
        });
    }

    private void RestoreWindowSize()
    {
        _restoringWindowState = true;
        try
        {
            var saved = File.Exists(WindowStatePath)
                ? JsonSerializer.Deserialize<SavedWindowSize>(File.ReadAllText(WindowStatePath))
                : null;
            var width = saved is { Width: >= 640 and <= 7680 } ? saved.Width : 1600;
            var height = saved is { Height: >= 480 and <= 4320 } ? saved.Height : 1000;
            AppWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
        }
        catch (JsonException)
        {
            AppWindow.Resize(new Windows.Graphics.SizeInt32(1600, 1000));
        }
        finally
        {
            _restoringWindowState = false;
        }
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidSizeChange) PersistWindowSize();
    }

    private void PersistWindowSize()
    {
        if (_restoringWindowState || AppWindow.Presenter is not OverlappedPresenter { State: OverlappedPresenterState.Restored }) return;
        var size = AppWindow.Size;
        if (size.Width < 640 || size.Height < 480) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(WindowStatePath)!);
            File.WriteAllText(WindowStatePath, JsonSerializer.Serialize(new SavedWindowSize(size.Width, size.Height)));
        }
        catch (IOException)
        {
            // Persisting chrome state must never prevent the window from closing or resizing.
        }
    }

    private string CreateHomeTab(bool select)
    {
        var id = $"home:{Guid.NewGuid():N}";
        _homeTabs.Add(id, CreateTabVisual(id, "主页", Symbol.Home, 220));
        if (select) ShowHome(id);
        return id;
    }

    // This is PageArc's detached tab visual: a 32-DIP Border hosting an
    // independent selection surface and a separate overlay close hit target.
    private ShellTabVisual CreateTabVisual(object tag, string title, Symbol symbol, double width)
    {
        var headerText = new TextBlock
        {
            Text = title,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        };
        var icon = new FontIcon
        {
            Glyph = symbol == Symbol.Home ? "\uE80F" : "\uE8F1",
            FontSize = 14,
            Width = 16,
            Height = 16,
            Opacity = 0.68,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var content = new Grid { ColumnSpacing = 6 };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.Children.Add(icon);
        Grid.SetColumn(headerText, 1); content.Children.Add(headerText);

        var selectButton = new Button
        {
            Tag = tag,
            Padding = new Thickness(10, 0, 36, 0),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(7),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = content
        };
        selectButton.Click += ShellTabSelect_Click;

        var closeButton = new Button
        {
            Tag = tag,
            Width = 28,
            Height = 28,
            MinWidth = 28,
            Margin = new Thickness(0, 2, 3, 2),
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(6),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Content = new TextBlock { Text = "×", FontSize = 12, Opacity = 0.68 }
        };
        closeButton.Click += ShellTabClose_Click;

        var layer = new Grid();
        layer.Children.Add(selectButton); layer.Children.Add(closeButton);
        var container = new Border { Tag = tag, Width = width, Height = 32, CornerRadius = new CornerRadius(7), BorderThickness = new Thickness(0), Child = layer };
        container.Transitions = [new EntranceThemeTransition { FromHorizontalOffset = 12 }, new RepositionThemeTransition()];
        ShellTabItems.Children.Add(container);
        return new ShellTabVisual(container, headerText);
    }

    private void ShowHome(string? tabId = null)
    {
        var target = tabId ?? _homeTabs.Keys.FirstOrDefault() ?? CreateHomeTab(select: false);
        ShowNavigationChrome();
        SelectShellItem(HomeNav);
        SelectTab(target);
        var view = new HomeView(_recentFiles);
        view.OpenRequested += async (_, paths) => await OpenFilesAsync(paths);
        view.FilePickerRequested += async (_, _) => await PickAndOpenAsync();
        MainContent.Content = view;
        ApplyResponsiveLayout();
    }

    private async Task OpenFilesAsync(IEnumerable<string> paths)
    {
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!FormatGate.IsSupported(path)) { await ShowInfoAsync(FormatGate.UnsupportedMessage(path)); continue; }
            var session = _workspace.OpenOrFocus(path, out var existing);
            if (!existing) _ = LoadSessionAsync(session);
            ShowDocument(session);
        }
    }

    private async Task LoadSessionAsync(DocumentSession session)
    {
        await session.LoadAsync(_importer, new Progress<SpatialViewer.Core.ImportProgress>(_ => { }));
        if (session.State == DocumentSessionState.Ready) await _recentFiles.RecordAsync(session.FilePath);
        if (ReferenceEquals(_workspace.ActiveDocument, session)) ShowDocument(session);
    }

    private void ShowDocument(DocumentSession session)
    {
        _workspace.Activate(session);
        ShowViewerChrome();
        SelectShellItem(null);
        SelectTab(EnsureDocumentTab(session));
        MainContent.Content = new CadViewerView(session);
    }

    private DocumentSession EnsureDocumentTab(DocumentSession session)
    {
        if (_documentTabs.ContainsKey(session)) return session;
        _documentTabs.Add(session, CreateTabVisual(session, session.DisplayName, Symbol.Page, 220));
        return session;
    }

    private void ShellNewTabButton_Click(object sender, RoutedEventArgs e) => CreateHomeTab(select: true);
    private void ShellTabSelect_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: { } tag }) return;
        if (tag is string homeId && _homeTabs.ContainsKey(homeId)) ShowHome(homeId);
        else if (tag is DocumentSession session) ShowDocument(session);
    }

    private void ShellTabClose_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: { } tag }) return;
        if (tag is DocumentSession session) { CloseSession(session); return; }
        if (tag is string homeId && _homeTabs.Remove(homeId, out var visual))
        {
            ShellTabItems.Children.Remove(visual.Container);
            if (Equals(_selectedTab, homeId))
            {
                if (_documentTabs.Keys.FirstOrDefault() is { } document) ShowDocument(document);
                else if (_homeTabs.Keys.FirstOrDefault() is { } nextHome) ShowHome(nextHome);
                else CreateHomeTab(select: true);
            }
            else RefreshTabVisuals();
        }
    }

    private void CloseSession(DocumentSession session)
    {
        if (_documentTabs.Remove(session, out var tab)) ShellTabItems.Children.Remove(tab.Container);
        _workspace.Close(session);
        if (_workspace.ActiveDocument is { } active) ShowDocument(active);
        else if (_homeTabs.Keys.FirstOrDefault() is { } home) ShowHome(home);
        else CreateHomeTab(select: true);
    }

    private void SelectTab(object tag)
    {
        _selectedTab = tag;
        RefreshTabVisuals();
    }

    private void RefreshTabVisuals()
    {
        var dark = RootGrid.ActualTheme == ElementTheme.Dark;
        foreach (var pair in _homeTabs)
            ApplyTabVisual(pair.Key, pair.Value, Equals(pair.Key, _selectedTab), dark);
        foreach (var pair in _documentTabs)
            ApplyTabVisual(pair.Key, pair.Value, Equals(pair.Key, _selectedTab), dark);
    }

    private static void ApplyTabVisual(object tag, ShellTabVisual visual, bool selected, bool dark)
    {
        visual.Container.Background = new SolidColorBrush(selected
            ? (dark ? ColorHelper.FromArgb(30, 255, 255, 255) : ColorHelper.FromArgb(214, 255, 255, 255))
            : (dark ? ColorHelper.FromArgb(10, 255, 255, 255) : ColorHelper.FromArgb(8, 0, 0, 0)));
        visual.Container.BorderThickness = new Thickness(selected ? 1 : 0);
        visual.Container.BorderBrush = new SolidColorBrush(dark ? ColorHelper.FromArgb(38, 255, 255, 255) : ColorHelper.FromArgb(51, 117, 117, 117));
        visual.HeaderText.FontWeight = selected ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        visual.HeaderText.Opacity = selected ? 0.9 : 0.68;
    }

    private async Task PickAndOpenAsync()
    {
        var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".dwg"); picker.FileTypeFilter.Add(".dxf");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        var files = await picker.PickMultipleFilesAsync();
        await OpenFilesAsync(files.Select(file => file.Path));
    }

    private void ShellNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_suppressShellSelection || args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag) return;
        switch (tag)
        {
            case "Home": ShowHome(); break;
            case "Projects": ShowPlaceholder("项目", "项目工作流将在后续版本提供。"); break;
            case "Favorites": ShowPlaceholder("收藏", "收藏夹将在后续版本提供。"); break;
            case "ImportFolder": ShowPlaceholder("导入文件夹", "文件夹导入将在后续版本提供。"); break;
            case "Settings": ShowSettings(); break;
            case "About": ShowPlaceholder("关于图览", "SpatialViewer · WinUI 3 CAD Viewer"); break;
        }
    }

    private void ShowSettings() { ShowNavigationChrome(); MainContent.Content = new SettingsView(); }
    private void ShowPlaceholder(string title, string message) { ShowNavigationChrome(); MainContent.Content = new PlaceholderView(title, message); }

    private void SelectShellItem(NavigationViewItem? item)
    {
        if (ReferenceEquals(ShellNavigation.SelectedItem, item)) return;
        _suppressShellSelection = true; ShellNavigation.SelectedItem = item; _suppressShellSelection = false;
    }

    private void ShowNavigationChrome()
    {
        var small = _responsiveMode == ResponsiveLayoutMode.Small;
        var displayMode = small ? NavigationViewPaneDisplayMode.LeftMinimal : NavigationViewPaneDisplayMode.Left;
        var paneOpen = !small;
        if (ShellNavigation.PaneDisplayMode != displayMode) ShellNavigation.PaneDisplayMode = displayMode;
        var compactPaneLength = small ? 64 : 52;
        if (ShellNavigation.CompactPaneLength != compactPaneLength) ShellNavigation.CompactPaneLength = compactPaneLength;
        if (!ShellNavigation.IsPaneToggleButtonVisible) ShellNavigation.IsPaneToggleButtonVisible = true;
        if (ShellNavigation.IsPaneOpen != paneOpen) ShellNavigation.IsPaneOpen = paneOpen;
    }

    private void ShowViewerChrome()
    {
        ShellNavigation.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftMinimal;
        ShellNavigation.CompactPaneLength = 0;
        ShellNavigation.IsPaneOpen = false;
        ShellNavigation.IsPaneToggleButtonVisible = false;
    }

    private async Task ShowInfoAsync(string message)
    {
        var dialog = new ContentDialog { Title = "Spatial Viewer Preview", Content = message, CloseButtonText = "关闭", XamlRoot = Content.XamlRoot };
        await dialog.ShowAsync();
    }

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyResponsiveLayout();
    private void ApplyResponsiveLayout(bool force = false)
    {
        var logicalWidth = Content.XamlRoot?.Size.Width ?? RootGrid.ActualWidth;
        if (logicalWidth <= 0) return;
        var mode = logicalWidth >= 1280 ? ResponsiveLayoutMode.Large : logicalWidth >= 760 ? ResponsiveLayoutMode.Medium : ResponsiveLayoutMode.Small;
        if (!force && _responsiveLayoutApplied && mode == _responsiveMode) return;
        _responsiveMode = mode;
        _responsiveLayoutApplied = true;
        if (MainContent.Content is CadViewerView) return;
        ShowNavigationChrome();
        if (MainContent.Content is HomeView home) home.SetResponsiveMode(_responsiveMode);
    }

    private async void RootGrid_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        var controlDown = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (controlDown && e.Key == Windows.System.VirtualKey.O) { e.Handled = true; await PickAndOpenAsync(); }
        else if (controlDown && e.Key == Windows.System.VirtualKey.W && _workspace.ActiveDocument is { } active) { e.Handled = true; CloseSession(active); }
    }
}

internal sealed record ShellTabVisual(Border Container, TextBlock HeaderText);
internal sealed record SavedWindowSize(int Width, int Height);
