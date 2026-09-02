using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;
using System.Runtime.InteropServices;
using Windows.System;

namespace SpatialViewer.Product.Views;

public sealed partial class AboutView : UserControl
{
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
        PopulateApplicationInfo();
        Loaded += AboutView_Loaded;
        Unloaded += AboutView_Unloaded;
        RenderProductUpdate();
        RenderCadCoreUpdate(_updates.CadCoreResult);
    }

    private void AboutView_Loaded(object sender, RoutedEventArgs e)
    {
        _updates.Changed -= Updates_Changed;
        _updates.Changed += Updates_Changed;
        PopulateApplicationInfo();
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

    private void PopulateApplicationInfo()
    {
        DisplayVersionText.Text = AppVersionProvider.DisplayVersion;
        PackageVersionText.Text = AppVersionProvider.GetPackageVersion();
        ArchitectureText.Text = RuntimeInformation.ProcessArchitecture.ToString();
        CurrentAppVersionText.Text = AppVersionProvider.DisplayVersion;
    }

    private async void CheckAppUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        var info = _updates.ProductInfo;
        if (info.IsReadyToInstall)
        {
            await _updates.InstallProductUpdateAsync();
            return;
        }

        if (info.IsUpdateAvailable)
        {
            await _updates.DownloadProductUpdateAsync();
            return;
        }

        await _updates.CheckProductUpdateAsync();
    }

    private void RenderProductUpdate()
    {
        var info = _updates.ProductInfo;
        ToolTipService.SetToolTip(AppUpdateStatusText, null);
        AvailableAppVersionText.Text = string.IsNullOrWhiteSpace(info.AvailableVersion)
            ? "—"
            : $"v{info.AvailableVersion}";
        AppUpdateStatusText.Text = ResolveProductUpdateStatus(info);
        if (info.State == AppUpdateState.Failed && !string.IsNullOrWhiteSpace(info.Detail))
            ToolTipService.SetToolTip(AppUpdateStatusText, info.Detail);

        CheckAppUpdateButton.Content = info.State switch
        {
            AppUpdateState.UpdateAvailable => L("下载并验证", "ダウンロードして検証", "Download & verify"),
            AppUpdateState.ReadyToInstall => RestartUpdateButtonText(),
            AppUpdateState.Downloading => L("正在下载", "ダウンロード中", "Downloading"),
            AppUpdateState.Verifying => L("正在验证", "検証中", "Verifying"),
            AppUpdateState.Installing or AppUpdateState.Restarting => L("正在更新", "更新中", "Updating"),
            AppUpdateState.Failed or AppUpdateState.Cancelled => T("Update_Retry"),
            _ => T("About_CheckUpdates")
        };

        var busyState = info.State is AppUpdateState.Checking or AppUpdateState.Downloading or AppUpdateState.Verifying or AppUpdateState.Installing or AppUpdateState.Restarting;
        CheckAppUpdateButton.IsEnabled = _updates.CanOperateProductUpdate && !busyState;

        var progressVisible = info.State is AppUpdateState.Downloading or AppUpdateState.Verifying or AppUpdateState.Installing or AppUpdateState.Restarting;
        AppUpdateProgressBar.Visibility = progressVisible ? Visibility.Visible : Visibility.Collapsed;
        AppUpdateProgressBar.IsIndeterminate = info.State is not AppUpdateState.Downloading || _updates.ProductProgress is null;
        if (_updates.ProductProgress is double progress) AppUpdateProgressBar.Value = progress * 100d;
    }

    private string ResolveProductUpdateStatus(AppUpdateInfo info) => info.State switch
    {
        AppUpdateState.NotChecked => T("Update_NotChecked"),
        AppUpdateState.Checking => T("Update_Checking"),
        AppUpdateState.UpToDate => T("Update_Latest"),
        AppUpdateState.UpdateAvailable => T("Update_NewVersion"),
        AppUpdateState.Downloading => _updates.ProductProgress is double progress
            ? $"{L("正在下载…", "ダウンロード中…", "Downloading…")} {progress:P0}"
            : L("正在下载…", "ダウンロード中…", "Downloading…"),
        AppUpdateState.Verifying => L("正在验证安装包…", "インストールパッケージを検証中…", "Verifying package…"),
        AppUpdateState.ReadyToInstall => L("安装包已验证，等待更新", "パッケージ検証済み。更新できます", "Package verified; ready to update"),
        AppUpdateState.Installing => L("正在安装更新…", "更新をインストール中…", "Installing update…"),
        AppUpdateState.Restarting => L("正在重启并完成更新…", "再起動して更新を完了しています…", "Restarting to finish update…"),
        AppUpdateState.Completed => L("更新已完成", "更新が完了しました", "Update completed"),
        AppUpdateState.Cancelled => L("更新已取消", "更新をキャンセルしました", "Update cancelled"),
        AppUpdateState.Failed => ResolveProductUpdateError(info.ErrorCode),
        _ => T("Update_NotChecked")
    };

    private string ResolveProductUpdateError(string? errorCode) => errorCode switch
    {
        "ReleaseNotFound" => T("Update_NoRelease"),
        "UnableToContactGitHub" or "DownloadNetwork" or "DownloadTimeout" => T("Update_NetworkFailed"),
        "BundleAssetNotFound" => L("发行版缺少唯一的 MSIXBundle 或校验清单", "リリースに一意の MSIXBundle またはチェックサム一覧がありません", "The release is missing the unique MSIXBundle or checksum manifest"),
        "ChecksumMissing" => L("校验清单缺少安装包哈希", "チェックサム一覧にパッケージのハッシュがありません", "The checksum manifest does not contain the package hash"),
        "ChecksumMismatch" => L("安装包 SHA-256 校验失败", "パッケージの SHA-256 検証に失敗しました", "Package SHA-256 verification failed"),
        "SignatureMissing" or "SignatureInvalid" or "SignerSubjectMismatch" or "SignerThumbprintMismatch" => L("安装包签名验证失败", "パッケージ署名の検証に失敗しました", "Package signature verification failed"),
        "PackageDeploymentFailed" => L("Windows 安装更新失败", "Windows による更新のインストールに失敗しました", "Windows failed to install the update"),
        "NoPendingUpdate" => L("没有可安装的已验证更新", "インストール可能な検証済み更新がありません", "No verified update is ready to install"),
        "Cancelled" => L("更新已取消", "更新をキャンセルしました", "Update cancelled"),
        null or "" => L("更新失败", "更新に失敗しました", "Update failed"),
        _ when errorCode.StartsWith("0x", StringComparison.OrdinalIgnoreCase) => $"{L("Windows 安装更新失败", "Windows による更新のインストールに失敗しました", "Windows failed to install the update")} · {errorCode}",
        _ => $"{L("更新失败", "更新に失敗しました", "Update failed")} · {errorCode}"
    };

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

    private string RestartUpdateButtonText() => L("重启并更新", "再起動して更新", "Restart to update");

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
        await Launcher.LaunchUriAsync(_updates.ProductInfo.Release is { HtmlUrl.Length: > 0 } release ? new Uri(release.HtmlUrl) : ReleasesUri);

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

    private string L(string zh, string ja, string en) => _localization.CurrentLanguage switch
    {
        "ja-JP" => ja,
        "en-US" => en,
        _ => zh
    };

    private string T(string key) => _localization.GetString(key);

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
