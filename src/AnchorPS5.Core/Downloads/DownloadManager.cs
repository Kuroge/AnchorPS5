using System.Diagnostics;
using System.Security.Cryptography;
using AnchorPS5.Core.Library;
using AnchorPS5.Core.Models;
using AnchorPS5.Core.Packages;

namespace AnchorPS5.Core.Downloads;

/// <summary>
/// Cola de descargas de ficheros concretos. Cada descarga: bajar (con progreso y
/// SHA-256 al vuelo) → verificar → extraer con 7-Zip → añadir a &lt;App&gt;\&lt;versión&gt;.
/// Todo se prepara en una carpeta temporal dentro de la de descargas y solo se mueve al
/// final, así nunca queda un fichero a medias. Varios ficheros de la misma versión
/// conviven en su carpeta.
/// </summary>
public sealed class DownloadManager
{
    /// <summary>Si no llega ningún byte en este tiempo, la descarga se da por caída.</summary>
    public static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(60);

    private readonly HttpClient _http;
    private readonly LibraryService _library;
    private readonly IArchiveExtractor _extractor;
    private readonly SemaphoreSlim _slots;
    private readonly SemaphoreSlim _finalize = new(1, 1);
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

    /// <summary>Descargas en curso (o en cola) de una app.</summary>
    public IReadOnlyList<DownloadJob> GetActiveJobs(string appId)
    {
        lock (_lock)
            return _jobs.Where(j => j.IsActive && SameApp(j, appId)).ToList();
    }

    /// <summary>Pone en cola un fichero. Si ya se está descargando, devuelve esa descarga.</summary>
    public DownloadJob Enqueue(HomebrewApp app, PackageFile file)
    {
        DownloadJob job;
        lock (_lock)
        {
            var existing = _jobs.LastOrDefault(j => j.IsActive && SameApp(j, app.Id)
                && string.Equals(j.File.Key, file.Key, StringComparison.OrdinalIgnoreCase)
                && string.Equals(j.File.Version, file.Version, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
                return existing;

            job = new DownloadJob(app, file);
            _jobs.Add(job);
        }

        _ = RunAsync(job);
        return job;
    }

    /// <summary>Descarga única de una app fuera de GitHub (downloadUrl del catálogo).</summary>
    public DownloadJob Enqueue(HomebrewApp app)
    {
        var file = PackageResolver.FromStatic(app).Files.FirstOrDefault()
            ?? new PackageFile(app.Id, app.DownloadUrl, app.DownloadUrl, app.SizeBytes, null, app.Version, false, ConsolePlatform.Unknown);
        return Enqueue(app, file);
    }

    /// <summary>Vuelve a intentar una descarga fallida o cancelada.</summary>
    public void Retry(DownloadJob job)
    {
        if (job.IsActive)
            return;

        job.ResetForRetry();
        _ = RunAsync(job);
    }

    private static bool SameApp(DownloadJob job, string appId) =>
        string.Equals(job.App.Id, appId, StringComparison.OrdinalIgnoreCase);

    private async Task RunAsync(DownloadJob job)
    {
        var token = job.CancellationToken;
        var workDir = Path.Combine(_library.TempRoot, Guid.NewGuid().ToString("N"));
        var acquired = false;

        try
        {
            await _slots.WaitAsync(token);
            acquired = true;

            if (!Uri.TryCreate(job.File.Url, UriKind.Absolute, out var url)
                || (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp))
            {
                job.Fail(DownloadError.InvalidUrl, job.File.Url);
                return;
            }

            Directory.CreateDirectory(workDir);
            var (filePath, sha256) = await DownloadAsync(job, url, workDir, token);

            job.SetPhase(DownloadPhase.Verifying);
            var expected = job.File.Sha256?.Trim();
            var verified = !string.IsNullOrEmpty(expected);
            if (verified && !string.Equals(expected, sha256, StringComparison.OrdinalIgnoreCase))
            {
                job.Fail(DownloadError.HashMismatch, $"esperado {expected}, obtenido {sha256}");
                return;
            }

            // Lo que se añadirá a la carpeta de la versión: el fichero tal cual o, si es
            // un comprimido, una subcarpeta con su nombre y lo extraído dentro.
            job.SetPhase(DownloadPhase.Extracting);
            string entryName;
            string entryPath;
            if (ArchiveTypes.IsArchive(filePath))
            {
                entryName = PathNames.Sanitize(ArchiveBaseName(Path.GetFileName(filePath)));
                entryPath = Path.Combine(workDir, "content", entryName);
                Directory.CreateDirectory(entryPath);
                try
                {
                    await _extractor.ExtractAsync(filePath, entryPath, token);
                }
                catch (ExtractionException ex)
                {
                    job.Fail(DownloadError.Extraction, ex.Message);
                    return;
                }
            }
            else
            {
                entryName = Path.GetFileName(filePath);
                entryPath = filePath;
            }

            var versionDir = _library.GetVersionFolder(job.App, job.Version);
            await _finalize.WaitAsync(CancellationToken.None);
            try
            {
                Directory.CreateDirectory(versionDir);
                var target = Path.Combine(versionDir, entryName);
                if (Directory.Exists(target))
                    Directory.Delete(target, recursive: true); // mismo fichero: se reemplaza
                else if (File.Exists(target))
                    File.Delete(target);

                if (Directory.Exists(entryPath))
                    Directory.Move(entryPath, target);
                else
                    File.Move(entryPath, target);

                LibraryService.RecordFile(versionDir, job.App.Id, job.App.Name, job.Version, new VersionFileMetadata
                {
                    Key = job.File.Key,
                    FileName = job.File.FileName,
                    Path = entryName,
                    DownloadUrl = url.AbsoluteUri,
                    Sha256 = sha256,
                    Verified = verified,
                    DownloadedAt = DateTimeOffset.Now,
                    Prerelease = job.File.IsPrerelease,
                });
            }
            finally
            {
                _finalize.Release();
            }

            job.Complete(versionDir);
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

        job.SetTotal(response.Content.Headers.ContentLength ?? (job.File.SizeBytes > 0 ? job.File.SizeBytes : null));

        // Se guarda con el nombre publicado; si no se conoce, el de la respuesta o la URL.
        var fileName = !string.IsNullOrWhiteSpace(job.File.FileName) ? PathNames.Sanitize(job.File.FileName) : GetFileName(response, url);
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

    /// <summary>"app-1.2.tar.gz" → "app-1.2"; "app.zip" → "app".</summary>
    private static string ArchiveBaseName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        return name.EndsWith(".tar", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
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
