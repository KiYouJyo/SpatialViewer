using System.Collections.ObjectModel;

namespace SpatialViewer.Presentation;

/// <summary>Tracks sessions and de-duplicates canonical file paths for a tabbed product shell.</summary>
public sealed class DocumentWorkspace
{
    private readonly Dictionary<string, DocumentSession> _byPath = new(StringComparer.OrdinalIgnoreCase);
    public ObservableCollection<DocumentSession> Documents { get; } = new();
    public DocumentSession? ActiveDocument { get; private set; }

    public DocumentSession OpenOrFocus(string filePath, out bool wasAlreadyOpen)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (_byPath.TryGetValue(fullPath, out var existing))
        {
            ActiveDocument = existing;
            wasAlreadyOpen = true;
            return existing;
        }
        var session = new DocumentSession(fullPath);
        Documents.Add(session);
        _byPath.Add(fullPath, session);
        ActiveDocument = session;
        wasAlreadyOpen = false;
        return session;
    }

    public bool Activate(DocumentSession session)
    {
        if (!Documents.Contains(session)) return false;
        ActiveDocument = session;
        return true;
    }

    public bool Close(DocumentSession session)
    {
        if (!Documents.Remove(session)) return false;
        _byPath.Remove(session.FilePath);
        session.Dispose();
        ActiveDocument = Documents.LastOrDefault();
        return true;
    }

    public void CloseAll()
    {
        foreach (var document in Documents.ToArray()) Close(document);
    }
}
