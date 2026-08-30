using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SpatialViewer.Product.Views;

public sealed partial class SettingsView : UserControl
{
    private bool _isSynchronizing;

    public SettingsView()
    {
        InitializeComponent();
        Loaded += SettingsView_Loaded;
    }

    private void SettingsView_Loaded(object sender, RoutedEventArgs e)
    {
        _isSynchronizing = true;
        try
        {
            var requestedTheme = (XamlRoot?.Content as FrameworkElement)?.RequestedTheme ?? ThemePreferenceStore.Load();
            ThemePicker.SelectedIndex = requestedTheme switch
            {
                ElementTheme.Light => 1,
                ElementTheme.Dark => 2,
                _ => 0
            };

            var settings = AppSettingsStore.Current;
            LanguagePicker.SelectedIndex = settings.Language switch
            {
                AppLanguagePreference.Japanese => 1,
                AppLanguagePreference.English => 2,
                _ => 0
            };
            RestoreSessionToggle.IsOn = settings.RestoreLastSession;
            RecentFilesToggle.IsOn = settings.RecordRecentFiles;
            WatchFilesToggle.IsOn = settings.AutoCheckFileChanges;
            FitOnOpenToggle.IsOn = settings.FitToWindowOnOpen;
            ViewerThemePicker.SelectedIndex = settings.ViewerTheme switch
            {
                ViewerThemePreference.Light => 1,
                ViewerThemePreference.Dark => 2,
                _ => 0
            };
            DrawingBackgroundPicker.SelectedIndex = settings.DrawingBackground switch
            {
                DrawingBackgroundPreference.Dark => 1,
                DrawingBackgroundPreference.Light => 2,
                _ => 0
            };
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    private void ThemePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSynchronizing || ThemePicker.SelectedIndex < 0 || XamlRoot?.Content is not FrameworkElement root) return;
        var theme = ThemePicker.SelectedIndex switch
        {
            1 => ElementTheme.Light,
            2 => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
        root.RequestedTheme = theme;
        ThemePreferenceStore.Save(theme);
    }

    private void LanguagePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSynchronizing || LanguagePicker.SelectedIndex < 0) return;
        var language = LanguagePicker.SelectedIndex switch
        {
            1 => AppLanguagePreference.Japanese,
            2 => AppLanguagePreference.English,
            _ => AppLanguagePreference.SimplifiedChinese
        };
        AppSettingsStore.Update(settings => settings with { Language = language });
    }

    private void RestoreSessionToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isSynchronizing) AppSettingsStore.Update(settings => settings with { RestoreLastSession = RestoreSessionToggle.IsOn });
    }

    private void RecentFilesToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isSynchronizing) AppSettingsStore.Update(settings => settings with { RecordRecentFiles = RecentFilesToggle.IsOn });
    }

    private void WatchFilesToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isSynchronizing) AppSettingsStore.Update(settings => settings with { AutoCheckFileChanges = WatchFilesToggle.IsOn });
    }

    private void FitOnOpenToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isSynchronizing) AppSettingsStore.Update(settings => settings with { FitToWindowOnOpen = FitOnOpenToggle.IsOn });
    }

    private void ViewerThemePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSynchronizing || ViewerThemePicker.SelectedIndex < 0) return;
        var preference = ViewerThemePicker.SelectedIndex switch
        {
            1 => ViewerThemePreference.Light,
            2 => ViewerThemePreference.Dark,
            _ => ViewerThemePreference.FollowApp
        };
        AppSettingsStore.Update(settings => settings with { ViewerTheme = preference });
    }

    private void DrawingBackgroundPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSynchronizing || DrawingBackgroundPicker.SelectedIndex < 0) return;
        var preference = DrawingBackgroundPicker.SelectedIndex switch
        {
            1 => DrawingBackgroundPreference.Dark,
            2 => DrawingBackgroundPreference.Light,
            _ => DrawingBackgroundPreference.FollowMode
        };
        AppSettingsStore.Update(settings => settings with { DrawingBackground = preference });
    }
}
