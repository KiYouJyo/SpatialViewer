using Microsoft.UI.Xaml.Controls;
namespace SpatialViewer.Product.Views;
public sealed partial class PlaceholderView : UserControl { public PlaceholderView(string title, string message) { InitializeComponent(); TitleText.Text = title; MessageText.Text = message; } }
