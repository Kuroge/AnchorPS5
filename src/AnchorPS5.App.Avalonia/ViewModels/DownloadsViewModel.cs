using AnchorPS5.Core.Packages;
using System.Collections.ObjectModel;
using AnchorPS5.Core;
using AnchorPS5.Core.Downloads;
using AnchorPS5.Core.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnchorPS5.App.ViewModels;

/// <summary>Una descarga vista desde la UI (los cambios llegan de hilos de fondo y se pasan al de UI).</summary>
public sealed partial class DownloadJobViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly DownloadManager _manager;
    private readonly IUiThread _dispatcher;
    private bool _refreshQueued;

    public DownloadJobViewModel(DownloadJob job, DownloadManager manager, LocalizationService localization, IUiThread dispatcher)
    {
        Job = job;
        _manager = manager;
        _localization = localization;
        _dispatcher = dispatcher;
        job.Changed += (_, _) => QueueRefresh();
        Refresh();
    }

    public DownloadJob Job { get; }

    public string Name => Job.App.Name;

    public string VersionText => VersionLabel.Format(Job.Version);

    public string FileName => Job.File.FileName;

    [ObservableProperty]
    public partial double ProgressValue { get; private set; }

    [ObservableProperty]
    public partial bool IsIndeterminate { get; private set; }

    [ObservableProperty]
    public partial string StatusText { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFailed), nameof(IsActive))]
    public partial DownloadPhase Phase { get; private set; }

    public bool IsActive => Job.IsActive;

    public bool IsFailed => Phase == DownloadPhase.Failed;

    [RelayCommand]
    private void Cancel() => Job.Cancel();

    [RelayCommand]
    private void Retry() => _manager.Retry(Job);

    /// <summary>Agrupa las notificaciones: como mucho un refresco pendiente en el hilo de UI.</summary>
    private void QueueRefresh()
    {
        if (_refreshQueued)
            return;
        _refreshQueued = true;
        _dispatcher.Enqueue(() =>
        {
            _refreshQueued = false;
            Refresh();
        });
    }

    public void Refresh()
    {
        Phase = Job.Phase;
        IsIndeterminate = Job.Phase is DownloadPhase.Queued or DownloadPhase.Verifying or DownloadPhase.Extracting
            || (Job.Phase == DownloadPhase.Downloading && Job.Progress is null);
        ProgressValue = (Job.Progress ?? 0) * 100;

        StatusText = Job.Phase switch
        {
            DownloadPhase.Queued => _localization.Get("download.queued"),
            DownloadPhase.Downloading when Job.TotalBytes is > 0 => _localization.Format(
                "download.downloading",
                (int)ProgressValue,
                ByteSize.Format(Job.BytesReceived),
                ByteSize.Format(Job.TotalBytes.Value)),
            DownloadPhase.Downloading => _localization.Format("download.downloadingUnknown", ByteSize.Format(Job.BytesReceived)),
            DownloadPhase.Verifying => _localization.Get("download.verifying"),
            DownloadPhase.Extracting => _localization.Get("download.extracting"),
            DownloadPhase.Completed => _localization.Get("download.completed"),
            DownloadPhase.Canceled => _localization.Get("download.canceled"),
            DownloadPhase.Failed => _localization.Get(Job.Error switch
            {
                DownloadError.InvalidUrl => "download.error.invalidUrl",
                DownloadError.HashMismatch => "download.error.hashMismatch",
                DownloadError.Extraction => "download.error.extraction",
                DownloadError.Disk => "download.error.disk",
                _ => "download.error.network",
            }),
            _ => string.Empty,
        };
        OnPropertyChanged(nameof(IsActive));
    }
}

/// <summary>Descargas en curso y fallidas (indicador de la barra de título).</summary>
public sealed partial class DownloadsViewModel : ObservableObject
{
    private readonly DownloadManager _manager;
    private readonly LocalizationService _localization;
    private readonly IUiThread _dispatcher;

    public DownloadsViewModel(DownloadManager manager, LocalizationService localization, IUiThread dispatcher)
    {
        _manager = manager;
        _localization = localization;
        _dispatcher = dispatcher;
        manager.JobFinished += (_, job) => _dispatcher.Enqueue(() => OnJobFinished(job));
    }

    public ObservableCollection<DownloadJobViewModel> Jobs { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActive))]
    public partial int ActiveCount { get; private set; }

    public bool HasActive => ActiveCount > 0;

    public bool HasJobs => Jobs.Count > 0;

    public bool HasNoJobs => Jobs.Count == 0;

    /// <summary>Se lanza en el hilo de UI cuando una descarga termina (bien, mal o cancelada).</summary>
    public event EventHandler<DownloadJobViewModel>? JobFinished;

    public DownloadJobViewModel? Find(DownloadJob job) => Jobs.FirstOrDefault(j => ReferenceEquals(j.Job, job));

    /// <summary>Empieza (o reutiliza) la descarga de una app y la añade a la lista.</summary>
    public DownloadJobViewModel Start(Core.Models.HomebrewApp app, Core.Packages.PackageFile file)
    {
        var job = _manager.Enqueue(app, file);
        var vm = Find(job);
        if (vm is null)
        {
            vm = new DownloadJobViewModel(job, _manager, _localization, _dispatcher);
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(DownloadJobViewModel.Phase))
                    UpdateCounts();
            };
            Jobs.Insert(0, vm);
        }

        UpdateCounts();
        return vm;
    }

    public void Retry(DownloadJobViewModel vm)
    {
        _manager.Retry(vm.Job);
        UpdateCounts();
    }

    [RelayCommand]
    private void Dismiss(DownloadJobViewModel vm)
    {
        if (vm.IsActive)
            return;
        Jobs.Remove(vm);
        UpdateCounts();
    }

    private void OnJobFinished(DownloadJob job)
    {
        if (Find(job) is not { } vm)
            return;

        vm.Refresh();
        // Las fallidas se quedan para poder reintentar; el resto se quitan.
        if (job.Phase != DownloadPhase.Failed)
            Jobs.Remove(vm);
        UpdateCounts();
        JobFinished?.Invoke(this, vm);
    }

    private void UpdateCounts()
    {
        ActiveCount = Jobs.Count(j => j.IsActive);
        OnPropertyChanged(nameof(HasJobs));
        OnPropertyChanged(nameof(HasNoJobs));
    }
}
