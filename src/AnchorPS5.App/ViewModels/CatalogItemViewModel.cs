using AnchorPS5.Core;
using AnchorPS5.Core.Catalog;
using AnchorPS5.Core.Localization;
using AnchorPS5.Core.Models;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace AnchorPS5.App.ViewModels;

/// <summary>Tarjeta y detalle de una app del catálogo.</summary>
public sealed class CatalogItemViewModel
{
    private ImageSource? _icon;

    public CatalogItemViewModel(CatalogEntry entry, LocalizationService localization)
    {
        Entry = entry;
        VersionAuthor = localization.Format("catalog.versionAuthor", entry.App.Version, entry.App.Author);
        Description = string.IsNullOrWhiteSpace(entry.App.Description)
            ? localization.Get("detail.noDescription")
            : entry.App.Description;
    }

    public CatalogEntry Entry { get; }

    public string Id => Entry.App.Id;
    public string Name => Entry.App.Name;
    public string Version => Entry.App.Version;
    public string Author => Entry.App.Author;
    public string Description { get; }
    public string VersionAuthor { get; }
    public string SourceName => Entry.Source.Name;
    public string Sha256 => Entry.App.Sha256;
    public string VersionText => "v" + Entry.App.Version;
    public bool HasVersion => !string.IsNullOrWhiteSpace(Entry.App.Version);
    public bool HasSize => Entry.App.SizeBytes > 0;

    /// <summary>Glifo de la fuente: nube si es remota, carpeta si es local.</summary>
    public string SourceGlyph => Entry.Source.Type == SourceType.Remote ? "" : "";

    public string SizeText => Entry.App.SizeBytes > 0 ? ByteSize.Format(Entry.App.SizeBytes) : "—";

    /// <summary>Se crea al pedirlo (en el hilo de UI); null si la app no trae icono.</summary>
    public ImageSource? Icon => Entry.IconUri is null ? null : _icon ??= new BitmapImage(Entry.IconUri);

    /// <summary>Lo que anuncian los lectores de pantalla para cada tarjeta.</summary>
    public override string ToString() => Name;
}
