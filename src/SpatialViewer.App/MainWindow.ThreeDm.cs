using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SpatialViewer.Product.Views;

namespace SpatialViewer.Product;

public sealed partial class MainWindow
{
    private readonly Dictionary<string, ThreeDmProductSession> _threeDmByPath =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ThreeDmProductSession, ShellTabVisual> _threeDmTabs = new();
    private readonly Dictionary<ThreeDmProductSession, ThreeDmViewerView> _threeDmViews = new();

    private IEnumerable<string> OpenDocumentPaths() =>
        _workspace.Documents.Select(document => document.FilePath)
            .Concat(_threeDmByPath.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private void OpenOrFocusThreeDm(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (_threeDmByPath.TryGetValue(fullPath, out var existing))
        {
            ShowThreeDmDocument(existing);
            return;
        }

        var session = new ThreeDmProductSession(fullPath);
        _threeDmByPath.Add(fullPath, session);
        _ = LoadThreeDmSessionAsync(session);
        ShowThreeDmDocument(session);
    }

    private async Task LoadThreeDmSessionAsync(ThreeDmProductSession session)
    {
        await session.LoadAsync();
        if (session.State == ThreeDmProductSessionState.Ready &&
            AppSettingsStore.Current.RecordRecentFiles)
        {
            await _recentFiles.RecordAsync(session.FilePath);
        }

        if (_threeDmViews.TryGetValue(session, out var view)) view.RefreshSessionState();
        if (Equals(_selectedTab, session)) ShowThreeDmDocument(session);
    }

    private void ShowThreeDmDocument(ThreeDmProductSession session)
    {
        ShowViewerChrome();
        SelectShellItem(null);
        SelectTab(EnsureThreeDmTab(session));

        foreach (var pair in _documentViews)
            pair.Value.Visibility = Visibility.Collapsed;

        var activeView = EnsureThreeDmView(session);
        foreach (var pair in _threeDmViews)
            pair.Value.Visibility = ReferenceEquals(pair.Key, session)
                ? Visibility.Visible
                : Visibility.Collapsed;

        if (!IsDocumentSurfaceVisible) MainContent.Content = _documentViewHost;
        if (activeView.Visibility != Visibility.Visible) activeView.Visibility = Visibility.Visible;
    }

    private ThreeDmProductSession EnsureThreeDmTab(ThreeDmProductSession session)
    {
        if (_threeDmTabs.ContainsKey(session)) return session;
        _threeDmTabs.Add(session, CreateTabVisual(session, session.DisplayName, Symbol.View, 220));
        return session;
    }

    private ThreeDmViewerView EnsureThreeDmView(ThreeDmProductSession session)
    {
        if (_threeDmViews.TryGetValue(session, out var existing)) return existing;
        var view = new ThreeDmViewerView(session) { Visibility = Visibility.Collapsed };
        _threeDmViews.Add(session, view);
        _documentViewHost.Children.Add(view);
        return view;
    }

    private void HideThreeDmViews()
    {
        foreach (var view in _threeDmViews.Values) view.Visibility = Visibility.Collapsed;
    }

    private void DisposeThreeDmView(ThreeDmProductSession session)
    {
        if (!_threeDmViews.Remove(session, out var view)) return;
        _documentViewHost.Children.Remove(view);
        view.Dispose();
    }

    private void DisposeThreeDmViews()
    {
        foreach (var view in _threeDmViews.Values) view.Dispose();
        _threeDmViews.Clear();
    }

    private void DisposeThreeDmSessions()
    {
        foreach (var session in _threeDmByPath.Values) session.Dispose();
        _threeDmByPath.Clear();
        _threeDmTabs.Clear();
    }

    private void CloseThreeDmSession(ThreeDmProductSession session)
    {
        if (_threeDmTabs.Remove(session, out var tab))
            ShellTabItems.Children.Remove(tab.Container);
        DisposeThreeDmView(session);
        _threeDmByPath.Remove(session.FilePath);
        session.Dispose();
        ShowFallbackDocumentOrHome();
    }

    private void ShowFallbackDocumentOrHome()
    {
        if (_workspace.ActiveDocument is { } cad)
        {
            ShowDocument(cad);
            return;
        }

        if (_threeDmTabs.Keys.LastOrDefault() is { } threeDm)
        {
            ShowThreeDmDocument(threeDm);
            return;
        }

        if (_homeTabs.Keys.FirstOrDefault() is { } home)
        {
            ShowHome(home);
            return;
        }

        CreateHomeTab(select: true);
    }
}
