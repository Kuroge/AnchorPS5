namespace AnchorPS5.Core.Library;

/// <summary>
/// Versión tolerante: acepta "1.2", "v1.2.0", "1.2.0-beta.3", "1.2.0+build" o "1.2.0beta".
/// Compara la parte numérica por componentes (1.10 &gt; 1.9, 1.2 == 1.2.0) y una
/// versión preliminar va antes que la final (1.0.0-beta &lt; 1.0.0). Si no empieza por
/// número, se compara el texto tal cual.
/// </summary>
public sealed class AppVersion : IComparable<AppVersion>, IEquatable<AppVersion>
{
    private readonly int[] _numbers;
    private readonly string[] _preRelease;

    private AppVersion(string raw, int[] numbers, string[] preRelease)
    {
        Raw = raw;
        _numbers = numbers;
        _preRelease = preRelease;
    }

    public string Raw { get; }

    public bool IsNumeric => _numbers.Length > 0;

    public static AppVersion Parse(string? text)
    {
        var raw = (text ?? string.Empty).Trim();
        var value = raw.StartsWith('v') || raw.StartsWith('V') ? raw[1..] : raw;

        var plus = value.IndexOf('+');
        if (plus >= 0)
            value = value[..plus];

        var end = 0;
        while (end < value.Length && (char.IsAsciiDigit(value[end]) || value[end] == '.'))
            end++;

        var numberPart = value[..end].Trim('.');
        if (numberPart.Length == 0)
            return new AppVersion(raw, [], []);

        var numbers = new List<int>();
        foreach (var piece in numberPart.Split('.', StringSplitOptions.RemoveEmptyEntries))
            numbers.Add(int.TryParse(piece, out var n) ? n : int.MaxValue);

        var pre = value[end..].TrimStart('-', '.', '_').Split(['.', '-'], StringSplitOptions.RemoveEmptyEntries);
        return new AppVersion(raw, numbers.ToArray(), pre);
    }

    public int CompareTo(AppVersion? other)
    {
        if (other is null)
            return 1;

        if (!IsNumeric || !other.IsNumeric)
            return string.Compare(Raw, other.Raw, StringComparison.OrdinalIgnoreCase);

        for (var i = 0; i < Math.Max(_numbers.Length, other._numbers.Length); i++)
        {
            var a = i < _numbers.Length ? _numbers[i] : 0;
            var b = i < other._numbers.Length ? other._numbers[i] : 0;
            if (a != b)
                return a.CompareTo(b);
        }

        // Final > preliminar.
        if (_preRelease.Length == 0 && other._preRelease.Length == 0)
            return 0;
        if (_preRelease.Length == 0)
            return 1;
        if (other._preRelease.Length == 0)
            return -1;

        for (var i = 0; i < Math.Min(_preRelease.Length, other._preRelease.Length); i++)
        {
            var result = CompareIdentifier(_preRelease[i], other._preRelease[i]);
            if (result != 0)
                return result;
        }

        return _preRelease.Length.CompareTo(other._preRelease.Length);
    }

    private static int CompareIdentifier(string a, string b)
    {
        var aIsNumber = int.TryParse(a, out var na);
        var bIsNumber = int.TryParse(b, out var nb);
        if (aIsNumber && bIsNumber)
            return na.CompareTo(nb);
        if (aIsNumber != bIsNumber)
            return aIsNumber ? -1 : 1;
        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    public bool Equals(AppVersion? other) => CompareTo(other) == 0;

    public override bool Equals(object? obj) => obj is AppVersion other && Equals(other);

    public override int GetHashCode()
    {
        if (!IsNumeric)
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Raw);

        // Coherente con CompareTo: 1.2 y 1.2.0 son iguales (ceros finales ignorados).
        var length = _numbers.Length;
        while (length > 0 && _numbers[length - 1] == 0)
            length--;

        var hash = new HashCode();
        for (var i = 0; i < length; i++)
            hash.Add(_numbers[i]);
        foreach (var part in _preRelease)
            hash.Add(part, StringComparer.OrdinalIgnoreCase);
        return hash.ToHashCode();
    }

    public override string ToString() => Raw;

    public static bool operator >(AppVersion a, AppVersion b) => a.CompareTo(b) > 0;
    public static bool operator <(AppVersion a, AppVersion b) => a.CompareTo(b) < 0;
}
