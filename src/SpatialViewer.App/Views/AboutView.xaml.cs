using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;
using System.Reflection;
using Windows.System;

namespace SpatialViewer.Product.Views;

public sealed partial class AboutView : UserControl
{
    private const string ProductRepository = "KiYouJyo/SpatialViewer";
    private static readonly Uri ProductRepositoryUri = new("https://github.com/KiYouJyo/SpatialViewer");
    private static readonly Uri CadCoreRepositoryUri = new("https://github.com/KiYouJyo/SpatialViewer.CadCore");
    private static readonly Uri ReleasesUri = new("https://github.com/KiYouJyo/SpatialViewer/releases");
    private static readonly Uri PrivacyUri = new("https://github.com/KiYouJyo/SpatialViewer/blob/main/PRIVACY.md");
    private readonly AppLocalizationService _localization = AppLocalizationService.Default;
    private readonly AboutUpdateSessionState _updates = AboutUpdateSessionState.Default;
    private AboutLayoutMode _layoutMode = AboutLayoutMode.Wide;

    public AboutView()
    {
        InitializeComponent();
        ApplyLocalizedText();
        Loaded += AboutView_Loaded;
        Unloaded += AboutView_Unloaded;
        RenderProductUpdate();
        RenderCadCoreUpdate(_updates.CadCoreResult);
    }

    private void AboutView_Loaded(object sender, RoutedEventArgs e)
    {
        // Exactly one observer per visible About page. The operation/state owner
        // lives for the process, while recreated pages attach and immediately
        // render the current state just like UrbanPlanToolbox's shared updater.
        _updates.Changed -= Updates_Changed;
        _updates.Changed += Updates_Changed;
        RenderProductUpdate();
        RenderCadCoreUpdate(_updates.CadCoreResult);
    }

    private void AboutView_Unloaded(object sender, RoutedEventArgs e) => _updates.Changed -= Updates_Changed;

    private void Updates_Changed(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (XamlRoot is null) return;
            RenderProductUpdate();
            RenderCadCoreUpdate(_updates.CadCoreResult);
        });
    }

    private async void CheckAppUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        CheckAppUpdateButton.IsEnabled = false;
        _updates.ProductState = ProductUpdateCheckState.Checking;
        RenderProductUpdate();
        try
        {
            var release = await GitHubUpdateService.GetLatestReleaseAsync(ProductRepository);
            _updates.LatestProductRelease = release;
            if (release is null)
            {
                _updates.ProductState = ProductUpdateCheckState.NoRelease;
            }
            else
            {
                _updates.ProductState = GitHubUpdateService.IsNewer(release.TagName, CurrentProductVersion())
                    ? ProductUpdateCheckState.NewVersion
                    : ProductUpdateCheckState.Latest;
            }
        }
        catch (HttpRequestException)
        {
            _updates.ProductState = ProductUpdateCheckState.NetworkFailed;
        }
        catch (TaskCanceledException)
        {
            _updates.ProductState = ProductUpdateCheckState.Timeout;
        }
        finally
        {
            RenderProductUpdate();
            CheckAppUpdateButton.IsEnabled = true;
        }
    }

    private void RenderProductUpdate()
    {
        AvailableAppVersionText.Text = _updates.LatestProductRelease is null
            ? "—"
            : $"v{_updates.LatestProductRelease.DisplayVersion}";
        AppUpdateStatusText.Text = _updates.ProductState switch
        {
            ProductUpdateCheckState.Checking => T("Update_Checking"),
            ProductUpdateCheckState.NoRelease => T("Update_NoRelease"),
            ProductUpdateCheckState.NewVersion => T("Update_NewVersion"),
            ProductUpdateCheckState.Latest => T("Update_Latest"),
            ProductUpdateCheckState.NetworkFailed => T("Update_NetworkFailed"),
            ProductUpdateCheckState.Timeout => T("Update_Timeout"),
            _ => T("Update_NotChecked")
        };
        CheckAppUpdateButton.IsEnabled = _updates.ProductState != ProductUpdateCheckState.Checking;
    }

    private async void CheckCadUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        var result = _updates.CadCoreResult;
        if (result.State == CadCoreUpdateState.ReadyForRestart)
        {
            RestartToApplyCadCoreUpdate();
            return;
        }
        if (result.State == CadCoreUpdateState.UpdateAvailable)
        {
            await DownloadCadCoreUpdateAsync();
            return;
        }

        CheckCadUpdateButton.IsEnabled = false;
        _updates.CadCoreResult = result with { State = CadCoreUpdateState.Checking, ErrorCode = null, ErrorDetail = null };
        RenderCadCoreUpdate(_updates.CadCoreResult);
        _updates.CadCoreResult = await _updates.CadCoreUpdateService.CheckForUpdatesAsync();
        RenderCadCoreUpdate(_updates.CadCoreResult);
    }

    private async Task DownloadCadCoreUpdateAsync()
    {
        CheckCadUpdateButton.IsEnabled = false;
        var progress = new Progress<CadCoreUpdateProgress>(value =>
        {
            _updates.CadCoreResult = _updates.CadCoreResult with { State = value.State };
            RenderCadCoreUpdate(_updates.CadCoreResult, value.Fraction);
        });
        try
        {
            _updates.CadCoreResult = await _updates.CadCoreUpdateService.DownloadAndStageAsync(progress);
        }
        catch (OperationCanceledException)
        {
            _updates.CadCoreResult = _updates.CadCoreResult with { State = CadCoreUpdateState.Failed, ErrorCode = "Cancelled", ErrorDetail = null };
        }
        RenderCadCoreUpdate(_updates.CadCoreResult);
    }

    private void RestartToApplyCadCoreUpdate()
    {
        // AppInstance.Restart terminates this process and starts a new instance when successful.
        // CadCoreRuntimeBootstrapper runs before XAML initialization in the restarted process,
        // so the staged pending kernel is activated before any static CadCore reference is touched.
        var failureReason = AppInstance.Restart(string.Empty);
        _updates.CadCoreResult = _updates.CadCoreResult with
        {
            State = CadCoreUpdateState.Failed,
            ErrorCode = "RestartFailed",
            ErrorDetail = $"Windows App SDK AppInstance.Restart failed: {failureReason}."
        };
        RenderCadCoreUpdate(_updates.CadCoreResult);
    }

    private void RenderCadCoreUpdate(CadCoreUpdateResult result, double? progress = null)
    {
        ToolTipService.SetToolTip(CadUpdateStatusText, null);
        CadCurrentVersionText.Text = $"v{CadCoreRuntimeBootstrapper.FormatVersion(result.CurrentVersion)}";
        CadAvailableVersionText.Text = result.AvailableVersion is null
            ? "—"
            : $"v{CadCoreRuntimeBootstrapper.FormatVersion(result.AvailableVersion)}";

        switch (result.State)
        {
            case CadCoreUpdateState.NotChecked:
                CadUpdateStatusText.Text = T("Update_NotChecked");
                CheckCadUpdateButton.Content = T("About_CheckUpdates");
                CheckCadUpdateButton.IsEnabled = true;
                break;
            case CadCoreUpdateState.Checking:
                CadUpdateStatusText.Text = T("Update_Checking");
                CheckCadUpdateButton.Content = T("About_CheckUpdates");
                CheckCadUpdateButton.IsEnabled = false;
                break;
            case CadCoreUpdateState.UpToDate:
                CadUpdateStatusText.Text = T("Update_CadUpToDate");
                CheckCadUpdateButton.Content = T("About_CheckUpdates");
                CheckCadUpdateButton.IsEnabled = true;
                break;
            case CadCoreUpdateState.UpdateAvailable:
                CadUpdateStatusText.Text = T("Update_CadAvailable");
                CheckCadUpdateButton.Content = T("Update_Download");
                CheckCadUpdateButton.IsEnabled = true;
                break;
            case CadCoreUpdateState.Downloading:
                CadUpdateStatusText.Text = progress is null
                    ? T("Update_CadDownloading")
                    : $"{T("Update_CadDownloading")} {progress.Value:P0}";
                CheckCadUpdateButton.Content = T("Update_Download");
                CheckCadUpdateButton.IsEnabled = false;
                break;
            case CadCoreUpdateState.Verifying:
                CadUpdateStatusText.Text = T("Update_CadVerifying");
                CheckCadUpdateButton.Content = T("Update_Download");
                CheckCadUpdateButton.IsEnabled = false;
                break;
            case CadCoreUpdateState.ReadyForRestart:
                CadUpdateStatusText.Text = T("Update_CadReadyRestart");
                CheckCadUpdateButton.Content = RestartUpdateButtonText();
                CheckCadUpdateButton.IsEnabled = true;
                break;
            case CadCoreUpdateState.Failed:
                CadUpdateStatusText.Text = ResolveCadUpdateError(result.ErrorCode);
                if (!string.IsNullOrWhiteSpace(result.ErrorDetail)) ToolTipService.SetToolTip(CadUpdateStatusText, result.ErrorDetail);
                CheckCadUpdateButton.Content = T("Update_Retry");
                CheckCadUpdateButton.IsEnabled = true;
                break;
        }
    }

    private string RestartUpdateButtonText() => _localization.CurrentLanguage switch
    {
        "ja-JP" => "再起動して更新",
        "en-US" => "Restart to update",
        _ => "重启更新"
    };

    private string ResolveCadUpdateError(string? errorCode) => errorCode switch
    {
        "NoRelease" => T("Update_CadNoRelease"),
        "Timeout" or "DownloadTimeout" => T("Update_Timeout"),
        "Network" or "DownloadNetwork" => T("Update_NetworkFailed"),
        "MissingAsset" => T("Update_CadMissingAsset"),
        "Cancelled" => T("Update_CadCancelled"),
        null or "" => T("Update_CadFailed"),
        _ => $"{T("Update_CadFailed")} · {errorCode}"
    };

    private async void ReleaseNotesButton_Click(object sender, RoutedEventArgs e) =>
        await Launcher.LaunchUriAsync(_updates.LatestProductRelease is { HtmlUrl.Length: > 0 } release ? new Uri(release.HtmlUrl) : ReleasesUri);

    private async void OpenRepositoryButton_Click(object sender, RoutedEventArgs e) => await Launcher.LaunchUriAsync(ProductRepositoryUri);
    private async void OpenCadCoreRepositoryButton_Click(object sender, RoutedEventArgs e) => await Launcher.LaunchUriAsync(CadCoreRepositoryUri);
    private async void OpenReleasesButton_Click(object sender, RoutedEventArgs e) => await Launcher.LaunchUriAsync(ReleasesUri);
    private async void OpenPrivacyButton_Click(object sender, RoutedEventArgs e) => await Launcher.LaunchUriAsync(PrivacyUri);

    private void AboutRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var mode = e.NewSize.Width >= 1040 ? AboutLayoutMode.Wide : e.NewSize.Width >= 680 ? AboutLayoutMode.Medium : AboutLayoutMode.Compact;
        if (mode == _layoutMode && MetadataGrid.ColumnDefinitions.Count > 0) return;
        _layoutMode = mode;
        ApplyResponsiveLayout();
    }

    private void ApplyResponsiveLayout()
    {
        ConfigureMetadataGrid(_layoutMode);
        ConfigureProjectGrid(_layoutMode);
    }

    private void ConfigureMetadataGrid(AboutLayoutMode mode)
    {
        MetadataGrid.ColumnDefinitions.Clear();
        MetadataGrid.RowDefinitions.Clear();
        if (mode == AboutLayoutMode.Wide)
        {
            AddColumns(MetadataGrid, 3);
            Grid.SetRow(MetadataColumn0, 0); Grid.SetColumn(MetadataColumn0, 0); Grid.SetColumnSpan(MetadataColumn0, 1);
            Grid.SetRow(MetadataColumn1, 0); Grid.SetColumn(MetadataColumn1, 1); Grid.SetColumnSpan(MetadataColumn1, 1);
            Grid.SetRow(MetadataColumn2, 0); Grid.SetColumn(MetadataColumn2, 2); Grid.SetColumnSpan(MetadataColumn2, 1);
            return;
        }

        if (mode == AboutLayoutMode.Medium)
        {
            AddColumns(MetadataGrid, 2); AddRows(MetadataGrid, 2);
            Grid.SetRow(MetadataColumn0, 0); Grid.SetColumn(MetadataColumn0, 0); Grid.SetColumnSpan(MetadataColumn0, 1);
            Grid.SetRow(MetadataColumn1, 0); Grid.SetColumn(MetadataColumn1, 1); Grid.SetColumnSpan(MetadataColumn1, 1);
            Grid.SetRow(MetadataColumn2, 1); Grid.SetColumn(MetadataColumn2, 0); Grid.SetColumnSpan(MetadataColumn2, 2);
            return;
        }

        AddColumns(MetadataGrid, 1); AddRows(MetadataGrid, 3);
        Grid.SetRow(MetadataColumn0, 0); Grid.SetColumn(MetadataColumn0, 0); Grid.SetColumnSpan(MetadataColumn0, 1);
        Grid.SetRow(MetadataColumn1, 1); Grid.SetColumn(MetadataColumn1, 0); Grid.SetColumnSpan(MetadataColumn1, 1);
        Grid.SetRow(MetadataColumn2, 2); Grid.SetColumn(MetadataColumn2, 0); Grid.SetColumnSpan(MetadataColumn2, 1);
    }

    private void ConfigureProjectGrid(AboutLayoutMode mode)
    {
        ProjectLinksGrid.ColumnDefinitions.Clear();
        ProjectLinksGrid.RowDefinitions.Clear();
        if (mode == AboutLayoutMode.Wide)
        {
            AddColumns(ProjectLinksGrid, 3);
            Place(RepositoryCard, 0, 0, 1); Place(ReleasesCard, 0, 1, 1); Place(PrivacyCard, 0, 2, 1);
            return;
        }
        if (mode == AboutLayoutMode.Medium)
        {
            AddColumns(ProjectLinksGrid, 2); AddRows(ProjectLinksGrid, 2);
            Place(RepositoryCard, 0, 0, 1); Place(ReleasesCard, 0, 1, 1); Place(PrivacyCard, 1, 0, 2);
            return;
        }
        AddColumns(ProjectLinksGrid, 1); AddRows(ProjectLinksGrid, 3);
        Place(RepositoryCard, 0, 0, 1); Place(ReleasesCard, 1, 0, 1); Place(PrivacyCard, 2, 0, 1);
    }

    private void ApplyLocalizedText()
    {
        AboutTitleText.Text = T("About_Title");
        ProductNameText.Text = T("AppName");
        TaglineText.Text = T("About_Tagline");
        DisplayVersionLabel.Text = T("About_DisplayVersion");
        ChannelLabel.Text = T("About_Channel");
        InternalVersionLabel.Text = T("About_InternalVersion");
        PublisherLabel.Text = T("About_Publisher");
        ArchitectureLabel.Text = T("About_Architecture");
        TechStackLabel.Text = T("About_TechStack");
        UpdateManagementTitleText.Text = T("About_UpdateManagement");
        AppProgramTitleText.Text = T("About_AppProgram");
        AppUpdateDescriptionText.Text = T("About_AppUpdate");
        CurrentVersionLabel.Text = T("About_CurrentVersion");
        AvailableVersionLabel.Text = T("About_AvailableVersion");
        UpdateSourceLabel.Text = T("About_UpdateSource");
        StatusLabel.Text = T("About_Status");
        ReleaseNotesButton.Content = T("About_ReleaseNotes");
        CheckAppUpdateButton.Content = T("About_CheckUpdates");
        KernelsTitleText.Text = T("About_Kernels");
        DisabledCheckButton1.Content = T("About_CheckUpdates");
        DisabledCheckButton2.Content = T("About_CheckUpdates");
        DisabledCheckButton3.Content = T("About_CheckUpdates");
        ProjectOpenSourceTitleText.Text = T("About_ProjectOpenSource");
        RepositoryTitleText.Text = T("About_Repository");
        RepositoryDescriptionText.Text = T("About_Repository_Desc");
        OpenRepositoryButton.Content = T("About_OpenRepository");
        ReleasesDescriptionText.Text = T("About_Releases_Desc");
        OpenReleasesButton.Content = T("About_ViewVersions");
        LicensePrivacyTitleText.Text = T("About_LicensePrivacy");
        LicensePrivacyDescriptionText.Text = T("About_LicensePrivacy_Desc");
        OpenPrivacyButton.Content = T("About_ViewInfo");
    }

    private string T(string key) => _localization.GetString(key);

    private static Version CurrentProductVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? new Version(0, 0) : new Version(version.Major, version.Minor, Math.Max(0, version.Build));
    }

    private static void AddColumns(Grid grid, int count)
    {
        for (var index = 0; index < count; index++) grid.ColumnDefinitions.Add(new ColumnDefinition());
    }

    private static void AddRows(Grid grid, int count)
    {
        for (var index = 0; index < count; index++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
    }

    private static void Place(FrameworkElement element, int row, int column, int columnSpan)
    {
        Grid.SetRow(element, row); Grid.SetColumn(element, column); Grid.SetColumnSpan(element, columnSpan);
    }
}

internal enum AboutLayoutMode { Wide, Medium, Compact }
