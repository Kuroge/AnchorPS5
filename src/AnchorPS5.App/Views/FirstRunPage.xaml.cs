using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AnchorPS5.App.Views;

public sealed partial class FirstRunPage : Page
{
    public const int StepCount = 2;

    /// <summary>Por debajo de este ancho se pliega el panel de marca.</summary>
    private const double HeroBreakpoint = 820;

    public FirstRunPage()
    {
        InitializeComponent();
    }

    public void ShowStep(UIElement step, int index)
    {
        StepHost.Content = step;
        Hero.SetSteps(StepCount, index);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var narrow = e.NewSize.Width < HeroBreakpoint;
        Hero.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
        HeroColumn.Width = new GridLength(narrow ? 0 : 300);
    }
}
