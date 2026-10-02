using System.Reflection;
using System.Globalization;
using AnchorPS5.Core;
using AnchorPS5.Core.Catalog;
using AnchorPS5.Core.Configuration;
using AnchorPS5.App.Services;
using AnchorPS5.Core.Diagnostics;
using AnchorPS5.Core.GitHub;
using AnchorPS5.Core.Library;
using AnchorPS5.Core.Packages;
using AnchorPS5.Core.Localization;
using AnchorPS5.Core.Models;
using Microsoft.UI.Xaml;

namespace AnchorPS5.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        AppLog.Initialize(Path.Combine(Paths.ConfigDirectory, "logs"));
        UnhandledException += (_, e) => AppLog.Error("Excepción no controlada", e.Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => AppLog.Error("Excepción no observada en una tarea", e.Exception);
    }

    /// <summary>Si config.json estaba mal escrito: dónde quedó el original (la ventana avisa).</summary>
    public static ConfigLoadProblem? ConfigProblem { get; private set; }

    /// <summary>Versión de la app (SemVer, p. ej. 0.1.0-alpha.1), de Directory.Build.props.</summary>
    public static string Version { get; } =
        typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(App).Assembly.GetName().Version?.ToString(3)
        ?? "0.0.0";

    public static AppPaths Paths { get; } = AppPaths.FromExecutable();

    public static ConfigService ConfigService { get; } = new(Paths);

    public static LocalizationService Localization { get; } = new(Paths.LangDirectory);

    /// <summary>Un único HttpClient para toda la app (fuentes remotas y, más adelante, descargas).</summary>
    public static HttpClient Http { get; } = CreateHttpClient(TimeSpan.FromSeconds(30));

    /// <summary>Para descargas: sin límite total de tiempo; DownloadManager detecta las que se quedan paradas.</summary>
    public static HttpClient DownloadHttp { get; } = CreateHttpClient(Timeout.InfiniteTimeSpan);

    /// <summary>7-Zip incluido junto al exe.</summary>
    public static string SevenZipPath { get; } = Path.Combine(AppContext.BaseDirectory, "tools", "7zip", "7z.exe");

    public static SourceLoader SourceLoader { get; } = new(Http, Paths.ConfigDirectory);

    /// <summary>config\state.json: apps ya vistas y canal (estable/beta) de cada fichero.</summary>
    public static AppStateStore State { get; } = new(Paths);

    public static SeenAppsService SeenApps { get; } = new(State);

    public static ChannelPreferences Channels { get; } = new(State);

    public static WarningPreferences Warnings { get; } = new(State);

    /// <summary>
    /// Client ID de la OAuth App "AnchorPS5" en GitHub (público: identifica a la app al
    /// iniciar sesión con el flujo de dispositivo; no hay secreto).
    /// </summary>
    public const string GitHubClientId = "Ov23liwzgH1dVqeAgPeP";

    private static string GitHubCacheDirectory => Path.Combine(Paths.ConfigDirectory, "cache", "github");

    /// <summary>Sesión de GitHub (opcional): el token sube el límite de la API de 60 a 5000 consultas/hora.</summary>
    public static GitHubSession GitHubSession { get; } = new(new CredentialTokenStore(), Http, GitHubCacheDirectory);

    /// <summary>API de GitHub con caché en config\cache\github; usa el token de la sesión si la hay.</summary>
    public static GitHubClient GitHub { get; } = new(Http, GitHubCacheDirectory, () => GitHubSession.Token);

    /// <summary>Índice de releases del catálogo oficial: evita preguntar a GitHub app por app.</summary>
    public static ReleaseIndex ReleaseIndex { get; } = new(Http, GitHubCacheDirectory);

    /// <summary>Con sesión de GitHub se pregunta a la API (datos más al día); sin sesión, el índice.</summary>
    public static PackageResolver PackageResolver { get; } = new(GitHub, ReleaseIndex, () => GitHubSession.IsSignedIn);

    /// <summary>Catálogo oficial: copia local en config\ y última versión recibida en config\cache.</summary>
    public static OfficialCatalogSync OfficialSync { get; } = new(Http, Paths.ConfigDirectory, Path.Combine(Paths.ConfigDirectory, "cache"));

    public static AppConfig Config { get; private set; } = null!;

    public static FirstRunService FirstRun { get; private set; } = null!;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Sin config.json se crea con los valores por defecto y el idioma del sistema (si hay traducción).
        var detectedLanguage = Localization.DetectLanguage(CultureInfo.CurrentUICulture);
        Config = ConfigService.LoadOrCreate(detectedLanguage);
        ConfigProblem = ConfigService.LastLoadProblem;
        Localization.Load(Config.Language);
        AppLog.Info($"AnchorPS5 {Version} · {Environment.OSVersion} · idioma {Localization.CurrentLanguage}");
        if (ConfigProblem is { } problem)
            AppLog.Warn($"config.json mal escrito (línea {problem.Line}); apartado como {problem.BrokenCopy}");
        FirstRun = new FirstRunService(ConfigService, Config);
        SeedOfficialCatalog();

        // El token se lee al momento; el perfil llega después sin bloquear el arranque.
        _ = GitHubSession.RestoreAsync();
        var signedIn = GitHubSession.IsSignedIn;
        GitHubSession.Changed += (_, _) =>
        {
            // Al iniciar o cerrar sesión cambia el cupo: el bloqueo anterior ya no vale.
            if (GitHubSession.IsSignedIn != signedIn)
                GitHub.ClearRateLimit();
            signedIn = GitHubSession.IsSignedIn;
        };

        _window = new MainWindow();
        _window.Activate();
    }

    /// <summary>Repo de AnchorPS5: sus releases son las actualizaciones de la app.</summary>
    public static readonly GitHubRepoRef AppRepo = new("Kuroge", "AnchorPS5");

    /// <summary>
    /// Releases de la propia app (de GitHub, o de config.updateFeed para pruebas). Null si no
    /// se han podido consultar.
    /// </summary>
    public static async Task<IReadOnlyList<GitHubRelease>?> GetAppReleasesAsync(bool forceRefresh)
    {
        if (Config.UpdateFeed is { Length: > 0 } feed)
            return AnchorPS5.Core.Updates.AppUpdates.ReadFeed(feed);

        var result = await GitHub.GetReleasesAsync(AppRepo, forceRefresh);
        return result.Status is GitHubStatus.Ok or GitHubStatus.FromCache ? result.Value : null;
    }

    /// <summary>
    /// Primer arranque: el catálogo oficial y su índice que viajan con la app (carpeta
    /// "catalog" junto al exe) se usan hasta que se pueda descargar la versión al día.
    /// </summary>
    private static void SeedOfficialCatalog()
    {
        var bundle = Path.Combine(AppContext.BaseDirectory, "catalog");
        foreach (var source in Config.Sources.Where(s => s.Enabled && s.Type == SourceType.Official))
        {
            OfficialSync.SeedFromBundle(source, Path.Combine(bundle, "catalog.json"));
            ReleaseIndex.SeedFromBundle(source, Path.Combine(bundle, ReleaseIndex.FileName));
        }
    }

    private static HttpClient CreateHttpClient(TimeSpan timeout)
    {
        var client = new HttpClient { Timeout = timeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"AnchorPS5/{Version}");
        return client;
    }
}
