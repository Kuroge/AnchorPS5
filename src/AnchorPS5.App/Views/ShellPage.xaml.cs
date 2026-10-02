using System.ComponentModel;
using AnchorPS5.App.Controls;
using AnchorPS5.App.ViewModels;
using AnchorPS5.Core.Catalog;
using AnchorPS5.Core.Downloads;
using AnchorPS5.Core.Library;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace AnchorPS5.App.Views;

/// <summary>Armazón principal: secciones del catálogo con contadores + marco de contenido.</summary>
public sealed partial class ShellPage : Page
{
    private readonly CatalogViewModel _catalog;
    private readonly DispatcherQueueTimer _refreshTimer;
    private DateTimeOffset _lastRefresh = DateTimeOffset.UtcNow;
    private bool _wasBlocked;
    private bool _signedIn = App.GitHubSession.IsSignedIn;

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
            App.Channels,
            downloads,
            App.PackageResolver,
            () => App.GitHubSession.IsSignedIn,
            App.OfficialSync,
            App.Warnings)
        {
            AskOfficialUpdate = AskOfficialUpdateAsync,
        };
        _catalog.PropertyChanged += OnCatalogPropertyChanged;

        // Refresco automático: cada minuto se comprueba si toca (caché caducada o fin del
        // bloqueo por límite). Lo que sigue fresco en caché no llega a consultar GitHub.
        _refreshTimer = DispatcherQueue.CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromMinutes(1);
        _refreshTimer.Tick += (_, _) => RefreshIfDue();
        _refreshTimer.Start();
        App.GitHubSession.Changed += OnSessionChanged;
        Unloaded += (_, _) =>
        {
            _refreshTimer.Stop();
            App.GitHubSession.Changed -= OnSessionChanged;
        };

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
        if (args.InvokedItemContainer?.Tag is "About")
        {
            if (ContentFrame.CurrentSourcePageType != typeof(AboutPage))
                ContentFrame.Navigate(typeof(AboutPage), null, new EntranceNavigationTransitionInfo());
            return;
        }

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

    /// <summary>Catálogo oficial nuevo con apps propias: conservarlas, sustituir o más tarde.</summary>
    private async Task<OfficialSyncChoice?> AskOfficialUpdateAsync(OfficialSyncResult update)
    {
        // En el primer arranque se pregunta antes de que la página esté en la ventana.
        if (XamlRoot is null)
        {
            var loaded = new TaskCompletionSource();
            Loaded += (_, _) => loaded.TrySetResult();
            await loaded.Task;
        }

        var names = string.Join(", ", update.CustomApps.Select(a => a.Name));
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            RequestedTheme = ActualTheme,
            Title = App.Localization.Get("official.updateTitle"),
            Content = new TextBlock
            {
                Text = App.Localization.Format(update.CustomApps.Count == 1 ? "official.updateTextOne" : "official.updateTextMany", update.CustomApps.Count, names),
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = App.Localization.Get("official.keepMine"),
            SecondaryButtonText = App.Localization.Get("official.replace"),
            CloseButtonText = App.Localization.Get("official.later"),
            DefaultButton = ContentDialogButton.Primary,
        };
        // Tres botones de texto largo: el ancho por defecto los corta.
        dialog.Resources["ContentDialogMaxWidth"] = 980d;
        dialog.Resources["ContentDialogMinWidth"] = 920d;

        try
        {
            return await dialog.ShowAsync() switch
            {
                ContentDialogResult.Primary => OfficialSyncChoice.KeepMine,
                ContentDialogResult.Secondary => OfficialSyncChoice.ReplaceAll,
                _ => null,
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            // Ya hay otro diálogo abierto: se pregunta en el siguiente refresco.
            return null;
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
    private void OnSessionChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(() =>
    {
        if (App.GitHubSession.IsSignedIn == _signedIn)
            return;

        _signedIn = App.GitHubSession.IsSignedIn;
        RefreshNow();
    });

    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        CanGoBackChanged?.Invoke(this, ContentFrame.CanGoBack);

        // El menú marca la página que se ve (también al volver atrás desde "Acerca de").
        if (e.SourcePageType == typeof(AboutPage))
            NavView.SelectedItem = AboutItem;
        else if (ReferenceEquals(NavView.SelectedItem, AboutItem))
            NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>()
                .FirstOrDefault(i => i.Tag as string == _catalog.Filter.ToString());
    }
}
