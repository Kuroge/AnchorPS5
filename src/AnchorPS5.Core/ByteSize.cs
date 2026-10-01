using System.Globalization;

namespace AnchorPS5.Core;

public static class ByteSize
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    /// <summary>Tamaño legible: 12345678 → "11,8 MB" (separador decimal según la cultura).</summary>
    public static string Format(long bytes, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        if (bytes < 1024)
            return string.Create(culture, $"{Math.Max(bytes, 0)} B");

        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return string.Create(culture, $"{value:0.#} {Units[unit]}");
    }
}
