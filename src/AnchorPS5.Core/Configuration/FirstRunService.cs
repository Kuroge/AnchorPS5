using AnchorPS5.Core.Models;

namespace AnchorPS5.Core.Configuration;

/// <summary>
/// Configuración inicial: guarda idioma y carpeta de descargas, y marca el primer
/// arranque como completado cuando el usuario responde si quiere ver la guía.
/// </summary>
public sealed class FirstRunService
{
    private readonly ConfigService _configService;
    private readonly AppConfig _config;

    public FirstRunService(ConfigService configService, AppConfig config)
    {
        _configService = configService;
        _config = config;
    }

    public bool IsRequired => !_config.FirstRunCompleted;

    /// <summary>
    /// Normaliza la ruta escrita por el usuario. Solo se aceptan rutas completas
    /// (C:\…, \\servidor\…); las relativas dependerían de la carpeta de trabajo.
    /// </summary>
    public static bool TryNormalizeDownloadPath(string? input, out string fullPath)
    {
        fullPath = string.Empty;
        var trimmed = input?.Trim().Trim('"');
        if (string.IsNullOrEmpty(trimmed) || !Path.IsPathFullyQualified(trimmed))
            return false;

        try
        {
            fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(trimmed));
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>
    /// Crea la carpeta de descargas y guarda idioma y ruta en config.json.
    /// Lanza IOException/UnauthorizedAccessException si la carpeta no se puede crear.
    /// </summary>
    public void SaveSettings(string language, string downloadPath)
    {
        if (!TryNormalizeDownloadPath(downloadPath, out var fullPath))
            throw new ArgumentException("La ruta de descargas no es una ruta completa válida.", nameof(downloadPath));

        Directory.CreateDirectory(fullPath);

        _config.Language = language;
        _config.DownloadPath = fullPath;
        _configService.Save(_config);
    }

    public void Complete()
    {
        _config.FirstRunCompleted = true;
        _configService.Save(_config);
    }
}
