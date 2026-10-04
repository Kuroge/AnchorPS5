using System.Globalization;
using AnchorPS5.Core.Localization;
using AnchorPS5.Core.Packages;

namespace AnchorPS5.App.ViewModels;

/// <summary>Versión con su fecha de publicación, para mostrar ("v0.21.1 (12/09/2026)").</summary>
public static class VersionDates
{
    public static string Date(DateTimeOffset date) => date.ToLocalTime().ToString("d", CultureInfo.CurrentCulture);

    /// <summary>"v0.21.1 (12/09/2026)", o solo "v0.21.1" si no se sabe la fecha.</summary>
    public static string WithDate(LocalizationService localization, string? version, DateTimeOffset? releasedAt) =>
        releasedAt is { } date
            ? localization.Format("version.withDate", VersionLabel.Format(version), Date(date))
            : VersionLabel.Format(version);

    /// <summary>Igual, sin la "v" (para la tarjeta de Información).</summary>
    public static string PlainWithDate(LocalizationService localization, string? version, DateTimeOffset? releasedAt) =>
        releasedAt is { } date
            ? localization.Format("version.withDate", version, Date(date))
            : version ?? string.Empty;
}
