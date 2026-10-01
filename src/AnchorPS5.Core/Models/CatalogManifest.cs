namespace AnchorPS5.Core.Models;

/// <summary>Contenido de un catalog.json (local o remoto, mismo esquema).</summary>
public sealed class CatalogManifest
{
    public int SchemaVersion { get; set; } = 1;

    public List<HomebrewApp> Apps { get; set; } = [];
}
