using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using AnchorPS5.Core.Configuration;
using AnchorPS5.Core.Library;
using AnchorPS5.Core.Models;

namespace AnchorPS5.Core.Downloads;

/// <summary>
/// Cola de descargas. Cada descarga: bajar (con progreso y SHA-256 al vuelo) → verificar
/// → extraer con 7-Zip → mover a &lt;App&gt;\&lt;versión&gt;. Todo se prepara en una carpeta
/// temporal dentro de la de descargas y solo se mueve al final, así nunca queda una
/// versión a medias.
/// </summary>
public sealed class DownloadManager
{
    /// <summary>Si no llega ningún byte en este tiempo, la descarga se da por caída.</summary>
    public static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(60);

    private readonly HttpClient _http;
    private readonly LibraryService _library;
    private readonly IArchiveExtractor _extractor;
    private readonly SemaphoreSlim _slots;
    private readonly List<DownloadJob> _jobs = [];
    private readonly Lock _lock = new();

    /// <param name="http">Sin Timeout global (las descargas pueden tardar); el bloqueo se detecta por inactividad.</param>
    public DownloadManager(HttpClient http, LibraryService library, IArchiveExtractor extractor, int maxConcurrentDownloads)
    {
        _http = http;
        _library = library;
        _extractor = extractor;
        _slots = new SemaphoreSlim(Math.Max(1, maxConcurrentDownloads));
    }

    /// <summary>Se lanza (desde un hilo de fondo) cuando una descarga termina, falla o se cancela.</summary>
    public event EventHandler<DownloadJob>? JobFinished;

    /// <summary>Descarga en curso (o en cola) de una app, si la hay.</summary>
    public DownloadJob? GetActiveJob(string appId)
    {
        lock (_lock)
            return FindActive(appId);
    }

    /// <summary>Pone en cola la versión del catálogo. Si ya se está descargando, devuelve esa descarga.</summary>
    public DownloadJob Enqueue(HomebrewApp app)
    {
        DownloadJob job;
        lock (_lock)
        {
            if (FindActive(app.Id) is { } existing)
                return existing;

            job = new DownloadJob(app);
            _jobs.Add(job);
        }

        _ = RunAsync(job);
        return job;
    }

    private DownloadJob? FindActive(string appId) =>
        _jobs.LastOrDefault(j => j.IsActive && string.Equals(j.App.Id, appId, StringComparison.OrdinalIgnoreCase));

    /// <summary>Vuelve a intentar una descarga fallida o cancelada.</summary>
    public void Retry(DownloadJob job)
    {
        if (job.IsActive)
            return;

        job.ResetForRetry();
        _ = RunAsync(job);
    }

    private async Task RunAsync(DownloadJob job)
    {
        var token = job.CancellationToken;
        var workDir = Path.Combine(_library.TempRoot, Guid.NewGuid().ToString("N"));
        var acquired = false;

        try
        {
            await _slots.WaitAsync(token);
            acquired = true;

            if (!Uri.TryCreate(job.App.DownloadUrl, UriKind.Absolute, out var url)
                || (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp))
            {
                job.Fail(DownloadError.InvalidUrl, job.App.DownloadUrl);
                return;
            }

            Directory.CreateDirectory(workDir);
            var (filePath, sha256) = await DownloadAsync(job, url, workDir, token);

            job.SetPhase(DownloadPhase.Verifying);
            var expected = job.App.Sha256?.Trim();
            var verified = !string.IsNullOrEmpty(expected);
            if (verified && !string.Equals(expected, sha256, StringComparison.OrdinalIgnoreCase))
            {
                job.Fail(DownloadError.HashMismatch, $"esperado {expected}, obtenido {sha256}");
                return;
            }

            job.SetPhase(DownloadPhase.Extracting);
            var contentDir = Path.Combine(workDir, "content");
            Directory.CreateDirectory(contentDir);
            if (ArchiveTypes.IsArchive(filePath))
            {
                try
                {
                    await _extractor.ExtractAsync(filePath, contentDir, token);
                }
                catch (ExtractionException ex)
                {
                    job.Fail(DownloadError.Extraction, ex.Message);
                    return;
                }
            }
            else
            {
                File.Move(filePath, Path.Combine(contentDir, Path.GetFileName(filePath)));
            }

            WriteMetadata(job, contentDir, url, sha256, verified);

            var finalDir = _library.GetVersionFolder(job.App, job.Version);
            Directory.CreateDirectory(Path.GetDirectoryName(finalDir)!);
            if (Directory.Exists(finalDir))
                Directory.Delete(finalDir, recursive: true); // misma versión: se reemplaza
            Directory.Move(contentDir, finalDir);

            job.Complete(finalDir);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            job.SetPhase(DownloadPhase.Canceled);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or HttpIOException)
        {
            // OperationCanceled sin cancelar el usuario = descarga estancada.
            job.Fail(DownloadError.Network, ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            job.Fail(DownloadError.Disk, ex.Message);
        }
        finally
        {
            if (acquired)
                _slots.Release();
            TryDelete(workDir);
            if (!job.IsActive)
                JobFinished?.Invoke(this, job);
        }
    }

    private async Task<(string FilePath, string Sha256)> DownloadAsync(DownloadJob job, Uri url, string workDir, CancellationToken token)
    {
        job.SetPhase(DownloadPhase.Downloading);

        using var stall = CancellationTokenSource.CreateLinkedTokenSource(token);
        stall.CancelAfter(StallTimeout);

        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, stall.Token);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}", null, response.StatusCode);

        job.SetTotal(response.Content.Headers.ContentLength ?? (job.App.SizeBytes > 0 ? job.App.SizeBytes : null));

        var fileName = GetFileName(response, url);
        var filePath = Path.Combine(workDir, fileName);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using (var source = await response.Content.ReadAsStreamAsync(stall.Token))
        await using (var target = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            var buffer = new byte[81920];
            long total = 0;
            var lastNotify = Stopwatch.StartNew();
            int read;
            while ((read = await source.ReadAsync(buffer, stall.Token)) > 0)
            {
                stall.CancelAfter(StallTimeout);
                hash.AppendData(buffer, 0, read);
                await target.WriteAsync(buffer.AsMemory(0, read), token);
                total += read;

                var notify = lastNotify.ElapsedMilliseconds >= 100;
                if (notify)
                    lastNotify.Restart();
                job.ReportBytes(total, notify);
            }

            job.ReportBytes(total, notify: true);
        }

        return (filePath, Convert.ToHexStringLower(hash.GetHashAndReset()));
    }

    /// <summary>Nombre del fichero: el de Content-Disposition o el último tramo de la URL.</summary>
    private static string GetFileName(HttpResponseMessage response, Uri url)
    {
        var disposition = response.Content.Headers.ContentDisposition;
        var name = disposition?.FileNameStar ?? disposition?.FileName?.Trim('"');
        if (string.IsNullOrWhiteSpace(name))
            name = Uri.UnescapeDataString(url.Segments.LastOrDefault() ?? string.Empty).Trim('/');

        name = Path.GetFileName(name ?? string.Empty);
        return string.IsNullOrWhiteSpace(name) ? "download.bin" : PathNames.Sanitize(name);
    }

    private static void WriteMetadata(DownloadJob job, string contentDir, Uri url, string sha256, bool verified)
    {
        var metadata = new VersionMetadata
        {
            Id = job.App.Id,
            Name = job.App.Name,
            Version = job.Version,
            DownloadedAt = DateTimeOffset.Now,
            DownloadUrl = url.AbsoluteUri,
            Sha256 = sha256,
            Verified = verified,
        };
        File.WriteAllText(Path.Combine(contentDir, VersionMetadata.FileName), JsonSerializer.Serialize(metadata, JsonDefaults.Options));
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Restos temporales: se limpian en la próxima descarga o al borrar la carpeta.
        }
    }
}
