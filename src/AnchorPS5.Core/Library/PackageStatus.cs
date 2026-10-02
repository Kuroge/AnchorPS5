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

/// <summary>
/// Estado de un fichero (una ficha) con sus dos canales.
/// </summary>
/// <param name="Stable">Último fichero estable publicado (null si solo existe en beta).</param>
/// <param name="Beta">Última beta publicada, solo si es más nueva que la estable.</param>
/// <param name="Channel">Canal en el que está el usuario para este fichero.</param>
/// <param name="Installed">Lo que tienes en ese canal (en estable, la última estable descargada).</param>
/// <param name="Target">Lo que descarga "Descargar/Actualizar" en ese canal.</param>
public sealed record FileStatus(
    string Key,
    PackageFile? Stable,
    PackageFile? Beta,
    FileChannel Channel,
    InstalledFileRef? Installed,
    PackageFile? Target,
    FileState State,
    bool BetaOffered,
    bool BetaDownloaded = false)
{
    /// <summary>Lo publicado que representa a la ficha (para nombre, tamaño…).</summary>
    public PackageFile? Available => Target ?? Stable ?? Beta;

    public bool IsBetaOnly => Stable is null && Beta is not null;

    /// <summary>Se puede volver al canal estable (estás en beta y existe estable).</summary>
    public bool CanReturnToStable => Channel == FileChannel.Beta && Stable is not null;
}

/// <summary>Estado de una app del catálogo frente a lo descargado, fichero a fichero.</summary>
public sealed record PackageStatus(PackageState State, IReadOnlyList<InstalledVersion> Versions, IReadOnlyList<FileStatus> Files)
{
    /// <summary>La versión descargada más reciente, o null.</summary>
    public InstalledVersion? Latest => Versions.Count > 0 ? Versions[0] : null;

    /// <summary>Ficheros con actualización en su canal.</summary>
    public IEnumerable<FileStatus> Updates => Files.Where(f => f.State == FileState.UpdateAvailable);

    /// <param name="versions">De más nueva a más antigua (como devuelve LibrarySnapshot).</param>
    /// <param name="channels">Canal elegido por fichero (clave → canal); los que falten se deducen.</param>
    public static PackageStatus Compute(
        ResolvedPackage package,
        IReadOnlyList<InstalledVersion> versions,
        IReadOnlyDictionary<string, FileChannel>? channels = null)
    {
        // Todo lo descargado de cada fichero, de lo más nuevo a lo más antiguo.
        var installed = new Dictionary<string, List<InstalledFileRef>>(StringComparer.OrdinalIgnoreCase);
        foreach (var version in versions)
        foreach (var file in version.Files)
        {
            if (!installed.TryGetValue(file.Key, out var list))
                installed[file.Key] = list = [];
            list.Add(new InstalledFileRef(file, version));
        }
        foreach (var list in installed.Values)
            list.Sort((a, b) => Compare(b, a));

        var keys = package.Files.Select(f => f.Key)
            .Concat(installed.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = new List<FileStatus>();
        foreach (var key in keys)
        {
            var stable = package.Files.FirstOrDefault(f => !f.IsPrerelease && Same(f.Key, key));
            var beta = package.Files.FirstOrDefault(f => f.IsPrerelease && Same(f.Key, key));
            installed.TryGetValue(key, out var mine);
            var newestMine = mine?.FirstOrDefault();

            // Canal: el elegido; si no, el de lo último que tienes; los ficheros solo-beta, beta.
            var channel = channels is not null && channels.TryGetValue(key, out var chosen) ? chosen
                : stable is null && beta is not null ? FileChannel.Beta
                : newestMine?.File.IsPrerelease == true ? FileChannel.Beta
                : FileChannel.Stable;

            InstalledFileRef? current;
            PackageFile? target;
            if (channel == FileChannel.Beta)
            {
                // En beta: lo último que tengas, frente a lo más nuevo publicado (beta o estable).
                current = newestMine;
                target = Newest(beta, stable);
            }
            else
            {
                // En estable: solo cuentan las estables que tengas.
                current = mine?.FirstOrDefault(m => !m.File.IsPrerelease);
                target = stable;
            }

            FileState state;
            if (target is null)
                state = package.Files.Count == 0 ? FileState.Unknown : FileState.NoLongerPublished;
            else if (current is null)
                state = FileState.NotDownloaded;
            else
                state = ReleaseOrder.IsNewer(target.Version, target.ReleasedAt, current.Version.Version, current.File.ReleasedAt)
                    ? FileState.UpdateAvailable
                    : FileState.UpToDate;

            if (target is null && mine is null)
                continue;

            // En estable se ofrece la beta si es más nueva que la estable que usas, aunque ya
            // la tengas descargada (así se puede volver a ella sin descargar nada).
            var betaOffered = channel == FileChannel.Stable && stable is not null && beta is not null
                && (current is null || ReleaseOrder.IsNewer(beta.Version, beta.ReleasedAt, current.Version.Version, current.File.ReleasedAt));
            var betaDownloaded = beta is not null && mine is not null
                && mine.Any(m => string.Equals(m.Version.Version, beta.Version, StringComparison.OrdinalIgnoreCase));

            rows.Add(new FileStatus(key, stable, beta, channel, current ?? (target is null ? newestMine : null), target, state, betaOffered, betaDownloaded));
        }

        // Sin lista de ficheros (no se sabe qué hay publicado, o carpetas copiadas a mano
        // sin contenido reconocible) se compara la versión de la app con la descargada.
        var compareWholeApp = versions.Count > 0
            && (package.Files.Count == 0 || versions.All(v => v.Files.Count == 0))
            && package.DisplayVersion is { Length: > 0 } published
            && AppVersion.Parse(published) > versions[0].ParsedVersion;

        var appState = versions.Count == 0 ? PackageState.NotDownloaded
            : compareWholeApp || rows.Any(r => r.State == FileState.UpdateAvailable) ? PackageState.UpdateAvailable
            : PackageState.Downloaded;

        return new PackageStatus(appState, versions, rows);
    }

    private static PackageFile? Newest(PackageFile? a, PackageFile? b) =>
        a is null ? b
        : b is null ? a
        : ReleaseOrder.IsNewer(b.Version, b.ReleasedAt, a.Version, a.ReleasedAt) ? b : a;

    private static int Compare(InstalledFileRef a, InstalledFileRef b) =>
        ReleaseOrder.Compare(a.Version.Version, a.File.ReleasedAt, b.Version.Version, b.File.ReleasedAt);

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
