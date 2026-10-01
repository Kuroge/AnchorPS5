using AnchorPS5.Core.Library;

namespace AnchorPS5.Core.Catalog;

/// <summary>Secciones del catálogo (menú lateral).</summary>
public enum CatalogFilter
{
    All,
    Downloaded,
    Updates,
    New,
}

public enum CatalogSort
{
    Name,
    /// <summary>Nuevas primero, luego con actualización, luego el resto; dentro, por nombre.</summary>
    WhatsNew,
}

/// <summary>Reglas de filtro y orden, sin dependencias de UI.</summary>
public static class CatalogView
{
    public static bool Matches(CatalogFilter filter, PackageState state, bool isNew) => filter switch
    {
        CatalogFilter.Downloaded => state != PackageState.NotDownloaded,
        CatalogFilter.Updates => state == PackageState.UpdateAvailable,
        CatalogFilter.New => isNew,
        _ => true,
    };

    public static IEnumerable<T> Sort<T>(IEnumerable<T> items, CatalogSort sort, Func<T, string> name, Func<T, PackageState> state, Func<T, bool> isNew)
    {
        var byName = StringComparer.CurrentCultureIgnoreCase;
        return sort == CatalogSort.WhatsNew
            ? items.OrderBy(i => isNew(i) ? 0 : state(i) == PackageState.UpdateAvailable ? 1 : 2).ThenBy(name, byName)
            : items.OrderBy(name, byName);
    }
}
