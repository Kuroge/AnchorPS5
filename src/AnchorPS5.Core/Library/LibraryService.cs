using System.Text.Json;
using AnchorPS5.Core.Configuration;
using AnchorPS5.Core.Models;
using AnchorPS5.Core.Packages;

namespace AnchorPS5.Core.Library;

/// <summary>
/// Lo descargado en disco: &lt;downloadPath&gt;\&lt;App&gt;\&lt;fichero&gt;\&lt;versión&gt;\… — una carpeta
/// por app, dentro una por fichero publicado y dentro una por cada versión descargada.
/// </summary>
public sealed class LibraryService
{
    /// <summary>Carpeta de trabajo de las descargas en curso (oculta para el escaneo).</summary>
    public const string TempFolderName = ".anchorps5-tmp";

    private static readonly string[] DoubleExtensions = [".tar.gz", ".tar.bz2", ".tar.xz", ".tar.zst"];

    public LibraryService(string downloadPath)
    {
        DownloadPath = Path.GetFullPath(downloadPath);
    }

    public string DownloadPath { get; }

    /// <summary>Dentro de la carpeta de descargas: así mover el resultado es instantáneo (mismo disco).</summary>
    public string TempRoot => Path.Combine(DownloadPath, TempFolderName);

    public static string GetAppFolderName(HomebrewApp app) => PathNames.Sanitize(app.Name);

    /// <summary>Carpeta de un fichero: su clave sin extensión ("ftpsrv-ps5.elf" → "ftpsrv-ps5").</summary>
    public static string GetFileFolderName(string fileKey)
    {
        var name = fileKey;
        var doubleExtension = DoubleExtensions.FirstOrDefault(e => name.EndsWith(e, StringComparison.OrdinalIgnoreCase));
        name = doubleExtension is not null ? name[..^doubleExtension.Length] : Path.GetFileNameWithoutExtension(name);
        return PathNames.Sanitize(string.IsNullOrWhiteSpace(name) ? fileKey : name);
    }

    public string GetAppFolder(HomebrewApp app) => Path.Combine(DownloadPath, GetAppFolderName(app));

    public string GetFileFolder(HomebrewApp app, string fileKey) => Path.Combine(GetAppFolder(app), GetFileFolderName(fileKey));

    public string GetVersionFolder(HomebrewApp app, string fileKey, string version) =>
        Path.Combine(GetFileFolder(app, fileKey), PathNames.Sanitize(string.IsNullOrWhiteSpace(version) ? "unknown" : version));

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

                foreach (var fileDir in Directory.EnumerateDirectories(appDir))
                foreach (var versionDir in Directory.EnumerateDirectories(fileDir))
                    versions.Add((appFolder, ReadVersion(versionDir)));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Carpeta inaccesible: se muestra lo que se haya podido leer.
        }

        return new LibrarySnapshot(versions);
    }

    /// <summary>Borra una versión y, si quedan vacías, las carpetas del fichero y de la app.</summary>
    public void DeleteVersion(InstalledVersion version)
    {
        var folder = EnsureInside(version.FolderPath);
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);

        // Sube borrando carpetas vacías sin salir nunca de la de descargas.
        var parent = Path.GetDirectoryName(folder);
        while (parent is not null && IsInside(parent) && Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any())
        {
            Directory.Delete(parent);
            parent = Path.GetDirectoryName(parent);
        }
    }

    public void DeleteVersions(IEnumerable<InstalledVersion> versions)
    {
        foreach (var version in versions.ToList())
            DeleteVersion(version);
    }

    /// <summary>
    /// Añade (o sustituye) un fichero en los metadatos de su carpeta de versión.
    /// </summary>
    public static void RecordFile(string versionDir, string appId, string appName, string version, VersionFileMetadata file)
    {
        var metadataFile = Path.Combine(versionDir, VersionMetadata.FileName);
        var metadata = ReadMetadata(metadataFile) ?? new VersionMetadata();

        metadata.SchemaVersion = 3;
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

    /// <summary>Nunca se borra nada fuera de la carpeta de descargas (ni la propia carpeta).</summary>
    private string EnsureInside(string path)
    {
        var full = Path.GetFullPath(path);
        if (!IsInside(full))
            throw new InvalidOperationException($"La ruta {full} no está dentro de la carpeta de descargas.");
        return full;
    }

    private bool IsInside(string path) =>
        Path.GetFullPath(path).StartsWith(Path.TrimEndingDirectorySeparator(DownloadPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static InstalledVersion ReadVersion(string versionDir)
    {
        var folderName = Path.GetFileName(versionDir);
        var metadata = ReadMetadata(Path.Combine(versionDir, VersionMetadata.FileName));
        if (metadata is null)
        {
            // Copiada a mano: la versión es el nombre de la carpeta y los ficheros, lo que haya.
            return new InstalledVersion(folderName, versionDir, null, null, Files: FilesInFolder(versionDir, folderName));
        }

        var version = string.IsNullOrWhiteSpace(metadata.Version) ? folderName : metadata.Version;
        var files = metadata.Files.Count > 0
            ? metadata.Files.Select(f => new InstalledFile(f.Key, f.FileName, f.Path, f.Sha256, f.Verified, f.DownloadedAt, f.Prerelease, f.ReleasedAt)).ToList()
            : FilesInFolder(versionDir, version);

        return new InstalledVersion(
            version,
            versionDir,
            string.IsNullOrWhiteSpace(metadata.Id) ? null : metadata.Id,
            files.Select(f => f.DownloadedAt).Max() ?? (metadata.DownloadedAt == default ? null : metadata.DownloadedAt),
            files.Count > 0 && files.All(f => f.Verified),
            files);
    }

    /// <summary>Ficheros de una carpeta de versión sin metadatos (copiada a mano).</summary>
    private static List<InstalledFile> FilesInFolder(string versionDir, string version) =>
        Directory.EnumerateFileSystemEntries(versionDir)
            .Select(Path.GetFileName)
            .Where(name => name is not null && !string.Equals(name, VersionMetadata.FileName, StringComparison.OrdinalIgnoreCase))
            .Cast<string>()
            .Select(name => new InstalledFile(AssetClassifier.GetKey(name, version), name, name, null, false, null, IsPrerelease: false))
            .ToList();

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
    /// Versiones descargadas de una app (de todos sus ficheros), de la más nueva a la más
    /// antigua. Se reconocen por el id de sus metadatos o, si no tienen, por la carpeta.
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

        list.Sort((a, b) => ReleaseOrder.Compare(b.Version, ReleasedAt(b), a.Version, ReleasedAt(a)));
        return list;
    }

    /// <summary>Versiones que sobran si solo se conserva la más nueva de cada fichero.</summary>
    public static IReadOnlyList<InstalledVersion> AllButLatestPerFile(IEnumerable<InstalledVersion> versions) =>
        versions
            .GroupBy(v => v.Files.FirstOrDefault()?.Key ?? v.FolderPath, StringComparer.OrdinalIgnoreCase)
            .SelectMany(g =>
            {
                var sorted = g.ToList();
                sorted.Sort((a, b) => ReleaseOrder.Compare(b.Version, ReleasedAt(b), a.Version, ReleasedAt(a)));
                return sorted.Skip(1);
            })
            .ToList();

    private static DateTimeOffset? ReleasedAt(InstalledVersion version) =>
        version.Files.Select(f => f.ReleasedAt).Where(d => d is not null).Max();
}
