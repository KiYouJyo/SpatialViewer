using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
    private readonly CadCoreUpdateService _cadCoreUpdate = new();
    private GitHubReleaseInfo? _latestProductRelease;
    private CadCoreUpdateResult _cadCoreUpdateResult;
    private AboutLayoutMode _layoutMode = AboutLayoutMode.Wide;

    public AboutView()
    {
        _cadCoreUpdateResult = new CadCoreUpdateResult(CadCoreUpdateState.NotChecked, CadCoreRuntimeBootstrapper.CurrentVersion);
        InitializeComponent();
        ApplyLocalizedText();
        RenderCadCoreUpdate(_cadCoreUpdateResult);
    }

    private async void CheckAppUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        CheckAppUpdateButton.IsEnabled = false;
        AppUpdateStatusText.Text = T("Update_Checking");
        try
        {
            _latestProductRelease = await GitHubUpdateService.GetLatestReleaseAsync(ProductRepository);
            if (_latestProductRelease is null)
            {
                AvailableAppVersionText.Text = "—";
                AppUpdateStatusText.Text = T("Update_NoRelease");
                return;
            }

            AvailableAppVersionText.Text = $"v{_latestProductRelease.DisplayVersion}";
            AppUpdateStatusText.Text = GitHubUpdateService.IsNewer(_latestProductRelease.TagName, CurrentProductVersion())
                ? T("Update_NewVersion")
                : T("Update_Latest");
        }
        catch (HttpRequestException)
        {
            AppUpdateStatusText.Text = T("Update_NetworkFailed");
        }
        catch (TaskCanceledException)
        {
            AppUpdateStatusText.Text = T("Update_Timeout");
        }
        finally
        {
            CheckAppUpdateButton.IsEnabled = true;
        }
    }

    private async void CheckCadUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_cadCoreUpdateResult.State == CadCoreUpdateState.ReadyForRestart) return;
        if (_cadCoreUpdateResult.State == CadCoreUpdateState.UpdateAvailable)
        {
            await DownloadCadCoreUpdateAsync();
            return;
        }

        CheckCadUpdateButton.IsEnabled = false;
        _cadCoreUpdateResult = _cadCoreUpdateResult with { State = CadCoreUpdateState.Checking, ErrorCode = null };
        RenderCadCoreUpdate(_cadCoreUpdateResult);
        _cadCoreUpdateResult = await _cadCoreUpdate.CheckForUpdatesAsync();
        RenderCadCoreUpdate(_cadCoreUpdateResult);
    }

    private async Task DownloadCadCoreUpdateAsync()
    {
        CheckCadUpdateButton.IsEnabled = false;
        var progress = new Progress<CadCoreUpdateProgress>(value =>
        {
            _cadCoreUpdateResult = _cadCoreUpdateResult with { State = value.State };
            RenderCadCoreUpdate(_cadCoreUpdateResult, value.Fraction);
        });
        try
        {
            _cadCoreUpdateResult = await _cadCoreUpdate.DownloadAndStageAsync(progress);
        }
        catch (OperationCanceledException)
        {
            _cadCoreUpdateResult = _cadCoreUpdateResult with { State = CadCoreUpdateState.Failed, ErrorCode = "Cancelled" };
        }
        RenderCadCoreUpdate(_cadCoreUpdateResult);
    }

    private void RenderCadCoreUpdate(CadCoreUpdateResult result, double? progress = null)
    {
        var current = $"v{CadCoreRuntimeBootstrapper.FormatVersion(result.CurrentVersion)}";
        var available = result.AvailableVersion is null ? "—" : $"v{CadCoreRuntimeBootstrapper.FormatVersion(result.AvailableVersion)}";
        CadVersionSummaryText.Text = $"{T("About_CurrentVersion")} {current} · {T("About_AvailableVersion")} {available}";

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
                CheckCadUpdateButton.Content = T("Update_WaitingRestart");
                CheckCadUpdateButton.IsEnabled = false;
                break;
            case CadCoreUpdateState.Failed:
                CadUpdateStatusText.Text = ResolveCadUpdateError(result.ErrorCode);
                CheckCadUpdateButton.Content = T("Update_Retry");
                CheckCadUpdateButton.IsEnabled = true;
                break;
        }
    }

    private string ResolveCadUpdateError(string? errorCode) => errorCode switch
    {
        "NoRelease" => T("Update_CadNoRelease"),
        "Timeout" => T("Update_Timeout"),
        "Network" => T("Update_NetworkFailed"),
        "MissingAsset" => T("Update_CadMissingAsset"),
        "Cancelled" => T("Update_CadCancelled"),
        _ => T("Update_CadFailed")
    };

    private async void ReleaseNotesButton_Click(object sender, RoutedEventArgs e) =>
        await Launcher.LaunchUriAsync(_latestProductRelease is { HtmlUrl.Length: > 0 } release ? new Uri(release.HtmlUrl) : ReleasesUri);

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

        var compact = _layoutMode == AboutLayoutMode.Compact;
        Grid.SetRow(AppUpdateButtons, compact ? 1 : 0);
        Grid.SetColumn(AppUpdateButtons, compact ? 0 : 1);
        AppUpdateButtons.HorizontalAlignment = compact ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        AppUpdateButtons.Margin = compact ? new Thickness(0, 4, 0, 0) : new Thickness(0);

        Grid.SetRow(CadCoreButtons, compact ? 1 : 0);
        Grid.SetColumn(CadCoreButtons, compact ? 0 : 1);
        CadCoreButtons.HorizontalAlignment = compact ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        CadCoreButtons.Margin = compact ? new Thickness(0, 4, 0, 0) : new Thickness(0);
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
        CadCoreDescriptionText.Text = T("About_CadCore_Desc");
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
        AppUpdateStatusText.Text = T("Update_NotChecked");
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
