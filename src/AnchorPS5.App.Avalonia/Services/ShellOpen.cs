using System.Diagnostics;
using AnchorPS5.Core.Diagnostics;

namespace AnchorPS5.App.Services;

/// <summary>
/// Abre una carpeta, fichero o URL con la aplicación del sistema. Process.Start con
/// UseShellExecute no vale en Linux para carpetas (las intenta ejecutar), así que se usa
/// el lanzador de cada plataforma: explorer (Windows), open (macOS), xdg-open (Linux).
/// </summary>
public static class ShellOpen
{
    public static bool Open(string target)
    {
        var program = OperatingSystem.IsWindows() ? "explorer.exe"
            : OperatingSystem.IsMacOS() ? "open"
            : "xdg-open";
        try
        {
            var start = new ProcessStartInfo(program) { UseShellExecute = false };
            start.ArgumentList.Add(target);
            using var _ = Process.Start(start);
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Warn($"No se ha podido abrir {target} con {program}", ex);
            return false;
        }
    }
}
