using System.Reflection;
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
    var bundledVersion = CadCoreRuntimeBootstrapper.BundledVersion;
    var bundledAbi = CadCoreRuntimeBootstrapper.BundledAbiVersion;
    var currentVersion = CadCoreRuntimeBootstrapper.CurrentVersion;
    var currentAbi = CadCoreRuntimeBootstrapper.CurrentAbiVersion;
    var isExternal = CadCoreRuntimeBootstrapper.IsUsingExternalKernel;
    var activationError = CadCoreRuntimeBootstrapper.LastActivationError;
    var pendingVersion = CadCoreRuntimeBootstrapper.PendingVersion;

    Console.WriteLine($"BundledVersion={bundledVersion}");
    Console.WriteLine($"BundledAbiVersion={bundledAbi}");
    Console.WriteLine($"CurrentVersion={currentVersion}");
    Console.WriteLine($"CurrentAbiVersion={currentAbi}");
    Console.WriteLine($"IsUsingExternalKernel={isExternal}");
    Console.WriteLine($"PendingVersion={pendingVersion}");
    Console.WriteLine($"LastActivationError={activationError ?? "-"}");

    if (currentVersion != expectedVersion)
        throw new InvalidOperationException($"Activation product-version mismatch: expected={expectedVersion} actual={currentVersion} bundled={bundledVersion} error={activationError ?? "-"}");
    if (!isExternal)
        throw new InvalidOperationException("Cad Core bootstrapper did not report an external kernel after activation.");
    if (pendingVersion is not null)
        throw new InvalidOperationException($"pending.json was not consumed after activation: {pendingVersion}");
    if (bundledAbi != currentAbi)
        throw new InvalidOperationException($"Cad Core ABI changed across update: bundled={bundledAbi} current={currentAbi}");

    // Exercise a real compile-time Cad Core reference. The probe itself was
    // compiled against the bundled 0.3.0 product, while the module initializer
    // must make that static reference resolve to the externally staged product.
    var importer = new ACadSharpCadImporter();
    if (!importer.CanImport("probe.dwg"))
        throw new InvalidOperationException("The activated ACadSharp importer is not functional.");
    var importerAssembly = typeof(ACadSharpCadImporter).Assembly;
    var importerAbi = importerAssembly.GetName().Version ?? new Version(0, 0, 0, 0);
    if (importerAbi != currentAbi)
        throw new InvalidOperationException($"Static importer ABI mismatch: {importerAbi} != {currentAbi}");
    if (!CadCoreRuntimeBootstrapper.TryReadFileProductVersion(importerAssembly.Location, out var importerProductVersion) || importerProductVersion != expectedVersion)
        throw new InvalidOperationException($"Static importer product-version mismatch: {importerProductVersion} != {expectedVersion}");

    var activePath = Path.Combine(CadCoreRuntimeBootstrapper.KernelRoot, "active.json");
    if (!File.Exists(activePath)) throw new InvalidOperationException("active.json was not created.");
    using var activeDocument = JsonDocument.Parse(File.ReadAllText(activePath));
    var activeVersion = activeDocument.RootElement.GetProperty("Version").GetString();
    if (!string.Equals(activeVersion, CadCoreRuntimeBootstrapper.FormatVersion(expectedVersion), StringComparison.Ordinal))
        throw new InvalidOperationException($"active.json version mismatch: {activeVersion}");

    string[] requiredNames =
    [
        "SpatialViewer.Core",
        "SpatialViewer.Rendering",
        "SpatialViewer.Formats.Cad",
        "SpatialViewer.Formats.Cad.ACadSharp",
        "SpatialViewer.Rendering.Windows"
    ];

    // Rendering assemblies are lazy in the real app and are not all touched by
    // the importer-only probe. Explicitly request every Cad Core assembly from
    // the default ALC so the test proves the resolver will also serve the later
    // renderer/document-model bindings from the selected external product.
    foreach (var name in requiredNames)
        _ = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(name));

    var requiredSet = requiredNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
    var loaded = AssemblyLoadContext.Default.Assemblies
        .Where(assembly => assembly.GetName().Name is { } name && requiredSet.Contains(name))
        .ToArray();
    if (loaded.Length != requiredNames.Length)
        throw new InvalidOperationException($"Expected {requiredNames.Length} loaded Cad Core assemblies, found {loaded.Length}.");

    var externalRoot = CadCoreRuntimeBootstrapper.GetVersionDirectory(expectedVersion);
    foreach (var assembly in loaded)
    {
        var name = assembly.GetName();
        var abiVersion = name.Version ?? new Version(0, 0, 0, 0);
        var hasProductVersion = CadCoreRuntimeBootstrapper.TryReadFileProductVersion(assembly.Location, out var productVersion);
        Console.WriteLine($"Loaded={name.Name} product={productVersion} ABI={abiVersion} @ {assembly.Location}");
        if (abiVersion != currentAbi)
            throw new InvalidOperationException($"Loaded assembly ABI mismatch: {name.Name}={abiVersion}, expected={currentAbi}");
        if (!hasProductVersion || productVersion != expectedVersion)
            throw new InvalidOperationException($"Loaded assembly product-version mismatch: {name.Name}={productVersion}, expected={expectedVersion}");
        if (!assembly.Location.StartsWith(externalRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Loaded assembly came from bundled path instead of staged Cad Core: {assembly.Location}");
    }

    Console.WriteLine($"Cad Core early static-binding activation PASS: product={CadCoreRuntimeBootstrapper.FormatVersion(expectedVersion)} ABI={currentAbi}");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}
