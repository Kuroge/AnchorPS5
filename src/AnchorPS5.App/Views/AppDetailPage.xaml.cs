using AnchorPS5.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;

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

    private async void OnCopyShaClick(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(Item.Sha256);
        Clipboard.SetContent(package);

        // Confirmación breve: el icono pasa a ✓ y vuelve.
        CopyShaIcon.Glyph = "";
        ToolTipService.SetToolTip(CopyShaButton, App.Localization.Get("detail.copied"));
        await Task.Delay(1500);
        CopyShaIcon.Glyph = "";
        ToolTipService.SetToolTip(CopyShaButton, App.Localization.Get("detail.copy"));
    }
}
