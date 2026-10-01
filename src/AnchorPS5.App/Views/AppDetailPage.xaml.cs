using AnchorPS5.App.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace AnchorPS5.App.Views;

/// <summary>Detalle de una app; recibe su <see cref="CatalogItemViewModel"/> al navegar.</summary>
public sealed partial class AppDetailPage : Page
{
    public AppDetailPage()
    {
        InitializeComponent();
    }

    public CatalogItemViewModel Item { get; private set; } = null!;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        Item = (CatalogItemViewModel)e.Parameter;
        Bindings.Update();
    }
}
