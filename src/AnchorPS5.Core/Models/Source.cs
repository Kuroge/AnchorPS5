namespace AnchorPS5.Core.Models;

/// <summary>Fuente de catálogo: fichero local o manifiesto remoto con el mismo esquema.</summary>
public sealed class Source
{
    public string Name { get; set; } = string.Empty;

    public SourceType Type { get; set; } = SourceType.Local;

    /// <summary>Solo para <see cref="SourceType.Local"/>; relativa a config/ o absoluta.</summary>
    public string? Path { get; set; }

    /// <summary>Solo para <see cref="SourceType.Remote"/>.</summary>
    public string? Url { get; set; }

    public bool Enabled { get; set; } = true;
}

public enum SourceType
{
    Local,
    Remote,
}
