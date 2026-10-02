using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AnchorPS5.Core.Configuration;
using AnchorPS5.Core.Models;

namespace AnchorPS5.Core.Catalog;

public enum OfficialSyncState
{
    /// <summary>El catálogo oficial no ha cambiado desde la última vez.</summary>
    UpToDate,
    /// <summary>Había versión nueva y se ha aplicado sin preguntar (no hay apps propias).</summary>
    Updated,
    /// <summary>Hay versión nueva y apps propias: hay que preguntar qué hacer.</summary>
    NeedsDecision,
    /// <summary>No se ha podido descargar o no es válido: se sigue con la copia local.</summary>
    Failed,
}

/// <summary>De dónde viene una app del catálogo.</summary>
public enum AppOrigin
{
    /// <summary>Está en el catálogo oficial.</summary>
    Official,
    /// <summary>La ha añadido el usuario (a su copia del oficial o en un catálogo local).</summary>
    Custom,
    /// <summary>Viene de otro catálogo remoto configurado por el usuario.</summary>
    External,
}

public enum OfficialSyncChoice
{
    /// <summary>Catálogo oficial nuevo + las apps propias.</summary>
    KeepMine,
    /// <summary>Solo el catálogo oficial nuevo (se pierden las apps propias).</summary>
    ReplaceAll,
}

/// <summary>Resultado de comprobar una fuente oficial.</summary>
/// <param name="CustomApps">Apps propias del usuario (solo con <see cref="OfficialSyncState.NeedsDecision"/>).</param>
public sealed record OfficialSyncResult(
    Source Source,
    OfficialSyncState State,
    IReadOnlyList<HomebrewApp> CustomApps,
    string? PendingJson = null,
    string? PendingETag = null)
{
    /// <summary>Huella de la versión oficial pendiente (para no volver a preguntar por la misma).</summary>
    public string? PendingHash => PendingJson is null ? null : OfficialCatalogSync.Hash(PendingJson);
}

/// <summary>
/// Catálogo oficial: se descarga del repo (fichero publicado, sin gastar cupo de la API)
/// y se guarda como copia local, que es lo que la app lee siempre. Aparte se guarda la
/// última versión oficial recibida para distinguir las apps que el usuario añade a mano.
/// </summary>
public sealed class OfficialCatalogSync
{
    private readonly HttpClient _http;
    private readonly string _configDirectory;
    private readonly string _cacheDirectory;

    /// <param name="configDirectory">Base de la ruta de la copia local (config/).</param>
    /// <param name="cacheDirectory">Dónde se guarda la última versión oficial recibida.</param>
    public OfficialCatalogSync(HttpClient http, string configDirectory, string cacheDirectory)
    {
        _http = http;
        _configDirectory = configDirectory;
        _cacheDirectory = cacheDirectory;
    }

    /// <summary>
    /// Primer arranque: si aún no hay copia local, la crea con el catálogo que viaja con la
    /// app y lo marca como última versión oficial recibida (así ninguna app se confunde con
    /// una propia y funciona sin conexión). Devuelve si ha sembrado algo.
    /// </summary>
    public bool SeedFromBundle(Source source, string bundledCatalogPath)
    {
        var localPath = LocalPath(source);
        if (File.Exists(localPath) || ReadSnapshot(source) is not null || !File.Exists(bundledCatalogPath))
            return false;

        try
        {
            var json = File.ReadAllText(bundledCatalogPath);
            if (Parse(json) is null)
                return false;

            WriteFile(localPath, json);
            WriteSnapshot(source, new Snapshot(null, json));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Comprueba si hay versión oficial nueva y, si no hay apps propias, la aplica.</summary>
    public async Task<OfficialSyncResult> CheckAsync(Source source, CancellationToken cancellationToken = default)
    {
        var snapshot = ReadSnapshot(source);
        var download = await DownloadAsync(source, snapshot?.ETag, cancellationToken);
        if (download is null)
            return new(source, OfficialSyncState.Failed, []);

        var localPath = LocalPath(source);
        var (remoteJson, etag) = download.Value;

        // 304 o el mismo contenido: no hay nada nuevo (salvo que falte la copia local).
        if (remoteJson is null || (snapshot is not null && snapshot.Body == remoteJson))
        {
            if (!File.Exists(localPath) && snapshot is not null)
                WriteFile(localPath, snapshot.Body);
            return new(source, OfficialSyncState.UpToDate, []);
        }

        if (Parse(remoteJson) is not { } remote)
            return new(source, OfficialSyncState.Failed, []);

        var custom = FindCustomApps(ReadLocal(localPath), snapshot is null ? null : Parse(snapshot.Body), remote);
        if (custom.Count == 0)
        {
            try
            {
                Apply(source, remoteJson, etag, [], OfficialSyncChoice.ReplaceAll);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new(source, OfficialSyncState.Failed, []);
            }

            return new(source, OfficialSyncState.Updated, []);
        }

        return new(source, OfficialSyncState.NeedsDecision, custom, remoteJson, etag);
    }

    /// <summary>Aplica la versión oficial pendiente según lo que haya elegido el usuario.</summary>
    public void Apply(OfficialSyncResult pending, OfficialSyncChoice choice)
    {
        if (pending.PendingJson is null)
            throw new InvalidOperationException("No hay ninguna versión oficial pendiente.");

        Apply(pending.Source, pending.PendingJson, pending.PendingETag, pending.CustomApps, choice);
    }

    private void Apply(Source source, string remoteJson, string? etag, IReadOnlyList<HomebrewApp> custom, OfficialSyncChoice choice)
    {
        if (choice == OfficialSyncChoice.ReplaceAll || custom.Count == 0)
        {
            // Tal cual llega del repo (conserva comentarios y formato).
            WriteFile(LocalPath(source), remoteJson);
        }
        else
        {
            var merged = Parse(remoteJson)!;
            merged.Apps.AddRange(custom);
            WriteFile(LocalPath(source), JsonSerializer.Serialize(merged, JsonDefaults.Options));
        }

        WriteSnapshot(source, new Snapshot(etag, remoteJson));
    }

    /// <summary>
    /// Apps propias: las de la copia local que no estaban en la última versión oficial
    /// recibida (sin versión anterior: las que no están en la nueva). Si una pasa a estar
    /// en el oficial (mismo id o mismo repo de GitHub), gana la oficial.
    /// </summary>
    public static List<HomebrewApp> FindCustomApps(CatalogManifest? local, CatalogManifest? previousOfficial, CatalogManifest newOfficial)
    {
        if (local is null)
            return [];

        var official = (previousOfficial ?? newOfficial).Apps;
        return local.Apps
            .Where(app => app is not null && !string.IsNullOrWhiteSpace(app.Id))
            .Where(app => !official.Any(o => SameApp(o, app)))
            .Where(app => !newOfficial.Apps.Any(o => SameApp(o, app)))
            .ToList();
    }

    /// <summary>
    /// Origen de una app. En una fuente oficial, es oficial si está en la última versión
    /// recibida (o si aún no se ha recibido ninguna: la copia que trae la app es oficial).
    /// </summary>
    public static AppOrigin GetOrigin(CatalogEntry entry, CatalogManifest? lastOfficial) => entry.Source.Type switch
    {
        SourceType.Official => lastOfficial is null || lastOfficial.Apps.Any(o => o is not null && SameApp(o, entry.App))
            ? AppOrigin.Official
            : AppOrigin.Custom,
        SourceType.Local => AppOrigin.Custom,
        _ => AppOrigin.External,
    };

    /// <summary>Última versión oficial recibida de la fuente (null si aún no hay).</summary>
    public CatalogManifest? ReadLastOfficial(Source source) => ReadSnapshot(source) is { } snapshot ? Parse(snapshot.Body) : null;

    /// <summary>Misma app: mismo id o mismo repo de GitHub.</summary>
    public static bool SameApp(HomebrewApp a, HomebrewApp b) =>
        string.Equals(a.Id, b.Id, StringComparison.OrdinalIgnoreCase)
        || (GitHub.GitHubRepoRef.TryParse(a.Repo, out var ra) && GitHub.GitHubRepoRef.TryParse(b.Repo, out var rb)
            && string.Equals(ra.Owner, rb.Owner, StringComparison.OrdinalIgnoreCase)
            && string.Equals(ra.Name, rb.Name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Contenido nuevo, (null, etag) si no ha cambiado (304), o null si falla.</summary>
    private async Task<(string? Json, string? ETag)?> DownloadAsync(Source source, string? etag, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(source.Url, UriKind.Absolute, out var url))
            return null;

        try
        {
            // Una ruta de fichero sirve para probar un catálogo oficial antes de publicarlo.
            if (url.IsFile)
                return File.Exists(url.LocalPath) ? (await File.ReadAllTextAsync(url.LocalPath, cancellationToken), null) : null;

            if (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp)
                return null;

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (etag is { Length: > 0 })
                request.Headers.TryAddWithoutValidation("If-None-Match", etag);
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
                return (null, etag);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > SourceLoader.MaxManifestBytes)
                return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return json.Length > SourceLoader.MaxManifestBytes ? null : (json, response.Headers.ETag?.ToString());
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return null;
        }
    }

    private string LocalPath(Source source) =>
        Path.GetFullPath(string.IsNullOrWhiteSpace(source.Path) ? "catalog.json" : source.Path, _configDirectory);

    private string SnapshotPath(Source source) =>
        Path.Combine(_cacheDirectory, $"official-{Hash(source.Url ?? string.Empty)[..12]}.json");

    internal static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static CatalogManifest? Parse(string json)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize<CatalogManifest>(json, JsonDefaults.Options);
            return manifest is { Apps: not null } ? manifest : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static CatalogManifest? ReadLocal(string path)
    {
        try
        {
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private Snapshot? ReadSnapshot(Source source)
    {
        try
        {
            var path = SnapshotPath(source);
            return File.Exists(path) ? JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(path), JsonDefaults.Options) : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void WriteSnapshot(Source source, Snapshot snapshot) =>
        WriteFile(SnapshotPath(source), JsonSerializer.Serialize(snapshot, JsonDefaults.Options));

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, path, overwrite: true);
    }

    private sealed record Snapshot(string? ETag, string Body);
}
