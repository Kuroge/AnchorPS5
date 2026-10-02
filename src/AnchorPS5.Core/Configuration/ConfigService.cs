using System.Text.Json;
using AnchorPS5.Core.Models;
using AnchorPS5.Core.Platform;

namespace AnchorPS5.Core.Configuration;

/// <summary>Carga y guarda config/config.json, rellenando valores por defecto.</summary>
public sealed class ConfigService
{
    public const string DownloadsSubfolder = "AnchorPS5_Downloads";

    private readonly AppPaths _paths;

    public ConfigService(AppPaths paths)
    {
        _paths = paths;
    }

    public static string DefaultDownloadPath =>
        Path.Combine(KnownFolders.GetDownloadsPath(), DownloadsSubfolder);

    /// <summary>Catálogo oficial: fichero publicado del repo (no gasta cupo de la API de GitHub).</summary>
    public const string OfficialCatalogUrl = "https://raw.githubusercontent.com/Kuroge/AnchorPS5-catalog/main/catalog.json";

    public static AppConfig CreateDefault(string language = "es") => new()
    {
        Language = language,
        DownloadPath = DefaultDownloadPath,
        Sources =
        [
            new Source { Name = "Catálogo oficial", Type = SourceType.Official, Url = OfficialCatalogUrl, Path = "catalog.json", Enabled = true },
        ],
    };

    /// <summary>
    /// Lee config.json; si no existe, lo crea con los valores por defecto
    /// y <paramref name="initialLanguage"/> como idioma (el autodetectado).
    /// Los campos ausentes o vacíos toman su valor por defecto.
    /// </summary>
    /// <summary>
    /// Si config.json no se ha podido leer (JSON mal escrito), dónde se ha guardado el
    /// original y en qué línea estaba el error. La app avisa y arranca con la configuración
    /// por defecto (vuelve a pedir idioma y carpeta).
    /// </summary>
    public ConfigLoadProblem? LastLoadProblem { get; private set; }

    public AppConfig LoadOrCreate(string initialLanguage = "es")
    {
        LastLoadProblem = null;
        if (!File.Exists(_paths.ConfigFile))
        {
            var created = CreateDefault(initialLanguage);
            Save(created);
            return created;
        }

        var json = File.ReadAllText(_paths.ConfigFile);
        AppConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<AppConfig>(json, JsonDefaults.Options);
        }
        catch (JsonException ex)
        {
            // Se aparta el fichero roto (para poder recuperarlo a mano) y se empieza de cero.
            var broken = Path.Combine(_paths.ConfigDirectory, $"config.broken-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.Move(_paths.ConfigFile, broken, overwrite: true);
            LastLoadProblem = new ConfigLoadProblem(broken, (ex.LineNumber ?? 0) + 1);
            var created = CreateDefault(initialLanguage);
            Save(created);
            return created;
        }

        config ??= CreateDefault(initialLanguage);
        ApplyDefaults(config);
        return config;
    }

    /// <summary>Guarda de forma atómica (fichero temporal + reemplazo).</summary>
    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(_paths.ConfigDirectory);

        var tempFile = _paths.ConfigFile + ".tmp";
        File.WriteAllText(tempFile, JsonSerializer.Serialize(config, JsonDefaults.Options));
        File.Move(tempFile, _paths.ConfigFile, overwrite: true);
    }

    private static void ApplyDefaults(AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Language))
            config.Language = "es";

        if (string.IsNullOrWhiteSpace(config.DownloadPath))
            config.DownloadPath = DefaultDownloadPath;

        if (config.MaxConcurrentDownloads < 1)
            config.MaxConcurrentDownloads = 1;

        config.Sources ??= [];
    }
}

/// <param name="BrokenCopy">Dónde ha quedado el config.json que no se pudo leer.</param>
/// <param name="Line">Línea (desde 1) del error de JSON.</param>
public sealed record ConfigLoadProblem(string BrokenCopy, long Line);
