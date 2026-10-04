using Avalonia.Threading;

namespace AnchorPS5.App;

/// <summary>Implementación de <see cref="IUiThread"/> sobre el dispatcher de Avalonia.</summary>
public sealed class AvaloniaUiThread : IUiThread
{
    public void Enqueue(Action action) => Dispatcher.UIThread.Post(action);
}
