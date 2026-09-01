using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using SpatialViewer.Presentation;
using SpatialViewer.Product.Views;

namespace SpatialViewer.Product;

/// <summary>
/// Window-level localization coordinator. This intentionally follows the
/// UrbanPlanToolbox model: the localization service switches MRT/culture and
/// raises LanguageChanged; the shell then discards localized view instances,
/// recreates them, and restores the user's current navigation/document state.
/// Individual pages must not try to walk the visual tree and hot-patch labels.
/// </summary>
public sealed partial class MainWindow
{
    private readonly AppLocalizationService _shellLocalization = AppLocalizationService.Default;
    private bool _localizationShellHooked;

    private void LocalizationShell_Loaded(object sender, RoutedEventArgs e)
    {
        if (_localizationShellHooked) return;
        _localizationShellHooked = true;
        Title = "SpatialViewer";
        _shellLocalization.LanguageChanged += ShellLocalization_LanguageChanged;
        ShellNewTabButton.Click += ShellNewTabButton_LocalizationClick;
        Closed += LocalizationWindow_Closed;
        ApplyLocalizedShellText();
    }

    private void LocalizationWindow_Closed(object sender, WindowEventArgs args)
    {
        _shellLocalization.LanguageChanged -= ShellLocalization_LanguageChanged;
        ShellNewTabButton.Click -= ShellNewTabButton_LocalizationClick;
        Closed -= LocalizationWindow_Closed;
    }

    private void ShellLocalization_LanguageChanged(object? sender, AppLanguageChangedEventArgs e) =>
        DispatcherQueue.TryEnqueue(ReloadLocalizedShell);

    private void ShellNewTabButton_LocalizationClick(object sender, RoutedEventArgs e) =>
        DispatcherQueue.TryEnqueue(ApplyLocalizedHomeTabHeaders);

    private void ReloadLocalizedShell()
    {
        var visibleSurface = MainContent.Content switch
        {
            _ when IsDocumentSurfaceVisible => LocalizedSurface.Document,
            CadViewerView => LocalizedSurface.Document,
            HomeView => LocalizedSurface.Home,
            SettingsView => LocalizedSurface.Settings,
            AboutView => LocalizedSurface.About,
            ProjectsView => LocalizedSurface.Library,
            FavoritesView => LocalizedSurface.Library,
            PlaceholderView => LocalizedSurface.Placeholder,
            _ => LocalizedSurface.Home
        };
        var selectedTab = _selectedTab;
        var navigationTag = (ShellNavigation.SelectedItem as NavigationViewItem)?.Tag?.ToString();

        // Cached document views intentionally survive ordinary tab switches, but
        // localization must recreate them so x:Uid and runtime-localized labels
        // resolve in the newly selected language.
        ResetDocumentViewsForLocalization();
        MainContent.Content = null;

        // Home pages are cached per tab. Recreating every cached page is the
        // equivalent of UrbanPlanToolbox re-navigating MainPage after a language
        // switch: every x:Uid and constructor-localized value is resolved again.
        foreach (var cachedHomeId in _homeTabs.Keys.ToArray())
            _homeViews[cachedHomeId] = CreateHomeView();

        ApplyLocalizedShellText();

        if (visibleSurface == LocalizedSurface.Document && selectedTab is DocumentSession session && _documentTabs.ContainsKey(session))
        {
            ShowDocument(session);
            return;
        }

        if (visibleSurface == LocalizedSurface.Home && selectedTab is string selectedHomeId && _homeTabs.ContainsKey(selectedHomeId))
        {
            ShowHome(selectedHomeId);
            return;
        }

        switch (navigationTag)
        {
            case "Projects":
                ShowProjects();
                break;
            case "Favorites":
                ShowFavorites();
                break;
            case "ImportFolder":
                ShowProjects();
                break;
            case "Settings":
                ShowSettings();
                break;
            case "About":
                ShowAbout();
                break;
            default:
                ShowHome(selectedTab as string);
                break;
        }
    }

    private void ApplyLocalizedShellText()
    {
        HomeNav.Content = T("Nav_Home");
        ProjectsNav.Content = T("Nav_Projects");
        FavoritesNav.Content = T("Nav_Favorites");
        ImportFolderNav.Content = T("Nav_ImportFolder");
        AboutNav.Content = T("Nav_About");
        SettingsNav.Content = T("Nav_Settings");
        AutomationProperties.SetName(ShellNewTabButton, T("Shell_NewHomeTab"));
        ApplyLocalizedHomeTabHeaders();
    }

    private void ApplyLocalizedHomeTabHeaders()
    {
        var title = T("Nav_Home");
        foreach (var tab in _homeTabs.Values) tab.HeaderText.Text = title;
    }

    private string T(string key) => _shellLocalization.GetString(key);

    private enum LocalizedSurface
    {
        Home,
        Document,
        Settings,
        About,
        Library,
        Placeholder
    }
}
