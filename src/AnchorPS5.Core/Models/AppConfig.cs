namespace AnchorPS5.Core.Models;

/// <summary>Contenido de config/config.json.</summary>
public sealed class AppConfig
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Código de idioma; en el primer arranque se autodetecta.</summary>
    public string Language { get; set; } = "es";

    public string DownloadPath { get; set; } = string.Empty;

    public AppTheme Theme { get; set; } = AppTheme.System;

    public int MaxConcurrentDownloads { get; set; } = 3;

    public List<Source> Sources { get; set; } = [];

    public bool FirstRunCompleted { get; set; }

    /// <summary>Última introVersion vista (0 = nunca).</summary>
    public int IntroSeenVersion { get; set; }

    /// <summary>
    /// Opcional, para pruebas: ruta (o file://) a un fichero con releases en el formato de la
    /// API de GitHub. Si está, las actualizaciones de la app se buscan ahí en vez de en GitHub.
    /// </summary>
    public string? UpdateFeed { get; set; }
}

public enum AppTheme
{
    System,
    Light,
    Dark,
}
