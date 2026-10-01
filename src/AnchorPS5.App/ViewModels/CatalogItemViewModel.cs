using System.Globalization;
using AnchorPS5.Core;
using AnchorPS5.Core.Catalog;
using AnchorPS5.Core.Library;
using AnchorPS5.Core.Localization;
using AnchorPS5.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace AnchorPS5.App.ViewModels;

/// <summary>Tarjeta y detalle de una app del catálogo, con su estado en la biblioteca.</summary>
public sealed partial class CatalogItemViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private ImageSource? _icon;

    public CatalogItemViewModel(CatalogEntry entry, LocalizationService localization, PackageStatus status, bool isNew, string appFolder)
    {
        _localization = localization;
        Entry = entry;
        IsNew = isNew;
        AppFolder = appFolder;
        Description = string.IsNullOrWhiteSpace(entry.App.Description)
            ? localization.Get("detail.noDescription")
            : entry.App.Description;
        SetStatus(status);
    }

    public CatalogEntry Entry { get; }

    public string Id => Entry.App.Id;
    public string Name => Entry.App.Name;
    public string Version => Entry.App.Version;
    public string Author => Entry.App.Author;
    public string Description { get; }
    public string SourceName => Entry.Source.Name;
    public string Sha256 => Entry.App.Sha256;
    public string VersionText => "v" + Entry.App.Version;
    public bool HasVersion => !string.IsNullOrWhiteSpace(Entry.App.Version);
    public bool HasSize => Entry.App.SizeBytes > 0;
    public string SizeText => Entry.App.SizeBytes > 0 ? ByteSize.Format(Entry.App.SizeBytes) : "—";

    /// <summary>Glifo de la fuente: nube si es remota, carpeta si es local.</summary>
    public string SourceGlyph => Entry.Source.Type == SourceType.Remote ? "" : "";

    /// <summary>Carpeta de la app dentro de la de descargas.</summary>
    public string AppFolder { get; }

    /// <summary>Nueva en esta apertura (no estaba la vez anterior).</summary>
    public bool IsNew { get; }

    // ---- Estado en la biblioteca ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDownloaded), nameof(HasUpdate), nameof(IsNotDownloaded))]
    public partial PackageState State { get; private set; }

    [ObservableProperty]
    public partial string StatusText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusTitle { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusDetail { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<InstalledVersionViewModel> InstalledVersions { get; private set; } = [];

    public bool IsNotDownloaded => State == PackageState.NotDownloaded;
    public bool IsDownloaded => State == PackageState.Downloaded;
    public bool HasUpdate => State == PackageState.UpdateAvailable;
    public bool HasInstalledVersions => InstalledVersions.Count > 0;

    partial void OnInstalledVersionsChanged(IReadOnlyList<InstalledVersionViewModel> value) => OnPropertyChanged(nameof(HasInstalledVersions));

    public void SetStatus(PackageStatus status)
    {
        State = status.State;
        InstalledVersions = status.Versions.Select(v => new InstalledVersionViewModel(v, _localization)).ToList();

        var latest = status.Latest?.Version ?? string.Empty;
        (StatusText, StatusTitle) = status.State switch
        {
            PackageState.UpdateAvailable => (
                _localization.Format("status.update", latest, Version),
                _localization.Format("status.updateTitle", latest, Version)),
            PackageState.Downloaded => (
                _localization.Format("status.downloaded", latest),
                _localization.Format("status.downloaded", latest)),
            _ => (_localization.Get("status.notDownloaded"), _localization.Get("status.notDownloaded")),
        };

        StatusDetail = status.Versions.Count switch
        {
            0 => _localization.Format("detail.willSaveTo", AppFolder),
            1 => _localization.Format("detail.versionCountOne", AppFolder),
            var n => _localization.Format("detail.versionCountMany", n, AppFolder),
        };
    }

    /// <summary>Se crea al pedirlo (en el hilo de UI); null si la app no trae icono.</summary>
    public ImageSource? Icon => Entry.IconUri is null ? null : _icon ??= new BitmapImage(Entry.IconUri);

    /// <summary>Lo que anuncian los lectores de pantalla para cada tarjeta.</summary>
    public override string ToString() => Name;
}

/// <summary>Una versión descargada, para la lista del detalle.</summary>
public sealed class InstalledVersionViewModel
{
    public InstalledVersionViewModel(InstalledVersion version, LocalizationService localization)
    {
        Version = "v" + version.Version;
        FolderPath = version.FolderPath;
        Detail = version.DownloadedAt is { } date
            ? localization.Format("detail.downloadedAt", date.ToLocalTime().ToString("d", CultureInfo.CurrentCulture))
            : localization.Get("detail.manualCopy");
    }

    public string Version { get; }
    public string FolderPath { get; }
    public string Detail { get; }
}
