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
    // CadCoreEarlyBootstrap has already executed as a module initializer before
    // this Main method (and therefore before these statically referenced types)
    // can be JIT-bound to the bundled project-reference assemblies.
    var bundledVersion = CadCoreRuntimeBootstrapper.BundledVersion;
    var currentVersion = CadCoreRuntimeBootstrapper.CurrentVersion;
    var isExternal = CadCoreRuntimeBootstrapper.IsUsingExternalKernel;
    var activationError = CadCoreRuntimeBootstrapper.LastActivationError;
    var pendingVersion = CadCoreRuntimeBootstrapper.PendingVersion;

    Console.WriteLine($"BundledVersion={bundledVersion}");
    Console.WriteLine($"CurrentVersion={currentVersion}");
    Console.WriteLine($"IsUsingExternalKernel={isExternal}");
    Console.WriteLine($"PendingVersion={pendingVersion}");
    Console.WriteLine($"LastActivationError={activationError ?? "-"}");

    if (currentVersion != expectedVersion)
        throw new InvalidOperationException($"Activation version mismatch: expected={expectedVersion} actual={currentVersion} bundled={bundledVersion} error={activationError ?? "-"}");
    if (!isExternal)
        throw new InvalidOperationException("Cad Core bootstrapper did not report an external kernel after activation.");
    if (pendingVersion is not null)
        throw new InvalidOperationException($"pending.json was not consumed after activation: {pendingVersion}");

    // Exercise a real compile-time Cad Core reference. If the default ALC still
    // bound to the bundled version, constructing this type or inspecting its
    // assembly will expose the mismatch immediately.
    var importer = new ACadSharpCadImporter();
    if (!importer.CanImport("probe.dwg"))
        throw new InvalidOperationException("The activated ACadSharp importer is not functional.");
    var importerVersion = CadCoreRuntimeBootstrapper.NormalizeVersion(typeof(ACadSharpCadImporter).Assembly.GetName().Version ?? new Version(0, 0, 0));
    if (importerVersion != expectedVersion)
        throw new InvalidOperationException($"Static importer binding mismatch: {importerVersion} != {expectedVersion}");

    var activePath = Path.Combine(CadCoreRuntimeBootstrapper.KernelRoot, "active.json");
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
    var loaded = AssemblyLoadContext.Default.Assemblies
        .Where(assembly => assembly.GetName().Name is { } name && requiredNames.Contains(name))
        .ToArray();
    if (loaded.Length != requiredNames.Count)
        throw new InvalidOperationException($"Expected {requiredNames.Count} loaded Cad Core assemblies, found {loaded.Length}.");
    foreach (var assembly in loaded)
    {
        var name = assembly.GetName();
        var version = CadCoreRuntimeBootstrapper.NormalizeVersion(name.Version ?? new Version(0, 0, 0));
        Console.WriteLine($"Loaded={name.Name} {version} @ {assembly.Location}");
        if (version != expectedVersion)
            throw new InvalidOperationException($"Loaded assembly version mismatch: {name.Name}={version}, expected={expectedVersion}");
        if (!assembly.Location.StartsWith(CadCoreRuntimeBootstrapper.GetVersionDirectory(expectedVersion), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Loaded assembly came from bundled path instead of staged Cad Core: {assembly.Location}");
    }

    Console.WriteLine($"Cad Core early static-binding activation PASS: {CadCoreRuntimeBootstrapper.FormatVersion(expectedVersion)}");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}
