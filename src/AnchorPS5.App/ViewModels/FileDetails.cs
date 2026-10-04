using System.Globalization;
using AnchorPS5.Core;
using AnchorPS5.Core.Library;
using AnchorPS5.Core.Localization;
using AnchorPS5.Core.Packages;

namespace AnchorPS5.App.ViewModels;

/// <summary>Datos de un fichero descargado (botón ⓘ de la ficha del fichero y de cada versión del historial).</summary>
public sealed class FileDetails
{
    private readonly string _fullPath;
    private string? _size;

    public FileDetails(InstalledFile file, InstalledVersion version, LocalizationService localization)
    {
        FileName = file.FileName;
        Version = VersionLabel.Format(version.Version);
        Channel = localization.Get(file.IsPrerelease ? "info.channelBeta" : "info.channelStable");
        ReleasedAt = file.ReleasedAt is { } released ? VersionDates.Date(released) : "—";
        DownloadedAt = (file.DownloadedAt ?? version.DownloadedAt) is { } downloaded
            ? downloaded.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
            : localization.Get("detail.manualCopy");
        Verified = localization.Get(file.Verified ? "info.verifiedYes" : "info.verifiedNo");
        Sha256 = string.IsNullOrEmpty(file.Sha256) ? "—" : file.Sha256;
        Origin = string.IsNullOrEmpty(file.DownloadUrl) ? "—" : file.DownloadUrl;
        _fullPath = Path.Combine(version.FolderPath, file.RelativePath);
    }

    public string FileName { get; }
    public string Version { get; }
    public string Channel { get; }
    public string ReleasedAt { get; }
    public string DownloadedAt { get; }
    public string Verified { get; }
    public string Sha256 { get; }
    public string Origin { get; }
    public string Location => _fullPath;

    /// <summary>Tamaño en disco: el fichero o, si era un comprimido extraído, toda su carpeta (se calcula al mostrarlo).</summary>
    public string SizeOnDisk => _size ??= MeasureSize();

    private string MeasureSize()
    {
        try
        {
            if (File.Exists(_fullPath))
                return ByteSize.Format(new FileInfo(_fullPath).Length);
            if (Directory.Exists(_fullPath))
                return ByteSize.Format(new DirectoryInfo(_fullPath).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return "—";
    }
}
