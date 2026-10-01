using System.Globalization;
using AnchorPS5.Core;
using AnchorPS5.Core.Catalog;
using AnchorPS5.Core.Configuration;
using AnchorPS5.Core.Library;
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
    }

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

    public static SeenAppsService SeenApps { get; } = new(Paths);

    public static AppConfig Config { get; private set; } = null!;

    public static FirstRunService FirstRun { get; private set; } = null!;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Sin config.json se crea con los valores por defecto y el idioma del sistema (si hay traducción).
        var detectedLanguage = Localization.DetectLanguage(CultureInfo.CurrentUICulture);
        Config = ConfigService.LoadOrCreate(detectedLanguage);
        Localization.Load(Config.Language);
        FirstRun = new FirstRunService(ConfigService, Config);

        _window = new MainWindow();
        _window.Activate();
    }

    private static HttpClient CreateHttpClient(TimeSpan timeout)
    {
        var client = new HttpClient { Timeout = timeout };
        var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"AnchorPS5/{version}");
        return client;
    }
}
