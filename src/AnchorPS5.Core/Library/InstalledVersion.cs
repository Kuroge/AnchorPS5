namespace AnchorPS5.Core.Library;

/// <summary>Una versión descargada: carpeta &lt;downloadPath&gt;\&lt;App&gt;\&lt;versión&gt; con uno o varios ficheros.</summary>
public sealed record InstalledVersion(
    string Version,
    string FolderPath,
    string? AppId,
    DateTimeOffset? DownloadedAt,
    bool Verified = false,
    IReadOnlyList<InstalledFile>? Files = null)
{
    public AppVersion ParsedVersion { get; } = AppVersion.Parse(Version);

    public IReadOnlyList<InstalledFile> Files { get; init; } = Files ?? [];
}

/// <summary>Un fichero descargado dentro de una carpeta de versión.</summary>
/// <param name="Key">Nombre sin versión (ver AssetClassifier.GetKey): el mismo fichero entre versiones.</param>
/// <param name="RelativePath">Fichero, o subcarpeta si era un comprimido extraído.</param>
public sealed record InstalledFile(
    string Key,
    string FileName,
    string RelativePath,
    string? Sha256,
    bool Verified,
    DateTimeOffset? DownloadedAt,
    bool IsPrerelease,
    DateTimeOffset? ReleasedAt = null);

/// <summary>
/// Metadatos que se guardan dentro de cada carpeta de versión (.anchorps5.json).
/// Permiten reconocer la app aunque se renombre la carpeta y saber qué ficheros hay.
/// </summary>
public sealed class VersionMetadata
{
    public const string FileName = ".anchorps5.json";

    public int SchemaVersion { get; set; } = 2;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;

    /// <summary>Ficheros descargados de esta versión (esquema 2).</summary>
    public List<VersionFileMetadata> Files { get; set; } = [];

    // Esquema 1 (un solo fichero por versión): se siguen leyendo.
    public DateTimeOffset DownloadedAt { get; set; }
    public string? DownloadUrl { get; set; }
    public string? Sha256 { get; set; }
    public bool Verified { get; set; }
}

public sealed class VersionFileMetadata
{
    public string Key { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? DownloadUrl { get; set; }
    public string? Sha256 { get; set; }
    public bool Verified { get; set; }
    public DateTimeOffset DownloadedAt { get; set; }
    public bool Prerelease { get; set; }

    /// <summary>Fecha de publicación de la release de GitHub de la que salió.</summary>
    public DateTimeOffset? ReleasedAt { get; set; }
}
