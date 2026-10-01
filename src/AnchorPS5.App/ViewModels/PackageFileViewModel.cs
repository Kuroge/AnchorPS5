using System.ComponentModel;
using System.Globalization;
using AnchorPS5.Core;
using AnchorPS5.Core.Library;
using AnchorPS5.Core.Localization;
using AnchorPS5.Core.Packages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnchorPS5.App.ViewModels;

/// <summary>
/// Ficha de un fichero: lo publicado (o lo que tienes si ya no se publica), su estado
/// propio y el histórico de versiones descargadas de ese fichero.
/// </summary>
public sealed partial class PackageFileViewModel : ObservableObject
{
    private readonly Action<PackageFileViewModel> _download;
    private readonly Action<string> _openFolder;

    public PackageFileViewModel(
        FileStatus status,
        IReadOnlyList<InstalledVersion> versions,
        string folderPath,
        LocalizationService localization,
        Action<PackageFileViewModel> download,
        Action<string> openFolder,
        Action<IReadOnlyList<InstalledVersion>> delete)
    {
        Status = status;
        _download = download;
        _openFolder = openFolder;
        FolderPath = folderPath;
        InstalledVersions = versions;

        var file = status.Available;
        var installedVersion = status.Installed?.Version.Version;
        FileName = file?.FileName ?? status.Installed?.File.FileName ?? status.Key;
        Label = file?.Label ?? FileName;
        HasLabel = file?.Label is not null;
        Description = file?.Description ?? string.Empty;
        SizeText = file is { SizeBytes: > 0 } ? ByteSize.Format(file.SizeBytes) : string.Empty;
        IsPs4 = (file?.Platform ?? AssetClassifier.DetectPlatform(FileName)) == ConsolePlatform.PS4;
        IsBeta = file?.IsPrerelease ?? status.Installed?.File.IsPrerelease ?? false;
        CanDownload = file is not null;

        StateText = status.State switch
        {
            FileState.UpToDate => localization.Format("file.upToDate", installedVersion),
            FileState.UpdateAvailable => localization.Format("file.update", installedVersion, file!.Version),
            FileState.NoLongerPublished => localization.Format("file.noLongerPublished", installedVersion),
            FileState.Unknown => localization.Format("file.installedOnly", installedVersion),
            _ => localization.Format("file.available", file?.Version),
        };
        DownloadText = localization.Get(status.State == FileState.UpdateAvailable ? "action.updateFile" : "action.download");
        MenuText = string.Join("  ·  ", new[] { Label, IsPs4 ? "PS4" : null, IsBeta ? "beta" : null, SizeText }.Where(s => !string.IsNullOrEmpty(s)));

        Versions = versions
            .Select(v => new FileVersionViewModel(v, localization, () => openFolder(v.FolderPath), () => delete([v])))
            .ToList();
    }

    public FileStatus Status { get; }

    public PackageFile? File => Status.Available;

    public string Key => Status.Key;
    public string Label { get; }
    public bool HasLabel { get; }
    public string FileName { get; }
    public string Description { get; }
    public bool HasDescription => Description.Length > 0;
    public string SizeText { get; }
    public bool IsPs4 { get; }
    public bool IsBeta { get; }
    public bool CanDownload { get; }
    public string StateText { get; }
    public string DownloadText { get; }

    /// <summary>Texto del menú "Descargar ▾" (etiqueta · PS4 · beta · tamaño).</summary>
    public string MenuText { get; }

    public bool IsUpToDate => Status.State == FileState.UpToDate;
    public bool HasUpdate => Status.State == FileState.UpdateAvailable;

    /// <summary>
    /// Ficha resaltada en dorado: solo si la actualización cuenta (una beta cuando usas la
    /// estable es opcional y no se resalta).
    /// </summary>
    public bool HighlightUpdate => HasUpdate && Status.CountsForUpdate;

    // ---- Histórico de este fichero ----

    /// <summary>Carpeta del fichero, con todas sus versiones: &lt;App&gt;\&lt;fichero&gt;.</summary>
    public string FolderPath { get; }

    /// <summary>Versiones descargadas de este fichero, de la más nueva a la más antigua.</summary>
    public IReadOnlyList<InstalledVersion> InstalledVersions { get; }

    public IReadOnlyList<FileVersionViewModel> Versions { get; }

    public bool HasVersions => Versions.Count > 0;
    public bool HasNoVersions => Versions.Count == 0;
    public bool HasManyVersions => Versions.Count > 1;

    // ---- Descarga en curso ----

    /// <summary>Muestra el botón de descargar/actualizar (no si ya está al día ni mientras descarga).</summary>
    public bool ShowDownloadButton => CanDownload && !IsUpToDate && !IsBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy), nameof(HasJobError), nameof(ShowDownloadButton))]
    public partial DownloadJobViewModel? ActiveJob { get; set; }

    public bool IsBusy => ActiveJob is { IsActive: true };
    public bool HasJobError => ActiveJob is { IsFailed: true };

    partial void OnActiveJobChanged(DownloadJobViewModel? oldValue, DownloadJobViewModel? newValue)
    {
        if (oldValue is not null)
            oldValue.PropertyChanged -= OnJobChanged;
        if (newValue is not null)
            newValue.PropertyChanged += OnJobChanged;
    }

    private void OnJobChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DownloadJobViewModel.Phase) or nameof(DownloadJobViewModel.IsActive))
        {
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(HasJobError));
            OnPropertyChanged(nameof(ShowDownloadButton));
        }
    }

    [RelayCommand]
    private void Download() => _download(this);

    [RelayCommand]
    private void Cancel() => ActiveJob?.Job.Cancel();

    [RelayCommand]
    private void Retry() => ActiveJob?.RetryCommand.Execute(null);

    [RelayCommand]
    private void OpenFolder() => _openFolder(FolderPath);
}

/// <summary>Una versión descargada de un fichero, dentro de su histórico.</summary>
public sealed partial class FileVersionViewModel
{
    private readonly Action _open;
    private readonly Action _delete;

    public FileVersionViewModel(InstalledVersion version, LocalizationService localization, Action open, Action delete)
    {
        Installed = version;
        _open = open;
        _delete = delete;
        VersionText = "v" + version.Version;
        FolderPath = version.FolderPath;

        var file = version.Files.FirstOrDefault();
        IsBeta = file?.IsPrerelease ?? false;
        var parts = new List<string>
        {
            version.DownloadedAt is { } date
                ? localization.Format("detail.downloadedAt", date.ToLocalTime().ToString("d", CultureInfo.CurrentCulture))
                : localization.Get("detail.manualCopy"),
        };
        if (version.Verified)
            parts.Add(localization.Get("detail.verified"));
        Detail = string.Join(" · ", parts);
    }

    public InstalledVersion Installed { get; }
    public string VersionText { get; }
    public string FolderPath { get; }
    public string Detail { get; }
    public bool IsBeta { get; }

    [RelayCommand]
    private void Open() => _open();

    /// <summary>Lo llama la vista tras confirmar.</summary>
    public void Delete() => _delete();
}
