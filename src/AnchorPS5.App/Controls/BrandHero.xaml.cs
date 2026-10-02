using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AnchorPS5.App.Controls;

/// <summary>
/// Panel de marca: acrílico, ancla flotante con halo azul, bokeh y chispas doradas
/// (inspirado en el arranque de PS5). Opcionalmente muestra el paso actual con puntos.
/// </summary>
public sealed partial class BrandHero : UserControl
{
    public BrandHero()
    {
        InitializeComponent();

        VersionText.Text = App.Localization.Format("app.versionBy", App.Version, "cheyen2008");
    }

    /// <summary>Muestra los puntos de progreso (0 = ocultos).</summary>
    public void SetSteps(int count, int current)
    {
        Pips.Visibility = count > 1 ? Visibility.Visible : Visibility.Collapsed;
        Pips.NumberOfPages = count;
        Pips.SelectedPageIndex = current;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        GlowStoryboard.Begin();
        FloatStoryboard.Begin();
        BokehStoryboard.Begin();
        SparksStoryboard.Begin();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        GlowStoryboard.Stop();
        FloatStoryboard.Stop();
        BokehStoryboard.Stop();
        SparksStoryboard.Stop();
    }
}
