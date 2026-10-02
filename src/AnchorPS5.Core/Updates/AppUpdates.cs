using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AnchorPS5.Core.Diagnostics;
using AnchorPS5.Core.GitHub;
using AnchorPS5.Core.Library;

namespace AnchorPS5.Core.Updates;

/// <summary>Versión nueva de AnchorPS5 publicada como release.</summary>
/// <param name="Sha256">Del digest de GitHub (si lo hay).</param>
/// <param name="Sha256Url">Fichero .sha256 publicado junto al zip (respaldo).</param>
public sealed record AppUpdate(
    AppVersion Version,
    string? Notes,
    string? PageUrl,
    string ZipUrl,
    string ZipName,
    long Size,
    string? Sha256,
    string? Sha256Url);

public enum AppUpdateError
{
    Network,
    /// <summary>El zip no coincide con su SHA-256 (o no hay SHA-256 con el que comprobarlo).</summary>
    Verification,
    /// <summary>El zip no tiene la forma de una release de AnchorPS5.</summary>
    Package,
    Disk,
}

public sealed class AppUpdateException(AppUpdateError error, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public AppUpdateError Error { get; } = error;
}

/// <summary>
/// Actualizaciones de la propia app: busca la release más nueva, la descarga, la verifica y
/// la deja preparada; un pequeño script la copia encima cuando la app se cierra.
/// Una release es un zip "AnchorPS5_&lt;versión&gt;_&lt;fecha&gt;.zip" con la app en la raíz y sin config\.
/// </summary>
public static class AppUpdates
{
    public const string ExecutableName = "AnchorPS5.exe";

    /// <summary>
    /// La release más nueva que la versión actual. Mientras la app sea una versión
    /// preliminar (alpha, beta…) se ofrecen también las preliminares; si es final, solo finales.
    /// </summary>
    public static AppUpdate? FindUpdate(IEnumerable<GitHubRelease> releases, AppVersion current)
    {
        AppUpdate? best = null;
        foreach (var release in releases.Where(r => !r.Draft))
        {
            var version = AppVersion.Parse(release.Version);
            if (!version.IsNumeric || version <= current || (release.Prerelease || version.IsPrerelease) && !current.IsPrerelease)
                continue;

            var zip = release.Assets.FirstOrDefault(a =>
                a.Name.StartsWith("AnchorPS5_", StringComparison.OrdinalIgnoreCase)
                && a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
            if (zip is null || (best is not null && version <= best.Version))
                continue;

            var shaFile = release.Assets.FirstOrDefault(a => string.Equals(a.Name, zip.Name + ".sha256", StringComparison.OrdinalIgnoreCase));
            best = new AppUpdate(version, release.Body, release.HtmlUrl, zip.BrowserDownloadUrl, zip.Name, zip.Size, zip.Sha256, shaFile?.BrowserDownloadUrl);
        }

        return best;
    }

    /// <summary>Lee un "feed" local de releases (mismo formato que la API de GitHub), para pruebas.</summary>
    public static List<GitHubRelease>? ReadFeed(string pathOrFileUrl)
    {
        var path = Uri.TryCreate(pathOrFileUrl, UriKind.Absolute, out var uri) && uri.IsFile ? uri.LocalPath : pathOrFileUrl;
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<List<GitHubRelease>>(File.ReadAllText(path)) : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            AppLog.Warn($"No se ha podido leer el feed de actualizaciones {path}", ex);
            return null;
        }
    }

    /// <summary>Si se puede escribir en la carpeta de la app (si no, no puede actualizarse sola).</summary>
    public static bool CanWrite(string directory)
    {
        try
        {
            var probe = Path.Combine(directory, $".anchorps5-write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Descarga el zip, comprueba su SHA-256 y lo descomprime en <paramref name="stagingRoot"/>\app.
    /// Devuelve la carpeta con la app nueva, lista para copiarse encima.
    /// </summary>
    public static async Task<string> DownloadAndStageAsync(
        HttpClient http,
        AppUpdate update,
        string stagingRoot,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (Directory.Exists(stagingRoot))
                Directory.Delete(stagingRoot, recursive: true);
            Directory.CreateDirectory(stagingRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new AppUpdateException(AppUpdateError.Disk, ex.Message, ex);
        }

        var zipPath = Path.Combine(stagingRoot, update.ZipName);
        await DownloadAsync(http, update.ZipUrl, zipPath, update.Size, progress, cancellationToken);

        var expected = update.Sha256 ?? await ReadSha256FileAsync(http, update, cancellationToken);
        if (expected is null)
            throw new AppUpdateException(AppUpdateError.Verification, "La release no publica el SHA-256 del zip.");

        string actual;
        await using (var stream = File.OpenRead(zipPath))
            actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
        if (!string.Equals(actual, expected.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new AppUpdateException(AppUpdateError.Verification, $"SHA-256 esperado {expected}, obtenido {actual}.");

        var appDir = Path.Combine(stagingRoot, "app");
        try
        {
            ZipFile.ExtractToDirectory(zipPath, appDir);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            throw new AppUpdateException(AppUpdateError.Package, ex.Message, ex);
        }

        if (!File.Exists(Path.Combine(appDir, ExecutableName)))
            throw new AppUpdateException(AppUpdateError.Package, $"El zip no contiene {ExecutableName} en la raíz.");

        // Nunca se sobrescribe la configuración del usuario, venga lo que venga en el zip.
        var config = Path.Combine(appDir, "config");
        if (Directory.Exists(config))
            Directory.Delete(config, recursive: true);

        return appDir;
    }

    /// <summary>
    /// Script (PowerShell) que espera a que se cierre la app, copia la versión nueva encima
    /// (sin tocar config\ ni borrar nada más) y la vuelve a abrir.
    /// </summary>
    public static string CreateInstallScript(int processId, string sourceDir, string targetDir, string stagingRoot, string? logFile)
    {
        static string Quote(string value) => "'" + value.Replace("'", "''") + "'";

        return $$"""
            # Actualizador de AnchorPS5 (generado por la app; se borra al terminar).
            $source = {{Quote(sourceDir)}}
            $target = {{Quote(targetDir)}}
            $staging = {{Quote(stagingRoot)}}
            $log = {{Quote(logFile ?? string.Empty)}}
            function Log($message) {
                if ($log) { Add-Content -LiteralPath $log -Encoding UTF8 -Value ((Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff') + ' INFO  [actualizador] ' + $message) }
            }

            try { Wait-Process -Id {{processId}} -Timeout 120 -ErrorAction Stop } catch { }
            Start-Sleep -Milliseconds 700

            $ok = $false
            for ($i = 0; $i -lt 15 -and -not $ok; $i++) {
                robocopy $source $target /E /XD config /R:2 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
                if ($LASTEXITCODE -lt 8) { $ok = $true } else { Start-Sleep -Seconds 1 }
            }
            Log ($(if ($ok) { 'Actualización copiada en ' + $target } else { 'No se ha podido copiar la actualización (robocopy ' + $LASTEXITCODE + ')' }))

            Start-Process -FilePath (Join-Path $target '{{ExecutableName}}') -WorkingDirectory $target
            Start-Sleep -Seconds 2
            Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
            """;
    }

    private static async Task DownloadAsync(HttpClient http, string url, string destination, long expectedSize, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.IsFile)
            {
                File.Copy(uri.LocalPath, destination, overwrite: true);
                progress?.Report(1);
                return;
            }

            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? expectedSize;

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = File.Create(destination);
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                done += read;
                if (total > 0)
                    progress?.Report((double)done / total);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or HttpIOException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new AppUpdateException(AppUpdateError.Network, ex.Message, ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new AppUpdateException(AppUpdateError.Disk, ex.Message, ex);
        }
    }

    private static async Task<string?> ReadSha256FileAsync(HttpClient http, AppUpdate update, CancellationToken cancellationToken)
    {
        if (update.Sha256Url is null)
            return null;

        try
        {
            var text = Uri.TryCreate(update.Sha256Url, UriKind.Absolute, out var uri) && uri.IsFile
                ? await File.ReadAllTextAsync(uri.LocalPath, Encoding.UTF8, cancellationToken)
                : await http.GetStringAsync(update.Sha256Url, cancellationToken);

            // Formato de sha256sum: "<hash>  <fichero>".
            return text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new AppUpdateException(AppUpdateError.Network, ex.Message, ex);
        }
    }
}
