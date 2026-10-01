using System.ComponentModel;
using AnchorPS5.Core;
using AnchorPS5.Core.Library;
using AnchorPS5.Core.Localization;
using AnchorPS5.Core.Packages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnchorPS5.App.ViewModels;

/// <summary>Una fila de "Archivos": un fichero publicado (o descargado) con su estado propio.</summary>
public sealed partial class PackageFileViewModel : ObservableObject
{
    private readonly Action<PackageFileViewModel> _download;

    public PackageFileViewModel(FileStatus status, LocalizationService localization, Action<PackageFileViewModel> download)
    {
        Status = status;
        _download = download;

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
}
