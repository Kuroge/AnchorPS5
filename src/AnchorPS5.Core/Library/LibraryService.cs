using System.Text.Json;
using AnchorPS5.Core.Configuration;
using AnchorPS5.Core.Models;

namespace AnchorPS5.Core.Library;

/// <summary>
/// Lo descargado en disco: &lt;downloadPath&gt;\&lt;App&gt;\&lt;versión&gt;\… (una carpeta por versión).
/// </summary>
public sealed class LibraryService
{
    /// <summary>Carpeta de trabajo de las descargas en curso (oculta para el escaneo).</summary>
    public const string TempFolderName = ".anchorps5-tmp";

    public LibraryService(string downloadPath)
    {
        DownloadPath = Path.GetFullPath(downloadPath);
    }

    public string DownloadPath { get; }

    /// <summary>Dentro de la carpeta de descargas: así mover el resultado es instantáneo (mismo disco).</summary>
    public string TempRoot => Path.Combine(DownloadPath, TempFolderName);

    public static string GetAppFolderName(HomebrewApp app) => PathNames.Sanitize(app.Name);

    public string GetAppFolder(HomebrewApp app) => Path.Combine(DownloadPath, GetAppFolderName(app));

    public string GetVersionFolder(HomebrewApp app, string version) =>
        Path.Combine(GetAppFolder(app), PathNames.Sanitize(string.IsNullOrWhiteSpace(version) ? "unknown" : version));

    /// <summary>Lee la carpeta de descargas. Si no existe, la biblioteca está vacía.</summary>
    public LibrarySnapshot Scan()
    {
        var versions = new List<(string AppFolder, InstalledVersion Version)>();
        if (!Directory.Exists(DownloadPath))
            return new LibrarySnapshot(versions);

        try
        {
            foreach (var appDir in Directory.EnumerateDirectories(DownloadPath))
            {
                var appFolder = Path.GetFileName(appDir);
                if (appFolder.StartsWith('.'))
                    continue; // temporales y similares

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

    /// <summary>Borra la carpeta de una versión y, si queda vacía, la de la app.</summary>
    public void DeleteVersion(InstalledVersion version)
    {
        var folder = EnsureInside(version.FolderPath);
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);

        var appFolder = Path.GetDirectoryName(folder);
        if (appFolder is not null && Directory.Exists(appFolder) && !Directory.EnumerateFileSystemEntries(appFolder).Any())
            Directory.Delete(appFolder);
    }

    public void DeleteVersions(IEnumerable<InstalledVersion> versions)
    {
        foreach (var version in versions)
            DeleteVersion(version);
    }

    /// <summary>Nunca se borra nada fuera de la carpeta de descargas (ni la propia carpeta).</summary>
    private string EnsureInside(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.TrimEndingDirectorySeparator(DownloadPath) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"La ruta {full} no está dentro de la carpeta de descargas.");
        return full;
    }

    /// <summary>
    /// Añade (o sustituye) un fichero en los metadatos de su carpeta de versión.
    /// Varias descargas de la misma versión conviven en la misma carpeta.
    /// </summary>
    public static void RecordFile(string versionDir, string appId, string appName, string version, VersionFileMetadata file)
    {
        var metadataFile = Path.Combine(versionDir, VersionMetadata.FileName);
        var metadata = ReadMetadata(metadataFile) ?? new VersionMetadata();
        if (metadata.Files.Count == 0)
            metadata.Files.AddRange(LegacyFiles(versionDir, metadata).Select(ToMetadata));

        metadata.SchemaVersion = 2;
        metadata.Id = appId;
        metadata.Name = appName;
        metadata.Version = version;
        metadata.Files.RemoveAll(f => string.Equals(f.Key, file.Key, StringComparison.OrdinalIgnoreCase)
            || string.Equals(f.Path, file.Path, StringComparison.OrdinalIgnoreCase));
        metadata.Files.Add(file);

        var temp = metadataFile + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(metadata, JsonDefaults.Options));
        File.Move(temp, metadataFile, overwrite: true);
    }

    private static InstalledVersion ReadVersion(string versionDir)
    {
        var folderName = Path.GetFileName(versionDir);
        var metadata = ReadMetadata(Path.Combine(versionDir, VersionMetadata.FileName));
        if (metadata is null)
        {
            // Copiada a mano: la versión es el nombre de la carpeta y los ficheros, lo que haya.
            return new InstalledVersion(folderName, versionDir, null, null, Files: LegacyFiles(versionDir, null, folderName));
        }

        var version = string.IsNullOrWhiteSpace(metadata.Version) ? folderName : metadata.Version;
        var files = metadata.Files.Count > 0
            ? metadata.Files.Select(f => new InstalledFile(f.Key, f.FileName, f.Path, f.Sha256, f.Verified, f.DownloadedAt, f.Prerelease, f.ReleasedAt)).ToList()
            : LegacyFiles(versionDir, metadata, version);

        return new InstalledVersion(
            version,
            versionDir,
            string.IsNullOrWhiteSpace(metadata.Id) ? null : metadata.Id,
            files.Select(f => f.DownloadedAt).Max() ?? (metadata.DownloadedAt == default ? null : metadata.DownloadedAt),
            files.Count > 0 && files.All(f => f.Verified),
            files);
    }

    /// <summary>Ficheros de una carpeta sin lista en los metadatos (esquema 1 o copia a mano).</summary>
    private static List<InstalledFile> LegacyFiles(string versionDir, VersionMetadata? metadata, string? version = null)
    {
        version ??= metadata?.Version ?? Path.GetFileName(versionDir);
        var entries = Directory.EnumerateFileSystemEntries(versionDir)
            .Select(Path.GetFileName)
            .Where(name => name is not null && !string.Equals(name, VersionMetadata.FileName, StringComparison.OrdinalIgnoreCase))
            .Cast<string>()
            .ToList();

        // En el esquema 1 había un único fichero: hereda el sha256 y la fecha.
        var single = entries.Count == 1 && metadata is not null;
        return entries
            .Select(name => new InstalledFile(
                Packages.AssetClassifier.GetKey(name, version),
                name,
                name,
                single ? metadata!.Sha256 : null,
                single && metadata!.Verified,
                metadata is not null && metadata.DownloadedAt != default ? metadata.DownloadedAt : null,
                IsPrerelease: false))
            .ToList();
    }

    private static VersionFileMetadata ToMetadata(InstalledFile file) => new()
    {
        Key = file.Key,
        FileName = file.FileName,
        Path = file.RelativePath,
        Sha256 = file.Sha256,
        Verified = file.Verified,
        DownloadedAt = file.DownloadedAt ?? default,
        Prerelease = file.IsPrerelease,
        ReleasedAt = file.ReleasedAt,
    };

    private static VersionMetadata? ReadMetadata(string metadataFile)
    {
        try
        {
            return File.Exists(metadataFile)
                ? JsonSerializer.Deserialize<VersionMetadata>(File.ReadAllText(metadataFile), JsonDefaults.Options)
                : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Metadatos dañados: se trata como copia a mano.
            return null;
        }
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
        var list = _versions
            .Where(v => v.Version.AppId is { } id
                ? string.Equals(id, app.Id, StringComparison.OrdinalIgnoreCase)
                : string.Equals(v.AppFolder, folderName, StringComparison.OrdinalIgnoreCase))
            .Select(v => v.Version)
            .ToList();

        // De la más nueva a la más antigua: por fecha de release si se conoce, si no por versión.
        list.Sort((a, b) => Packages.ReleaseOrder.Compare(b.Version, ReleasedAt(b), a.Version, ReleasedAt(a)));
        return list;
    }

    private static DateTimeOffset? ReleasedAt(InstalledVersion version) =>
        version.Files.Select(f => f.ReleasedAt).Where(d => d is not null).Max();
}
