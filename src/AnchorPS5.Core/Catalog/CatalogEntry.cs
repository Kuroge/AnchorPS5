using AnchorPS5.Core.Models;

namespace AnchorPS5.Core.Catalog;

/// <summary>App del catálogo junto con la fuente de la que viene y su icono ya resuelto.</summary>
public sealed record CatalogEntry(HomebrewApp App, Source Source, Uri? IconUri);
