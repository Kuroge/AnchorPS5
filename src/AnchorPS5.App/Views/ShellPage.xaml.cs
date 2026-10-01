using AnchorPS5.App.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace AnchorPS5.App.Views;

/// <summary>Armazón principal: menú lateral + marco de contenido con navegación atrás.</summary>
public sealed partial class ShellPage : Page
{
    private readonly CatalogViewModel _catalog;

    public ShellPage()
    {
        InitializeComponent();

        _catalog = new CatalogViewModel(App.SourceLoader, App.Config.Sources, App.Localization);
        NavView.SelectedItem = CatalogItem;
        ContentFrame.Navigate(typeof(CatalogPage), _catalog);
    }

    private void OnCatalogTapped(object sender, TappedRoutedEventArgs e)
    {
        // Desde el detalle, volver al grid limpiando la pila.
        if (ContentFrame.CurrentSourcePageType != typeof(CatalogPage))
        {
            ContentFrame.Navigate(typeof(CatalogPage), _catalog);
            ContentFrame.BackStack.Clear();
            NavView.IsBackEnabled = false;
        }
    }

    private void OnBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        if (ContentFrame.CanGoBack)
            ContentFrame.GoBack();
    }

    private void OnNavigated(object sender, NavigationEventArgs e) => NavView.IsBackEnabled = ContentFrame.CanGoBack;
}
