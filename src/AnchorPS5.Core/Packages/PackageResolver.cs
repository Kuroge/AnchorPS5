using AnchorPS5.Core.GitHub;
using AnchorPS5.Core.Library;
using AnchorPS5.Core.Models;

namespace AnchorPS5.Core.Packages;

/// <summary>
/// Calcula qué se puede descargar de cada app: de las releases de GitHub si la app trae
/// <c>repo</c> (sin tener que listar los ficheros), o de su <c>downloadUrl</c> si no.
/// </summary>
public sealed class PackageResolver
{
    private readonly GitHubClient _github;

    public PackageResolver(GitHubClient github)
    {
        _github = github;
    }

    /// <summary>Cuándo vuelve a estar disponible la API si se alcanzó el límite (hora de GitHub).</summary>
    public DateTimeOffset? LastRateLimitReset { get; private set; }

    public async Task<ResolvedPackage> ResolveAsync(HomebrewApp app, CancellationToken cancellationToken = default)
    {
        if (!GitHubRepoRef.TryParse(app.Repo, out var repo))
            return FromStatic(app);

        var result = await _github.GetReleasesAsync(repo, cancellationToken);
        if (result.Status == GitHubStatus.RateLimited && result.RateLimitReset is { } reset)
            LastRateLimitReset = reset;

        if (result.Value is null)
        {
            // Sin datos de GitHub: si el catálogo trae una descarga directa, se usa.
            var fallback = FromStatic(app);
            return fallback with { Source = result.Status };
        }

        return FromReleases(app, result.Value, result.Status);
    }

    /// <summary>Última estable + última beta, solo si la beta se publicó después de la estable.</summary>
    public static ResolvedPackage FromReleases(HomebrewApp app, IEnumerable<GitHubRelease> releases, GitHubStatus source)
    {
        var published = releases.Where(r => !r.Draft).OrderByDescending(r => r.PublishedAt ?? DateTimeOffset.MinValue).ToList();
        var stable = published.FirstOrDefault(r => !r.Prerelease);
        var beta = published.FirstOrDefault(r => r.Prerelease);
        if (beta is not null && stable is not null
            && !ReleaseOrder.IsNewer(beta.Version, beta.PublishedAt, stable.Version, stable.PublishedAt))
            beta = null;

        var files = new List<PackageFile>();
        if (stable is not null)
            files.AddRange(FilesOf(app, stable));
        if (beta is not null)
        {
            // Una beta con otro nombre ("app-beta.elf") se empareja con su estable ("app.elf")
            // quitando marcas de canal; si no casa con ninguna, queda como fichero solo-beta.
            var stableKeys = files.Select(f => f.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var file in FilesOf(app, beta))
            {
                var key = file.Key;
                if (!stableKeys.Contains(key) && AssetClassifier.WithoutChannelMarkers(key) is var plain && stableKeys.Contains(plain))
                    key = plain;
                files.Add(file with { Key = key });
            }
        }

        var releaseUrl = Uri.TryCreate((stable ?? beta)?.HtmlUrl, UriKind.Absolute, out var url) ? url : null;
        return new ResolvedPackage(files, stable?.Version, beta?.Version, releaseUrl, source);
    }

    /// <summary>Descarga única declarada en el catálogo (apps fuera de GitHub).</summary>
    public static ResolvedPackage FromStatic(HomebrewApp app)
    {
        if (!Uri.TryCreate(app.DownloadUrl, UriKind.Absolute, out var url))
            return new ResolvedPackage([], NullIfEmpty(app.Version), null, null, GitHubStatus.Ok);

        var fileName = Path.GetFileName(Uri.UnescapeDataString(url.AbsolutePath));
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "download.bin";

        var rule = AssetClassifier.FindRule(app.Assets, fileName);
        var file = new PackageFile(
            AssetClassifier.GetKey(fileName, app.Version),
            fileName,
            url.AbsoluteUri,
            app.SizeBytes,
            NullIfEmpty(app.Sha256),
            app.Version,
            IsPrerelease: false,
            AssetClassifier.DetectPlatform(fileName),
            rule?.Label,
            rule?.Description);

        return new ResolvedPackage([file], NullIfEmpty(app.Version), null, null, GitHubStatus.Ok);
    }

    private static IEnumerable<PackageFile> FilesOf(HomebrewApp app, GitHubRelease release)
    {
        foreach (var asset in release.Assets)
        {
            if (AssetClassifier.IsExcluded(asset.Name))
                continue;

            var rule = AssetClassifier.FindRule(app.Assets, asset.Name);
            if (rule?.Hidden == true)
                continue;

            yield return new PackageFile(
                AssetClassifier.GetKey(asset.Name, release.Version),
                asset.Name,
                asset.BrowserDownloadUrl,
                asset.Size,
                asset.Sha256,
                release.Version,
                release.Prerelease,
                AssetClassifier.DetectPlatform(asset.Name),
                rule?.Label,
                rule?.Description,
                rule is null ? int.MaxValue : app.Assets.IndexOf(rule),
                release.PublishedAt);
        }
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
