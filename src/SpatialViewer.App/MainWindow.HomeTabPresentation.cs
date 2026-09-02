using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SpatialViewer.Product.Views;

namespace SpatialViewer.Product;

public sealed partial class MainWindow
{
    private bool _homeTabPresentationTrackingEnabled;
    private long _homeTabContentCallbackToken;

    private void EnsureHomeTabPresentationTracking()
    {
        if (_homeTabPresentationTrackingEnabled) return;
        _homeTabPresentationTrackingEnabled = true;
        _homeTabContentCallbackToken = MainContent.RegisterPropertyChangedCallback(
            ContentControl.ContentProperty,
            MainContent_ContentChanged);
        Closed += MainWindow_HomeTabPresentationClosed;
        UpdateSelectedHomeTabPresentation(MainContent.Content);
    }

    private void MainWindow_HomeTabPresentationClosed(object sender, WindowEventArgs e)
    {
        if (!_homeTabPresentationTrackingEnabled) return;
        MainContent.UnregisterPropertyChangedCallback(ContentControl.ContentProperty, _homeTabContentCallbackToken);
        _homeTabPresentationTrackingEnabled = false;
        Closed -= MainWindow_HomeTabPresentationClosed;
    }

    private void MainContent_ContentChanged(DependencyObject sender, DependencyProperty dependencyProperty)
    {
        if (sender is ContentControl contentControl)
            UpdateSelectedHomeTabPresentation(contentControl.Content);
    }

    private void UpdateSelectedHomeTabPresentation(object? content)
    {
        if (_selectedTab is not string homeId || !_homeTabs.TryGetValue(homeId, out var visual)) return;

        var (title, glyph) = content switch
        {
            ProjectsView => (T("Nav_Projects"), "\uE8F1"),
            FavoritesView => (T("Nav_Favorites"), "\uE734"),
            AboutView => (T("Nav_About"), "\uE897"),
            SettingsView => (T("Nav_Settings"), "\uE713"),
            _ => (T("Nav_Home"), "\uE80F")
        };

        visual.HeaderText.Text = title;
        if (FindDescendant<FontIcon>(visual.Container) is { } icon)
            icon.Glyph = glyph;

        // The page identity shown on the tab and its hover card must always match.
        // Replacing only the tooltip content preserves the user's approved hover
        // geometry and CAD preview renderer.
        ToolTipService.SetToolTip(visual.Container, CreateTabPreviewToolTip(homeId, title));
    }
}
