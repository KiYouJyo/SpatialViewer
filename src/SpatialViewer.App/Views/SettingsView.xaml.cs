using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace SpatialViewer.Product.Views;
public sealed partial class SettingsView : UserControl
{
    private bool _isSynchronizingThemePicker;

    public SettingsView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var requestedTheme = (XamlRoot?.Content as FrameworkElement)?.RequestedTheme ?? ElementTheme.Default;
            _isSynchronizingThemePicker = true;
            ThemePicker.SelectedIndex = requestedTheme switch
            {
                ElementTheme.Light => 1,
                ElementTheme.Dark => 2,
                _ => 0
            };
            _isSynchronizingThemePicker = false;
        };
    }

    private void ThemePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSynchronizingThemePicker || ThemePicker.SelectedIndex < 0 || XamlRoot?.Content is not FrameworkElement root) return;
        var theme = ThemePicker.SelectedIndex switch
        {
            1 => ElementTheme.Light,
            2 => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
        root.RequestedTheme = theme;
        ThemePreferenceStore.Save(theme);
    }
}
