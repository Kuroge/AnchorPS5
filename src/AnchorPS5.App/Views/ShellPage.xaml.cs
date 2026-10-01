using AnchorPS5.App.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
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

    /// <summary>Avisa a la barra de título de si hay a dónde volver.</summary>
    public event EventHandler<bool>? CanGoBackChanged;

    public void GoBack()
    {
        if (ContentFrame.CanGoBack)
            ContentFrame.GoBack(new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromLeft });
    }

    public void TogglePane() => NavView.IsPaneOpen = !NavView.IsPaneOpen;

    private void OnCatalogTapped(object sender, TappedRoutedEventArgs e)
    {
        // Desde el detalle, volver al grid limpiando la pila.
        if (ContentFrame.CurrentSourcePageType != typeof(CatalogPage))
        {
            ContentFrame.Navigate(typeof(CatalogPage), _catalog, new EntranceNavigationTransitionInfo());
            ContentFrame.BackStack.Clear();
            CanGoBackChanged?.Invoke(this, false);
        }
    }

    private void OnNavigated(object sender, NavigationEventArgs e) => CanGoBackChanged?.Invoke(this, ContentFrame.CanGoBack);
}
