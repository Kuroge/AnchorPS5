using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AnchorPS5.Core.Configuration;
using AnchorPS5.Core.Models;

namespace AnchorPS5.Core.GitHub;

/// <summary>
/// Índice de releases que publica el repo del catálogo oficial (releases.json, junto a
/// catalog.json): las últimas releases de cada app, generadas cada hora por el propio
/// repo. Con él la app no pregunta a GitHub app por app (no gasta el límite de la API);
/// lo que no esté en el índice (apps propias) se sigue pidiendo a la API.
/// </summary>
public sealed class ReleaseIndex
{
    public const string FileName = "releases.json";

    private readonly HttpClient _http;
    private readonly string _cacheDirectory;
    private readonly Func<DateTimeOffset> _now;
    private Dictionary<string, List<GitHubRelease>> _repos = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="now">Reloj (para los tests).</param>
    public ReleaseIndex(HttpClient http, string cacheDirectory, Func<DateTimeOffset>? now = null)
    {
        _http = http;
        _cacheDirectory = cacheDirectory;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Antigüedad máxima de la copia guardada antes de volver a descargarlo.</summary>
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Un índice generado hace más de esto se ignora (el repo ha dejado de actualizarlo).</summary>
    public TimeSpan MaxStaleness { get; set; } = TimeSpan.FromHours(48);

    /// <summary>Releases del repo según el índice, si está en él.</summary>
    public bool TryGet(GitHubRepoRef repo, out List<GitHubRelease> releases) =>
        _repos.TryGetValue($"{repo.Owner}/{repo.Name}", out releases!);

    /// <summary>
    /// Primer arranque: guarda como copia (caducada, para que se descargue en cuanto haya
    /// conexión) el índice que viaja con la app. Devuelve si ha sembrado algo.
    /// </summary>
    public bool SeedFromBundle(Source source, string bundledIndexPath)
    {
        if (!Uri.TryCreate(source.Url, UriKind.Absolute, out var catalogUrl) || !File.Exists(bundledIndexPath))
            return false;

        var cacheFile = CacheFile(new Uri(catalogUrl, FileName));
        if (File.Exists(cacheFile))
            return false;

        try
        {
            var body = File.ReadAllText(bundledIndexPath);
            if (Parse(body) is null)
                return false;

            WriteCache(cacheFile, new CacheEntry(null, body));
            return File.Exists(cacheFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Carga los índices de las fuentes oficiales activas (sin red si la copia es reciente).</summary>
    /// <param name="forceRefresh">Lo descarga aunque la copia guardada sea reciente (recarga manual).</param>
    public async Task LoadAsync(IEnumerable<Source> sources, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var repos = new Dictionary<string, List<GitHubRelease>>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources.Where(s => s.Enabled && s.Type == SourceType.Official))
        {
            if (!Uri.TryCreate(source.Url, UriKind.Absolute, out var catalogUrl))
                continue;

            var (index, downloaded) = await LoadOneAsync(new Uri(catalogUrl, FileName), forceRefresh, cancellationToken);

            // Recién descargado y viejo: el repo ha dejado de actualizarlo, mejor la API.
            // Sin conexión, la última copia vale aunque sea vieja (no hay nada mejor).
            if (index is null || (downloaded && _now() - index.GeneratedAt > MaxStaleness))
                continue;

            foreach (var (repo, releases) in index.Repos)
                repos.TryAdd(repo, releases);
        }

        _repos = repos;
    }

    private string CacheFile(Uri url) => Path.Combine(_cacheDirectory, $"releases-{Hash(url.AbsoluteUri)}.json");

    /// <returns>El índice y si viene de la red (o de una copia reciente) en vez de ser un respaldo sin conexión.</returns>
    private async Task<(IndexFile? Index, bool Downloaded)> LoadOneAsync(Uri url, bool forceRefresh, CancellationToken cancellationToken)
    {
        var cacheFile = CacheFile(url);
        var cached = ReadCache(cacheFile);
        if (!forceRefresh && cached is not null && _now() - cached.FetchedAt < MaxAge)
            return (Parse(cached.Body), true);

        try
        {
            // Una ruta de fichero sirve para probar el índice antes de publicarlo.
            if (url.IsFile)
                return (File.Exists(url.LocalPath) ? Parse(await File.ReadAllTextAsync(url.LocalPath, cancellationToken)) : null, true);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (cached?.ETag is { Length: > 0 } etag)
                request.Headers.TryAddWithoutValidation("If-None-Match", etag);
            using var response = await _http.SendAsync(request, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotModified && cached is not null)
            {
                WriteCache(cacheFile, cached with { FetchedAt = _now() });
                return (Parse(cached.Body), true);
            }

            if (!response.IsSuccessStatusCode)
                return (cached is null ? null : Parse(cached.Body), false);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (Parse(body) is not { } index)
                return (cached is null ? null : Parse(cached.Body), false);

            WriteCache(cacheFile, new CacheEntry(response.Headers.ETag?.ToString(), body, _now()));
            return (index, true);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // Sin conexión: la última copia guardada.
            return (cached is null ? null : Parse(cached.Body), false);
        }
    }

    private static IndexFile? Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<IndexFile>(json) is { Repos: not null } index ? index : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12];

    private static CacheEntry? ReadCache(string file)
    {
        try
        {
            return File.Exists(file) ? JsonSerializer.Deserialize<CacheEntry>(File.ReadAllText(file), JsonDefaults.Options) : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void WriteCache(string file, CacheEntry entry)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var temp = file + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(entry, JsonDefaults.Options));
            File.Move(temp, file, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Sin copia, la próxima vez se vuelve a descargar.
        }
    }

    private sealed record CacheEntry(string? ETag, string Body, DateTimeOffset FetchedAt = default);

    private sealed class IndexFile
    {
        [JsonPropertyName("generatedAt")]
        public DateTimeOffset GeneratedAt { get; set; }

        [JsonPropertyName("repos")]
        public Dictionary<string, List<GitHubRelease>> Repos { get; set; } = [];
    }
}
