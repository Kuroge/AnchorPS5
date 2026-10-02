using System.Globalization;
using System.Text;

namespace AnchorPS5.Core.Diagnostics;

/// <summary>
/// Registro sencillo en config/logs/anchorps5-AAAA-MM-DD.log (uno por día, se guardan los
/// últimos <see cref="KeepDays"/>). Sirve para que quien reporte un fallo pueda adjuntarlo.
/// Nunca lanza: si no puede escribir, se calla.
/// </summary>
public static class AppLog
{
    public const int KeepDays = 7;

    private static readonly Lock Gate = new();
    private static string? _directory;

    /// <summary>Carpeta de los registros (null hasta <see cref="Initialize"/>).</summary>
    public static string? Directory => _directory;

    public static void Initialize(string directory)
    {
        _directory = directory;
        try
        {
            System.IO.Directory.CreateDirectory(directory);
            foreach (var old in new DirectoryInfo(directory).GetFiles("anchorps5-*.log")
                         .OrderByDescending(f => f.Name)
                         .Skip(KeepDays))
                old.Delete();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message, Exception? exception = null) => Write("WARN", message, exception);

    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        if (_directory is null)
            return;

        var now = DateTimeOffset.Now;
        var line = new StringBuilder()
            .Append(now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
            .Append(' ').Append(level.PadRight(5)).Append(' ').Append(message);
        if (exception is not null)
            line.AppendLine().Append(exception);

        try
        {
            lock (Gate)
                File.AppendAllText(
                    Path.Combine(_directory, $"anchorps5-{now:yyyy-MM-dd}.log"),
                    line.AppendLine().ToString());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
