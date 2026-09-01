using System.Text.Json;

namespace SpatialViewer.Product;

public sealed record ProjectLibraryItem(
    Guid Id,
    string Name,
    IReadOnlyList<string> Files,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastOpenedAt);

public sealed class AppLibraryStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private LibraryState _state;

    public AppLibraryStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
        _state = Load(path);
    }

    public event EventHandler? Changed;

    public IReadOnlyList<ProjectLibraryItem> Projects
    {
        get
        {
            lock (_gate)
            {
                return _state.Projects
                    .OrderByDescending(project => project.LastOpenedAt)
                    .Select(ToModel)
                    .ToArray();
            }
        }
    }

    public IReadOnlyList<string> Favorites
    {
        get
        {
            lock (_gate)
            {
                return _state.Favorites
                    .Where(File.Exists)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase)
                    .ToArray();
            }
        }
    }

    public ProjectLibraryItem CreateProject(string name, IEnumerable<string>? files = null)
    {
        var normalizedName = string.IsNullOrWhiteSpace(name) ? "Untitled project" : name.Trim();
        var normalizedFiles = NormalizeFiles(files ?? []);
        ProjectRecord created;
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            created = new ProjectRecord
            {
                Id = Guid.NewGuid(),
                Name = normalizedName,
                Files = normalizedFiles.ToList(),
                CreatedAt = now,
                LastOpenedAt = now
            };
            _state.Projects.Add(created);
            PersistLocked();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return ToModel(created);
    }

    public void ReplaceProjectFiles(Guid projectId, IEnumerable<string> files)
    {
        var normalized = NormalizeFiles(files);
        lock (_gate)
        {
            var project = _state.Projects.FirstOrDefault(candidate => candidate.Id == projectId);
            if (project is null) return;
            project.Files = normalized.ToList();
            project.LastOpenedAt = DateTimeOffset.UtcNow;
            PersistLocked();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void TouchProject(Guid projectId)
    {
        lock (_gate)
        {
            var project = _state.Projects.FirstOrDefault(candidate => candidate.Id == projectId);
            if (project is null) return;
            project.LastOpenedAt = DateTimeOffset.UtcNow;
            PersistLocked();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveProject(Guid projectId)
    {
        var changed = false;
        lock (_gate)
        {
            changed = _state.Projects.RemoveAll(project => project.Id == projectId) > 0;
            if (changed) PersistLocked();
        }

        if (changed) Changed?.Invoke(this, EventArgs.Empty);
    }

    public void AddFavorite(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var normalized = NormalizePath(path);
        lock (_gate)
        {
            if (_state.Favorites.Contains(normalized, StringComparer.OrdinalIgnoreCase)) return;
            _state.Favorites.Add(normalized);
            PersistLocked();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveFavorite(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var normalized = NormalizePath(path);
        var changed = false;
        lock (_gate)
        {
            changed = _state.Favorites.RemoveAll(candidate => string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase)) > 0;
            if (changed) PersistLocked();
        }

        if (changed) Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool IsFavorite(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var normalized = NormalizePath(path);
        lock (_gate)
        {
            return _state.Favorites.Contains(normalized, StringComparer.OrdinalIgnoreCase);
        }
    }

    private static IReadOnlyList<string> NormalizeFiles(IEnumerable<string> files) => files
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Select(NormalizePath)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static string NormalizePath(string path) => Path.GetFullPath(path.Trim());

    private void PersistLocked()
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temporaryPath = $"{_path}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(_state, JsonOptions));
        File.Move(temporaryPath, _path, overwrite: true);
    }

    private static LibraryState Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new LibraryState();
            var state = JsonSerializer.Deserialize<LibraryState>(File.ReadAllText(path), JsonOptions) ?? new LibraryState();
            state.Projects ??= [];
            state.Favorites ??= [];
            foreach (var project in state.Projects)
            {
                project.Name = string.IsNullOrWhiteSpace(project.Name) ? "Untitled project" : project.Name.Trim();
                project.Files ??= [];
                if (project.Id == Guid.Empty) project.Id = Guid.NewGuid();
                if (project.CreatedAt == default) project.CreatedAt = DateTimeOffset.UtcNow;
                if (project.LastOpenedAt == default) project.LastOpenedAt = project.CreatedAt;
            }

            return state;
        }
        catch (JsonException)
        {
            return new LibraryState();
        }
        catch (IOException)
        {
            return new LibraryState();
        }
    }

    private static ProjectLibraryItem ToModel(ProjectRecord project) => new(
        project.Id,
        project.Name,
        project.Files.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
        project.CreatedAt,
        project.LastOpenedAt);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private sealed class LibraryState
    {
        public List<ProjectRecord> Projects { get; set; } = [];
        public List<string> Favorites { get; set; } = [];
    }

    private sealed class ProjectRecord
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<string> Files { get; set; } = [];
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset LastOpenedAt { get; set; }
    }
}
