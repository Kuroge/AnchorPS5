using AnchorPS5.Core.Models;

namespace AnchorPS5.Core.Library;

public enum PackageState
{
    NotDownloaded,
    Downloaded,
    UpdateAvailable,
}

/// <summary>Estado de una app del catálogo frente a lo descargado.</summary>
public sealed record PackageStatus(PackageState State, IReadOnlyList<InstalledVersion> Versions)
{
    /// <summary>La versión descargada más reciente, o null.</summary>
    public InstalledVersion? Latest => Versions.Count > 0 ? Versions[0] : null;

    /// <param name="versions">De más nueva a más antigua (como devuelve LibrarySnapshot).</param>
    public static PackageStatus Compute(HomebrewApp app, IReadOnlyList<InstalledVersion> versions)
    {
        if (versions.Count == 0)
            return new PackageStatus(PackageState.NotDownloaded, versions);

        var catalogVersion = AppVersion.Parse(app.Version);
        var state = !string.IsNullOrWhiteSpace(app.Version) && catalogVersion > versions[0].ParsedVersion
            ? PackageState.UpdateAvailable
            : PackageState.Downloaded;

        return new PackageStatus(state, versions);
    }
}
