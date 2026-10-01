using System.Runtime.InteropServices;

namespace AnchorPS5.Core.Platform;

/// <summary>
/// Carpetas conocidas de Windows. Environment.SpecialFolder no incluye Descargas
/// y el usuario puede haberla reubicado, así que se pregunta al shell.
/// </summary>
public static partial class KnownFolders
{
    private static readonly Guid FolderIdDownloads = new("374DE290-123F-4565-9164-39C4925E467B");

    /// <summary>
    /// Carpeta Descargas real (FOLDERID_Downloads); si falla, %USERPROFILE%\Downloads.
    /// </summary>
    public static string GetDownloadsPath()
    {
        if (OperatingSystem.IsWindows()
            && SHGetKnownFolderPath(FolderIdDownloads, 0, IntPtr.Zero, out var pathPtr) == 0)
        {
            try
            {
                var path = Marshal.PtrToStringUni(pathPtr);
                if (!string.IsNullOrWhiteSpace(path))
                    return path;
            }
            finally
            {
                Marshal.FreeCoTaskMem(pathPtr);
            }
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(profile, "Downloads");
    }

    [LibraryImport("shell32.dll")]
    private static partial int SHGetKnownFolderPath(in Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);
}
