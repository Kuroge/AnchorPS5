using AnchorPS5.Core.Models;
using AnchorPS5.Core.Packages;

namespace AnchorPS5.Core.Downloads;

public enum DownloadPhase
{
    Queued,
    Downloading,
    Verifying,
    Extracting,
    Completed,
    Failed,
    Canceled,
}

public enum DownloadError
{
    None,
    /// <summary>La app no trae una URL http(s) válida.</summary>
    InvalidUrl,
    Network,
    /// <summary>El SHA-256 del fichero no coincide con el del catálogo.</summary>
    HashMismatch,
    Extraction,
    /// <summary>No se puede escribir en la carpeta de descargas.</summary>
    Disk,
}

/// <summary>
/// Una descarga de una versión concreta de una app. Sus propiedades cambian desde
/// hilos de fondo: quien la muestre debe pasar <see cref="Changed"/> al hilo de UI.
/// </summary>
public sealed class DownloadJob
{
    private CancellationTokenSource _cts = new();

    internal DownloadJob(HomebrewApp app, PackageFile file)
    {
        App = app;
        File = file;
    }

    public HomebrewApp App { get; }

    /// <summary>El fichero concreto de la release que se descarga.</summary>
    public PackageFile File { get; }

    public string Version => File.Version;

    public DownloadPhase Phase { get; private set; } = DownloadPhase.Queued;

    public long BytesReceived { get; private set; }

    /// <summary>Tamaño total si se conoce (cabecera Content-Length o sizeBytes del catálogo).</summary>
    public long? TotalBytes { get; private set; }

    public DownloadError Error { get; private set; }

    /// <summary>Detalle técnico del error (código HTTP, salida de 7-Zip…).</summary>
    public string? ErrorDetail { get; private set; }

    /// <summary>Carpeta final de la versión, cuando termina bien.</summary>
    public string? InstalledPath { get; private set; }

    public bool IsActive => Phase is DownloadPhase.Queued or DownloadPhase.Downloading or DownloadPhase.Verifying or DownloadPhase.Extracting;

    /// <summary>Progreso 0..1, o null si no se conoce el tamaño.</summary>
    public double? Progress => TotalBytes is > 0 ? Math.Min(1.0, (double)BytesReceived / TotalBytes.Value) : null;

    /// <summary>Se lanza al cambiar de fase y, como mucho cada 100 ms, con el progreso.</summary>
    public event EventHandler? Changed;

    internal CancellationToken CancellationToken => _cts.Token;

    public void Cancel() => _cts.Cancel();

    internal void ResetForRetry()
    {
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        Phase = DownloadPhase.Queued;
        BytesReceived = 0;
        Error = DownloadError.None;
        ErrorDetail = null;
        InstalledPath = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void SetPhase(DownloadPhase phase)
    {
        Phase = phase;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void SetTotal(long? total) => TotalBytes = total;

    internal void ReportBytes(long bytes, bool notify)
    {
        BytesReceived = bytes;
        if (notify)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void Complete(string installedPath)
    {
        InstalledPath = installedPath;
        SetPhase(DownloadPhase.Completed);
    }

    internal void Fail(DownloadError error, string? detail)
    {
        Error = error;
        ErrorDetail = detail;
        SetPhase(DownloadPhase.Failed);
    }
}
