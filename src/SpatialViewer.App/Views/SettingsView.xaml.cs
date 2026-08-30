using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace SpatialViewer.Product.Views;
public sealed partial class SettingsView : UserControl { public SettingsView() { InitializeComponent(); ThemePicker.SelectedIndex = 0; } private void ThemePicker_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (ThemePicker.SelectedIndex < 0) return; ((FrameworkElement)XamlRoot.Content).RequestedTheme = ThemePicker.SelectedIndex switch { 1 => ElementTheme.Light, 2 => ElementTheme.Dark, _ => ElementTheme.Default }; } }
