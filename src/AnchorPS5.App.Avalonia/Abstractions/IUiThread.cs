namespace AnchorPS5.App;

/// <summary>
/// Acceso al hilo de UI sin depender del framework (WinUI usa DispatcherQueue, Avalonia su
/// propio Dispatcher). Las notificaciones de descargas llegan de hilos de fondo y se pasan al
/// de UI a través de esta abstracción.
/// </summary>
public interface IUiThread
{
    /// <summary>Ejecuta una acción en el hilo de UI (sin esperar).</summary>
    void Enqueue(Action action);
}
