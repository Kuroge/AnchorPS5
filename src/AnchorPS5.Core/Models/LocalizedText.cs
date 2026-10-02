using System.Text.Json;
using System.Text.Json.Serialization;

namespace AnchorPS5.Core.Models;

/// <summary>
/// Texto del catálogo traducible: en el JSON puede ser un texto simple ("…") o un objeto
/// por idioma ({ "es": "…", "en": "…" }). Se muestra en el idioma activo, con respaldo
/// en español y, si tampoco está, en el primero que haya.
/// </summary>
[JsonConverter(typeof(LocalizedTextConverter))]
public sealed class LocalizedText : IEquatable<LocalizedText>
{
    public const string FallbackLanguage = "es";

    // Texto simple del JSON: se guarda con la clave vacía y vale para cualquier idioma.
    private const string PlainKey = "";

    private readonly Dictionary<string, string> _values;

    public LocalizedText(IEnumerable<KeyValuePair<string, string>> values)
    {
        _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (language, text) in values)
        {
            if (!string.IsNullOrWhiteSpace(text))
                _values[language.Trim()] = text;
        }
    }

    public static LocalizedText Empty { get; } = new([]);

    /// <summary>Idiomas con traducción (vacío si es un texto simple).</summary>
    public IEnumerable<string> Languages => _values.Keys.Where(k => k != PlainKey);

    /// <summary>Todos los textos (para buscar en cualquier idioma).</summary>
    public IEnumerable<string> AllValues => _values.Values;

    public bool IsEmpty => _values.Count == 0;

    internal bool IsPlain => _values.Count == 1 && _values.ContainsKey(PlainKey);

    internal IReadOnlyDictionary<string, string> Values => _values;

    public static LocalizedText FromPlain(string? text) =>
        new(string.IsNullOrWhiteSpace(text) ? [] : [new(PlainKey, text)]);

    public static implicit operator LocalizedText(string? text) => FromPlain(text);

    /// <summary>Texto en <paramref name="language"/> (o su código de 2 letras), español o el primero.</summary>
    public string Get(string? language)
    {
        if (_values.Count == 0)
            return string.Empty;

        if (!string.IsNullOrEmpty(language))
        {
            if (_values.TryGetValue(language, out var exact))
                return exact;

            var dash = language.IndexOf('-');
            if (dash > 0 && _values.TryGetValue(language[..dash], out var neutral))
                return neutral;
        }

        if (_values.TryGetValue(PlainKey, out var plain))
            return plain;

        return _values.TryGetValue(FallbackLanguage, out var fallback) ? fallback : _values.Values.First();
    }

    public override string ToString() => Get(FallbackLanguage);

    public bool Equals(LocalizedText? other) =>
        other is not null
        && _values.Count == other._values.Count
        && _values.All(kv => other._values.TryGetValue(kv.Key, out var text) && text == kv.Value);

    public override bool Equals(object? obj) => Equals(obj as LocalizedText);

    public override int GetHashCode() =>
        _values.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Aggregate(0, (hash, kv) => HashCode.Combine(hash, kv.Key.ToLowerInvariant(), kv.Value));
}

public sealed class LocalizedTextConverter : JsonConverter<LocalizedText>
{
    public override LocalizedText Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return LocalizedText.Empty;
            case JsonTokenType.String:
                return LocalizedText.FromPlain(reader.GetString());
            case JsonTokenType.StartObject:
                var values = JsonSerializer.Deserialize<Dictionary<string, string?>>(ref reader, options) ?? [];
                return new LocalizedText(values
                    .Where(kv => kv.Value is not null)
                    .Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value!)));
            default:
                throw new JsonException("Se esperaba un texto o un objeto { \"es\": \"…\" }.");
        }
    }

    public override void Write(Utf8JsonWriter writer, LocalizedText value, JsonSerializerOptions options)
    {
        if (value.IsPlain)
        {
            writer.WriteStringValue(value.Values.Values.First());
            return;
        }

        writer.WriteStartObject();
        foreach (var (language, text) in value.Values)
            writer.WriteString(language, text);
        writer.WriteEndObject();
    }
}
