namespace AnchorPS5.Core.Models;

/// <summary>Entrada de la lista "apps" de catalog.json.</summary>
public sealed class HomebrewApp
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Versión publicada. Con <see cref="Repo"/> se toma de la última release de GitHub
    /// y este campo es solo un valor de respaldo.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;
    /// <summary>Texto simple o traducciones por idioma ({ "es": "…", "en": "…" }).</summary>
    public LocalizedText Description { get; set; } = LocalizedText.Empty;

    /// <summary>URL https o ruta local.</summary>
    public string? IconUrl { get; set; }

    /// <summary>
    /// Repo de GitHub ("owner/repo" o URL). Si está, los ficheros descargables salen de
    /// sus releases sin tener que listarlos aquí.
    /// </summary>
    public string? Repo { get; set; }

    /// <summary>Etiquetas, descripciones u ocultación para ficheros de la release (opcional).</summary>
    public List<AssetRule> Assets { get; set; } = [];

    // Descarga única, para apps que no están en GitHub.
    public string DownloadUrl { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}

/// <summary>Regla para los ficheros de una release cuyo nombre encaja con <see cref="Match"/> (* y ?).</summary>
public sealed class AssetRule
{
    public string Match { get; set; } = string.Empty;
    public LocalizedText? Label { get; set; }
    public LocalizedText? Description { get; set; }
    public bool Hidden { get; set; }
}
