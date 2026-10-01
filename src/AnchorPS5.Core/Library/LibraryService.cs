using System.Text.Json;
using AnchorPS5.Core.Configuration;
using AnchorPS5.Core.Models;

namespace AnchorPS5.Core.Library;

/// <summary>
/// Lo descargado en disco: &lt;downloadPath&gt;\&lt;App&gt;\&lt;versión&gt;\… (una carpeta por versión).
/// </summary>
public sealed class LibraryService
{
    private readonly string _downloadPath;

    public LibraryService(string downloadPath)
    {
        _downloadPath = downloadPath;
    }

    public static string GetAppFolderName(HomebrewApp app) => PathNames.Sanitize(app.Name);

    public string GetAppFolder(HomebrewApp app) => Path.Combine(_downloadPath, GetAppFolderName(app));

    public string GetVersionFolder(HomebrewApp app, string version) =>
        Path.Combine(GetAppFolder(app), PathNames.Sanitize(version));

    /// <summary>Lee la carpeta de descargas. Si no existe, la biblioteca está vacía.</summary>
    public LibrarySnapshot Scan()
    {
        var versions = new List<(string AppFolder, InstalledVersion Version)>();
        if (!Directory.Exists(_downloadPath))
            return new LibrarySnapshot(versions);

        try
        {
            foreach (var appDir in Directory.EnumerateDirectories(_downloadPath))
            {
                var appFolder = Path.GetFileName(appDir);
                foreach (var versionDir in Directory.EnumerateDirectories(appDir))
                    versions.Add((appFolder, ReadVersion(versionDir)));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Carpeta inaccesible: se muestra lo que se haya podido leer.
        }

        return new LibrarySnapshot(versions);
    }

    private static InstalledVersion ReadVersion(string versionDir)
    {
        var folderName = Path.GetFileName(versionDir);
        var metadataFile = Path.Combine(versionDir, VersionMetadata.FileName);
        if (File.Exists(metadataFile))
        {
            try
            {
                var metadata = JsonSerializer.Deserialize<VersionMetadata>(File.ReadAllText(metadataFile), JsonDefaults.Options);
                if (metadata is not null)
                {
                    return new InstalledVersion(
                        string.IsNullOrWhiteSpace(metadata.Version) ? folderName : metadata.Version,
                        versionDir,
                        string.IsNullOrWhiteSpace(metadata.Id) ? null : metadata.Id,
                        metadata.DownloadedAt);
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                // Metadatos dañados: se usa el nombre de la carpeta.
            }
        }

        // Copiada a mano: la versión es el nombre de la carpeta.
        return new InstalledVersion(folderName, versionDir, null, null);
    }
}

/// <summary>Resultado de escanear la carpeta de descargas.</summary>
public sealed class LibrarySnapshot
{
    private readonly IReadOnlyList<(string AppFolder, InstalledVersion Version)> _versions;

    public LibrarySnapshot(IReadOnlyList<(string AppFolder, InstalledVersion Version)> versions)
    {
        _versions = versions;
    }

    public static LibrarySnapshot Empty { get; } = new([]);

    /// <summary>
    /// Versiones de una app, de la más nueva a la más antigua. Se reconocen por el id
    /// de sus metadatos o, si no tienen (copiadas a mano), por el nombre de la carpeta.
    /// </summary>
    public IReadOnlyList<InstalledVersion> GetVersions(HomebrewApp app)
    {
        var folderName = LibraryService.GetAppFolderName(app);
        return _versions
            .Where(v => v.Version.AppId is { } id
                ? string.Equals(id, app.Id, StringComparison.OrdinalIgnoreCase)
                : string.Equals(v.AppFolder, folderName, StringComparison.OrdinalIgnoreCase))
            .Select(v => v.Version)
            .OrderByDescending(v => v.ParsedVersion)
            .ToList();
    }
}
