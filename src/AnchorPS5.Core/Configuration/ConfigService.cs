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

    public static AppConfig CreateDefault(string language = "es") => new()
    {
        Language = language,
        DownloadPath = DefaultDownloadPath,
        Sources =
        [
            new Source { Name = "Catálogo local", Type = SourceType.Local, Path = "catalog.json", Enabled = true },
        ],
    };

    /// <summary>
    /// Lee config.json; si no existe, lo crea con los valores por defecto
    /// y <paramref name="initialLanguage"/> como idioma (el autodetectado).
    /// Los campos ausentes o vacíos toman su valor por defecto.
    /// </summary>
    public AppConfig LoadOrCreate(string initialLanguage = "es")
    {
        if (!File.Exists(_paths.ConfigFile))
        {
            var created = CreateDefault(initialLanguage);
            Save(created);
            return created;
        }

        var json = File.ReadAllText(_paths.ConfigFile);
        var config = JsonSerializer.Deserialize<AppConfig>(json, JsonDefaults.Options) ?? CreateDefault();
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
