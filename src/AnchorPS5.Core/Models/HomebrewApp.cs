namespace AnchorPS5.Core.Models;

/// <summary>Entrada de la lista "apps" de catalog.json.</summary>
public sealed class HomebrewApp
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>URL https o ruta local.</summary>
    public string? IconUrl { get; set; }

    public string DownloadUrl { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}
