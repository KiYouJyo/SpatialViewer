using System.Runtime.CompilerServices;

namespace SpatialViewer.Product;

internal static class CadCoreEarlyBootstrap
{
    [ModuleInitializer]
    internal static void InitializeModule()
    {
        // WinUI can materialize generated XAML metadata before App() executes.
        // Activate the staged Cad Core at module-load time so every later static
        // reference binds to the selected external version in the default ALC.
        CadCoreActivationDiagnostics.Write("module-before");
        CadCoreRuntimeBootstrapper.Initialize();
        CadCoreActivationDiagnostics.Write("module-after");
    }
}
