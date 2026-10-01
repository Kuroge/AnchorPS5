using System.ComponentModel;
using System.Globalization;
using AnchorPS5.Core;
using AnchorPS5.Core.Catalog;
using AnchorPS5.Core.Library;
using AnchorPS5.Core.Localization;
using AnchorPS5.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace AnchorPS5.App.ViewModels;

/// <summary>Acciones sobre los paquetes (las implementa el catálogo).</summary>
public interface IPackageActions
{
    void Download(CatalogItemViewModel item);
    void Retry(CatalogItemViewModel item);
    void OpenFolder(string path);
    void DeleteVersion(CatalogItemViewModel item, InstalledVersion version);
    void DeleteAll(CatalogItemViewModel item);
}

/// <summary>Tarjeta y detalle de una app del catálogo, con su estado en la biblioteca.</summary>
public sealed partial class CatalogItemViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly IPackageActions _actions;
    private ImageSource? _icon;
    private IReadOnlyList<InstalledVersion> _versions = [];

    public CatalogItemViewModel(
        CatalogEntry entry,
        LocalizationService localization,
        IPackageActions actions,
        PackageStatus status,
        bool isNew,
        string appFolder)
    {
        _localization = localization;
        _actions = actions;
        Entry = entry;
        IsNew = isNew;
        AppFolder = appFolder;
        Description = string.IsNullOrWhiteSpace(entry.App.Description)
            ? localization.Get("detail.noDescription")
            : entry.App.Description;
        UpdateButtonText = localization.Format("action.update", entry.App.Version);
        DeleteAllConfirmText = localization.Format("confirm.deleteAll", entry.App.Name);
        SetStatus(status);
    }

    public CatalogEntry Entry { get; }

    public string Id => Entry.App.Id;
    public string Name => Entry.App.Name;
    public string Version => Entry.App.Version;
    public string Author => Entry.App.Author;
    public string Description { get; }
    public string SourceName => Entry.Source.Name;
    public string Sha256 => Entry.App.Sha256;
    public string VersionText => "v" + Entry.App.Version;
    public bool HasVersion => !string.IsNullOrWhiteSpace(Entry.App.Version);
    public bool HasSize => Entry.App.SizeBytes > 0;
    public string SizeText => Entry.App.SizeBytes > 0 ? ByteSize.Format(Entry.App.SizeBytes) : "—";
    public string UpdateButtonText { get; }
    public string DeleteAllConfirmText { get; }

    /// <summary>Glifo de la fuente: nube si es remota, carpeta si es local.</summary>
    public string SourceGlyph => Entry.Source.Type == SourceType.Remote ? "" : "";

    /// <summary>Carpeta de la app dentro de la de descargas.</summary>
    public string AppFolder { get; }

    /// <summary>Nueva en esta apertura (no estaba la vez anterior).</summary>
    public bool IsNew { get; }

    // ---- Estado en la biblioteca ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDownloaded), nameof(HasUpdate), nameof(IsNotDownloaded), nameof(ShowDownloadButton), nameof(ShowUpdateButton))]
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

    // ---- Descarga en curso ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy), nameof(IsIdle), nameof(ShowDownloadButton), nameof(ShowUpdateButton), nameof(HasJobError))]
    public partial DownloadJobViewModel? ActiveJob { get; set; }

    /// <summary>Descargando, verificando o extrayendo.</summary>
    public bool IsBusy => ActiveJob is { IsActive: true };
    public bool IsIdle => !IsBusy;
    public bool HasJobError => ActiveJob is { IsFailed: true };
    public bool ShowDownloadButton => IsNotDownloaded && IsIdle;
    public bool ShowUpdateButton => HasUpdate && IsIdle;

    /// <summary>Error de la última acción (borrar, abrir carpeta…), aparte de los de descarga.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActionError))]
    public partial string ActionError { get; set; } = string.Empty;

    public bool HasActionError => ActionError.Length > 0;

    partial void OnActiveJobChanged(DownloadJobViewModel? oldValue, DownloadJobViewModel? newValue)
    {
        if (oldValue is not null)
            oldValue.PropertyChanged -= OnJobPropertyChanged;
        if (newValue is not null)
            newValue.PropertyChanged += OnJobPropertyChanged;
    }

    private void OnJobPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DownloadJobViewModel.Phase) or nameof(DownloadJobViewModel.IsActive))
        {
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(HasJobError));
            OnPropertyChanged(nameof(ShowDownloadButton));
            OnPropertyChanged(nameof(ShowUpdateButton));
        }
    }

    [RelayCommand]
    private void Download()
    {
        ActionError = string.Empty;
        _actions.Download(this);
    }

    [RelayCommand]
    private void CancelDownload() => ActiveJob?.Job.Cancel();

    [RelayCommand]
    private void RetryDownload() => _actions.Retry(this);

    [RelayCommand]
    private void OpenFolder() => _actions.OpenFolder(AppFolder);

    [RelayCommand]
    private void DeleteAll() => _actions.DeleteAll(this);

    public IReadOnlyList<InstalledVersion> Versions => _versions;

    public void SetStatus(PackageStatus status)
    {
        _versions = status.Versions;
        State = status.State;
        InstalledVersions = status.Versions
            .Select(v => new InstalledVersionViewModel(
                v,
                _localization,
                open: () => _actions.OpenFolder(v.FolderPath),
                delete: () => _actions.DeleteVersion(this, v)))
            .ToList();

        var latest = status.Latest?.Version ?? string.Empty;
        (StatusText, StatusTitle) = status.State switch
        {
            PackageState.UpdateAvailable => (
                _localization.Format("status.update", latest, Version),
                _localization.Format("status.updateTitle", latest, Version)),
            PackageState.Downloaded => (
                _localization.Format("status.downloaded", latest),
                _localization.Format("status.downloaded", latest)),
            _ => (_localization.Get("status.notDownloaded"), _localization.Get("status.notDownloaded")),
        };

        StatusDetail = status.Versions.Count switch
        {
            0 => _localization.Format("detail.willSaveTo", AppFolder),
            1 => _localization.Format("detail.versionCountOne", AppFolder),
            var n => _localization.Format("detail.versionCountMany", n, AppFolder),
        };
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
        Detail = version.Verified ? when + " · " + localization.Get("detail.verified") : when;
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
