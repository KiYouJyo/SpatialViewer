using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SpatialViewer.Product.Views;

namespace SpatialViewer.Product;

public sealed partial class MainWindow
{
    private readonly Dictionary<string, ShellPageKind> _homeTabRoutes = new(StringComparer.Ordinal);
    private bool _homeTabPresentationTrackingEnabled;
    private long _homeTabContentCallbackToken;
    private ShellPageKind? _pendingNewHomeTabRoute;
    private bool _restoringHomeTabRoute;

    private void EnsureHomeTabPresentationTracking()
    {
        if (_homeTabPresentationTrackingEnabled) return;
        _homeTabPresentationTrackingEnabled = true;
        _homeTabContentCallbackToken = MainContent.RegisterPropertyChangedCallback(
            ContentControl.ContentProperty,
            MainContent_ContentChanged);
        ShellNewTabButton.PointerPressed += ShellNewTabButton_RoutePointerPressed;
        ShellNewTabButton.Click += ShellNewTabButton_RouteClick;
        Closed += MainWindow_HomeTabPresentationClosed;

        foreach (var homeId in _homeTabs.Keys)
            _homeTabRoutes.TryAdd(homeId, ShellPageKind.Home);
        UpdateSelectedHomeTabPresentation(MainContent.Content, rememberRoute: true);
    }

    private void MainWindow_HomeTabPresentationClosed(object sender, WindowEventArgs e)
    {
        if (!_homeTabPresentationTrackingEnabled) return;
        MainContent.UnregisterPropertyChangedCallback(ContentControl.ContentProperty, _homeTabContentCallbackToken);
        ShellNewTabButton.PointerPressed -= ShellNewTabButton_RoutePointerPressed;
        ShellNewTabButton.Click -= ShellNewTabButton_RouteClick;
        _homeTabPresentationTrackingEnabled = false;
        Closed -= MainWindow_HomeTabPresentationClosed;
    }

    private void RememberSelectedHomeTabRoute(string navigationTag)
    {
        if (_selectedTab is not string homeId || !_homeTabs.ContainsKey(homeId)) return;
        var route = navigationTag switch
        {
            "Projects" => ShellPageKind.Projects,
            "Favorites" => ShellPageKind.Favorites,
            "About" => ShellPageKind.About,
            "Settings" => ShellPageKind.Settings,
            "ImportFolder" => ShellPageKind.Projects,
            _ => ShellPageKind.Home
        };
        _homeTabRoutes[homeId] = route;
        ApplyHomeTabPresentation(homeId, route);
    }

    private void MainContent_ContentChanged(DependencyObject sender, DependencyProperty dependencyProperty)
    {
        if (sender is not ContentControl contentControl || _selectedTab is not string homeId || !_homeTabs.ContainsKey(homeId)) return;

        var incomingRoute = GetShellPageKind(contentControl.Content);
        if (!_restoringHomeTabRoute
            && _homeTabRoutes.TryGetValue(homeId, out var storedRoute)
            && storedRoute != incomingRoute)
        {
            // Selecting an existing shell tab still passes through the legacy
            // ShowHome(homeId) path. Do not let that temporary HomeView erase the
            // page that belongs to the tab; restore the stored route immediately.
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => RestoreHomeTabRoute(homeId, storedRoute));
            return;
        }

        UpdateSelectedHomeTabPresentation(contentControl.Content, rememberRoute: true);
    }

    private void ShellNewTabButton_RoutePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _pendingNewHomeTabRoute = _selectedTab is string homeId && _homeTabRoutes.TryGetValue(homeId, out var route)
            ? route
            : GetShellPageKind(MainContent.Content);
    }

    private void ShellNewTabButton_RouteClick(object sender, RoutedEventArgs e)
    {
        var route = _pendingNewHomeTabRoute;
        _pendingNewHomeTabRoute = null;
        if (route is null || _selectedTab is not string newHomeId || !_homeTabs.ContainsKey(newHomeId)) return;

        _homeTabRoutes[newHomeId] = route.Value;
        ApplyHomeTabPresentation(newHomeId, route.Value);
        if (route.Value != ShellPageKind.Home)
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => RestoreHomeTabRoute(newHomeId, route.Value));
    }

    private void RestoreHomeTabRoute(string homeId, ShellPageKind route)
    {
        if (_selectedTab is not string selectedHomeId
            || !string.Equals(selectedHomeId, homeId, StringComparison.Ordinal)
            || !_homeTabs.ContainsKey(homeId)
            || !_homeTabRoutes.TryGetValue(homeId, out var storedRoute)
            || storedRoute != route
            || GetShellPageKind(MainContent.Content) == route)
            return;

        _restoringHomeTabRoute = true;
        try
        {
            switch (route)
            {
                case ShellPageKind.Projects:
                    SelectShellItem(ProjectsNav);
                    ShowProjects();
                    break;
                case ShellPageKind.Favorites:
                    SelectShellItem(FavoritesNav);
                    ShowFavorites();
                    break;
                case ShellPageKind.About:
                    SelectShellItem(AboutNav);
                    ShowAbout();
                    break;
                case ShellPageKind.Settings:
                    SelectShellItem(SettingsNav);
                    ShowSettings();
                    break;
                default:
                    SelectShellItem(HomeNav);
                    ShowHome(homeId);
                    break;
            }
        }
        finally
        {
            _restoringHomeTabRoute = false;
        }
        UpdateSelectedHomeTabPresentation(MainContent.Content, rememberRoute: false);
    }

    private void UpdateSelectedHomeTabPresentation(object? content, bool rememberRoute)
    {
        if (_selectedTab is not string homeId || !_homeTabs.ContainsKey(homeId)) return;
        var route = GetShellPageKind(content);
        if (rememberRoute) _homeTabRoutes[homeId] = route;
        ApplyHomeTabPresentation(homeId, rememberRoute ? route : _homeTabRoutes.GetValueOrDefault(homeId, route));
    }

    private void RefreshHomeTabPresentationsForLocalization()
    {
        foreach (var homeId in _homeTabs.Keys)
            ApplyHomeTabPresentation(homeId, _homeTabRoutes.GetValueOrDefault(homeId, ShellPageKind.Home));
    }

    private void ApplyHomeTabPresentation(string homeId, ShellPageKind route)
    {
        if (!_homeTabs.TryGetValue(homeId, out var visual)) return;
        var (title, glyph) = route switch
        {
            ShellPageKind.Projects => (T("Nav_Projects"), "\uE8F1"),
            ShellPageKind.Favorites => (T("Nav_Favorites"), "\uE734"),
            ShellPageKind.About => (T("Nav_About"), "\uE897"),
            ShellPageKind.Settings => (T("Nav_Settings"), "\uE713"),
            _ => (T("Nav_Home"), "\uE80F")
        };

        visual.HeaderText.Text = title;
        if (FindDescendant<FontIcon>(visual.Container) is { } icon)
            icon.Glyph = glyph;

        // Shell/home tabs deliberately do not own a hover preview. Document tabs
        // are the only tabs with a preview surface.
        ToolTipService.SetToolTip(visual.Container, null);
    }

    private static ShellPageKind GetShellPageKind(object? content) => content switch
    {
        ProjectsView => ShellPageKind.Projects,
        FavoritesView => ShellPageKind.Favorites,
        AboutView => ShellPageKind.About,
        SettingsView => ShellPageKind.Settings,
        _ => ShellPageKind.Home
    };

    private enum ShellPageKind
    {
        Home,
        Projects,
        Favorites,
        About,
        Settings
    }
}
