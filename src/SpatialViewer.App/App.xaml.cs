using Microsoft.UI.Xaml;

namespace SpatialViewer.Product;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        AppSettingsStore.ApplySavedLanguage();
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
