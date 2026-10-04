using Avalonia.Controls;

namespace AnchorPS5.App.Views;

public partial class FirstRunPage : UserControl
{
    public const int StepCount = 2;

    /// <summary>Por debajo de este ancho se pliega el panel de marca.</summary>
    private const double HeroBreakpoint = 820;

    public FirstRunPage()
    {
        InitializeComponent();
        SizeChanged += (_, e) =>
        {
            var narrow = e.NewSize.Width < HeroBreakpoint;
            Hero.IsVisible = !narrow;
            Root.ColumnDefinitions[0].Width = new GridLength(narrow ? 0 : 300);
            Root.ColumnSpacing = narrow ? 0 : 24;
        };
    }

    public void ShowStep(Control step, int index)
    {
        StepHost.Content = step;
        Hero.SetSteps(StepCount, index);
    }
}
