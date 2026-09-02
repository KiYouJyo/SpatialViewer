using Microsoft.UI;
using Microsoft.UI.Dispatching;
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
using Windows.Graphics;
using Windows.UI;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SpatialViewer.Product;

public sealed partial class MainWindow : Window
{
    private readonly DocumentWorkspace _workspace = new();
    private readonly ACadSharpCadImporter _importer = new();
    private readonly RecentFilesService _recentFiles;
    private readonly Dictionary<string, ShellTabVisual> _homeTabs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HomeView> _homeViews = new(StringComparer.Ordinal);
    private readonly Dictionary<DocumentSession, ShellTabVisual> _documentTabs = new();
    private readonly Dictionary<DocumentSession, CadViewerView> _documentViews = new();
    private readonly Grid _documentViewHost = new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch
    };
    private object? _selectedTab;
    private ResponsiveLayoutMode _responsiveMode = ResponsiveLayoutMode.Large;
    private bool _responsiveLayoutApplied;
    private bool _shellReady;
    private bool _restoringWindowState;
    private bool _sessionRestoreAttempted;
    private SizeInt32 _lastNormalWindowSize;
    private bool _wasWindowMaximized;
    private SplitView? _navigationSplitView;
    private bool _navigationPaneBackgroundHooked;
    private bool _navigationChromeHiddenForViewer;
    private static readonly string WindowStatePath = GetWindowStatePath();

    public MainWindow()
    {
        InitializeComponent();
        Title = "Spatial Viewer Preview";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        RootGrid.RequestedTheme = ThemePreferenceStore.Load();
        UpdateTitleBarColors();
        RestoreWindowSize();
        AppWindow.Changed += AppWindow_Changed;
        _recentFiles = new RecentFilesService(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpatialViewer", "recent-files.json"));
        Closed += OnWindowClosed;
        RootGrid.ActualThemeChanged += (_, _) =>
        {
            UpdateTitleBarColors();
            RefreshTabVisuals();
            QueueNavigationPaneBackgroundUpdate();
        };
        CreateHomeTab(select: true);
    }

    // Keep the NavigationView lifecycle identical to UrbanPlanToolbox: allow the
    // native Auto mode to choose the pane, then adapt only the page content.
    private async void ShellNavigation_Loaded(object sender, RoutedEventArgs e)
    {
        HookNavigationPaneBackground();
        _shellReady = true;
        ApplyResponsiveLayout(force: true);
        // NavigationView.Auto completes its first measure after Loaded. Recheck
        // only the page grid on the next dispatcher turn; unlike the old code,
        // this never changes PaneDisplayMode or IsPaneOpen.
        DispatcherQueue.TryEnqueue(() => ApplyResponsiveLayout(force: true));
        if (!_sessionRestoreAttempted)
        {
            _sessionRestoreAttempted = true;
            await RestoreLastSessionAsync();
        }
    }

    private async Task RestoreLastSessionAsync()
    {
        if (!AppSettingsStore.Current.RestoreLastSession) return;
        var files = SessionStateStore.Load();
        if (files.Count > 0) await OpenFilesAsync(files);
    }

    private static string GetWindowStatePath()
    {
        // Isolate debug/test artefacts from the installed app and other output
        // directories. Previously all executables raced on window-state.json.
        var identity = Path.GetFullPath(Environment.ProcessPath ?? AppContext.BaseDirectory);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..16];
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpatialViewer",
            $"window-state-{hash}.json");
    }

    private void RestoreWindowSize()
    {
        _restoringWindowState = true;
        try
        {
            var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
            var saved = File.Exists(WindowStatePath)
                ? JsonSerializer.Deserialize<SavedWindowSize>(File.ReadAllText(WindowStatePath))
                : null;
            var placement = saved is { Width: >= 320, Height: >= 240 }
                ? new WindowPlacement(saved.Width, saved.Height, saved.WasMaximized)
                : WindowPlacement.CreateDefault(workArea);
            placement = WindowPlacement.ClampToWorkArea(placement, workArea);
            _lastNormalWindowSize = new SizeInt32(placement.Width, placement.Height);
            _wasWindowMaximized = placement.WasMaximized;
            AppWindow.Resize(_lastNormalWindowSize);
            if (_wasWindowMaximized && AppWindow.Presenter is OverlappedPresenter presenter) presenter.Maximize();
            else if (saved is null) CenterWindow(_lastNormalWindowSize, workArea);
        }
        catch (Exception) when (File.Exists(WindowStatePath))
        {
            var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
            _lastNormalWindowSize = WindowPlacement.CreateDefault(workArea).ToSize();
            _wasWindowMaximized = false;
            AppWindow.Resize(_lastNormalWindowSize);
            CenterWindow(_lastNormalWindowSize, workArea);
        }
        finally
        {
            _restoringWindowState = false;
        }
    }

    private void CenterWindow(SizeInt32 size, RectInt32 workArea)
    {
        var x = workArea.X + Math.Max(0, (workArea.Width - size.Width) / 2);
        var y = workArea.Y + Math.Max(0, (workArea.Height - size.Height) / 2);
        AppWindow.Move(new PointInt32(x, y));
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (_restoringWindowState || AppWindow.Presenter is not OverlappedPresenter presenter) return;
        switch (presenter.State)
        {
            case OverlappedPresenterState.Maximized:
                _wasWindowMaximized = true;
                break;
            case OverlappedPresenterState.Restored:
                _wasWindowMaximized = false;
                if (args.DidSizeChange) _lastNormalWindowSize = AppWindow.Size;
                break;
        }
    }

    private void PersistWindowSize()
    {
        if (_restoringWindowState) return;
        if (_lastNormalWindowSize.Width < 320 || _lastNormalWindowSize.Height < 240) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(WindowStatePath)!);
            var temporaryPath = $"{WindowStatePath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(new SavedWindowSize(_lastNormalWindowSize.Width, _lastNormalWindowSize.Height, _wasWindowMaximized)));
            File.Move(temporaryPath, WindowStatePath, overwrite: true);
        }
        catch (IOException)
        {
            // Persisting chrome state must never prevent the window from closing or resizing.
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs e)
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            _wasWindowMaximized = presenter.State == OverlappedPresenterState.Maximized;
            if (presenter.State == OverlappedPresenterState.Restored) _lastNormalWindowSize = AppWindow.Size;
        }

        PersistWindowSize();
        SessionStateStore.Save(_workspace.Documents.Select(document => document.FilePath));
        AppWindow.Changed -= AppWindow_Changed;
        DisposeDocumentViews();
        _workspace.CloseAll();
    }

    private string CreateHomeTab(bool select)
    {
        var id = $"home:{Guid.NewGuid():N}";
        _homeTabs.Add(id, CreateTabVisual(id, T("Nav_Home"), Symbol.Home, 220));
        _homeViews.Add(id, CreateHomeView());
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
        container.Transitions = [new RepositionThemeTransition()];
        var visual = new ShellTabVisual(container, headerText);
        ApplyTabVisual(tag, visual, selected: false, RootGrid.ActualTheme == ElementTheme.Dark);
        ShellTabItems.Children.Add(container);
        ConfigureTabInteractions(container, tag, title, width);
        return visual;
    }

    private void ShowHome(string? tabId = null)
    {
        var target = tabId ?? _homeTabs.Keys.FirstOrDefault() ?? CreateHomeTab(select: false);
        ShowNavigationChrome();
        // NavigationView already selects its item before ItemInvoked. Writing
        // the same selection again reopens a minimal pane, so only restore a
        // selection when returning from the viewer where none is selected.
        if (IsDocumentSurfaceVisible) SelectShellItem(HomeNav);
        SelectTab(target);
        var view = _homeViews[target];
        MainContent.Content = view;
        view.SetResponsiveMode(_responsiveMode);
    }

    private HomeView CreateHomeView()
    {
        var view = new HomeView(_recentFiles);
        view.OpenRequested += async (_, paths) => await OpenFilesAsync(paths);
        view.FilePickerRequested += async (_, _) => await PickAndOpenAsync();
        return view;
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
        if (session.State == DocumentSessionState.Ready && AppSettingsStore.Current.RecordRecentFiles)
            await _recentFiles.RecordAsync(session.FilePath);
        if (_documentViews.TryGetValue(session, out var view)) view.RefreshSessionState();
        if (ReferenceEquals(_workspace.ActiveDocument, session)) ShowDocument(session);
    }

    private void ShowDocument(DocumentSession session)
    {
        _workspace.Activate(session);
        ShowViewerChrome();
        SelectShellItem(null);
        SelectTab(EnsureDocumentTab(session));

        var activeView = EnsureDocumentView(session);
        foreach (var pair in _documentViews)
            pair.Value.Visibility = ReferenceEquals(pair.Key, session) ? Visibility.Visible : Visibility.Collapsed;

        if (!IsDocumentSurfaceVisible) MainContent.Content = _documentViewHost;
        if (activeView.Visibility != Visibility.Visible) activeView.Visibility = Visibility.Visible;
    }

    private DocumentSession EnsureDocumentTab(DocumentSession session)
    {
        if (_documentTabs.ContainsKey(session)) return session;
        _documentTabs.Add(session, CreateTabVisual(session, session.DisplayName, Symbol.Page, 220));
        return session;
    }

    private CadViewerView EnsureDocumentView(DocumentSession session)
    {
        if (_documentViews.TryGetValue(session, out var existing)) return existing;
        var view = new CadViewerView(session) { Visibility = Visibility.Collapsed };
        _documentViews.Add(session, view);
        _documentViewHost.Children.Add(view);
        return view;
    }

    private bool IsDocumentSurfaceVisible => ReferenceEquals(MainContent.Content, _documentViewHost);

    private void DisposeDocumentView(DocumentSession session)
    {
        if (!_documentViews.Remove(session, out var view)) return;
        _documentViewHost.Children.Remove(view);
        view.Dispose();
    }

    private void DisposeDocumentViews()
    {
        foreach (var view in _documentViews.Values) view.Dispose();
        _documentViews.Clear();
        _documentViewHost.Children.Clear();
    }

    private void ResetDocumentViewsForLocalization()
    {
        if (IsDocumentSurfaceVisible) MainContent.Content = null;
        DisposeDocumentViews();
    }

    private void ShellNewTabButton_Click(object sender, RoutedEventArgs e) => CreateHomeTab(select: true);
    private void ShellTabSelect_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: { } tag } || !IsKnownTab(tag)) return;

        // Paint the tiny selected-state delta first. The potentially heavier page
        // activation runs at low dispatcher priority so rapid clicks coalesce and
        // stale tab activations never rebuild/measure a page the user already left.
        SelectTab(tag);
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (!Equals(_selectedTab, tag) || !IsKnownTab(tag)) return;
            if (tag is string homeId) ShowHome(homeId);
            else if (tag is DocumentSession session) ShowDocument(session);
        });
    }

    private bool IsKnownTab(object tag) =>
        tag is string homeId ? _homeTabs.ContainsKey(homeId) :
        tag is DocumentSession session && _documentTabs.ContainsKey(session);

    private void ShellTabClose_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: { } tag }) return;
        if (tag is DocumentSession session) { CloseSession(session); return; }
        if (tag is string homeId && _homeTabs.Remove(homeId, out var visual))
        {
            _homeViews.Remove(homeId);
            ShellTabItems.Children.Remove(visual.Container);
            if (Equals(_selectedTab, homeId))
            {
                if (_documentTabs.Keys.FirstOrDefault() is { } document) ShowDocument(document);
                else if (_homeTabs.Keys.FirstOrDefault() is { } nextHome) ShowHome(nextHome);
                else CreateHomeTab(select: true);
            }
        }
    }

    private void CloseSession(DocumentSession session)
    {
        if (_documentTabs.Remove(session, out var tab)) ShellTabItems.Children.Remove(tab.Container);
        DisposeDocumentView(session);
        _workspace.Close(session);
        if (_workspace.ActiveDocument is { } active) ShowDocument(active);
        else if (_homeTabs.Keys.FirstOrDefault() is { } home) ShowHome(home);
        else CreateHomeTab(select: true);
    }

    private void SelectTab(object tag)
    {
        if (Equals(_selectedTab, tag)) return;
        var previous = _selectedTab;
        _selectedTab = tag;
        var dark = RootGrid.ActualTheme == ElementTheme.Dark;
        if (previous is not null && TryGetTabVisual(previous, out var previousVisual))
            ApplyTabVisual(previous, previousVisual, selected: false, dark);
        if (TryGetTabVisual(tag, out var selectedVisual))
            ApplyTabVisual(tag, selectedVisual, selected: true, dark);
    }

    private bool TryGetTabVisual(object tag, out ShellTabVisual visual)
    {
        if (tag is string homeId && _homeTabs.TryGetValue(homeId, out var homeVisual))
        {
            visual = homeVisual;
            return true;
        }
        if (tag is DocumentSession session && _documentTabs.TryGetValue(session, out var documentVisual))
        {
            visual = documentVisual;
            return true;
        }
        visual = null!;
        return false;
    }

    private void RefreshTabVisuals()
    {
        var dark = RootGrid.ActualTheme == ElementTheme.Dark;
        foreach (var pair in _homeTabs)
            ApplyTabVisual(pair.Key, pair.Value, Equals(pair.Key, _selectedTab), dark);
        foreach (var pair in _documentTabs)
            ApplyTabVisual(pair.Key, pair.Value, Equals(pair.Key, _selectedTab), dark);
    }

    private void UpdateTitleBarColors()
    {
        var dark = RootGrid.ActualTheme == ElementTheme.Dark;
        if (AppWindowTitleBar.IsCustomizationSupported())
            AppWindow.TitleBar.PreferredTheme = dark ? TitleBarTheme.Dark : TitleBarTheme.Light;
        var foreground = dark ? Color.FromArgb(255, 240, 245, 245) : Color.FromArgb(255, 21, 32, 32);
        var hoverBackground = dark ? Color.FromArgb(32, 255, 255, 255) : Color.FromArgb(24, 0, 0, 0);
        var pressedBackground = dark ? Color.FromArgb(48, 255, 255, 255) : Color.FromArgb(40, 0, 0, 0);
        AppWindow.TitleBar.ButtonForegroundColor = foreground;
        AppWindow.TitleBar.ButtonInactiveForegroundColor = foreground;
        AppWindow.TitleBar.ButtonHoverForegroundColor = foreground;
        AppWindow.TitleBar.ButtonPressedForegroundColor = foreground;
        AppWindow.TitleBar.ButtonBackgroundColor = Color.FromArgb(0, 0, 0, 0);
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Color.FromArgb(0, 0, 0, 0);
        AppWindow.TitleBar.ButtonHoverBackgroundColor = hoverBackground;
        AppWindow.TitleBar.ButtonPressedBackgroundColor = pressedBackground;
    }

    private static void ApplyTabVisual(object tag, ShellTabVisual visual, bool selected, bool dark)
    {
        visual.Container.Background = new SolidColorBrush(selected
            ? (dark ? ColorHelper.FromArgb(30, 255, 255, 255) : ColorHelper.FromArgb(214, 255, 255, 255))
            : (dark ? ColorHelper.FromArgb(10, 255, 255, 255) : ColorHelper.FromArgb(8, 0, 0, 0)));
        visual.Container.BorderThickness = new Thickness(selected ? 1 : 0);
        visual.Container.BorderBrush = new SolidColorBrush(dark ? ColorHelper.FromArgb(38, 255, 255, 255) : ColorHelper.FromArgb(51, 117, 117, 117));
        visual.HeaderText.FontWeight = selected ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        // An available, unselected tab is not a disabled tab. Preserve the
        // detached-tab shape, but leave text at native readable opacity.
        visual.HeaderText.Opacity = 1;
    }

    private async Task PickAndOpenAsync()
    {
        var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".dwg"); picker.FileTypeFilter.Add(".dxf");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        var files = await picker.PickMultipleFilesAsync();
        await OpenFilesAsync(files.Select(file => file.Path));
    }

    private void ShellNavigation_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is not NavigationViewItem item || item.Tag is not string tag) return;
        switch (tag)
        {
            case "Home": ShowHome(); break;
            case "Projects": ShowPlaceholder(T("Placeholder_Projects_Title"), T("Placeholder_Projects_Message")); break;
            case "Favorites": ShowPlaceholder(T("Placeholder_Favorites_Title"), T("Placeholder_Favorites_Message")); break;
            case "ImportFolder": ShowPlaceholder(T("Placeholder_ImportFolder_Title"), T("Placeholder_ImportFolder_Message")); break;
            case "Settings": ShowSettings(); break;
            case "About": ShowAbout(); break;
        }
    }

    private void ShowSettings() { ShowNavigationChrome(); MainContent.Content = new SettingsView(); }
    private void ShowAbout() { ShowNavigationChrome(); MainContent.Content = new AboutView(); }
    private void ShowPlaceholder(string title, string message) { ShowNavigationChrome(); MainContent.Content = new PlaceholderView(title, message); }

    private void SelectShellItem(NavigationViewItem? item)
    {
        if (ReferenceEquals(ShellNavigation.SelectedItem, item)) return;
        ShellNavigation.SelectedItem = item;
    }

    private void ShowNavigationChrome()
    {
        // Ordinary navigation deliberately does nothing here. In particular,
        // selecting Home must not reopen a pane the user collapsed.
        if (!_navigationChromeHiddenForViewer) return;

        ShellNavigation.CompactPaneLength = 48;
        ShellNavigation.IsPaneToggleButtonVisible = true;
        ShellNavigation.PaneDisplayMode = NavigationViewPaneDisplayMode.Auto;
        _navigationChromeHiddenForViewer = false;
        QueueNavigationPaneBackgroundUpdate();
    }

    private void ShowViewerChrome()
    {
        if (_navigationChromeHiddenForViewer) return;
        _navigationChromeHiddenForViewer = true;
        ShellNavigation.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftMinimal;
        ShellNavigation.CompactPaneLength = 0;
        ShellNavigation.IsPaneOpen = false;
        ShellNavigation.IsPaneToggleButtonVisible = false;
    }

    private async Task ShowInfoAsync(string message)
    {
        var dialog = new ContentDialog { Title = "Spatial Viewer Preview", Content = message, CloseButtonText = "OK", XamlRoot = Content.XamlRoot };
        await dialog.ShowAsync();
    }

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_shellReady) ApplyResponsiveLayout();
    }
    private void ApplyResponsiveLayout(bool force = false)
    {
        if (!_shellReady) return;
        // RootGrid is the actual window content and updates synchronously during
        // resize/navigation. XamlRoot.Size can still report the previous window
        // size while the viewer is being replaced by the home page.
        var logicalWidth = RootGrid.ActualWidth;
        if (logicalWidth <= 0) logicalWidth = Content.XamlRoot?.Size.Width ?? 0;
        if (logicalWidth <= 0) return;
        var mode = logicalWidth >= 1280 ? ResponsiveLayoutMode.Large : logicalWidth >= 760 ? ResponsiveLayoutMode.Medium : ResponsiveLayoutMode.Small;
        if (!force && _responsiveLayoutApplied && mode == _responsiveMode) return;
        _responsiveMode = mode;
        _responsiveLayoutApplied = true;
        if (MainContent.Content is HomeView home) home.SetResponsiveMode(_responsiveMode);
    }

    private void HookNavigationPaneBackground()
    {
        if (!_navigationPaneBackgroundHooked)
        {
            _navigationPaneBackgroundHooked = true;
            ShellNavigation.PaneOpening += (_, _) => QueueNavigationPaneBackgroundUpdate();
        }

        QueueNavigationPaneBackgroundUpdate();
    }

    private void QueueNavigationPaneBackgroundUpdate() => DispatcherQueue.TryEnqueue(ApplySharedNavigationPaneBackground);

    private void ApplySharedNavigationPaneBackground()
    {
        _navigationSplitView ??= FindDescendant<SplitView>(ShellNavigation);
        var themeKey = new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast
            ? "HighContrast"
            : RootGrid.ActualTheme == ElementTheme.Dark ? "Dark" : "Light";
        var themeResources = Application.Current.Resources.ThemeDictionaries[themeKey] as ResourceDictionary;
        if (_navigationSplitView is not null)
        {
            if (themeResources?["ShellNavigationPaneBackgroundBrush"] is Brush brush)
                _navigationSplitView.PaneBackground = brush;
        }
    }

    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) return match;
            var descendant = FindDescendant<T>(child);
            if (descendant is not null) return descendant;
        }

        return null;
    }

    private async void RootGrid_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        var controlDown = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (controlDown && e.Key == Windows.System.VirtualKey.O) { e.Handled = true; await PickAndOpenAsync(); }
        else if (controlDown && e.Key == Windows.System.VirtualKey.W && _workspace.ActiveDocument is { } active) { e.Handled = true; CloseSession(active); }
    }
}

internal sealed record ShellTabVisual(Border Container, TextBlock HeaderText);
internal sealed record SavedWindowSize(int Width, int Height, bool WasMaximized = false);

internal sealed record WindowPlacement(int Width, int Height, bool WasMaximized)
{
    public static WindowPlacement CreateDefault(RectInt32 workArea) => new(
        Math.Max(320, (int)Math.Round(workArea.Width * .70)),
        Math.Max(240, (int)Math.Round(workArea.Height * .75)),
        false);

    public static WindowPlacement ClampToWorkArea(WindowPlacement placement, RectInt32 workArea) => placement with
    {
        Width = Math.Clamp(placement.Width, Math.Min(320, workArea.Width), Math.Max(1, workArea.Width)),
        Height = Math.Clamp(placement.Height, Math.Min(240, workArea.Height), Math.Max(1, workArea.Height))
    };

    public SizeInt32 ToSize() => new(Width, Height);
}