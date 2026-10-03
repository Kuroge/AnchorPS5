using System.ComponentModel;
using System.Globalization;
using AnchorPS5.Core;
using AnchorPS5.Core.Library;
using AnchorPS5.Core.Localization;
using AnchorPS5.Core.Packages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnchorPS5.App.ViewModels;

/// <summary>Lo que el usuario puede pedir sobre una ficha (lo resuelve el catálogo).</summary>
public interface IFileActions
{
    void Download(PackageFileViewModel file);
    void TryBeta(PackageFileViewModel file);

    /// <summary>Vuelve al canal estable; si <paramref name="deleteBeta"/>, borra las betas descargadas de ese fichero.</summary>
    void BackToStable(PackageFileViewModel file, bool deleteBeta);
    void OpenFolder(string path);
    void Delete(IReadOnlyList<InstalledVersion> versions);
}

/// <summary>
/// Ficha de un fichero con sus dos canales (estable/beta): su estado en el canal en el que
/// está el usuario, la beta disponible si la hay y el histórico de versiones descargadas.
/// </summary>
public sealed partial class PackageFileViewModel : ObservableObject
{
    private readonly IFileActions _actions;

    public PackageFileViewModel(
        FileStatus status,
        IReadOnlyList<InstalledVersion> versions,
        string folderPath,
        LocalizationService localization,
        IFileActions actions)
    {
        Status = status;
        _actions = actions;
        FolderPath = folderPath;
        InstalledVersions = versions;

        var shown = status.Available;
        var installed = status.Installed;
        FileName = shown?.FileName ?? installed?.File.FileName ?? status.Key;
        Label = shown?.Label is { IsEmpty: false } label ? label.Get(localization.CurrentLanguage) : FileName;
        HasLabel = shown?.Label is { IsEmpty: false };
        Description = shown?.Description?.Get(localization.CurrentLanguage) ?? string.Empty;
        SizeText = status.Target is { SizeBytes: > 0 } t ? ByteSize.Format(t.SizeBytes) : string.Empty;
        IsPs4 = AssetClassifier.DetectPlatform(FileName) == ConsolePlatform.PS4;
        IsBeta = status.Channel == FileChannel.Beta;

        var mine = VersionLabel.Format(installed?.Version.Version);
        var target = VersionLabel.Format(status.Target?.Version);
        StateText = (status.State, IsBeta) switch
        {
            (FileState.UpToDate, false) => localization.Format("file.upToDate", mine),
            (FileState.UpToDate, true) => localization.Format("file.betaUpToDate", mine),
            (FileState.UpdateAvailable, false) => localization.Format("file.update", mine, target),
            (FileState.UpdateAvailable, true) => localization.Format("file.betaUpdate", mine, target),
            (FileState.NoLongerPublished, _) => localization.Format("file.noLongerPublished", mine),
            (FileState.Unknown, _) => localization.Format("file.installedOnly", mine),
            (_, true) when status.IsBetaOnly => localization.Format("file.betaOnlyAvailable", target),
            _ => localization.Format("file.available", target),
        };
        DownloadText = localization.Get(status.State == FileState.UpdateAvailable ? "action.updateFile" : "action.download");
        BetaOfferText = !status.BetaOffered ? string.Empty
            : localization.Format(status.BetaDownloaded ? "file.betaOfferDownloaded" : "file.betaOffer", VersionLabel.Format(status.Beta!.Version));
        TryBetaText = localization.Get(status.BetaDownloaded ? "action.useBeta" : "action.tryBeta");

        var parts = new[] { Label, IsPs4 ? "PS4" : null, IsBeta ? "beta" : null, SizeText };
        MenuText = string.Join("  ·  ", parts.Where(s => !string.IsNullOrEmpty(s)));
        if (IsUpToDate)
            MenuText += "  ·  " + localization.Get("file.alreadyHave");

        Versions = versions
            .Select(v => new FileVersionViewModel(v, localization, () => actions.OpenFolder(v.FolderPath), () => actions.Delete([v])))
            .ToList();
    }

    public FileStatus Status { get; }

    /// <summary>Lo que descargan "Descargar/Actualizar" en el canal actual.</summary>
    public PackageFile? File => Status.Target;

    public string Key => Status.Key;
    public string Label { get; }
    public bool HasLabel { get; }
    public string FileName { get; }
    public string Description { get; }
    public bool HasDescription => Description.Length > 0;
    public string SizeText { get; }
    public bool IsPs4 { get; }

    /// <summary>El usuario está en el canal beta de este fichero (o solo existe en beta).</summary>
    public bool IsBeta { get; }

    public bool IsBetaOnly => Status.IsBetaOnly;
    public string StateText { get; }
    public string DownloadText { get; }
    public string MenuText { get; }

    /// <summary>Se puede descargar algo en el canal actual.</summary>
    public bool CanDownload => Status.Target is not null;

    public bool IsUpToDate => Status.State == FileState.UpToDate;
    public bool HasUpdate => Status.State == FileState.UpdateAvailable;

    /// <summary>Ficha resaltada en dorado: hay actualización en su canal.</summary>
    public bool HighlightUpdate => HasUpdate;

    /// <summary>Descargar este fichero sería bajar una beta por primera vez: pide confirmación.</summary>
    public bool NeedsBetaWarning => IsBetaOnly && Status.State == FileState.NotDownloaded;

    // ---- Canal beta ----

    public bool HasBetaOffer => Status.BetaOffered;
    public string BetaOfferText { get; }
    public string BetaVersion => Status.Beta?.Version ?? string.Empty;

    /// <summary>La beta ofrecida ya está descargada: pasar a ella no descarga nada.</summary>
    public bool BetaDownloaded => Status.BetaDownloaded;

    /// <summary>"Probar beta" (hay que descargarla) o "Usar la beta" (ya la tienes).</summary>
    public string TryBetaText { get; }
    public bool CanReturnToStable => Status.CanReturnToStable;

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

    public bool ShowTryBetaButton => HasBetaOffer && !IsBusy;

    public bool ShowBackToStableButton => CanReturnToStable && !IsBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy), nameof(HasJobError), nameof(ShowDownloadButton), nameof(ShowTryBetaButton), nameof(ShowBackToStableButton))]
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
            OnPropertyChanged(nameof(ShowTryBetaButton));
            OnPropertyChanged(nameof(ShowBackToStableButton));
        }
    }

    /// <summary>Descarga lo del canal actual (la vista confirma antes si es una beta nueva).</summary>
    public void Download() => _actions.Download(this);

    /// <summary>Pasa a la beta y la descarga (la vista ya ha mostrado el aviso).</summary>
    public void TryBeta() => _actions.TryBeta(this);

    /// <summary>Vuelve a la estable (la vista ya ha preguntado qué hacer con las betas).</summary>
    public void BackToStable(bool deleteBeta) => _actions.BackToStable(this, deleteBeta);

    /// <summary>Versiones beta descargadas de este fichero, de la más nueva a la más antigua.</summary>
    public IReadOnlyList<InstalledVersion> BetaVersions =>
        InstalledVersions.Where(v => v.Files.Any(f => f.IsPrerelease)).ToList();

    /// <summary>Carpeta a abrir desde el aviso: la de la beta si solo hay una, si no la del fichero.</summary>
    public string BetaFolderPath => BetaVersions is [var only] ? only.FolderPath : FolderPath;

    [RelayCommand]
    private void Cancel() => ActiveJob?.Job.Cancel();

    [RelayCommand]
    private void Retry() => ActiveJob?.RetryCommand.Execute(null);

    [RelayCommand]
    private void OpenFolder() => _actions.OpenFolder(FolderPath);
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
        VersionText = VersionLabel.Format(version.Version);
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
