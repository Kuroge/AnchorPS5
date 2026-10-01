using System.Text.Json;
using AnchorPS5.Core.Configuration;
using AnchorPS5.Core.Models;

namespace AnchorPS5.Core.Catalog;

/// <summary>
/// Lee los manifiestos de las fuentes (local: fichero; remote: URL http/https)
/// y los combina en una sola lista.
/// </summary>
public sealed class SourceLoader
{
    /// <summary>Tope de tamaño de un manifiesto, para no cargar en memoria algo desmesurado.</summary>
    public const long MaxManifestBytes = 10 * 1024 * 1024;

    private readonly HttpClient _http;
    private readonly string _configDirectory;

    /// <param name="configDirectory">Base de las rutas relativas de las fuentes locales.</param>
    public SourceLoader(HttpClient http, string configDirectory)
    {
        _http = http;
        _configDirectory = configDirectory;
    }

    /// <summary>
    /// Carga todas las fuentes activas en paralelo. Si dos fuentes traen el mismo id,
    /// se queda la de la fuente que aparece antes en config.json.
    /// </summary>
    public async Task<CatalogLoadResult> LoadAllAsync(IEnumerable<Source> sources, CancellationToken cancellationToken = default)
    {
        var results = await Task.WhenAll(sources.Where(s => s.Enabled).Select(s => LoadAsync(s, cancellationToken)));

        var entries = new List<CatalogEntry>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in results.SelectMany(r => r.Entries))
        {
            if (seenIds.Add(entry.App.Id))
                entries.Add(entry);
        }

        return new CatalogLoadResult(entries, results.Where(r => !r.Succeeded).ToList());
    }

    public async Task<SourceLoadResult> LoadAsync(Source source, CancellationToken cancellationToken = default)
    {
        return source.Type switch
        {
            SourceType.Local => await LoadLocalAsync(source, cancellationToken),
            SourceType.Remote => await LoadRemoteAsync(source, cancellationToken),
            _ => Failed(source, SourceErrorKind.InvalidSource),
        };
    }

    private async Task<SourceLoadResult> LoadLocalAsync(Source source, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source.Path))
            return Failed(source, SourceErrorKind.InvalidSource);

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(source.Path, _configDirectory);
        }
        catch (ArgumentException)
        {
            return Failed(source, SourceErrorKind.InvalidSource);
        }

        if (!File.Exists(fullPath))
            return Failed(source, SourceErrorKind.NotFound);
        if (new FileInfo(fullPath).Length > MaxManifestBytes)
            return Failed(source, SourceErrorKind.TooLarge);

        try
        {
            await using var stream = File.OpenRead(fullPath);
            var baseUri = new Uri(fullPath);
            return await ParseAsync(source, stream, baseUri, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Failed(source, SourceErrorKind.NotFound);
        }
    }

    private async Task<SourceLoadResult> LoadRemoteAsync(Source source, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(source.Url, UriKind.Absolute, out var url) || !IsHttp(url))
            return Failed(source, SourceErrorKind.InvalidSource);

        try
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Failed(source, response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? SourceErrorKind.NotFound
                    : SourceErrorKind.Network);

            if (response.Content.Headers.ContentLength > MaxManifestBytes)
                return Failed(source, SourceErrorKind.TooLarge);

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            if (!await CopyWithLimitAsync(body, buffer, MaxManifestBytes, cancellationToken))
                return Failed(source, SourceErrorKind.TooLarge);

            buffer.Position = 0;
            // Tras redirecciones, los iconos relativos se resuelven contra la URL final.
            return await ParseAsync(source, buffer, response.RequestMessage?.RequestUri ?? url, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return Failed(source, SourceErrorKind.Network);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout del HttpClient.
            return Failed(source, SourceErrorKind.Network);
        }
    }

    private static async Task<SourceLoadResult> ParseAsync(Source source, Stream json, Uri baseUri, CancellationToken cancellationToken)
    {
        CatalogManifest? catalog;
        try
        {
            catalog = await JsonSerializer.DeserializeAsync<CatalogManifest>(json, JsonDefaults.Options, cancellationToken);
        }
        catch (JsonException)
        {
            return Failed(source, SourceErrorKind.InvalidJson);
        }

        if (catalog is null)
            return Failed(source, SourceErrorKind.InvalidJson);

        var entries = catalog.Apps
            .Where(app => app is not null && !string.IsNullOrWhiteSpace(app.Id) && !string.IsNullOrWhiteSpace(app.Name))
            .Select(app => new CatalogEntry(app, source, ResolveIcon(app.IconUrl, baseUri)))
            .ToList();

        return new SourceLoadResult(source, entries);
    }

    /// <summary>
    /// iconUrl puede ser una URL http(s), una ruta absoluta o una ruta relativa al manifiesto.
    /// </summary>
    public static Uri? ResolveIcon(string? iconUrl, Uri baseUri)
    {
        if (string.IsNullOrWhiteSpace(iconUrl))
            return null;

        if (Uri.TryCreate(iconUrl, UriKind.Absolute, out var absolute))
            return IsHttp(absolute) || absolute.IsFile ? absolute : null;

        return Uri.TryCreate(baseUri, iconUrl, out var relative) && (IsHttp(relative) || relative.IsFile)
            ? relative
            : null;
    }

    private static bool IsHttp(Uri uri) => uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp;

    private static async Task<bool> CopyWithLimitAsync(Stream source, Stream destination, long limit, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > limit)
                return false;
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return true;
    }

    private static SourceLoadResult Failed(Source source, SourceErrorKind error) => new(source, [], error);
}
