using AnchorPS5.Core.Models;

namespace AnchorPS5.Core.Catalog;

public enum SourceErrorKind
{
    None,
    /// <summary>Fuente mal configurada (sin ruta/URL, URL no http(s)…).</summary>
    InvalidSource,
    NotFound,
    InvalidJson,
    Network,
    TooLarge,
}

public sealed record SourceLoadResult(Source Source, IReadOnlyList<CatalogEntry> Entries, SourceErrorKind Error = SourceErrorKind.None)
{
    public bool Succeeded => Error == SourceErrorKind.None;
}

/// <summary>Resultado de cargar todas las fuentes activas.</summary>
public sealed record CatalogLoadResult(IReadOnlyList<CatalogEntry> Entries, IReadOnlyList<SourceLoadResult> FailedSources);
