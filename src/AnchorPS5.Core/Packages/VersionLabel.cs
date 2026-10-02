namespace AnchorPS5.Core.Packages;

/// <summary>
/// Versión tal y como se muestra: "v0.21.1" si es un número, el texto tal cual si no lo es
/// ("alpha-1", "vk-285-117"…), para no mostrar cosas como "valpha-1".
/// </summary>
public static class VersionLabel
{
    public static string Format(string? version)
    {
        var text = version?.Trim() ?? string.Empty;
        if (text.Length == 0)
            return string.Empty;

        if (char.IsDigit(text[0]))
            return "v" + text;

        // "V1.2" / "v1.2": se respeta, en minúscula.
        return text.Length > 1 && text[0] is 'v' or 'V' && char.IsDigit(text[1]) ? "v" + text[1..] : text;
    }
}
