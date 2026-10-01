using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AnchorPS5.App.Views;

/// <summary>"¿Quieres ver la guía de introducción?" — la respuesta va en el evento.</summary>
public sealed partial class IntroQuestionPage : Page
{
    public IntroQuestionPage()
    {
        InitializeComponent();
    }

    /// <summary>true = el usuario quiere ver la guía.</summary>
    public event EventHandler<bool>? Answered;

    private void OnLoaded(object sender, RoutedEventArgs e) => PopStoryboard.Begin();

    private void OnYesClick(object sender, RoutedEventArgs e) => Answered?.Invoke(this, true);

    private void OnNoClick(object sender, RoutedEventArgs e) => Answered?.Invoke(this, false);
}
