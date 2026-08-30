using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace SpatialViewer.Product.Views;

public sealed partial class SettingsView : UserControl
{
    private readonly AppLocalizationService _localization = AppLocalizationService.Default;
    private bool _isSynchronizing;

    public SettingsView()
    {
        InitializeComponent();
        ApplyLocalizedText();
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
                AppLanguagePreference.SimplifiedChinese => 1,
                AppLanguagePreference.Japanese => 2,
                AppLanguagePreference.English => 3,
                _ => 0
            };
            RestoreSessionToggle.IsOn = settings.RestoreLastSession;
            RecentFilesToggle.IsOn = settings.RecordRecentFiles;
            WatchFilesToggle.IsOn = settings.AutoCheckFileChanges;
            FitOnOpenToggle.IsOn = settings.FitToWindowOnOpen;
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

    private async void LanguagePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSynchronizing || LanguagePicker.SelectedIndex < 0) return;
        var preference = LanguagePicker.SelectedIndex switch
        {
            1 => AppLanguagePreference.SimplifiedChinese,
            2 => AppLanguagePreference.Japanese,
            3 => AppLanguagePreference.English,
            _ => AppLanguagePreference.System
        };

        LanguagePicker.IsEnabled = false;
        LanguageStatusText.Visibility = Visibility.Collapsed;
        var switched = await _localization.SwitchLanguageAsync(preference);
        LanguagePicker.IsEnabled = true;
        if (!switched)
        {
            LanguageStatusText.Text = _localization.GetString("Language_SwitchFailed");
            LanguageStatusText.Visibility = Visibility.Visible;
            _isSynchronizing = true;
            try
            {
                LanguagePicker.SelectedIndex = AppSettingsStore.Current.Language switch
                {
                    AppLanguagePreference.SimplifiedChinese => 1,
                    AppLanguagePreference.Japanese => 2,
                    AppLanguagePreference.English => 3,
                    _ => 0
                };
            }
            finally { _isSynchronizing = false; }
            return;
        }

        ApplyLocalizedText();
        ApplyLocalizedNavigation();
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

    private void ApplyLocalizedText()
    {
        string T(string key) => _localization.GetString(key);
        SettingsTitleText.Text = T("Settings_Title");
        AppearanceLanguageTitleText.Text = T("Settings_AppearanceLanguage");
        ThemeTitleText.Text = T("Settings_Theme_Title");
        ThemeDescriptionText.Text = T("Settings_Theme_Desc");
        ThemeSystemItem.Content = T("Theme_System");
        ThemeLightItem.Content = T("Theme_Light");
        ThemeDarkItem.Content = T("Theme_Dark");
        LanguageTitleText.Text = T("Settings_Language_Title");
        LanguageDescriptionText.Text = T("Settings_Language_Desc");
        LanguageSystemItem.Content = T("Language_System");
        LanguageZhItem.Content = T("Language_ZhCn");
        LanguageJaItem.Content = T("Language_JaJp");
        LanguageEnItem.Content = T("Language_EnUs");
        FileSessionTitleText.Text = T("Settings_FileSession");
        RestoreTitleText.Text = T("Settings_Restore_Title");
        RestoreDescriptionText.Text = T("Settings_Restore_Desc");
        RecentTitleText.Text = T("Settings_Recent_Title");
        RecentDescriptionText.Text = T("Settings_Recent_Desc");
        WatchTitleText.Text = T("Settings_Watch_Title");
        WatchDescriptionText.Text = T("Settings_Watch_Desc");
        ViewerTitleText.Text = T("Settings_Viewer");
        FitTitleText.Text = T("Settings_Fit_Title");
        FitDescriptionText.Text = T("Settings_Fit_Desc");
        DrawingBackgroundTitleText.Text = T("Settings_DrawingBackground_Title");
        DrawingBackgroundDescriptionText.Text = T("Settings_DrawingBackground_Desc");
        DrawingBackgroundFollowItem.Content = T("DrawingBackground_Follow");
        DrawingBackgroundDarkItem.Content = T("DrawingBackground_Dark");
        DrawingBackgroundLightItem.Content = T("DrawingBackground_Light");
    }

    private void ApplyLocalizedNavigation()
    {
        if (XamlRoot?.Content is not DependencyObject root) return;
        var navigation = FindDescendant<NavigationView>(root);
        if (navigation is null) return;
        foreach (var candidate in navigation.MenuItems.Concat(navigation.FooterMenuItems).OfType<NavigationViewItem>())
        {
            if (candidate.Tag is not string tag) continue;
            candidate.Content = tag switch
            {
                "Home" => _localization.GetString("Nav_Home"),
                "Projects" => _localization.GetString("Nav_Projects"),
                "Favorites" => _localization.GetString("Nav_Favorites"),
                "ImportFolder" => _localization.GetString("Nav_ImportFolder"),
                "About" => _localization.GetString("Nav_About"),
                "Settings" => _localization.GetString("Nav_Settings"),
                _ => candidate.Content
            };
        }
    }

    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) return match;
            if (FindDescendant<T>(child) is { } descendant) return descendant;
        }
        return null;
    }
}
