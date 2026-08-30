using System.Text.Json;
using Windows.Globalization;

namespace SpatialViewer.Product;

internal enum AppLanguagePreference { SimplifiedChinese, Japanese, English }
internal enum ViewerThemePreference { FollowApp, Light, Dark }
internal enum DrawingBackgroundPreference { FollowMode, Dark, Light }

internal sealed record AppSettings(
    AppLanguagePreference Language = AppLanguagePreference.SimplifiedChinese,
    bool RestoreLastSession = true,
    bool RecordRecentFiles = true,
    bool AutoCheckFileChanges = true,
    bool FitToWindowOnOpen = true,
    ViewerThemePreference ViewerTheme = ViewerThemePreference.FollowApp,
    DrawingBackgroundPreference DrawingBackground = DrawingBackgroundPreference.FollowMode);

/// <summary>Owns persisted user-facing settings for the product shell and viewer.</summary>
internal static class AppSettingsStore
{
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
        var next = update(_current);
        if (next == _current) return;
        _current = next;
        SaveCore(next);
        ApplyLanguage(next.Language);
        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static void ApplySavedLanguage() => ApplyLanguage(_current.Language);

    private static AppSettings LoadCore()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
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
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, SettingsPath, overwrite: true);
        }
        catch (IOException)
        {
            // Settings persistence must never block an interactive change.
        }
    }

    private static void ApplyLanguage(AppLanguagePreference language)
    {
        ApplicationLanguages.PrimaryLanguageOverride = language switch
        {
            AppLanguagePreference.Japanese => "ja-JP",
            AppLanguagePreference.English => "en-US",
            _ => "zh-CN"
        };
    }
}
