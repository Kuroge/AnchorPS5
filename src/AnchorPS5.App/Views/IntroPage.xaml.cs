using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AnchorPS5.App.Views;

public sealed partial class IntroPage : Page
{
    public IntroPage()
    {
        InitializeComponent();
    }

    public event EventHandler? Closed;

    private void OnSkipClick(object sender, RoutedEventArgs e) => Closed?.Invoke(this, EventArgs.Empty);
}
