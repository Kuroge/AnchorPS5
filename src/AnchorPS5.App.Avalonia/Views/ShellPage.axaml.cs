using System.ComponentModel;
using AnchorPS5.App.Controls;
using AnchorPS5.App.ViewModels;
using AnchorPS5.Core.Catalog;
using AnchorPS5.Core.Downloads;
using AnchorPS5.Core.Library;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AnchorPS5.App.Views;

/// <summary>Armazón principal: secciones del catálogo con contadores + marco de contenido.</summary>
public partial class ShellPage : UserControl
{
    private readonly CatalogViewModel _catalog;
    private readonly DispatcherTimer _refreshTimer;
    private DateTimeOffset _lastRefresh = DateTimeOffset.UtcNow;
    private bool _wasBlocked;
    private bool _signedIn = App.GitHubSession.IsSignedIn;

    private readonly NavItem _catalogItem, _downloadedItem, _updatesItem, _newItem, _aboutItem;

    /// <summary>Instancia actual (para los comandos de navegación de NavItem).</summary>
    public static ShellPage? Current { get; private set; }

    public ShellPage()
    {
        InitializeComponent();
        Current = this;

        var library = new LibraryService(App.Config.DownloadPath);
        var manager = new DownloadManager(App.DownloadHttp, library, new SevenZipExtractor(App.SevenZipPath), App.Config.MaxConcurrentDownloads);
        var downloads = new DownloadsViewModel(manager, App.Localization, App.UiThread);
        DownloadsIndicator = new DownloadsIndicator(downloads);

        _catalog = new CatalogViewModel(
            App.SourceLoader,
            App.Config.Sources,
            App.Localization,
            library,
            App.SeenApps,
            App.Channels,
            downloads,
            App.PackageResolver,
            () => App.GitHubSession.IsSignedIn,
            App.OfficialSync,
            App.Warnings);
        _catalog.PropertyChanged += OnCatalogPropertyChanged;

        // Elementos del menú.
        _catalogItem = new NavItem(Glyphs.Catalog, App.Localization.Get("nav.catalog"), nameof(CatalogFilter.All));
        _downloadedItem = new NavItem(Glyphs.Download, App.Localization.Get("nav.downloaded"), nameof(CatalogFilter.Downloaded));
        _updatesItem = new NavItem(Glyphs.Sync, App.Localization.Get("nav.updates"), nameof(CatalogFilter.Updates));
        _newItem = new NavItem(Glyphs.Star, App.Localization.Get("nav.new"), nameof(CatalogFilter.New));
        _aboutItem = new NavItem(Glyphs.Info, App.Localization.Get("nav.about"), "About");
        Nav.ItemsSource = new[] { _catalogItem, _downloadedItem, _updatesItem, _newItem, _aboutItem };

        // Refresco automático: cada minuto se comprueba si toca (caché caducada o fin del bloqueo).
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _refreshTimer.Tick += (_, _) => RefreshIfDue();
        _refreshTimer.Start();
        App.GitHubSession.Changed += OnSessionChanged;
        Unloaded += (_, _) =>
        {
            _refreshTimer.Stop();
            App.GitHubSession.Changed -= OnSessionChanged;
            if (ReferenceEquals(Current, this))
                Current = null;
        };

        NavigateTo(nameof(CatalogFilter.All));
    }

    /// <summary>Indicador de descargas para la barra de título.</summary>
    public DownloadsIndicator DownloadsIndicator { get; }

    public void NavigateTo(string tag)
    {
        if (tag == "About")
        {
            ContentHost.Content = new AboutPage();
            return;
        }

        if (!Enum.TryParse<CatalogFilter>(tag, out var filter))
            return;

        _catalog.Filter = filter;
        // Volver al grid desde el detalle.
        if (ContentHost.Content is not CatalogPage)
            ContentHost.Content = new CatalogPage(_catalog);
        else
            ((CatalogPage)ContentHost.Content).EnsureLoaded();
    }

    /// <summary>Abre el detalle de una app (desde una tarjeta del catálogo).</summary>
    public void ShowDetail(CatalogItemViewModel item)
    {
        ContentHost.Content = new AppDetailPage(item);
    }

    private void OnCatalogPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(CatalogViewModel.DownloadedCount):
                _downloadedItem.Count = _catalog.DownloadedCount;
                break;
            case nameof(CatalogViewModel.UpdatesCount):
                _updatesItem.Count = _catalog.UpdatesCount;
                break;
            case nameof(CatalogViewModel.NewCount):
                _newItem.Count = _catalog.NewCount;
                break;
        }
    }

    private void RefreshIfDue()
    {
        var now = DateTimeOffset.UtcNow;
        var blocked = App.GitHub.BlockedUntil is not null;
        var unblocked = _wasBlocked && !blocked;
        _wasBlocked = blocked;

        if (unblocked || now - _lastRefresh >= App.GitHub.MaxAge)
            RefreshNow();
    }

    private void RefreshNow()
    {
        _lastRefresh = DateTimeOffset.UtcNow;
        _ = _catalog.RefreshInBackgroundAsync();
    }

    // Al iniciar o cerrar sesión cambia el límite: se vuelve a consultar lo pendiente.
    private void OnSessionChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (App.GitHubSession.IsSignedIn == _signedIn)
                return;
            _signedIn = App.GitHubSession.IsSignedIn;
            RefreshNow();
        });
}
