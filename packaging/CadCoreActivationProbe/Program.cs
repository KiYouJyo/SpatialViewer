using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: CadCoreActivationProbe <SpatialViewer.App.dll> <kernel-root> <expected-version>");
    return 64;
}

var appAssemblyPath = Path.GetFullPath(args[0]);
var kernelRoot = Path.GetFullPath(args[1]);
if (!Version.TryParse(args[2], out var expectedVersion) || expectedVersion is null)
{
    Console.Error.WriteLine($"Invalid expected version: {args[2]}");
    return 65;
}
expectedVersion = new Version(expectedVersion.Major, expectedVersion.Minor, Math.Max(0, expectedVersion.Build));

if (!File.Exists(appAssemblyPath))
{
    Console.Error.WriteLine($"SpatialViewer.App.dll not found: {appAssemblyPath}");
    return 66;
}
if (!Directory.Exists(kernelRoot))
{
    Console.Error.WriteLine($"Kernel root not found: {kernelRoot}");
    return 67;
}

Environment.SetEnvironmentVariable("SPATIALVIEWER_CADCORE_ROOT", kernelRoot);

try
{
    var appAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(appAssemblyPath);
    var bootstrapper = appAssembly.GetType("SpatialViewer.Product.CadCoreRuntimeBootstrapper", throwOnError: true)
        ?? throw new InvalidOperationException("CadCoreRuntimeBootstrapper type was not found.");

    var initialize = bootstrapper.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException("CadCoreRuntimeBootstrapper.Initialize was not found.");
    initialize.Invoke(null, null);

    var bundledVersion = ReadProperty<Version>(bootstrapper, "BundledVersion");
    var currentVersion = ReadProperty<Version>(bootstrapper, "CurrentVersion");
    var isExternal = ReadProperty<bool>(bootstrapper, "IsUsingExternalKernel");
    var activationError = ReadNullableProperty<string>(bootstrapper, "LastActivationError");
    var pendingVersion = ReadNullableProperty<Version>(bootstrapper, "PendingVersion");

    Console.WriteLine($"BundledVersion={bundledVersion}");
    Console.WriteLine($"CurrentVersion={currentVersion}");
    Console.WriteLine($"IsUsingExternalKernel={isExternal}");
    Console.WriteLine($"PendingVersion={pendingVersion}");
    Console.WriteLine($"LastActivationError={activationError ?? "-"}");

    if (currentVersion != expectedVersion)
        throw new InvalidOperationException($"Activation version mismatch: expected={expectedVersion} actual={currentVersion} error={activationError ?? "-"}");
    if (!isExternal)
        throw new InvalidOperationException("CadCore bootstrapper did not report an external kernel after activation.");
    if (pendingVersion is not null)
        throw new InvalidOperationException($"pending.json was not consumed after activation: {pendingVersion}");

    var activePath = Path.Combine(kernelRoot, "active.json");
    if (!File.Exists(activePath)) throw new InvalidOperationException("active.json was not created.");
    using var activeDocument = JsonDocument.Parse(File.ReadAllText(activePath));
    var activeVersion = activeDocument.RootElement.GetProperty("Version").GetString();
    if (!string.Equals(activeVersion, $"{expectedVersion.Major}.{expectedVersion.Minor}.{expectedVersion.Build}", StringComparison.Ordinal))
        throw new InvalidOperationException($"active.json version mismatch: {activeVersion}");

    var loadedNames = AssemblyLoadContext.Default.Assemblies
        .Select(static assembly => assembly.GetName())
        .Where(static name => name.Name is "SpatialViewer.Core" or "SpatialViewer.Rendering" or "SpatialViewer.Formats.Cad" or "SpatialViewer.Formats.Cad.ACadSharp" or "SpatialViewer.Rendering.Windows")
        .ToArray();
    if (loadedNames.Length != 5)
        throw new InvalidOperationException($"Expected 5 loaded CadCore assemblies, found {loadedNames.Length}.");
    foreach (var name in loadedNames)
    {
        var version = name.Version is null ? new Version(0, 0, 0) : new Version(name.Version.Major, name.Version.Minor, Math.Max(0, name.Version.Build));
        if (version != expectedVersion) throw new InvalidOperationException($"Loaded assembly version mismatch: {name.Name}={version}, expected={expectedVersion}");
    }

    Console.WriteLine("CadCore fresh-process activation PASS");
    return 0;
}
catch (TargetInvocationException exception) when (exception.InnerException is not null)
{
    Console.Error.WriteLine(exception.InnerException);
    return 1;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}

static T ReadProperty<T>(Type type, string propertyName)
{
    var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Missing property: {propertyName}");
    var value = property.GetValue(null);
    return value is T typed ? typed : throw new InvalidOperationException($"Unexpected value for {propertyName}.");
}

static T? ReadNullableProperty<T>(Type type, string propertyName) where T : class
{
    var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Missing property: {propertyName}");
    return property.GetValue(null) as T;
}
