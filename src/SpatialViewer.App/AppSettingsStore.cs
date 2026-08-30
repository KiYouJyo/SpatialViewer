using System.Text.Json;

namespace SpatialViewer.Product;

internal enum AppLanguagePreference { System, SimplifiedChinese, Japanese, English }
// Retained only so existing v0.2 JSON can be migrated without data loss. The
// viewer now always follows the application theme and exposes no separate UI.
internal enum ViewerThemePreference { FollowApp, Light, Dark }
internal enum DrawingBackgroundPreference { FollowMode, Dark, Light }

internal sealed record AppSettings(
    AppLanguagePreference Language = AppLanguagePreference.System,
    bool RestoreLastSession = true,
    bool RecordRecentFiles = true,
    bool AutoCheckFileChanges = true,
    bool FitToWindowOnOpen = true,
    ViewerThemePreference ViewerTheme = ViewerThemePreference.FollowApp,
    DrawingBackgroundPreference DrawingBackground = DrawingBackgroundPreference.FollowMode);

/// <summary>Owns persisted user-facing settings for the product shell and viewer.</summary>
internal static class AppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SpatialViewer",
        "settings-v0.2.json");
    private static AppSettings _current = LoadCore();

    public static event EventHandler? Changed;
    public static AppSettings Current => _current;

    public static void Update(Func<AppSettings, AppSettings> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        var next = update(_current) with { ViewerTheme = ViewerThemePreference.FollowApp };
        if (next == _current) return;
        _current = next;
        SaveCore(next);
        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static AppSettings LoadCore()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
            // v0.2 briefly exposed a viewer-only theme. Migrate every existing
            // installation back to the application theme on first load.
            return loaded with { ViewerTheme = ViewerThemePreference.FollowApp };
        }
        catch (IOException) { return new AppSettings(); }
        catch (JsonException) { return new AppSettings(); }
    }

    private static void SaveCore(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var temporaryPath = $"{SettingsPath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, SerializerOptions));
            File.Move(temporaryPath, SettingsPath, overwrite: true);
        }
        catch (IOException)
        {
            // Settings persistence must never block an interactive change.
        }
    }
}
