using System.Collections.ObjectModel;
using AnchorPS5.Core.Catalog;
using AnchorPS5.Core.GitHub;
using AnchorPS5.Core.Library;
using AnchorPS5.Core.Packages;
using AnchorPS5.Core.Localization;
using AnchorPS5.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnchorPS5.App.ViewModels;

/// <summary>Catálogo: carga de fuentes y biblioteca, secciones, búsqueda, orden, avisos y acciones.</summary>
public sealed partial class CatalogViewModel : ObservableObject, IPackageActions
{
    private readonly SourceLoader _loader;
    private readonly IReadOnlyList<Source> _sources;
    private readonly LocalizationService _localization;
    private readonly LibraryService _library;
    private readonly SeenAppsService _seenApps;
    private readonly ChannelPreferences _channels;
    private readonly DownloadsViewModel _downloads;
    private readonly PackageResolver _resolver;
    private readonly Func<bool> _isSignedIn;

    // Nuevas en esta sesión: se mantienen aunque se recargue.
    private readonly HashSet<string> _newIds = new(StringComparer.OrdinalIgnoreCase);
    private List<CatalogItemViewModel> _all = [];
    private bool _refreshing;

    public CatalogViewModel(
        SourceLoader loader,
        IReadOnlyList<Source> sources,
        LocalizationService localization,
        LibraryService library,
        SeenAppsService seenApps,
        ChannelPreferences channels,
        DownloadsViewModel downloads,
        PackageResolver resolver,
        Func<bool> isSignedIn)
    {
        _isSignedIn = isSignedIn;
        _resolver = resolver;
        _loader = loader;
        _sources = sources;
        _localization = localization;
        _library = library;
        _seenApps = seenApps;
        _channels = channels;
        _downloads = downloads;
        _downloads.JobFinished += OnJobFinished;
        Title = localization.Get("nav.catalog");
    }

    public ObservableCollection<CatalogItemViewModel> Items { get; } = [];

    public bool IsLoaded { get; private set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial CatalogFilter Filter { get; set; }

    /// <summary>Índice del desplegable de orden (0 = nombre, 1 = novedades).</summary>
    [ObservableProperty]
    public partial int SortIndex { get; set; }

    [ObservableProperty]
    public partial string Title { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSourceErrors))]
    public partial string SourceErrors { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string CountText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int DownloadedCount { get; private set; }

    [ObservableProperty]
    public partial int UpdatesCount { get; private set; }

    [ObservableProperty]
    public partial int NewCount { get; private set; }

    public bool HasStatus => StatusMessage.Length > 0;

    public bool HasSourceErrors => SourceErrors.Length > 0;

    /// <summary>Aviso de GitHub (límite de la API alcanzado o datos sin conexión).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGitHubWarning))]
    public partial string GitHubWarning { get; set; } = string.Empty;

    public bool HasGitHubWarning => GitHubWarning.Length > 0;

    /// <summary>El aviso es por el límite de la API (muestra "Saber más" e "Iniciar sesión").</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSignInFromWarning))]
    public partial bool IsGitHubRateLimited { get; private set; }

    public bool CanSignInFromWarning => IsGitHubRateLimited && !_isSignedIn();

    private string BuildGitHubWarning(IReadOnlyList<ResolvedPackage> packages)
    {
        IsGitHubRateLimited = packages.Any(p => p.Source == GitHubStatus.RateLimited);
        OnPropertyChanged(nameof(CanSignInFromWarning)); // también cambia al iniciar sesión
        if (IsGitHubRateLimited)
        {
            var reset = _resolver.LastRateLimitReset;
            var message = reset is { } at
                ? _localization.Format("github.rateLimitedUntil", at.ToLocalTime().ToString("t", System.Globalization.CultureInfo.CurrentCulture))
                : _localization.Get("github.rateLimited");
            return _isSignedIn() ? message : message + " " + _localization.Get("github.signInHint");
        }

        return packages.Any(p => p.Source is GitHubStatus.FromCache or GitHubStatus.Error)
            ? _localization.Get("github.offline")
            : string.Empty;
    }

    private static bool SameFile(DownloadJobViewModel job, CatalogItemViewModel item, PackageFileViewModel file) =>
        string.Equals(job.Job.App.Id, item.Id, StringComparison.OrdinalIgnoreCase)
        && string.Equals(job.Job.File.Key, file.Key, StringComparison.OrdinalIgnoreCase)
        && string.Equals(job.Job.File.Version, file.File?.Version, StringComparison.OrdinalIgnoreCase);

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSortIndexChanged(int value) => ApplyFilter();

    partial void OnFilterChanged(CatalogFilter value)
    {
        Title = _localization.Get(value switch
        {
            CatalogFilter.Downloaded => "nav.downloaded",
            CatalogFilter.Updates => "nav.updates",
            CatalogFilter.New => "nav.new",
            _ => "nav.catalog",
        });
        ApplyFilter();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        StatusMessage = string.Empty;
        SourceErrors = string.Empty;
        CountText = string.Empty;
        Items.Clear();

        GitHubWarning = string.Empty;

        var data = await FetchAsync();
        IsLoading = false;
        Apply(data);
    }

    /// <summary>
    /// Refresco automático en segundo plano: no vacía la lista y solo la reconstruye si
    /// el catálogo o lo publicado en GitHub ha cambiado.
    /// </summary>
    public async Task RefreshInBackgroundAsync()
    {
        if (!IsLoaded || IsLoading || _refreshing)
            return;

        _refreshing = true;
        try
        {
            var data = await FetchAsync();
            if (IsLoading)
                return; // se ha recargado a mano mientras tanto

            if (SamePackages(data))
                GitHubWarning = BuildGitHubWarning(data.Packages);
            else
                Apply(data);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private sealed record LoadedData(
        CatalogLoadResult Result,
        ResolvedPackage[] Packages,
        LibrarySnapshot Library,
        IReadOnlyDictionary<string, FileChannel>[] Channels);

    private async Task<LoadedData> FetchAsync()
    {
        var loadCatalog = _loader.LoadAllAsync(_sources);
        var scanLibrary = Task.Run(_library.Scan);
        var result = await loadCatalog;

        // Ficheros publicados de cada app (de GitHub si trae repo), en paralelo.
        var packages = await Task.WhenAll(result.Entries.Select(e => _resolver.ResolveAsync(e.App)));
        var library = await scanLibrary;
        _newIds.UnionWith(await Task.Run(() => _seenApps.RegisterAndGetNew(result.Entries.Select(e => e.App.Id))));
        var appChannels = await Task.Run(() => result.Entries.Select(e => _channels.GetForApp(e.App.Id)).ToArray());
        return new LoadedData(result, packages, library, appChannels);
    }

    private bool SamePackages(LoadedData data) =>
        data.Packages.Length == _all.Count
        && _all.Select((item, i) =>
                string.Equals(item.Id, data.Result.Entries[i].App.Id, StringComparison.OrdinalIgnoreCase)
                && item.Package.ReleaseUrl == data.Packages[i].ReleaseUrl
                && item.Package.Files.SequenceEqual(data.Packages[i].Files))
            .All(same => same);

    private void Apply(LoadedData data)
    {
        var (result, packages, library, appChannels) = data;

        _all = result.Entries
            .Select((e, i) => new CatalogItemViewModel(
                e,
                packages[i],
                _localization,
                this,
                PackageStatus.Compute(packages[i], library.GetVersions(e.App), appChannels[i]),
                _newIds.Contains(e.App.Id),
                _library.GetAppFolder(e.App),
                key => _library.GetFileFolder(e.App, key)))
            .ToList();

        // Descargas que siguen en marcha (o fallidas) tras recargar.
        foreach (var item in _all)
        {
            foreach (var file in item.Files)
                file.ActiveJob = _downloads.Jobs.FirstOrDefault(j => SameFile(j, item, file));
            item.RefreshActiveJob();
        }

        GitHubWarning = BuildGitHubWarning(packages);
        UpdateCounts();

        SourceErrors = string.Join(Environment.NewLine, result.FailedSources.Select(f =>
            _localization.Format("catalog.sourceError", f.Source.Name, _localization.Get(ErrorKey(f.Error)))));
        IsLoaded = true;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (!IsLoaded)
            return;

        var inSection = _all.Where(i => CatalogView.Matches(Filter, i.State, i.IsNew)).ToList();
        var matching = CatalogSearch.Filter(inSection.Select(i => i.Entry), SearchText).ToHashSet();
        var visible = CatalogView.Sort(
            inSection.Where(i => matching.Contains(i.Entry)),
            SortIndex == 1 ? CatalogSort.WhatsNew : CatalogSort.Name,
            i => i.Name,
            i => i.State,
            i => i.IsNew);

        Items.Clear();
        foreach (var item in visible)
            Items.Add(item);

        CountText = inSection.Count == 1
            ? _localization.Get("catalog.countOne")
            : _localization.Format("catalog.countMany", inSection.Count);

        StatusMessage = inSection.Count == 0 ? _localization.Get(EmptyKey(Filter))
            : Items.Count == 0 ? _localization.Format("catalog.noResults", SearchText.Trim())
            : string.Empty;
    }

    // ---- Acciones sobre paquetes ----

    public void Download(CatalogItemViewModel item, PackageFileViewModel row, PackageFile file)
    {
        // Red de seguridad: nunca se vuelve a descargar un fichero en una versión que ya tienes.
        var alreadyHave = row.InstalledVersions.Any(v =>
            string.Equals(v.Version, file.Version, StringComparison.OrdinalIgnoreCase)
            && v.Files.Any(f => string.Equals(f.Key, file.Key, StringComparison.OrdinalIgnoreCase)));
        if (alreadyHave)
        {
            _ = RefreshItemAsync(item); // por si el canal ha cambiado
            return;
        }

        row.ActiveJob = _downloads.Start(item.Entry.App, file);
        item.RefreshActiveJob();
    }

    public async void SetChannel(CatalogItemViewModel item, PackageFileViewModel row, FileChannel channel)
    {
        await Task.Run(() => _channels.Set(item.Id, row.Key, channel));
        await RefreshItemAsync(item);
    }

    public void OpenFolder(string path)
    {
        // Si la carpeta aún no existe, se abre la más cercana que sí exista (como mucho, la de descargas).
        var target = path;
        while (!Directory.Exists(target) && Path.GetDirectoryName(target) is { } parent
            && parent.StartsWith(_library.DownloadPath, StringComparison.OrdinalIgnoreCase))
            target = parent;
        if (!Directory.Exists(target))
            target = _library.DownloadPath;
        try
        {
            Directory.CreateDirectory(target);
            var start = new System.Diagnostics.ProcessStartInfo("explorer.exe") { UseShellExecute = false };
            start.ArgumentList.Add(target);
            System.Diagnostics.Process.Start(start);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            // Sin consecuencias: simplemente no se abre.
        }
    }

    public void DeleteVersions(CatalogItemViewModel item, IReadOnlyList<InstalledVersion> versions) =>
        RunDelete(item, () => _library.DeleteVersions(versions));

    private async void RunDelete(CatalogItemViewModel item, Action delete)
    {
        item.ActionError = string.Empty;
        try
        {
            await Task.Run(delete);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            item.ActionError = _localization.Get("action.deleteError");
        }

        await RefreshItemAsync(item);
    }

    private async void OnJobFinished(object? sender, DownloadJobViewModel job)
    {
        var item = _all.FirstOrDefault(i => string.Equals(i.Id, job.Job.App.Id, StringComparison.OrdinalIgnoreCase));
        if (item is null)
            return;

        // Las fallidas se quedan enlazadas a su fichero para mostrar el error y reintentar.
        if (!job.IsFailed)
        {
            foreach (var file in item.Files.Where(f => ReferenceEquals(f.ActiveJob, job)))
                file.ActiveJob = null;
        }

        await RefreshItemAsync(item);
    }

    /// <summary>Vuelve a leer del disco el estado de una app tras descargar o borrar.</summary>
    private async Task RefreshItemAsync(CatalogItemViewModel item)
    {
        var library = await Task.Run(_library.Scan);
        item.SetStatus(PackageStatus.Compute(item.Package, library.GetVersions(item.Entry.App), _channels.GetForApp(item.Id)));
        UpdateCounts();

        // En "Descargadas" o "Actualizaciones" la app puede entrar o salir de la sección.
        if (Filter != CatalogFilter.All)
            ApplyFilter();
    }

    private void UpdateCounts()
    {
        DownloadedCount = _all.Count(i => i.State != PackageState.NotDownloaded);
        UpdatesCount = _all.Count(i => i.HasUpdate);
        NewCount = _all.Count(i => i.IsNew);
    }

    private static string EmptyKey(CatalogFilter filter) => filter switch
    {
        CatalogFilter.Downloaded => "catalog.emptyDownloaded",
        CatalogFilter.Updates => "catalog.emptyUpdates",
        CatalogFilter.New => "catalog.emptyNew",
        _ => "catalog.empty",
    };

    private static string ErrorKey(SourceErrorKind error) => error switch
    {
        SourceErrorKind.NotFound => "catalog.error.notFound",
        SourceErrorKind.InvalidJson => "catalog.error.invalidJson",
        SourceErrorKind.Network => "catalog.error.network",
        SourceErrorKind.TooLarge => "catalog.error.tooLarge",
        _ => "catalog.error.invalidSource",
    };
}
