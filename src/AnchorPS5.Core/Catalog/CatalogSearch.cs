using System.Globalization;

namespace AnchorPS5.Core.Catalog;

/// <summary>Búsqueda por texto en nombre, autor, descripción e id; ignora mayúsculas y acentos.</summary>
public static class CatalogSearch
{
    private const CompareOptions Options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    public static IEnumerable<CatalogEntry> Filter(IEnumerable<CatalogEntry> entries, string? query)
    {
        var terms = (query ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (terms.Length == 0)
            return entries;

        // Cada palabra debe aparecer en algún campo.
        return entries.Where(e => terms.All(term => Matches(e, term)));
    }

    private static bool Matches(CatalogEntry entry, string term)
    {
        var app = entry.App;
        return Contains(app.Name, term)
            || Contains(app.Author, term)
            || Contains(app.Description, term)
            || Contains(app.Id, term);
    }

    private static bool Contains(string? text, string term) =>
        !string.IsNullOrEmpty(text) && CultureInfo.InvariantCulture.CompareInfo.IndexOf(text, term, Options) >= 0;
}
