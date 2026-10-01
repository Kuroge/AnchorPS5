using AnchorPS5.Core.Packages;

namespace AnchorPS5.Core.Library;

public enum PackageState
{
    NotDownloaded,
    Downloaded,
    UpdateAvailable,
}

public enum FileState
{
    NotDownloaded,
    UpToDate,
    UpdateAvailable,
    /// <summary>Lo tienes descargado pero la última release ya no lo incluye.</summary>
    NoLongerPublished,
    /// <summary>No se sabe qué hay publicado (GitHub no disponible y sin caché).</summary>
    Unknown,
}

/// <summary>Un fichero descargado junto con la carpeta de versión en la que está.</summary>
public sealed record InstalledFileRef(InstalledFile File, InstalledVersion Version);

/// <summary>Estado de un fichero: lo publicado frente a lo que tienes.</summary>
/// <param name="CountsForUpdate">
/// Si su actualización cuenta para la app. Una beta solo cuenta si ya usas la beta de ese
/// fichero o si no existe versión estable de él.
/// </param>
public sealed record FileStatus(string Key, PackageFile? Available, InstalledFileRef? Installed, FileState State, bool CountsForUpdate);

/// <summary>Estado de una app del catálogo frente a lo descargado, fichero a fichero.</summary>
public sealed record PackageStatus(PackageState State, IReadOnlyList<InstalledVersion> Versions, IReadOnlyList<FileStatus> Files)
{
    /// <summary>La versión descargada más reciente, o null.</summary>
    public InstalledVersion? Latest => Versions.Count > 0 ? Versions[0] : null;

    /// <summary>Ficheros con actualización que cuentan para la app.</summary>
    public IEnumerable<FileStatus> Updates => Files.Where(f => f.State == FileState.UpdateAvailable && f.CountsForUpdate);

    /// <param name="versions">De más nueva a más antigua (como devuelve LibrarySnapshot).</param>
    public static PackageStatus Compute(ResolvedPackage package, IReadOnlyList<InstalledVersion> versions)
    {
        // Lo más nuevo que tienes de cada fichero (por fecha de release si se conoce).
        var installed = new Dictionary<string, InstalledFileRef>(StringComparer.OrdinalIgnoreCase);
        foreach (var version in versions)
        foreach (var file in version.Files)
        {
            var candidate = new InstalledFileRef(file, version);
            if (!installed.TryGetValue(file.Key, out var current) || IsNewer(candidate, current))
                installed[file.Key] = candidate;
        }

        var stableKeys = package.Files.Where(f => !f.IsPrerelease).Select(f => f.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = new List<FileStatus>();

        foreach (var file in package.Files)
        {
            installed.TryGetValue(file.Key, out var mine);
            var fileState = mine is null ? FileState.NotDownloaded
                : ReleaseOrder.IsNewer(file.Version, file.ReleasedAt, mine.Version.Version, mine.File.ReleasedAt) ? FileState.UpdateAvailable
                : FileState.UpToDate;
            var counts = !file.IsPrerelease || mine?.File.IsPrerelease == true || !stableKeys.Contains(file.Key);
            rows.Add(new FileStatus(file.Key, file, mine, fileState, counts));
        }

        // Lo que tienes y ya no se publica (solo si se sabe qué hay publicado).
        var publishedKeys = package.Files.Select(f => f.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, mine) in installed)
        {
            if (publishedKeys.Contains(key))
                continue;
            rows.Add(new FileStatus(key, null, mine, package.Files.Count == 0 ? FileState.Unknown : FileState.NoLongerPublished, false));
        }

        // Sin lista de ficheros (no se sabe qué hay publicado, o carpetas copiadas a mano
        // sin contenido reconocible) se compara la versión de la app con la descargada.
        var compareWholeApp = versions.Count > 0
            && (package.Files.Count == 0 || versions.All(v => v.Files.Count == 0))
            && package.DisplayVersion is { Length: > 0 } published
            && AppVersion.Parse(published) > versions[0].ParsedVersion;

        var state = versions.Count == 0 ? PackageState.NotDownloaded
            : compareWholeApp || rows.Any(r => r.State == FileState.UpdateAvailable && r.CountsForUpdate) ? PackageState.UpdateAvailable
            : PackageState.Downloaded;

        return new PackageStatus(state, versions, rows);
    }

    private static bool IsNewer(InstalledFileRef a, InstalledFileRef b) =>
        ReleaseOrder.IsNewer(a.Version.Version, a.File.ReleasedAt, b.Version.Version, b.File.ReleasedAt);
}
