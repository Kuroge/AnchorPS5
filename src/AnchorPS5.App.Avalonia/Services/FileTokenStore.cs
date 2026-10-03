using AnchorPS5.Core.GitHub;

namespace AnchorPS5.App.Services;

/// <summary>
/// Token de GitHub en un fichero dentro de la carpeta portable de config (sin depender del
/// Administrador de credenciales de Windows ni de libsecret; la app es portable y todo vive
/// junto al ejecutable).
/// </summary>
public sealed class FileTokenStore : ITokenStore
{
    private readonly string _path;

    public FileTokenStore(string configDirectory)
    {
        _path = Path.Combine(configDirectory, "github-token.txt");
    }

    public string? Read()
    {
        try
        {
            return File.Exists(_path) ? File.ReadAllText(_path).Trim() : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Save(string token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllText(_path, token);
            return;
        }

        // Solo legible por el usuario (0600): con el umask por defecto quedaría 0644 y
        // cualquier otra cuenta del equipo podría leer el token.
        var options = new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
        };
        using (var writer = new StreamWriter(_path, options))
            writer.Write(token);
        // UnixCreateMode solo aplica al crear: un fichero que ya existía conserva sus permisos.
        File.SetUnixFileMode(_path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    public void Delete()
    {
        try { File.Delete(_path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
