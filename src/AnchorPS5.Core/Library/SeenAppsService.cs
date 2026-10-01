using System.Text.Json;
using AnchorPS5.Core.Configuration;

namespace AnchorPS5.Core.Library;

/// <summary>Contenido de config/state.json: estado local de la app que no es configuración.</summary>
public sealed class AppState
{
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Ids de apps que ya han aparecido alguna vez en el catálogo.</summary>
    public List<string> KnownAppIds { get; set; } = [];
}

/// <summary>
/// Detecta las apps nuevas del catálogo. Una app es "nueva" en la primera apertura en
/// que aparece; a partir de ahí queda registrada como conocida.
/// En la primera ejecución (sin state.json) todo se registra sin marcar nada como nuevo.
/// </summary>
public sealed class SeenAppsService
{
    private readonly string _stateFile;

    public SeenAppsService(AppPaths paths)
    {
        _stateFile = Path.Combine(paths.ConfigDirectory, "state.json");
    }

    /// <summary>Registra los ids y devuelve los que no se conocían.</summary>
    public IReadOnlySet<string> RegisterAndGetNew(IEnumerable<string> appIds)
    {
        var ids = appIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        var state = Load(out var existed);
        var known = new HashSet<string>(state.KnownAppIds, StringComparer.OrdinalIgnoreCase);

        var newIds = existed
            ? ids.Where(id => !known.Contains(id)).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!existed || ids.Any(id => !known.Contains(id)))
        {
            // Solo se añade: si una fuente falla, sus apps no se "olvidan".
            known.UnionWith(ids);
            state.KnownAppIds = known.Order(StringComparer.OrdinalIgnoreCase).ToList();
            Save(state);
        }

        return newIds;
    }

    private AppState Load(out bool existed)
    {
        existed = File.Exists(_stateFile);
        if (!existed)
            return new AppState();

        try
        {
            return JsonSerializer.Deserialize<AppState>(File.ReadAllText(_stateFile), JsonDefaults.Options) ?? new AppState();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Estado dañado: se regenera sin marcar nada como nuevo.
            existed = false;
            return new AppState();
        }
    }

    private void Save(AppState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_stateFile)!);
        var temp = _stateFile + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, JsonDefaults.Options));
        File.Move(temp, _stateFile, overwrite: true);
    }
}
