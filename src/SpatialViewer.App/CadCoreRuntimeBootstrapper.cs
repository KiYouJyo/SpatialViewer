using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

namespace SpatialViewer.Product;

internal sealed record CadCorePackageDescriptor(
    Version Version,
    Version AbiVersion,
    string DirectoryPath,
    IReadOnlyDictionary<string, string> RequiredAssemblies,
    IReadOnlyDictionary<string, string> ResolverAssemblies);

internal static class CadCoreRuntimeBootstrapper
{
    private const string KernelRootOverrideEnvironmentVariable = "SPATIALVIEWER_CADCORE_ROOT";
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

    public static string KernelRoot { get; } = ResolveKernelRoot();

    public static string VersionsRoot => Path.Combine(KernelRoot, "versions");
    public static Version BundledVersion { get; private set; } = new(0, 0, 0);
    public static Version BundledAbiVersion { get; private set; } = new(0, 0, 0, 0);
    public static Version CurrentVersion { get; private set; } = new(0, 0, 0);
    public static Version CurrentAbiVersion { get; private set; } = new(0, 0, 0, 0);
    public static bool IsUsingExternalKernel { get; private set; }
    public static string? LastActivationError { get; private set; }
    public static Version? PendingVersion
    {
        get
        {
            var pending = ReadState(PendingStatePath);
            return pending is not null && TryGetVersion(pending.Version, out var version) ? version : null;
        }
    }

    public static void Initialize()
    {
        lock (Gate)
        {
            if (_initialized) return;
            _initialized = true;
            BundledVersion = ReadBundledProductVersion();
            BundledAbiVersion = ReadBundledAbiVersion();
            CurrentVersion = BundledVersion;
            CurrentAbiVersion = BundledAbiVersion;

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
                if (package.AbiVersion != BundledAbiVersion)
                {
                    LastActivationError = $"CadCore ABI mismatch: bundled={BundledAbiVersion}; staged={package.AbiVersion}.";
                    TryDelete(ActiveStatePath);
                    return;
                }

                ActivatePackage(package);
                CurrentVersion = package.Version;
                CurrentAbiVersion = package.AbiVersion;
                IsUsingExternalKernel = true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or FileLoadException or BadImageFormatException)
            {
                LastActivationError = exception.Message;
                TryDelete(ActiveStatePath);
            }
        }
    }

    public static bool IsPendingVersion(Version version) => PendingVersion == version;

    public static void StageForNextLaunch(Version version)
    {
        var directory = GetVersionDirectory(version);
        if (!CadCorePackageValidator.TryValidate(directory, out var package, out var error) || package is null || package.Version != version)
            throw new InvalidDataException(error ?? "The CadCore package cannot be staged.");
        if (package.AbiVersion != BundledAbiVersion)
            throw new InvalidDataException($"The CadCore package ABI {package.AbiVersion} is incompatible with bundled ABI {BundledAbiVersion}.");
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
        if (!CadCorePackageValidator.TryValidate(directory, out var package, out _) || package is null || package.Version != version || package.AbiVersion != BundledAbiVersion)
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
                if (string.Equals(Path.GetFullPath(alreadyLoaded.Location), Path.GetFullPath(assemblyPath), StringComparison.OrdinalIgnoreCase)) continue;
                throw new FileLoadException($"{simpleName} was loaded from '{alreadyLoaded.Location}' before the staged CadCore package could be activated.");
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

    private static Version ReadBundledProductVersion()
    {
        var candidate = FindBundledCadCoreAssembly();
        return candidate is not null && TryReadFileProductVersion(candidate, out var version) ? version : new Version(0, 0, 0);
    }

    private static Version ReadBundledAbiVersion()
    {
        var candidate = FindBundledCadCoreAssembly();
        var version = candidate is null ? null : AssemblyName.GetAssemblyName(candidate).Version;
        return version ?? new Version(0, 0, 0, 0);
    }

    private static string? FindBundledCadCoreAssembly()
    {
        var bootstrapAssemblyDirectory = Path.GetDirectoryName(typeof(CadCoreRuntimeBootstrapper).Assembly.Location);
        var baseDirectory = string.IsNullOrWhiteSpace(bootstrapAssemblyDirectory) ? AppContext.BaseDirectory : bootstrapAssemblyDirectory;
        var directPath = Path.Combine(baseDirectory, "SpatialViewer.Formats.Cad.ACadSharp.dll");
        return File.Exists(directPath)
            ? directPath
            : Directory.EnumerateFiles(baseDirectory, "SpatialViewer.Formats.Cad.ACadSharp.dll", SearchOption.AllDirectories).FirstOrDefault();
    }

    internal static bool TryReadFileProductVersion(string assemblyPath, out Version version)
    {
        version = new Version(0, 0, 0);
        var fileVersion = FileVersionInfo.GetVersionInfo(assemblyPath).FileVersion;
        if (!Version.TryParse(fileVersion, out var parsed) || parsed is null) return false;
        version = NormalizeVersion(parsed);
        return true;
    }

    private static string ResolveKernelRoot()
    {
        var overrideRoot = Environment.GetEnvironmentVariable(KernelRootOverrideEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overrideRoot)) return Path.GetFullPath(overrideRoot);
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpatialViewer",
            "Kernels",
            "CadCore");
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
            if (!TryReadString(root, "abiVersion", out var abiVersionText) || !Version.TryParse(abiVersionText, out var parsedAbiVersion) || parsedAbiVersion is null)
                return Fail("The CadCore package ABI version is invalid.", out error);
            var abiVersion = parsedAbiVersion;
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
                if (assemblyVersion is null || assemblyVersion != abiVersion)
                    return Fail($"CadCore ABI version does not match the release manifest: {pair.Key}.dll", out error);
                if (!CadCoreRuntimeBootstrapper.TryReadFileProductVersion(assemblyPath, out var fileProductVersion) || fileProductVersion != version)
                    return Fail($"CadCore file product version does not match the release manifest: {pair.Key}.dll", out error);
                requiredAssemblies[pair.Key] = Path.GetFullPath(assemblyPath);
            }

            var resolverAssemblies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dll in Directory.EnumerateFiles(Path.Combine(fullDirectory, "bin"), "*.dll", SearchOption.AllDirectories))
            {
                var simpleName = Path.GetFileNameWithoutExtension(dll);
                resolverAssemblies.TryAdd(simpleName, Path.GetFullPath(dll));
            }
            foreach (var pair in requiredAssemblies) resolverAssemblies[pair.Key] = pair.Value;

            package = new CadCorePackageDescriptor(version, abiVersion, fullDirectory, requiredAssemblies, resolverAssemblies);
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
