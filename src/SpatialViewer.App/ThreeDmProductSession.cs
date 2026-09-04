using System.ComponentModel;
using System.Runtime.CompilerServices;
using SpatialViewer.Formats.ThreeDm.Rhino3dm;
using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Integration;
using SpatialViewer.ThreeDm.Rendering;

namespace SpatialViewer.Product;

internal enum ThreeDmProductSessionState
{
    Loading,
    Ready,
    Failed,
    Closed,
}

internal sealed class ThreeDmProductSession : INotifyPropertyChanged, IDisposable
{
    private readonly ThreeDmSession _session = new(new Rhino3dmThreeDmImporter());
    private ThreeDmProductSessionState _state = ThreeDmProductSessionState.Loading;
    private string? _errorMessage;
    private int _processedObjects;
    private int _totalObjects;
    private ThreeDmPreparedRenderScene? _renderScene;
    private ThreeDmRenderDisplayMode _displayMode = ThreeDmRenderDisplayMode.ShadedWithEdges;
    private IReadOnlyList<ThreeDmViewPreset> _viewPresets = Array.Empty<ThreeDmViewPreset>();
    private bool _disposed;

    public ThreeDmProductSession(string filePath)
    {
        FilePath = Path.GetFullPath(filePath);
        DisplayName = Path.GetFileName(filePath);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string FilePath { get; }
    public string DisplayName { get; }
    public ThreeDmProductSessionState State
    {
        get => _state;
        private set
        {
            if (_state == value) return;
            _state = value;
            OnChanged();
            OnChanged(nameof(IsLoading));
        }
    }

    public bool IsLoading => State == ThreeDmProductSessionState.Loading;
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (string.Equals(_errorMessage, value, StringComparison.Ordinal)) return;
            _errorMessage = value;
            OnChanged();
        }
    }

    public int ProcessedObjects
    {
        get => _processedObjects;
        private set
        {
            if (_processedObjects == value) return;
            _processedObjects = value;
            OnChanged();
            OnChanged(nameof(ProgressFraction));
        }
    }

    public int TotalObjects
    {
        get => _totalObjects;
        private set
        {
            if (_totalObjects == value) return;
            _totalObjects = value;
            OnChanged();
            OnChanged(nameof(ProgressFraction));
        }
    }

    public double? ProgressFraction => TotalObjects > 0
        ? Math.Clamp((double)ProcessedObjects / TotalObjects, 0, 1)
        : null;

    public ThreeDmSceneDocument? Document => _session.Document;
    public ThreeDmPreparedRenderScene? RenderScene
    {
        get => _renderScene;
        private set
        {
            _renderScene = value;
            OnChanged();
        }
    }

    public ThreeDmRenderDisplayMode DisplayMode
    {
        get => _displayMode;
        private set
        {
            if (_displayMode == value) return;
            _displayMode = value;
            OnChanged();
        }
    }

    public IReadOnlyList<ThreeDmViewPreset> ViewPresets
    {
        get => _viewPresets;
        private set
        {
            _viewPresets = value;
            OnChanged();
        }
    }

    public IReadOnlyList<ThreeDmLayerNode> Layers =>
        State == ThreeDmProductSessionState.Ready ? _session.GetLayerTree() : Array.Empty<ThreeDmLayerNode>();

    public ThreeDmDocumentSummary? Summary =>
        State == ThreeDmProductSessionState.Ready ? _session.GetDocumentSummary() : null;

    public async Task LoadAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        State = ThreeDmProductSessionState.Loading;
        ErrorMessage = null;
        ProcessedObjects = 0;
        TotalObjects = 0;

        try
        {
            await _session.OpenProgressivelyAsync(
                FilePath,
                (update, _) =>
                {
                    switch (update)
                    {
                        case ThreeDmImportHeaderUpdate header:
                            TotalObjects = header.TotalObjects;
                            break;
                        case ThreeDmImportObjectBatchUpdate batch:
                            ProcessedObjects = batch.ProcessedObjects;
                            TotalObjects = batch.TotalObjects;
                            break;
                        case ThreeDmImportCompletedUpdate completed:
                            ProcessedObjects = completed.TotalObjects;
                            TotalObjects = completed.TotalObjects;
                            break;
                    }

                    return ValueTask.CompletedTask;
                });

            if (_disposed) return;
            RebuildRenderScene();
            var standard = _session.GetStandardViewPresets();
            var named = _session.GetNamedViewPresets();
            ViewPresets = standard.Concat(named).ToArray();
            State = ThreeDmProductSessionState.Ready;
            OnChanged(nameof(Document));
            OnChanged(nameof(Layers));
            OnChanged(nameof(Summary));
        }
        catch (OperationCanceledException)
        {
            if (!_disposed) State = ThreeDmProductSessionState.Closed;
        }
        catch (Exception exception)
        {
            if (_disposed) return;
            ErrorMessage = exception.Message;
            State = ThreeDmProductSessionState.Failed;
        }
    }

    public async Task ReloadAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (State != ThreeDmProductSessionState.Ready) return;
        await _session.CloseAsync();
        await LoadAsync();
    }

    public void SetDisplayMode(ThreeDmRenderDisplayMode mode)
    {
        if (State != ThreeDmProductSessionState.Ready || _disposed) return;
        DisplayMode = mode;
        RebuildRenderScene();
    }

    public void SetLayerVisibility(Guid layerId, bool? visible)
    {
        if (State != ThreeDmProductSessionState.Ready || _disposed) return;
        _session.SetLayerVisibility(layerId, visible);
        RebuildRenderScene();
        OnChanged(nameof(Layers));
    }

    public IReadOnlyList<ThreeDmSelectionId> GetSelectionIds() =>
        RenderScene is { } scene ? _session.GetSelectionIds(scene) : Array.Empty<ThreeDmSelectionId>();

    public ThreeDmSelectionProperties? GetSelectionProperties(ThreeDmSelectionId selectionId) =>
        State == ThreeDmProductSessionState.Ready ? _session.GetSelectionProperties(selectionId) : null;

    private void RebuildRenderScene()
    {
        RenderScene = _session.BuildPreparedRenderScene(new ThreeDmVisualRenderSettings(DisplayMode));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session.CancelOpen();
        State = ThreeDmProductSessionState.Closed;
        _ = _session.CloseAsync();
    }

    private void OnChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
