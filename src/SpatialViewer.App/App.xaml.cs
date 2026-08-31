using Microsoft.UI.Xaml;

namespace SpatialViewer.Product;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        // CadCoreEarlyBootstrap runs as a module initializer before WinUI/XAML
        // can materialize any type that statically references the bundled kernel.
        AppLocalizationService.Default.ApplyPersistedLanguage(AppSettingsStore.Current);
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
