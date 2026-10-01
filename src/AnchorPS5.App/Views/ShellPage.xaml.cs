using System.ComponentModel;
using AnchorPS5.App.Controls;
using AnchorPS5.App.ViewModels;
using AnchorPS5.Core.Catalog;
using AnchorPS5.Core.Downloads;
using AnchorPS5.Core.Library;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace AnchorPS5.App.Views;

/// <summary>Armazón principal: secciones del catálogo con contadores + marco de contenido.</summary>
public sealed partial class ShellPage : Page
{
    private readonly CatalogViewModel _catalog;

    public ShellPage()
    {
        InitializeComponent();

        var library = new LibraryService(App.Config.DownloadPath);
        var manager = new DownloadManager(App.DownloadHttp, library, new SevenZipExtractor(App.SevenZipPath), App.Config.MaxConcurrentDownloads);
        var downloads = new DownloadsViewModel(manager, App.Localization);
        DownloadsIndicator = new DownloadsIndicator(downloads);

        _catalog = new CatalogViewModel(
            App.SourceLoader,
            App.Config.Sources,
            App.Localization,
            library,
            App.SeenApps,
            downloads,
            App.PackageResolver);
        _catalog.PropertyChanged += OnCatalogPropertyChanged;

        NavView.SelectedItem = CatalogItem;
        ContentFrame.Navigate(typeof(CatalogPage), _catalog);
    }

    /// <summary>Indicador de descargas para la barra de título.</summary>
    public DownloadsIndicator DownloadsIndicator { get; }

    /// <summary>Avisa a la barra de título de si hay a dónde volver.</summary>
    public event EventHandler<bool>? CanGoBackChanged;

    public void GoBack()
    {
        if (ContentFrame.CanGoBack)
            ContentFrame.GoBack(new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromLeft });
    }

    public void TogglePane() => NavView.IsPaneOpen = !NavView.IsPaneOpen;

    private void OnItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer?.Tag is not string tag || !Enum.TryParse<CatalogFilter>(tag, out var filter))
            return;

        _catalog.Filter = filter;

        // Desde el detalle, volver al grid limpiando la pila.
        if (ContentFrame.CurrentSourcePageType != typeof(CatalogPage))
        {
            ContentFrame.Navigate(typeof(CatalogPage), _catalog, new EntranceNavigationTransitionInfo());
            ContentFrame.BackStack.Clear();
            CanGoBackChanged?.Invoke(this, false);
        }
    }

    private void OnCatalogPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(CatalogViewModel.DownloadedCount):
                SetBadge(DownloadedBadge, _catalog.DownloadedCount);
                break;
            case nameof(CatalogViewModel.UpdatesCount):
                SetBadge(UpdatesBadge, _catalog.UpdatesCount);
                break;
            case nameof(CatalogViewModel.NewCount):
                SetBadge(NewBadge, _catalog.NewCount);
                break;
        }
    }

    private static void SetBadge(InfoBadge badge, int count)
    {
        badge.Value = count;
        badge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnNavigated(object sender, NavigationEventArgs e) => CanGoBackChanged?.Invoke(this, ContentFrame.CanGoBack);
}
