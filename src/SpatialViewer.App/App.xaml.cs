using Microsoft.UI.Xaml;

namespace SpatialViewer.Product;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        AppLocalizationService.Default.ApplyPersistedLanguage(AppSettingsStore.Current);
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
