using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

namespace SpatialViewer.Product;

internal sealed record CadCorePackageDescriptor(
    Version Version,
    string DirectoryPath,
    IReadOnlyDictionary<string, string> RequiredAssemblies,
    IReadOnlyDictionary<string, string> ResolverAssemblies);

internal static class CadCoreRuntimeBootstrapper
{
    private const string KernelProduct = "SpatialViewer.CadCore";
    private static readonly string[] LoadOrder =
    [
        "SpatialViewer.Core",
        "SpatialViewer.Rendering",
        "SpatialViewer.Formats.Cad",
        "SpatialViewer.Formats.Cad.ACadSharp",
        "SpatialViewer.Rendering.Windows"
    ];
    private static readonly object Gate = new();
    private static bool _initialized;
    private static IReadOnlyDictionary<string, string> _resolverAssemblies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public static string KernelRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SpatialViewer",
        "Kernels",
        "CadCore");

    public static string VersionsRoot => Path.Combine(KernelRoot, "versions");
    public static Version BundledVersion { get; private set; } = new(0, 0, 0);
    public static Version CurrentVersion { get; private set; } = new(0, 0, 0);
    public static bool IsUsingExternalKernel { get; private set; }
    public static string? LastActivationError { get; private set; }

    public static void Initialize()
    {
        lock (Gate)
        {
            if (_initialized) return;
            _initialized = true;
            BundledVersion = ReadBundledVersion();
            CurrentVersion = BundledVersion;

            try
            {
                Directory.CreateDirectory(VersionsRoot);
                PromotePendingUpdate();
                var active = ReadState(ActiveStatePath);
                if (active is null) return;
                if (!TryGetVersion(active.Version, out var activeVersion) || activeVersion <= BundledVersion)
                {
                    TryDelete(ActiveStatePath);
                    return;
                }

                var directory = GetVersionDirectory(activeVersion);
                if (!CadCorePackageValidator.TryValidate(directory, out var package, out var validationError) || package is null)
                {
                    LastActivationError = validationError ?? "The staged CadCore package is invalid.";
                    TryDelete(ActiveStatePath);
                    return;
                }

                ActivatePackage(package);
                CurrentVersion = package.Version;
                IsUsingExternalKernel = true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or FileLoadException or BadImageFormatException)
            {
                LastActivationError = exception.Message;
                TryDelete(ActiveStatePath);
            }
        }
    }

    public static bool IsPendingVersion(Version version)
    {
        var pending = ReadState(PendingStatePath);
        return pending is not null && TryGetVersion(pending.Version, out var pendingVersion) && pendingVersion == version;
    }

    public static void StageForNextLaunch(Version version)
    {
        var directory = GetVersionDirectory(version);
        if (!CadCorePackageValidator.TryValidate(directory, out var package, out var error) || package is null || package.Version != version)
            throw new InvalidDataException(error ?? "The CadCore package cannot be staged.");
        WriteState(PendingStatePath, new CadCoreActivationState(FormatVersion(version)));
    }

    public static string GetVersionDirectory(Version version) => Path.Combine(VersionsRoot, FormatVersion(version));

    private static void PromotePendingUpdate()
    {
        var pending = ReadState(PendingStatePath);
        if (pending is null) return;
        if (!TryGetVersion(pending.Version, out var version) || version <= BundledVersion)
        {
            TryDelete(PendingStatePath);
            if (ReadState(ActiveStatePath) is { } active && TryGetVersion(active.Version, out var activeVersion) && activeVersion <= BundledVersion)
                TryDelete(ActiveStatePath);
            return;
        }

        var directory = GetVersionDirectory(version);
        if (!CadCorePackageValidator.TryValidate(directory, out var package, out _) || package is null || package.Version != version)
        {
            TryDelete(PendingStatePath);
            return;
        }

        WriteState(ActiveStatePath, pending);
        TryDelete(PendingStatePath);
    }

    private static void ActivatePackage(CadCorePackageDescriptor package)
    {
        _resolverAssemblies = package.ResolverAssemblies;
        AssemblyLoadContext.Default.Resolving -= ResolveAssembly;
        AssemblyLoadContext.Default.Resolving += ResolveAssembly;

        foreach (var simpleName in LoadOrder)
        {
            if (!package.RequiredAssemblies.TryGetValue(simpleName, out var assemblyPath))
                throw new InvalidDataException($"Required CadCore assembly is missing: {simpleName}");

            var alreadyLoaded = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(
                assembly => string.Equals(assembly.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase));
            if (alreadyLoaded is not null)
            {
                if (alreadyLoaded.GetName().Version is { } loadedVersion && loadedVersion >= package.Version) continue;
                throw new FileLoadException($"{simpleName} was loaded before the staged CadCore package could be activated.");
            }

            AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
        }
    }

    private static Assembly? ResolveAssembly(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName.Name) || !_resolverAssemblies.TryGetValue(assemblyName.Name, out var path)) return null;
        var loaded = context.Assemblies.FirstOrDefault(
            assembly => string.Equals(assembly.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase));
        return loaded ?? context.LoadFromAssemblyPath(path);
    }

    private static Version ReadBundledVersion()
    {
        var directPath = Path.Combine(AppContext.BaseDirectory, "SpatialViewer.Formats.Cad.ACadSharp.dll");
        var candidate = File.Exists(directPath)
            ? directPath
            : Directory.EnumerateFiles(AppContext.BaseDirectory, "SpatialViewer.Formats.Cad.ACadSharp.dll", SearchOption.AllDirectories).FirstOrDefault();
        if (candidate is null) return new Version(0, 0, 0);
        var version = AssemblyName.GetAssemblyName(candidate).Version;
        return version is null ? new Version(0, 0, 0) : NormalizeVersion(version);
    }

    private static string PendingStatePath => Path.Combine(KernelRoot, "pending.json");
    private static string ActiveStatePath => Path.Combine(KernelRoot, "active.json");

    private static CadCoreActivationState? ReadState(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<CadCoreActivationState>(File.ReadAllText(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static void WriteState(string path, CadCoreActivationState state)
    {
        Directory.CreateDirectory(KernelRoot);
        var temporaryPath = $"{path}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LastActivationError ??= exception.Message;
        }
    }

    private static bool TryGetVersion(string value, out Version version)
    {
        if (!Version.TryParse(value, out var parsed) || parsed is null)
        {
            version = new Version(0, 0, 0);
            return false;
        }
        version = NormalizeVersion(parsed);
        return true;
    }

    internal static Version NormalizeVersion(Version version) => new(version.Major, version.Minor, Math.Max(0, version.Build));
    internal static string FormatVersion(Version version) => $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";

    private sealed record CadCoreActivationState(string Version);
}

internal static class CadCorePackageValidator
{
    private const string ProductName = "SpatialViewer.CadCore";
    private static readonly IReadOnlyDictionary<string, string> RequiredProjects = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["SpatialViewer.Core"] = "SpatialViewer.Core",
        ["SpatialViewer.Formats.Cad"] = "SpatialViewer.Formats.Cad",
        ["SpatialViewer.Formats.Cad.ACadSharp"] = "SpatialViewer.Formats.Cad.ACadSharp",
        ["SpatialViewer.Rendering"] = "SpatialViewer.Rendering",
        ["SpatialViewer.Rendering.Windows"] = "SpatialViewer.Rendering.Windows"
    };

    public static bool TryValidate(string directory, out CadCorePackageDescriptor? package, out string? error)
    {
        package = null;
        error = null;
        try
        {
            var fullDirectory = Path.GetFullPath(directory);
            var manifestPath = Path.Combine(fullDirectory, "cadcore-release.json");
            if (!File.Exists(manifestPath)) return Fail("cadcore-release.json is missing.", out error);

            using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = manifest.RootElement;
            if (!TryReadString(root, "product", out var product) || !string.Equals(product, ProductName, StringComparison.Ordinal))
                return Fail("The CadCore package product identity is invalid.", out error);
            if (!TryReadString(root, "version", out var versionText) || !Version.TryParse(versionText, out var parsedVersion) || parsedVersion is null)
                return Fail("The CadCore package version is invalid.", out error);
            var version = CadCoreRuntimeBootstrapper.NormalizeVersion(parsedVersion);
            if (!TryReadString(root, "runtime", out var runtime) || !string.Equals(runtime, "x64", StringComparison.OrdinalIgnoreCase))
                return Fail("The CadCore package runtime is not x64.", out error);
            if (!TryReadString(root, "sourceRepository", out var repository) || !string.Equals(repository, "KiYouJyo/SpatialViewer.CadCore", StringComparison.OrdinalIgnoreCase))
                return Fail("The CadCore package source repository is invalid.", out error);
            if (!TryReadString(root, "compatibility", out var compatibility) || !IsCompatible(compatibility))
                return Fail("The CadCore package is not compatible with this SpatialViewer version.", out error);

            var requiredAssemblies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in RequiredProjects)
            {
                var projectRoot = Path.Combine(fullDirectory, "bin", pair.Value);
                if (!Directory.Exists(projectRoot)) return Fail($"Required CadCore project payload is missing: {pair.Value}", out error);
                var assemblyPath = Directory.EnumerateFiles(projectRoot, $"{pair.Key}.dll", SearchOption.AllDirectories).FirstOrDefault();
                if (assemblyPath is null) return Fail($"Required CadCore assembly is missing: {pair.Key}.dll", out error);
                var assemblyVersion = AssemblyName.GetAssemblyName(assemblyPath).Version;
                if (assemblyVersion is null || CadCoreRuntimeBootstrapper.NormalizeVersion(assemblyVersion) != version)
                    return Fail($"CadCore assembly version does not match the release manifest: {pair.Key}.dll", out error);
                requiredAssemblies[pair.Key] = Path.GetFullPath(assemblyPath);
            }

            var resolverAssemblies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dll in Directory.EnumerateFiles(Path.Combine(fullDirectory, "bin"), "*.dll", SearchOption.AllDirectories))
            {
                var simpleName = Path.GetFileNameWithoutExtension(dll);
                resolverAssemblies.TryAdd(simpleName, Path.GetFullPath(dll));
            }
            foreach (var pair in requiredAssemblies) resolverAssemblies[pair.Key] = pair.Value;

            package = new CadCorePackageDescriptor(version, fullDirectory, requiredAssemblies, resolverAssemblies);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or BadImageFormatException)
        {
            error = exception.Message;
            return false;
        }
    }

    private static bool IsCompatible(string compatibility)
    {
        var appVersion = typeof(CadCorePackageValidator).Assembly.GetName().Version ?? new Version(0, 0, 0);
        return compatibility.Equals($"SpatialViewer {appVersion.Major}.{appVersion.Minor}.x", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.String) return false;
        value = element.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool Fail(string message, out string? error)
    {
        error = message;
        return false;
    }
}
