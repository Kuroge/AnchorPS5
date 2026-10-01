using System.Text.RegularExpressions;
using AnchorPS5.Core.Models;

namespace AnchorPS5.Core.Packages;

/// <summary>Reglas sobre los nombres de los ficheros de una release.</summary>
public static partial class AssetClassifier
{
    private static readonly string[] AuxiliaryExtensions =
        [".sha256", ".sha512", ".sha1", ".md5", ".sig", ".asc", ".minisig", ".sum", ".pdb", ".sbom", ".spdx", ".intoto"];

    private static readonly string[] ArchiveExtensions = [".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".xz", ".zst"];

    /// <summary>Checksums, firmas y código fuente: no se ofrecen para descargar.</summary>
    public static bool IsExcluded(string fileName)
    {
        var name = fileName.ToLowerInvariant();
        if (AuxiliaryExtensions.Any(name.EndsWith))
            return true;
        if (name.Contains("checksum") || name is "sha256sums" or "sha256sums.txt" or "sha512sums" or "md5sums")
            return true;

        // Código fuente subido a mano ("app-src.zip", "source-1.2.tar.gz"…).
        return ArchiveExtensions.Any(name.EndsWith) && SourcePattern().IsMatch(name);
    }

    public static ConsolePlatform DetectPlatform(string fileName)
    {
        var name = fileName.ToLowerInvariant();
        if (name.Contains("ps5"))
            return ConsolePlatform.PS5;
        return name.Contains("ps4") ? ConsolePlatform.PS4 : ConsolePlatform.Unknown;
    }

    /// <summary>
    /// Nombre sin la versión, para reconocer el mismo fichero entre releases:
    /// "app-v1.2.0.zip" y "app-v1.3.0.zip" → "app.zip".
    /// </summary>
    public static string GetKey(string fileName, string? version = null)
    {
        var name = fileName.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(version))
        {
            var v = version.Trim().ToLowerInvariant().TrimStart('v');
            if (v.Length > 0)
                name = Regex.Replace(name, @"[-_. ]?v?" + Regex.Escape(v), string.Empty);
        }

        name = VersionPattern().Replace(name, string.Empty);
        name = SeparatorRuns().Replace(name, m => m.Value[..1]);
        name = SeparatorBeforeDot().Replace(name, ".");
        return name.Trim('-', '_', '.', ' ');
    }

    /// <summary>Regla del catálogo que aplica a un fichero (la primera que encaje).</summary>
    public static AssetRule? FindRule(IEnumerable<AssetRule> rules, string fileName) =>
        rules.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.Match) && GlobToRegex(r.Match).IsMatch(fileName));

    private static Regex GlobToRegex(string glob) =>
        new("^" + Regex.Escape(glob).Replace(@"\*", ".*").Replace(@"\?", ".") + "$", RegexOptions.IgnoreCase);

    [GeneratedRegex(@"(^|[-_. ])(src|source|sources)([-_. ]|$)")]
    private static partial Regex SourcePattern();

    [GeneratedRegex(@"[-_ ]?v?\d+(?:\.\d+)+(?:[-_.]?(?:alpha|beta|rc|pre|preview)[-_.]?\d*)?")]
    private static partial Regex VersionPattern();

    [GeneratedRegex(@"[-_ ]{2,}")]
    private static partial Regex SeparatorRuns();

    [GeneratedRegex(@"[-_ ]+\.")]
    private static partial Regex SeparatorBeforeDot();
}
