using System.ComponentModel;
using System.Runtime.CompilerServices;
using SpatialViewer.Core;

namespace SpatialViewer.Presentation;

public enum DocumentSessionState { Loading, Ready, Failed, Cancelled, Closed }

/// <summary>Owns one document's viewer state. It deliberately contains no WinUI or reader-adapter type.</summary>
public sealed class DocumentSession : INotifyPropertyChanged, IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private IDocument? _document;
    private ObjectId? _selection;
    private DocumentSessionState _state = DocumentSessionState.Loading;
    private string? _errorMessage;
    private bool _disposed;

    public DocumentSession(string filePath)
    {
        FilePath = Path.GetFullPath(filePath);
        DisplayName = Path.GetFileName(filePath);
        Camera = new Camera2D(Point2D.Origin);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public string FilePath { get; }
    public string DisplayName { get; }
    public Camera2D Camera { get; }
    public IDocument? Document { get => _document; private set { _document = value; OnChanged(); OnChanged(nameof(Layers)); OnChanged(nameof(Diagnostics)); } }
    public IReadOnlyList<Layer> Layers => Document?.Layers ?? Array.Empty<Layer>();
    public IReadOnlyList<Diagnostic> Diagnostics => Document?.Diagnostics ?? Array.Empty<Diagnostic>();
    public ObjectId? Selection { get => _selection; set { _selection = value; OnChanged(); } }
    public DocumentSessionState State { get => _state; private set { _state = value; OnChanged(); OnChanged(nameof(IsLoading)); } }
    public bool IsLoading => State == DocumentSessionState.Loading;
    public string? ErrorMessage { get => _errorMessage; private set { _errorMessage = value; OnChanged(); } }
    public CancellationToken CancellationToken => _cancellation.Token;

    public async Task LoadAsync(IDocumentImporter importer, IProgress<ImportProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(importer);
        ObjectDisposedException.ThrowIf(_disposed, this);
        try
        {
            var result = await importer.ImportAsync(new ImportRequest(FilePath), progress, _cancellation.Token).ConfigureAwait(false);
            if (_disposed) return;
            if (result.Document is null)
            {
                ErrorMessage = result.Diagnostics.Count > 0 ? result.Diagnostics[0].Message : "Unable to open document.";
                State = DocumentSessionState.Failed;
                return;
            }
            Document = result.Document;
            Camera.Fit(Document.Bounds, new Size2D(1280, 720));
            State = DocumentSessionState.Ready;
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
            State = DocumentSessionState.Cancelled;
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            State = DocumentSessionState.Failed;
        }
    }

    public void Close()
    {
        if (_disposed) return;
        _cancellation.Cancel();
        State = DocumentSessionState.Closed;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Close();
        _cancellation.Dispose();
        _disposed = true;
    }

    private void OnChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
