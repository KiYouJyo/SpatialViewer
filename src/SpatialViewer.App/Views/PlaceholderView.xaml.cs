using Microsoft.UI.Xaml.Controls;

namespace SpatialViewer.Product.Views;

public sealed partial class PlaceholderView : UserControl
{
    public PlaceholderView(string title, string message)
    {
        InitializeComponent();
        var localization = AppLocalizationService.Default;
        TitleText.Text = title switch
        {
            "项目" => localization.GetString("Placeholder_Projects_Title"),
            "收藏" => localization.GetString("Placeholder_Favorites_Title"),
            "导入文件夹" => localization.GetString("Placeholder_ImportFolder_Title"),
            _ => title
        };
        MessageText.Text = message switch
        {
            "项目工作流将在后续版本提供。" => localization.GetString("Placeholder_Projects_Message"),
            "收藏夹将在后续版本提供。" => localization.GetString("Placeholder_Favorites_Message"),
            "文件夹导入将在后续版本提供。" => localization.GetString("Placeholder_ImportFolder_Message"),
            _ => message
        };
    }
}
