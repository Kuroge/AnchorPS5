namespace AnchorPS5.Core.Library;

/// <summary>Una versión descargada: carpeta &lt;downloadPath&gt;\&lt;App&gt;\&lt;versión&gt;.</summary>
public sealed record InstalledVersion(string Version, string FolderPath, string? AppId, DateTimeOffset? DownloadedAt)
{
    public AppVersion ParsedVersion { get; } = AppVersion.Parse(Version);
}

/// <summary>
/// Metadatos que se guardan dentro de cada carpeta de versión (.anchorps5.json).
/// Permiten reconocer la app aunque se renombre la carpeta.
/// </summary>
public sealed class VersionMetadata
{
    public const string FileName = ".anchorps5.json";

    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTimeOffset DownloadedAt { get; set; }
    public string? DownloadUrl { get; set; }
    public string? Sha256 { get; set; }
}
