using System.Runtime.CompilerServices;

namespace SpatialViewer.Product;

internal static class CadCoreModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        CadCoreRuntimeBootstrapper.Initialize();
    }
}
