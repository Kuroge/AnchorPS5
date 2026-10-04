using System.Text.Json;
using AnchorPS5.Core.Configuration;

namespace AnchorPS5.Core.Library;

/// <summary>Contenido de config/state.json: estado local de la app que no es configuración.</summary>
public sealed class AppState
{
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Ids de apps que ya han aparecido alguna vez en el catálogo.</summary>
    public List<string> KnownAppIds { get; set; } = [];

    /// <summary>Canal elegido por fichero: "appId::fichero" → "beta" o "stable".</summary>
    public Dictionary<string, string> FileChannels { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>"No volver a mostrar" en el aviso de recargar sin sesión de GitHub.</summary>
    public bool HideReloadWarning { get; set; }

    /// <summary>"No volver a preguntar" al actualizar: se conservan siempre las versiones anteriores.</summary>
    public bool KeepOldVersionsOnUpdate { get; set; }
}

/// <summary>Preferencias de avisos que el usuario ha pedido no volver a ver.</summary>
public sealed class WarningPreferences(AppStateStore store)
{
    public bool HideReloadWarning => store.Read(state => state.HideReloadWarning);

    public void HideReloadWarningFromNowOn() =>
        store.Update<bool>((state, _) =>
        {
            state.HideReloadWarning = true;
            return (true, true);
        });

    public bool KeepOldVersionsWithoutAsking => store.Read(state => state.KeepOldVersionsOnUpdate);

    public void KeepOldVersionsFromNowOn() =>
        store.Update<bool>((state, _) =>
        {
            state.KeepOldVersionsOnUpdate = true;
            return (true, true);
        });
}

/// <summary>Lee y guarda config/state.json (con un cerrojo: lo usan varios servicios).</summary>
public sealed class AppStateStore
{
    private readonly string _stateFile;
    private readonly Lock _lock = new();

    public AppStateStore(AppPaths paths)
    {
        _stateFile = Path.Combine(paths.ConfigDirectory, "state.json");
    }

    /// <summary>Lee, modifica y guarda de una vez. <paramref name="existed"/>: si había fichero válido.</summary>
    public T Update<T>(Func<AppState, bool, (T Result, bool Save)> change)
    {
        lock (_lock)
        {
            var state = Load(out var existed);
            var (result, save) = change(state, existed);
            if (save)
                Save(state);
            return result;
        }
    }

    public T Read<T>(Func<AppState, T> read)
    {
        lock (_lock)
            return read(Load(out _));
    }

    private AppState Load(out bool existed)
    {
        existed = File.Exists(_stateFile);
        if (!existed)
            return new AppState();

        try
        {
            var state = JsonSerializer.Deserialize<AppState>(File.ReadAllText(_stateFile), JsonDefaults.Options) ?? new AppState();
            state.FileChannels = new Dictionary<string, string>(state.FileChannels ?? [], StringComparer.OrdinalIgnoreCase);
            return state;
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

/// <summary>
/// Detecta las apps nuevas del catálogo. Una app es "nueva" en la primera apertura en
/// que aparece; a partir de ahí queda registrada como conocida.
/// En la primera ejecución (sin state.json) todo se registra sin marcar nada como nuevo.
/// </summary>
public sealed class SeenAppsService
{
    private readonly AppStateStore _store;

    public SeenAppsService(AppPaths paths) : this(new AppStateStore(paths))
    {
    }

    public SeenAppsService(AppStateStore store)
    {
        _store = store;
    }

    /// <summary>Registra los ids y devuelve los que no se conocían.</summary>
    public IReadOnlySet<string> RegisterAndGetNew(IEnumerable<string> appIds)
    {
        var ids = appIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        return _store.Update<IReadOnlySet<string>>((state, existed) =>
        {
            var known = new HashSet<string>(state.KnownAppIds, StringComparer.OrdinalIgnoreCase);
            var newIds = existed
                ? ids.Where(id => !known.Contains(id)).ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var changed = !existed || ids.Any(id => !known.Contains(id));
            if (changed)
            {
                // Solo se añade: si una fuente falla, sus apps no se "olvidan".
                known.UnionWith(ids);
                state.KnownAppIds = known.Order(StringComparer.OrdinalIgnoreCase).ToList();
            }

            return (newIds, changed);
        });
    }
}

public enum FileChannel
{
    Stable,
    Beta,
}

/// <summary>Canal (estable/beta) que el usuario ha elegido para cada fichero de cada app.</summary>
public sealed class ChannelPreferences
{
    private readonly AppStateStore _store;

    public ChannelPreferences(AppStateStore store)
    {
        _store = store;
    }

    /// <summary>Preferencias de una app: clave del fichero → canal (solo las que se han elegido).</summary>
    public IReadOnlyDictionary<string, FileChannel> GetForApp(string appId) =>
        _store.Read(state => state.FileChannels
            .Where(kv => kv.Key.StartsWith(appId + "::", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                kv => kv.Key[(appId.Length + 2)..],
                kv => string.Equals(kv.Value, "beta", StringComparison.OrdinalIgnoreCase) ? FileChannel.Beta : FileChannel.Stable,
                StringComparer.OrdinalIgnoreCase));

    public void Set(string appId, string fileKey, FileChannel channel) =>
        _store.Update<bool>((state, _) =>
        {
            state.FileChannels[$"{appId}::{fileKey}"] = channel == FileChannel.Beta ? "beta" : "stable";
            return (true, true);
        });
}
