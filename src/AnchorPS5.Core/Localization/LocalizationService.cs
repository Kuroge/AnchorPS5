using System.Globalization;
using System.Text.Json;

namespace AnchorPS5.Core.Localization;

/// <summary>
/// Textos de la interfaz desde lang/&lt;código&gt;.json (clave plana → texto).
/// Las claves que faltan en el idioma activo se toman del idioma por defecto;
/// si tampoco están, se devuelve la propia clave.
/// </summary>
public sealed class LocalizationService
{
    public const string FallbackLanguage = "es";

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly string _langDirectory;
    private Dictionary<string, string> _strings = new(StringComparer.Ordinal);
    private Dictionary<string, string> _fallback = new(StringComparer.Ordinal);

    public LocalizationService(string langDirectory, string defaultLanguage = FallbackLanguage)
    {
        _langDirectory = langDirectory;
        DefaultLanguage = defaultLanguage;
        CurrentLanguage = defaultLanguage;
    }

    public string DefaultLanguage { get; }

    public string CurrentLanguage { get; private set; }

    public string this[string key] => Get(key);

    public string Get(string key) =>
        _strings.TryGetValue(key, out var text) || _fallback.TryGetValue(key, out text) ? text : key;

    /// <summary>Texto con marcadores {0}, {1}… sustituidos.</summary>
    public string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    /// <summary>Idiomas presentes en lang/*.json. Los ficheros ilegibles se ignoran.</summary>
    public IReadOnlyList<LanguageInfo> DiscoverLanguages()
    {
        if (!Directory.Exists(_langDirectory))
            return [];

        var languages = new List<LanguageInfo>();
        foreach (var file in Directory.EnumerateFiles(_langDirectory, "*.json"))
        {
            var fileCode = Path.GetFileNameWithoutExtension(file);
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file), DocumentOptions);
                var name = fileCode;
                if (doc.RootElement.TryGetProperty("_meta", out var meta)
                    && meta.ValueKind == JsonValueKind.Object
                    && meta.TryGetProperty("name", out var nameElement)
                    && nameElement.ValueKind == JsonValueKind.String)
                {
                    name = nameElement.GetString() ?? fileCode;
                }

                // El nombre del fichero manda: es el que se busca al cargar.
                languages.Add(new LanguageInfo(fileCode, name, file));
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
            }
        }

        return languages.OrderBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>
    /// Idioma a preseleccionar para una cultura: coincidencia exacta (es-ES),
    /// luego por código de 2 letras (es) y, si no, el idioma por defecto.
    /// </summary>
    public string DetectLanguage(CultureInfo culture)
    {
        var available = DiscoverLanguages();

        var exact = available.FirstOrDefault(l => string.Equals(l.Code, culture.Name, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact.Code;

        var twoLetter = culture.TwoLetterISOLanguageName;
        var byLanguage =
            available.FirstOrDefault(l => string.Equals(l.Code, twoLetter, StringComparison.OrdinalIgnoreCase))
            ?? available.FirstOrDefault(l => l.Code.StartsWith(twoLetter + "-", StringComparison.OrdinalIgnoreCase));

        return byLanguage?.Code ?? DefaultLanguage;
    }

    /// <summary>
    /// Carga el idioma indicado. Si no existe su fichero, queda activo el idioma por defecto.
    /// </summary>
    public void Load(string languageCode)
    {
        _fallback = ReadStrings(DefaultLanguage) ?? new(StringComparer.Ordinal);

        var strings = string.Equals(languageCode, DefaultLanguage, StringComparison.OrdinalIgnoreCase)
            ? null
            : ReadStrings(languageCode);

        if (strings is null)
        {
            _strings = _fallback;
            CurrentLanguage = DefaultLanguage;
        }
        else
        {
            _strings = strings;
            CurrentLanguage = languageCode;
        }
    }

    private Dictionary<string, string>? ReadStrings(string languageCode)
    {
        var file = Path.Combine(_langDirectory, languageCode + ".json");
        if (!File.Exists(file))
            return null;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(File.ReadAllText(file), DocumentOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Un idioma mal escrito no tumba la app: se usa el de respaldo y queda en el log.
            Diagnostics.AppLog.Warn($"No se ha podido leer lang/{languageCode}.json", ex);
            return null;
        }

        using var _ = doc;
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            // Las claves con "_" (como _meta) son metadatos, no textos.
            if (property.Name.StartsWith('_') || property.Value.ValueKind != JsonValueKind.String)
                continue;

            strings[property.Name] = property.Value.GetString()!;
        }

        return strings;
    }
}
