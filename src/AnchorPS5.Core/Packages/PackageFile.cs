namespace AnchorPS5.Core.Packages;

public enum ConsolePlatform
{
    Unknown,
    PS4,
    PS5,
}

/// <summary>Un fichero descargable de una app en una versión concreta.</summary>
/// <param name="Key">Identifica el fichero entre versiones: su nombre sin la versión ("app-v1.2.zip" → "app.zip").</param>
/// <param name="Order">Posición de su regla en "assets" del catálogo, para mostrarlos en ese orden.</param>
public sealed record PackageFile(
    string Key,
    string FileName,
    string Url,
    long SizeBytes,
    string? Sha256,
    string Version,
    bool IsPrerelease,
    ConsolePlatform Platform,
    string? Label = null,
    string? Description = null,
    int Order = int.MaxValue);

/// <summary>Lo que se puede descargar de una app: ficheros de la última estable y, si es más nueva, de la última beta.</summary>
public sealed record ResolvedPackage(
    IReadOnlyList<PackageFile> Files,
    string? StableVersion,
    string? BetaVersion,
    Uri? ReleaseUrl,
    GitHub.GitHubStatus Source)
{
    public static ResolvedPackage Empty(GitHub.GitHubStatus source) => new([], null, null, null, source);

    /// <summary>Versión a mostrar: la estable o, si solo hay beta, la beta.</summary>
    public string? DisplayVersion => StableVersion ?? BetaVersion;
}
