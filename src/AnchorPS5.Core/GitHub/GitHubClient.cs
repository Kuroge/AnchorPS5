using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AnchorPS5.Core.Configuration;

namespace AnchorPS5.Core.GitHub;

/// <summary>
/// Acceso a la API de GitHub con caché en disco:
/// - Lo guardado hace menos de <see cref="MaxAge"/> se usa sin preguntar a GitHub.
/// - Después se pregunta con ETag (con sesión, un 304 no gasta cupo; sin sesión, sí).
/// - Si GitHub dice que se ha alcanzado el límite, no se le vuelve a preguntar hasta la
///   hora que indica (se recuerda entre arranques).
/// </summary>
public sealed class GitHubClient
{
    private const string ApiBase = "https://api.github.com/";

    private readonly HttpClient _http;
    private readonly string _cacheDirectory;
    private readonly Func<string?> _token;
    private readonly Func<DateTimeOffset> _now;
    private readonly string _rateLimitFile;
    private DateTimeOffset? _blockedUntil;
    private bool _simulatedBlock;

    /// <param name="token">Token de sesión (opcional): sube el límite de la API.</param>
    /// <param name="now">Reloj (para los tests).</param>
    public GitHubClient(HttpClient http, string cacheDirectory, Func<string?>? token = null, Func<DateTimeOffset>? now = null)
    {
        _http = http;
        _cacheDirectory = cacheDirectory;
        _token = token ?? (() => null);
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _rateLimitFile = Path.Combine(cacheDirectory, "ratelimit.json");
        var block = ReadRateLimit();
        _blockedUntil = block?.Until;
        _simulatedBlock = block?.Simulated == true;
    }

    /// <summary>Antigüedad máxima de la caché antes de volver a preguntar a GitHub.</summary>
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Hasta cuándo no se pregunta a GitHub por haber alcanzado el límite (null = sin bloqueo).</summary>
    public DateTimeOffset? BlockedUntil => _blockedUntil is { } until && until > _now() ? until : null;

    /// <summary>Olvida el bloqueo por límite (al iniciar o cerrar sesión cambia el cupo).</summary>
    public void ClearRateLimit()
    {
#if DEBUG
        // Bloqueo simulado por el entorno de pruebas: se mantiene aunque se inicie sesión.
        if (_simulatedBlock && BlockedUntil is not null)
            return;
#endif
        _blockedUntil = null;
        TryDelete(_rateLimitFile);
    }

    /// <summary>Releases del repo, de la más reciente a la más antigua (hasta 20).</summary>
    public async Task<GitHubResult<List<GitHubRelease>>> GetReleasesAsync(GitHubRepoRef repo, CancellationToken cancellationToken = default)
    {
        var result = await GetCachedAsync($"repos/{repo.Owner}/{repo.Name}/releases?per_page=20", cancellationToken);
        if (result.Value is null)
            return new GitHubResult<List<GitHubRelease>>(null, result.Status, result.RateLimitReset);

        try
        {
            var releases = JsonSerializer.Deserialize<List<GitHubRelease>>(result.Value) ?? [];
            return new GitHubResult<List<GitHubRelease>>(releases, result.Status, result.RateLimitReset);
        }
        catch (JsonException)
        {
            return new GitHubResult<List<GitHubRelease>>(null, GitHubStatus.Error);
        }
    }

    private async Task<GitHubResult<string>> GetCachedAsync(string path, CancellationToken cancellationToken)
    {
        var cacheFile = Path.Combine(_cacheDirectory, CacheName(path));
        var cached = ReadCache(cacheFile);

        if (cached is not null && _now() - cached.FetchedAt < MaxAge)
            return new GitHubResult<string>(cached.Body, GitHubStatus.Ok);

        if (BlockedUntil is { } blocked)
            return new GitHubResult<string>(cached?.Body, GitHubStatus.RateLimited, blocked);

        using var request = new HttpRequestMessage(HttpMethod.Get, ApiBase + path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        if (_token() is { Length: > 0 } token)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (cached?.ETag is { Length: > 0 } etag)
            request.Headers.TryAddWithoutValidation("If-None-Match", etag);

        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotModified && cached is not null)
            {
                WriteCache(cacheFile, cached with { FetchedAt = _now() });
                return new GitHubResult<string>(cached.Body, GitHubStatus.Ok);
            }

            if (IsRateLimited(response, out var reset))
            {
                // Sin hora de GitHub se espera una hora, que es su ventana de límite.
                var until = reset ?? _now().AddHours(1);
                _blockedUntil = until;
                WriteRateLimit(until);
                return new GitHubResult<string>(cached?.Body, GitHubStatus.RateLimited, until);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new GitHubResult<string>(null, GitHubStatus.NotFound);

            if (!response.IsSuccessStatusCode)
                return new GitHubResult<string>(cached?.Body, cached is null ? GitHubStatus.Error : GitHubStatus.FromCache);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            WriteCache(cacheFile, new CacheEntry(response.Headers.ETag?.ToString(), body, _now()));
            return new GitHubResult<string>(body, GitHubStatus.Ok);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return new GitHubResult<string>(cached?.Body, cached is null ? GitHubStatus.Error : GitHubStatus.FromCache);
        }
    }

    /// <summary>GitHub indica el límite con 403/429 y X-RateLimit-Remaining: 0.</summary>
    private static bool IsRateLimited(HttpResponseMessage response, out DateTimeOffset? reset)
    {
        reset = null;
        if (response.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests))
            return false;

        var remaining = response.Headers.TryGetValues("X-RateLimit-Remaining", out var values) ? values.FirstOrDefault() : null;
        if (response.Headers.TryGetValues("X-RateLimit-Reset", out var resets)
            && long.TryParse(resets.FirstOrDefault(), out var unix))
            reset = DateTimeOffset.FromUnixTimeSeconds(unix);

        return remaining == "0" || response.StatusCode == HttpStatusCode.TooManyRequests;
    }

    private static string CacheName(string path) =>
        Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(path))) + ".json";

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

    private RateLimitEntry? ReadRateLimit()
    {
        try
        {
            return File.Exists(_rateLimitFile)
                ? JsonSerializer.Deserialize<RateLimitEntry>(File.ReadAllText(_rateLimitFile), JsonDefaults.Options)
                : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void WriteRateLimit(DateTimeOffset until)
    {
        try
        {
            Directory.CreateDirectory(_cacheDirectory);
            File.WriteAllText(_rateLimitFile, JsonSerializer.Serialize(new RateLimitEntry(until), JsonDefaults.Options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Solo se pierde el recuerdo del bloqueo entre arranques.
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
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
            // Sin caché la próxima vez se vuelve a pedir; no es un error para el usuario.
        }
    }

    /// <param name="FetchedAt">Cuándo se confirmó con GitHub por última vez (las cachés antiguas, sin fecha, cuentan como caducadas).</param>
    private sealed record CacheEntry(string? ETag, string Body, DateTimeOffset FetchedAt = default);

    /// <param name="Simulated">Escrito por el entorno de pruebas (solo cuenta en compilaciones Debug).</param>
    private sealed record RateLimitEntry(DateTimeOffset Until, bool Simulated = false);
}
