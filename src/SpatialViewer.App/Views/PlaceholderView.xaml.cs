using Microsoft.UI.Xaml.Controls;

namespace SpatialViewer.Product.Views;

public sealed partial class PlaceholderView : UserControl
{
    public PlaceholderView(string title, string message)
    {
        InitializeComponent();
        if (string.Equals(title, "关于图览", StringComparison.Ordinal))
        {
            RootHost.Children.Clear();
            RootHost.Children.Add(new AboutView());
            return;
        }
        TitleText.Text = title;
        MessageText.Text = message;
    }
}
