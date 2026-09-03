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

        // The custom title bar stays untouched. Set only the native window icon so
        // taskbar buttons, Alt+Tab and taskbar thumbnail headers use the product icon.
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (File.Exists(iconPath)) _window.AppWindow.SetIcon(iconPath);
        }
        catch
        {
            // A missing/non-loadable icon must never block app startup.
        }

        _window.Activate();
    }
}
