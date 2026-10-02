namespace AnchorPS5.Core.Models;

/// <summary>Fuente de catálogo: fichero local o manifiesto remoto con el mismo esquema.</summary>
public sealed class Source
{
    public string Name { get; set; } = string.Empty;

    public SourceType Type { get; set; } = SourceType.Local;

    /// <summary>Para <see cref="SourceType.Local"/> y <see cref="SourceType.Official"/> (copia local); relativa a config/ o absoluta.</summary>
    public string? Path { get; set; }

    /// <summary>Para <see cref="SourceType.Remote"/> y <see cref="SourceType.Official"/> (original).</summary>
    public string? Url { get; set; }

    public bool Enabled { get; set; } = true;
}

public enum SourceType
{
    Local,
    Remote,
    /// <summary>
    /// Catálogo oficial: <see cref="Source.Url"/> es el original (repo) y
    /// <see cref="Source.Path"/> la copia local que se lee (por defecto catalog.json).
    /// </summary>
    Official,
}
