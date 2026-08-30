using System.Text.Json;

namespace SpatialViewer.Product;

internal static class SessionStateStore
{
    private static readonly string SessionPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SpatialViewer",
        "last-session.json");

    public static IReadOnlyList<string> Load()
    {
        try
        {
            if (!File.Exists(SessionPath)) return Array.Empty<string>();
            var state = JsonSerializer.Deserialize<SavedSession>(File.ReadAllText(SessionPath));
            return state?.Files.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>();
        }
        catch (IOException) { return Array.Empty<string>(); }
        catch (JsonException) { return Array.Empty<string>(); }
    }

    public static void Save(IEnumerable<string> files)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SessionPath)!);
            var state = new SavedSession(files.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            var temporary = $"{SessionPath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(state));
            File.Move(temporary, SessionPath, overwrite: true);
        }
        catch (IOException)
        {
            // Session persistence must never block shutdown.
        }
    }

    private sealed record SavedSession(string[] Files);
}
