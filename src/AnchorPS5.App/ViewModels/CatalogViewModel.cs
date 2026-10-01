using System.Collections.ObjectModel;
using AnchorPS5.Core.Catalog;
using AnchorPS5.Core.Library;
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
    private readonly DownloadsViewModel _downloads;

    // Nuevas en esta sesión: se mantienen aunque se recargue.
    private readonly HashSet<string> _newIds = new(StringComparer.OrdinalIgnoreCase);
    private List<CatalogItemViewModel> _all = [];

    public CatalogViewModel(
        SourceLoader loader,
        IReadOnlyList<Source> sources,
        LocalizationService localization,
        LibraryService library,
        SeenAppsService seenApps,
        DownloadsViewModel downloads)
    {
        _loader = loader;
        _sources = sources;
        _localization = localization;
        _library = library;
        _seenApps = seenApps;
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

        var loadCatalog = _loader.LoadAllAsync(_sources);
        var scanLibrary = Task.Run(_library.Scan);
        var result = await loadCatalog;
        var library = await scanLibrary;
        _newIds.UnionWith(await Task.Run(() => _seenApps.RegisterAndGetNew(result.Entries.Select(e => e.App.Id))));
        IsLoading = false;

        _all = result.Entries
            .Select(e => new CatalogItemViewModel(
                e,
                _localization,
                this,
                PackageStatus.Compute(e.App, library.GetVersions(e.App)),
                _newIds.Contains(e.App.Id),
                _library.GetAppFolder(e.App)))
            .ToList();

        // Descargas que siguen en marcha (o fallidas) tras recargar.
        foreach (var item in _all)
            item.ActiveJob = _downloads.Jobs.FirstOrDefault(j => string.Equals(j.Job.App.Id, item.Id, StringComparison.OrdinalIgnoreCase));

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

    public void Download(CatalogItemViewModel item) => item.ActiveJob = _downloads.Start(item.Entry.App);

    public void Retry(CatalogItemViewModel item)
    {
        if (item.ActiveJob is { } job)
            _downloads.Retry(job);
        else
            Download(item);
    }

    public void OpenFolder(string path)
    {
        // Si la carpeta aún no existe, se abre la de descargas.
        var target = Directory.Exists(path) ? path : _library.DownloadPath;
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

    public void DeleteVersion(CatalogItemViewModel item, InstalledVersion version) =>
        RunDelete(item, () => _library.DeleteVersion(version));

    public void DeleteAll(CatalogItemViewModel item) =>
        RunDelete(item, () => _library.DeleteVersions(item.Versions));

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

        // Las fallidas se quedan enlazadas para mostrar el error y reintentar.
        if (!job.IsFailed)
            item.ActiveJob = null;
        await RefreshItemAsync(item);
    }

    /// <summary>Vuelve a leer del disco el estado de una app tras descargar o borrar.</summary>
    private async Task RefreshItemAsync(CatalogItemViewModel item)
    {
        var library = await Task.Run(_library.Scan);
        item.SetStatus(PackageStatus.Compute(item.Entry.App, library.GetVersions(item.Entry.App)));
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
