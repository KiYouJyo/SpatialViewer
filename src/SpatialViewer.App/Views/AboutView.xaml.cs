using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Reflection;
using Windows.System;

namespace SpatialViewer.Product.Views;

public sealed partial class AboutView : UserControl
{
    private const string ProductRepository = "KiYouJyo/SpatialViewer";
    private const string CadCoreRepository = "KiYouJyo/SpatialViewer.CadCore";
    private static readonly Uri ProductRepositoryUri = new("https://github.com/KiYouJyo/SpatialViewer");
    private static readonly Uri ReleasesUri = new("https://github.com/KiYouJyo/SpatialViewer/releases");
    private static readonly Uri PrivacyUri = new("https://github.com/KiYouJyo/SpatialViewer/blob/main/PRIVACY.md");
    private readonly GitHubUpdateService _updates = new();
    private GitHubReleaseInfo? _latestProductRelease;

    public AboutView()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        if (version is not null)
            CadCurrentVersionText.ToolTipServiceSet($"SpatialViewer bundle {version.Major}.{version.Minor}.{version.Build}");
    }

    private async void CheckAppUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        CheckAppUpdateButton.IsEnabled = false;
        AppUpdateStatusText.Text = "正在检查…";
        try
        {
            _latestProductRelease = await _updates.GetLatestReleaseAsync(ProductRepository);
            if (_latestProductRelease is null)
            {
                AvailableAppVersionText.Text = "—";
                AppUpdateStatusText.Text = "暂无 GitHub Release";
                return;
            }

            AvailableAppVersionText.Text = $"v{_latestProductRelease.DisplayVersion}";
            var current = new Version(0, 2, 0);
            AppUpdateStatusText.Text = GitHubUpdateService.IsNewer(_latestProductRelease.TagName, current)
                ? "发现新版本"
                : "已是最新预览版";
        }
        catch (HttpRequestException)
        {
            AppUpdateStatusText.Text = "网络检查失败";
        }
        catch (TaskCanceledException)
        {
            AppUpdateStatusText.Text = "检查超时";
        }
        finally
        {
            CheckAppUpdateButton.IsEnabled = true;
        }
    }

    private async void CheckCadUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        CadUpdateStatusText.Text = "正在检查…";
        try
        {
            var release = await _updates.GetLatestReleaseAsync(CadCoreRepository);
            if (release is null)
            {
                CadAvailableVersionText.Text = "—";
                CadUpdateStatusText.Text = "暂无独立 Release";
                return;
            }
            CadAvailableVersionText.Text = $"v{release.DisplayVersion}";
            CadUpdateStatusText.Text = "已读取独立仓库";
        }
        catch (HttpRequestException)
        {
            CadUpdateStatusText.Text = "网络检查失败";
        }
        catch (TaskCanceledException)
        {
            CadUpdateStatusText.Text = "检查超时";
        }
    }

    private async void ReleaseNotesButton_Click(object sender, RoutedEventArgs e) =>
        await Launcher.LaunchUriAsync(_latestProductRelease is { HtmlUrl.Length: > 0 } release ? new Uri(release.HtmlUrl) : ReleasesUri);

    private async void OpenRepositoryButton_Click(object sender, RoutedEventArgs e) => await Launcher.LaunchUriAsync(ProductRepositoryUri);
    private async void OpenReleasesButton_Click(object sender, RoutedEventArgs e) => await Launcher.LaunchUriAsync(ReleasesUri);
    private async void OpenPrivacyButton_Click(object sender, RoutedEventArgs e) => await Launcher.LaunchUriAsync(PrivacyUri);

    private void AboutRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Figma's 1312-DIP desktop composition remains the preferred layout.
        // Below that width only the project cards change topology; sections and
        // nested surfaces stay fluid and keep their native WinUI controls.
        var compact = e.NewSize.Width < 900;
        if (compact && ProjectLinksGrid.ColumnDefinitions.Count == 3)
        {
            ProjectLinksGrid.ColumnDefinitions.Clear();
            ProjectLinksGrid.ColumnDefinitions.Add(new ColumnDefinition());
            ProjectLinksGrid.RowDefinitions.Clear();
            ProjectLinksGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ProjectLinksGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            ProjectLinksGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ProjectLinksGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            ProjectLinksGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var index = 0; index < ProjectLinksGrid.Children.Count; index++)
            {
                Grid.SetColumn(ProjectLinksGrid.Children[index], 0);
                Grid.SetRow(ProjectLinksGrid.Children[index], index * 2);
            }
        }
        else if (!compact && ProjectLinksGrid.ColumnDefinitions.Count == 1)
        {
            ProjectLinksGrid.RowDefinitions.Clear();
            ProjectLinksGrid.ColumnDefinitions.Clear();
            ProjectLinksGrid.ColumnDefinitions.Add(new ColumnDefinition());
            ProjectLinksGrid.ColumnDefinitions.Add(new ColumnDefinition());
            ProjectLinksGrid.ColumnDefinitions.Add(new ColumnDefinition());
            for (var index = 0; index < ProjectLinksGrid.Children.Count; index++)
            {
                Grid.SetRow(ProjectLinksGrid.Children[index], 0);
                Grid.SetColumn(ProjectLinksGrid.Children[index], index);
            }
        }
    }
}

internal static class ToolTipCompatibilityExtensions
{
    public static void ToolTipServiceSet(this FrameworkElement element, string value) => ToolTipService.SetToolTip(element, value);
}
