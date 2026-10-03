using System.Collections.ObjectModel;
using AnchorPS5.Core;
using AnchorPS5.Core.Catalog;
using AnchorPS5.Core.GitHub;
using AnchorPS5.Core.Library;
using AnchorPS5.Core.Localization;
using AnchorPS5.Core.Models;
using AnchorPS5.Core.Packages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnchorPS5.App.ViewModels;

/// <summary>Acciones sobre los paquetes (las implementa el catálogo).</summary>
public interface IPackageActions
{
    /// <summary>Descarga un fichero concreto (nunca uno que ya tengas en esa versión).</summary>
    void Download(CatalogItemViewModel item, PackageFileViewModel row, PackageFile file);

    /// <summary>Cambia el canal de un fichero (estable/beta) y vuelve a calcular su estado.</summary>
    void SetChannel(CatalogItemViewModel item, PackageFileViewModel row, FileChannel channel);

    void OpenFolder(string path);
    void DeleteVersions(CatalogItemViewModel item, IReadOnlyList<InstalledVersion> versions);
}

/// <summary>Tarjeta y detalle de una app del catálogo, con sus ficheros y su estado en la biblioteca.</summary>
public sealed partial class CatalogItemViewModel : ObservableObject, IFileActions
{
    private readonly LocalizationService _localization;
    private readonly IPackageActions _actions;
    private readonly Func<string, string> _fileFolder;
    private IReadOnlyList<InstalledVersion> _versions = [];

    /// <param name="fileFolder">Carpeta de un fichero de esta app a partir de su clave.</param>
    public CatalogItemViewModel(
        CatalogEntry entry,
        ResolvedPackage package,
        LocalizationService localization,
        IPackageActions actions,
        PackageStatus status,
        bool isNew,
        string appFolder,
        Func<string, string> fileFolder,
        AppOrigin origin)
    {
        _localization = localization;
        Origin = origin;
        _actions = actions;
        _fileFolder = fileFolder;
        Entry = entry;
        Package = package;
        IsNew = isNew;
        AppFolder = appFolder;
        Description = entry.App.Description.IsEmpty
            ? localization.Get("detail.noDescription")
            : entry.App.Description.Get(localization.CurrentLanguage);
        BetaText = package.BetaVersion is { } beta ? localization.Format("file.betaChip", VersionLabel.Format(beta)) : string.Empty;
        FilesTitle = package.DisplayVersion is { } v ? localization.Format("detail.filesTitle", v) : localization.Get("detail.filesTitleNoVersion");
        SetStatus(status);
    }

    public CatalogEntry Entry { get; }

    /// <summary>Lo publicado: ficheros de la última estable (y de la beta si es más nueva).</summary>
    public ResolvedPackage Package { get; }

    public string Id => Entry.App.Id;
    public string Name => Entry.App.Name;
    public string Author => Entry.App.Author;
    public string Description { get; }
    public string FilesTitle { get; }

    /// <summary>Versión publicada: la de GitHub o, si no hay, la del catálogo.</summary>
    public string Version => Package.DisplayVersion ?? Entry.App.Version;
    public string VersionText => VersionLabel.Format(Version);
    /// <summary>Chip de versión: no se muestra si la única versión es una beta (ya sale en su chip).</summary>
    public bool HasVersion => !string.IsNullOrWhiteSpace(Version) && !(HasBeta && Package.StableVersion is null);

    /// <summary>Versión estable para Información ("—" si la app solo publica betas).</summary>
    public string StableVersionText => HasBeta && Package.StableVersion is null ? "—" : Version;

    public bool HasBeta => Package.BetaVersion is not null;
    public string BetaText { get; }

    /// <summary>Tamaño del fichero principal (si solo hay uno).</summary>

    public bool HasReleaseUrl => Package.ReleaseUrl is not null;

    /// <summary>Repo de GitHub de la app (si lo tiene).</summary>
    public GitHubRepoRef? Repo => GitHubRepoRef.TryParse(Entry.App.Repo, out var repo) ? repo : null;

    public bool HasRepo => Repo is not null;

    /// <summary>https://github.com/owner/repo</summary>
    public Uri? RepoUrl => Repo is { } r ? new Uri($"https://github.com/{r.Owner}/{r.Name}") : null;

    public string RepoText => Repo?.ToString() ?? "—";

    /// <summary>Catálogo oficial, añadida por el usuario u otro catálogo remoto.</summary>
    public AppOrigin Origin { get; }

    public bool IsOfficialOrigin => Origin == AppOrigin.Official;

    public bool IsCustomOrigin => Origin == AppOrigin.Custom;

    public bool IsExternalOrigin => Origin == AppOrigin.External;

    public string OriginText => Origin switch
    {
        AppOrigin.Official => _localization.Get("origin.official"),
        AppOrigin.Custom => _localization.Get("origin.custom"),
        _ => Entry.Source.Name,
    };

    public string OriginGlyph => Origin switch
    {
        AppOrigin.Official => "\uE73E",
        AppOrigin.Custom => "\uE70F",
        _ => "\uE753",
    };

    /// <summary>Perfil de GitHub del autor (el dueño del repo).</summary>
    public Uri? AuthorUrl => Repo is { } r ? new Uri($"https://github.com/{r.Owner}") : null;

    public bool HasAuthorUrl => AuthorUrl is not null;
    public bool HasNoAuthorUrl => AuthorUrl is null;

    /// <summary>"owner/repo · v1.2" o, sin GitHub, un guion.</summary>
    public string ReleaseLinkText => Package.ReleaseUrl is { } url
        ? url.AbsolutePath.Trim('/').Replace("/releases/tag/", " · ")
        : "—";


    /// <summary>Carpeta de la app dentro de la de descargas.</summary>
    public string AppFolder { get; }

    /// <summary>Nueva en esta apertura (no estaba la vez anterior).</summary>
    public bool IsNew { get; }

    // ---- Ficheros ----

    public ObservableCollection<PackageFileViewModel> Files { get; } = [];

    /// <summary>Ficheros publicados que se pueden descargar (para el menú "Descargar ▾").</summary>
    public IEnumerable<PackageFileViewModel> DownloadableFiles => Files.Where(f => f.CanDownload);

    public bool HasSingleFile => DownloadableFiles.Count() == 1;
    public bool HasManyFiles => DownloadableFiles.Count() > 1;
    public bool HasFiles => Files.Count > 0;

    // ---- Estado en la biblioteca ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDownloaded), nameof(HasUpdate), nameof(IsNotDownloaded), nameof(ShowDownloadButton), nameof(ShowDownloadMenu), nameof(ShowUpdateButton))]
    public partial PackageState State { get; private set; }

    /// <summary>Resumen corto: "No descargada", "Descargada · v1.0", "0.21 → 0.21.1", "2 actualizaciones".</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = string.Empty;

    public bool IsNotDownloaded => State == PackageState.NotDownloaded;
    public bool IsDownloaded => State == PackageState.Downloaded;
    public bool HasUpdate => State == PackageState.UpdateAvailable;

    /// <summary>Todo lo descargado de la app (todos sus ficheros y versiones).</summary>
    public IReadOnlyList<InstalledVersion> Versions => _versions;

    public bool HasInstalledVersions => _versions.Count > 0;

    /// <summary>Algún fichero tiene más de una versión descargada.</summary>
    public bool HasManyVersionsOfAnyFile => LibrarySnapshot.AllButLatestPerFile(_versions).Count > 0;

    // ---- Descargas en curso (una por fichero) ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy), nameof(IsIdle))]
    public partial DownloadJobViewModel? ActiveJob { get; private set; }

    public bool IsBusy => ActiveJob is { IsActive: true };
    public bool IsIdle => !IsBusy;
    public bool ShowDownloadButton => IsNotDownloaded && HasSingleFile && !IsAllUpToDate;
    public bool ShowDownloadMenu => HasManyFiles && !IsAllUpToDate;

    /// <summary>Tienes todos los ficheros publicados en su última versión (los solo-beta no cuentan).</summary>
    public bool IsAllUpToDate
    {
        get
        {
            var files = Files.Where(f => f.CanDownload && !f.IsBetaOnly).ToList();
            return files.Count > 0 && files.All(f => f.IsUpToDate);
        }
    }
    public bool ShowUpdateButton => HasUpdate;

    /// <summary>Error de la última acción (borrar, abrir carpeta…), aparte de los de descarga.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActionError))]
    public partial string ActionError { get; set; } = string.Empty;

    public bool HasActionError => ActionError.Length > 0;

    /// <summary>Tras empezar o terminar una descarga: la tarjeta muestra la primera que siga activa.</summary>
    public void RefreshActiveJob()
    {
        ActiveJob = Files.Select(f => f.ActiveJob).FirstOrDefault(j => j is { IsActive: true });
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(IsIdle));
    }

    [RelayCommand]
    private void Download()
    {
        if (DownloadableFiles.FirstOrDefault(f => !f.IsUpToDate) is { } file)
            DownloadFile(file);
    }

    /// <summary>Descarga la última versión (en su canal) de cada fichero desactualizado.</summary>
    [RelayCommand]
    private void UpdateAll()
    {
        foreach (var file in Files.Where(f => f.HasUpdate).ToList())
            DownloadFile(file);
    }

    public void DownloadFile(PackageFileViewModel file) => ((IFileActions)this).Download(file);

    [RelayCommand]
    private void OpenFolder() => _actions.OpenFolder(AppFolder);

    /// <summary>Abre la carpeta de la beta de un fichero (desde el aviso de volver a la estable).</summary>
    public void OpenBetaFolder(PackageFileViewModel file) => _actions.OpenFolder(file.BetaFolderPath);

    /// <summary>Borra versiones (ya confirmado por la vista).</summary>
    public void DeleteVersions(IReadOnlyList<InstalledVersion> versions)
    {
        if (versions.Count > 0)
            _actions.DeleteVersions(this, versions);
    }

    // ---- Acciones de las fichas ----

    void IFileActions.Download(PackageFileViewModel file)
    {
        if (file.File is null || file.IsUpToDate)
            return;
        ActionError = string.Empty;
        _actions.Download(this, file, file.File);
    }

    void IFileActions.TryBeta(PackageFileViewModel file)
    {
        if (file.Status.Beta is not { } beta)
            return;
        ActionError = string.Empty;
        _actions.SetChannel(this, file, FileChannel.Beta);
        _actions.Download(this, file, beta);
    }

    void IFileActions.BackToStable(PackageFileViewModel file, bool deleteBeta)
    {
        ActionError = string.Empty;
        var betas = file.BetaVersions;
        _actions.SetChannel(this, file, FileChannel.Stable);
        if (deleteBeta && betas.Count > 0)
            _actions.DeleteVersions(this, betas);
    }

    void IFileActions.OpenFolder(string path) => _actions.OpenFolder(path);

    void IFileActions.Delete(IReadOnlyList<InstalledVersion> versions) => DeleteVersions(versions);

    public void SetStatus(PackageStatus status)
    {
        _versions = status.Versions;

        // Las descargas en curso siguen enlazadas a su fichero tras recalcular.
        var jobs = Files.Where(f => f.ActiveJob is not null).ToDictionary(f => (f.Key, f.File?.Version), f => f.ActiveJob);
        Files.Clear();
        foreach (var fileStatus in status.Files
            .OrderBy(f => f.Target is null ? 1 : 0)
            .ThenBy(f => f.IsBetaOnly ? 1 : 0)
            .ThenBy(f => AssetClassifier.DetectPlatform(f.Available?.FileName ?? f.Key) == ConsolePlatform.PS4 ? 1 : 0)
            .ThenBy(f => f.Available?.Order ?? int.MaxValue)
            .ThenBy(f => f.Key, StringComparer.OrdinalIgnoreCase))
        {
            var versionsOfFile = status.Versions
                .Where(v => v.Files.Any(f => string.Equals(f.Key, fileStatus.Key, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            var row = new PackageFileViewModel(fileStatus, versionsOfFile, _fileFolder(fileStatus.Key), _localization, this);

            // La descarga en curso de ese fichero (estable o beta) sigue enlazada.
            row.ActiveJob = jobs.FirstOrDefault(j => string.Equals(j.Key.Key, row.Key, StringComparison.OrdinalIgnoreCase)).Value;
            Files.Add(row);
        }

        State = status.State;
        var updates = status.Updates.ToList();
        StatusText = status.State switch
        {
            PackageState.UpdateAvailable when updates.Count > 1 => _localization.Format("status.updatesMany", updates.Count),
            PackageState.UpdateAvailable => _localization.Format(
                "status.update",
                updates.FirstOrDefault()?.Installed?.Version.Version ?? status.Latest?.Version,
                updates.FirstOrDefault()?.Available?.Version ?? Version),
            PackageState.Downloaded => _localization.Format("status.downloaded", VersionLabel.Format(status.Latest?.Version)),
            _ => _localization.Get("status.notDownloaded"),
        };

        OnPropertyChanged(nameof(Versions));
        OnPropertyChanged(nameof(HasInstalledVersions));
        OnPropertyChanged(nameof(HasManyVersionsOfAnyFile));
        OnPropertyChanged(nameof(HasSingleFile));
        OnPropertyChanged(nameof(HasManyFiles));
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(DownloadableFiles));
        OnPropertyChanged(nameof(IsAllUpToDate));
        OnPropertyChanged(nameof(ShowDownloadButton));
        OnPropertyChanged(nameof(ShowDownloadMenu));
        RefreshActiveJob();
    }

    private Avalonia.Media.Imaging.Bitmap? _icon;
    private bool _iconRequested;

    /// <summary>Icono de la app; se descarga la primera vez que la vista lo pide (null mientras tanto o si no trae).</summary>
    public Avalonia.Media.Imaging.Bitmap? Icon
    {
        get
        {
            if (!_iconRequested && Entry.IconUri is { } uri)
            {
                _iconRequested = true;
                _ = LoadIconAsync(uri);
            }
            return _icon;
        }
    }

    private async Task LoadIconAsync(Uri uri)
    {
        var bitmap = await AnchorPS5.App.Services.IconLoader.LoadAsync(uri);
        if (bitmap is null)
            return;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _icon = bitmap;
            OnPropertyChanged(nameof(Icon));
        });
    }

    /// <summary>Lo que anuncian los lectores de pantalla para cada tarjeta.</summary>
    public override string ToString() => Name;
}
