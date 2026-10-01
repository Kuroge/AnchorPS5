using System.Collections.ObjectModel;
using System.Globalization;
using AnchorPS5.Core;
using AnchorPS5.Core.Catalog;
using AnchorPS5.Core.Library;
using AnchorPS5.Core.Localization;
using AnchorPS5.Core.Models;
using AnchorPS5.Core.Packages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace AnchorPS5.App.ViewModels;

/// <summary>Acciones sobre los paquetes (las implementa el catálogo).</summary>
public interface IPackageActions
{
    void Download(CatalogItemViewModel item, PackageFileViewModel file);
    void OpenFolder(string path);
    void DeleteVersion(CatalogItemViewModel item, InstalledVersion version);
    void DeleteAll(CatalogItemViewModel item);
}

/// <summary>Tarjeta y detalle de una app del catálogo, con sus ficheros y su estado en la biblioteca.</summary>
public sealed partial class CatalogItemViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly IPackageActions _actions;
    private ImageSource? _icon;
    private IReadOnlyList<InstalledVersion> _versions = [];

    public CatalogItemViewModel(
        CatalogEntry entry,
        ResolvedPackage package,
        LocalizationService localization,
        IPackageActions actions,
        PackageStatus status,
        bool isNew,
        string appFolder)
    {
        _localization = localization;
        _actions = actions;
        Entry = entry;
        Package = package;
        IsNew = isNew;
        AppFolder = appFolder;
        Description = string.IsNullOrWhiteSpace(entry.App.Description)
            ? localization.Get("detail.noDescription")
            : entry.App.Description;
        DeleteAllConfirmText = localization.Format("confirm.deleteAll", entry.App.Name);
        BetaText = package.BetaVersion is { } beta ? localization.Format("file.betaChip", beta) : string.Empty;
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
    public string SourceName => Entry.Source.Name;
    public string DeleteAllConfirmText { get; }
    public string FilesTitle { get; }

    /// <summary>Versión publicada: la de GitHub o, si no hay, la del catálogo.</summary>
    public string Version => Package.DisplayVersion ?? Entry.App.Version;
    public string VersionText => "v" + Version;
    public bool HasVersion => !string.IsNullOrWhiteSpace(Version);

    public bool HasBeta => Package.BetaVersion is not null;
    public string BetaText { get; }

    /// <summary>Tamaño del fichero principal (si solo hay uno).</summary>
    public bool HasSize => Package.Files.Count(f => !f.IsPrerelease) == 1 && Package.Files[0].SizeBytes > 0;
    public string SizeText => HasSize ? ByteSize.Format(Package.Files[0].SizeBytes) : "—";

    public bool HasReleaseUrl => Package.ReleaseUrl is not null;

    /// <summary>"owner/repo · v1.2" o, sin GitHub, un guion.</summary>
    public string ReleaseLinkText => Package.ReleaseUrl is { } url
        ? url.AbsolutePath.Trim('/').Replace("/releases/tag/", " · ")
        : "—";

    /// <summary>Glifo de la fuente: nube si es remota, carpeta si es local.</summary>
    public string SourceGlyph => Entry.Source.Type == SourceType.Remote ? "" : "";

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

    [ObservableProperty]
    public partial string StatusText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusTitle { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusDetail { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInstalledVersions))]
    public partial IReadOnlyList<InstalledVersionViewModel> InstalledVersions { get; private set; } = [];

    public bool IsNotDownloaded => State == PackageState.NotDownloaded;
    public bool IsDownloaded => State == PackageState.Downloaded;
    public bool HasUpdate => State == PackageState.UpdateAvailable;
    public bool HasInstalledVersions => InstalledVersions.Count > 0;

    // ---- Descargas en curso (una por fichero) ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy), nameof(IsIdle), nameof(ShowDownloadButton), nameof(ShowDownloadMenu), nameof(ShowUpdateButton))]
    public partial DownloadJobViewModel? ActiveJob { get; private set; }

    public bool IsBusy => ActiveJob is { IsActive: true };
    public bool IsIdle => !IsBusy;
    public bool ShowDownloadButton => IsNotDownloaded && HasSingleFile;
    public bool ShowDownloadMenu => HasManyFiles;
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
        if (DownloadableFiles.FirstOrDefault() is { } file)
            DownloadFile(file);
    }

    /// <summary>Descarga la última versión de cada fichero que tienes desactualizado.</summary>
    [RelayCommand]
    private void UpdateAll()
    {
        foreach (var file in Files.Where(f => f.HasUpdate && f.Status.CountsForUpdate).ToList())
            DownloadFile(file);
    }

    public void DownloadFile(PackageFileViewModel file)
    {
        ActionError = string.Empty;
        _actions.Download(this, file);
    }

    [RelayCommand]
    private void OpenFolder() => _actions.OpenFolder(AppFolder);

    [RelayCommand]
    private void DeleteAll() => _actions.DeleteAll(this);

    public IReadOnlyList<InstalledVersion> Versions => _versions;

    public void SetStatus(PackageStatus status)
    {
        _versions = status.Versions;

        // Las descargas en curso siguen enlazadas a su fichero tras recalcular.
        var jobs = Files.Where(f => f.ActiveJob is not null).ToDictionary(f => (f.Key, f.File?.Version), f => f.ActiveJob);
        Files.Clear();
        foreach (var fileStatus in status.Files
            .OrderBy(f => f.Available is null ? 1 : 0)
            .ThenBy(f => f.Available?.IsPrerelease == true ? 1 : 0)
            .ThenBy(f => AssetClassifier.DetectPlatform(f.Available?.FileName ?? f.Key) == ConsolePlatform.PS4 ? 1 : 0)
            .ThenBy(f => f.Available?.Order ?? int.MaxValue)
            .ThenBy(f => f.Key, StringComparer.OrdinalIgnoreCase))
        {
            var row = new PackageFileViewModel(fileStatus, _localization, DownloadFile);
            if (jobs.TryGetValue((row.Key, row.File?.Version), out var job))
                row.ActiveJob = job;
            Files.Add(row);
        }

        State = status.State;
        InstalledVersions = status.Versions
            .Select(v => new InstalledVersionViewModel(
                v,
                _localization,
                open: () => _actions.OpenFolder(v.FolderPath),
                delete: () => _actions.DeleteVersion(this, v)))
            .ToList();

        var installed = status.Latest?.Version ?? string.Empty;
        var updates = status.Updates.ToList();
        (StatusText, StatusTitle) = status.State switch
        {
            PackageState.UpdateAvailable when updates.Count > 1 => (
                _localization.Format("status.updatesMany", updates.Count),
                _localization.Format("status.updatesManyTitle", updates.Count)),
            PackageState.UpdateAvailable => (
                _localization.Format("status.update", updates.FirstOrDefault()?.Installed?.Version.Version ?? installed, updates.FirstOrDefault()?.Available?.Version ?? Version),
                _localization.Format("status.updateTitle", updates.FirstOrDefault()?.Installed?.Version.Version ?? installed, updates.FirstOrDefault()?.Available?.Version ?? Version)),
            PackageState.Downloaded => (
                _localization.Format("status.downloaded", installed),
                _localization.Format("status.downloaded", installed)),
            _ => (_localization.Get("status.notDownloaded"), _localization.Get("status.notDownloaded")),
        };

        StatusDetail = status.Versions.Count switch
        {
            0 => _localization.Format("detail.willSaveTo", AppFolder),
            1 => _localization.Format("detail.versionCountOne", AppFolder),
            var n => _localization.Format("detail.versionCountMany", n, AppFolder),
        };

        OnPropertyChanged(nameof(HasSingleFile));
        OnPropertyChanged(nameof(HasManyFiles));
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(DownloadableFiles));
        OnPropertyChanged(nameof(ShowDownloadButton));
        OnPropertyChanged(nameof(ShowDownloadMenu));
        RefreshActiveJob();
    }

    /// <summary>Se crea al pedirlo (en el hilo de UI); null si la app no trae icono.</summary>
    public ImageSource? Icon => Entry.IconUri is null ? null : _icon ??= new BitmapImage(Entry.IconUri);

    /// <summary>Lo que anuncian los lectores de pantalla para cada tarjeta.</summary>
    public override string ToString() => Name;
}

/// <summary>Una versión descargada, para la lista del detalle.</summary>
public sealed partial class InstalledVersionViewModel
{
    private readonly Action _open;
    private readonly Action _delete;

    public InstalledVersionViewModel(InstalledVersion version, LocalizationService localization, Action open, Action delete)
    {
        _open = open;
        _delete = delete;
        Version = "v" + version.Version;
        FolderPath = version.FolderPath;
        var when = version.DownloadedAt is { } date
            ? localization.Format("detail.downloadedAt", date.ToLocalTime().ToString("d", CultureInfo.CurrentCulture))
            : localization.Get("detail.manualCopy");
        var parts = new List<string> { when };
        if (version.Verified)
            parts.Add(localization.Get("detail.verified"));
        if (version.Files.Count > 0)
            parts.Add(string.Join(", ", version.Files.Select(f => f.FileName)));
        Detail = string.Join(" · ", parts);
        DeleteConfirmText = localization.Format("confirm.deleteVersion", Version);
    }

    public string Version { get; }
    public string FolderPath { get; }
    public string Detail { get; }
    public string DeleteConfirmText { get; }

    [RelayCommand]
    private void Open() => _open();

    [RelayCommand]
    private void Delete() => _delete();
}
