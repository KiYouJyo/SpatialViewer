using System.Runtime.Loader;

namespace SpatialViewer.Product;

internal static class CadCoreActivationDiagnostics
{
    private static readonly HashSet<string> CadCoreAssemblyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "SpatialViewer.Core",
        "SpatialViewer.Rendering",
        "SpatialViewer.Rendering.Windows",
        "SpatialViewer.Formats.Cad",
        "SpatialViewer.Formats.Cad.ACadSharp"
    };

    public static string LogPath => Path.Combine(CadCoreRuntimeBootstrapper.KernelRoot, "activation.log");

    public static void Write(string stage)
    {
        try
        {
            Directory.CreateDirectory(CadCoreRuntimeBootstrapper.KernelRoot);
            var loaded = AssemblyLoadContext.Default.Assemblies
                .Where(assembly => assembly.GetName().Name is { } name && CadCoreAssemblyNames.Contains(name))
                .Select(assembly =>
                {
                    var name = assembly.GetName();
                    var product = CadCoreRuntimeBootstrapper.TryReadFileProductVersion(assembly.Location, out var productVersion)
                        ? productVersion.ToString()
                        : "?";
                    return $"{name.Name}@product={product},abi={name.Version}:{assembly.Location}";
                })
                .DefaultIfEmpty("none");
            var line = string.Join('\t',
                DateTimeOffset.UtcNow.ToString("O"),
                stage,
                $"bundled={CadCoreRuntimeBootstrapper.BundledVersion}",
                $"bundledAbi={CadCoreRuntimeBootstrapper.BundledAbiVersion}",
                $"current={CadCoreRuntimeBootstrapper.CurrentVersion}",
                $"currentAbi={CadCoreRuntimeBootstrapper.CurrentAbiVersion}",
                $"external={CadCoreRuntimeBootstrapper.IsUsingExternalKernel}",
                $"pending={CadCoreRuntimeBootstrapper.PendingVersion?.ToString() ?? "-"}",
                $"error={Sanitize(CadCoreRuntimeBootstrapper.LastActivationError)}",
                $"loaded={string.Join('|', loaded)}");
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Diagnostics must never block application startup.
        }
    }

    private static string Sanitize(string? value) => string.IsNullOrWhiteSpace(value)
        ? "-"
        : value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
}
