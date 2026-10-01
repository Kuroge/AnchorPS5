using AnchorPS5.Core.Library;

namespace AnchorPS5.Core.Packages;

/// <summary>
/// Qué es más nuevo. Si las dos partes vienen de releases de GitHub con fecha, manda la
/// fecha de publicación: hay autores que numeran como decimales (1.6 → 1.61b → 1.62b → 1.7)
/// y comparar números diría que 1.62 es mayor que 1.7. Sin fechas, se compara la versión.
/// </summary>
public static class ReleaseOrder
{
    public static int Compare(string versionA, DateTimeOffset? releasedA, string versionB, DateTimeOffset? releasedB)
    {
        if (string.Equals(Normalize(versionA), Normalize(versionB), StringComparison.OrdinalIgnoreCase))
            return 0;

        if (releasedA is { } a && releasedB is { } b && a != b)
            return a.CompareTo(b);

        return AppVersion.Parse(versionA).CompareTo(AppVersion.Parse(versionB));
    }

    public static bool IsNewer(string version, DateTimeOffset? released, string thanVersion, DateTimeOffset? thanReleased) =>
        Compare(version, released, thanVersion, thanReleased) > 0;

    private static string Normalize(string version) => version.Trim().TrimStart('v', 'V');
}
