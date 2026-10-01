namespace AnchorPS5.Core.Library;

/// <summary>Convierte nombres de app y versiones en nombres de carpeta válidos en Windows.</summary>
public static class PathNames
{
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private static readonly char[] Invalid = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    public static string Sanitize(string? name)
    {
        var chars = (name ?? string.Empty)
            .Select(c => c < 32 || Invalid.Contains(c) ? '_' : c)
            .ToArray();

        // Windows no admite espacios ni puntos al final.
        var result = new string(chars).Trim().TrimEnd('.', ' ');
        if (result.Length == 0)
            return "_";

        if (Reserved.Contains(result.Split('.')[0]))
            result = "_" + result;

        return result.Length > 100 ? result[..100].TrimEnd('.', ' ') : result;
    }
}
