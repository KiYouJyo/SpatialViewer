using Microsoft.UI.Xaml;

namespace SpatialViewer.Product;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        // A downloaded CadCore is activated only at process start. This runs
        // before XAML or MainWindow can touch any statically referenced kernel
        // type, so the default AssemblyLoadContext can bind to the staged newer
        // assembly version without modifying the read-only MSIX install folder.
        CadCoreRuntimeBootstrapper.Initialize();
        AppLocalizationService.Default.ApplyPersistedLanguage(AppSettingsStore.Current);
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
