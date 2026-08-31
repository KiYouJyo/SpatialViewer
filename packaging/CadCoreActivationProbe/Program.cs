using System.Runtime.Loader;
using System.Text.Json;
using SpatialViewer.Formats.Cad.ACadSharp;
using SpatialViewer.Product;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: CadCoreActivationProbe <expected-version>");
    return 64;
}
if (!Version.TryParse(args[0], out var expectedVersion) || expectedVersion is null)
{
    Console.Error.WriteLine($"Invalid expected version: {args[0]}");
    return 65;
}
expectedVersion = CadCoreRuntimeBootstrapper.NormalizeVersion(expectedVersion);

try
{
    var kernelRoot = CadCoreRuntimeBootstrapper.KernelRoot;
    var bundledVersion = CadCoreRuntimeBootstrapper.BundledVersion;
    var currentVersion = CadCoreRuntimeBootstrapper.CurrentVersion;
    var isExternal = CadCoreRuntimeBootstrapper.IsUsingExternalKernel;
    var activationError = CadCoreRuntimeBootstrapper.LastActivationError;
    var pendingVersion = CadCoreRuntimeBootstrapper.PendingVersion;

    // This is a compile-time/static reference to the bundled Cad Core project.
    // If startup preloading is too late, this resolves to the bundled version.
    var staticallyReferencedVersion = CadCoreRuntimeBootstrapper.NormalizeVersion(
        typeof(ACadSharpCadImporter).Assembly.GetName().Version ?? new Version(0, 0, 0));

    Console.WriteLine($"KernelRoot={kernelRoot}");
    Console.WriteLine($"BundledVersion={bundledVersion}");
    Console.WriteLine($"CurrentVersion={currentVersion}");
    Console.WriteLine($"StaticReferenceVersion={staticallyReferencedVersion}");
    Console.WriteLine($"IsUsingExternalKernel={isExternal}");
    Console.WriteLine($"PendingVersion={pendingVersion}");
    Console.WriteLine($"LastActivationError={activationError ?? "-"}");

    if (currentVersion != expectedVersion)
        throw new InvalidOperationException($"Activation version mismatch: expected={expectedVersion} actual={currentVersion} bundled={bundledVersion} error={activationError ?? "-"}");
    if (staticallyReferencedVersion != expectedVersion)
        throw new InvalidOperationException($"Static Cad Core reference remained bound to the bundled version: static={staticallyReferencedVersion} expected={expectedVersion}");
    if (!isExternal)
        throw new InvalidOperationException("Cad Core bootstrapper did not report an external kernel after activation.");
    if (pendingVersion is not null)
        throw new InvalidOperationException($"pending.json was not consumed after activation: {pendingVersion}");

    var activePath = Path.Combine(kernelRoot, "active.json");
    if (!File.Exists(activePath)) throw new InvalidOperationException("active.json was not created.");
    using var activeDocument = JsonDocument.Parse(File.ReadAllText(activePath));
    var activeVersion = activeDocument.RootElement.GetProperty("Version").GetString();
    if (!string.Equals(activeVersion, CadCoreRuntimeBootstrapper.FormatVersion(expectedVersion), StringComparison.Ordinal))
        throw new InvalidOperationException($"active.json version mismatch: {activeVersion}");

    var requiredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "SpatialViewer.Core",
        "SpatialViewer.Rendering",
        "SpatialViewer.Formats.Cad",
        "SpatialViewer.Formats.Cad.ACadSharp",
        "SpatialViewer.Rendering.Windows"
    };
    var loadedNames = AssemblyLoadContext.Default.Assemblies
        .Select(static assembly => assembly.GetName())
        .Where(name => name.Name is not null && requiredNames.Contains(name.Name))
        .ToArray();
    if (loadedNames.Length != requiredNames.Count)
        throw new InvalidOperationException($"Expected {requiredNames.Count} loaded Cad Core assemblies, found {loadedNames.Length}: {string.Join(", ", loadedNames.Select(static name => name.FullName))}");
    foreach (var name in loadedNames)
    {
        var version = name.Version is null ? new Version(0, 0, 0) : CadCoreRuntimeBootstrapper.NormalizeVersion(name.Version);
        if (version != expectedVersion)
            throw new InvalidOperationException($"Loaded assembly version mismatch: {name.Name}={version}, expected={expectedVersion}");
    }

    Console.WriteLine($"Cad Core static-binding activation PASS: {CadCoreRuntimeBootstrapper.FormatVersion(expectedVersion)}");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}
