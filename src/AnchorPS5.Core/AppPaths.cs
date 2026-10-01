namespace AnchorPS5.Core;

/// <summary>
/// Rutas de la estructura portable: todo cuelga de la carpeta del ejecutable.
/// </summary>
public sealed class AppPaths
{
    public AppPaths(string baseDirectory)
    {
        BaseDirectory = baseDirectory;
    }

    /// <summary>Rutas relativas a la carpeta del ejecutable en ejecución.</summary>
    public static AppPaths FromExecutable() => new(AppContext.BaseDirectory);

    public string BaseDirectory { get; }

    public string ConfigDirectory => Path.Combine(BaseDirectory, "config");
    public string LangDirectory => Path.Combine(BaseDirectory, "lang");
    public string IntroDirectory => Path.Combine(BaseDirectory, "intro");

    public string ConfigFile => Path.Combine(ConfigDirectory, "config.json");
}
