using System.Globalization;
using AnchorPS5.Core;
using AnchorPS5.Core.Configuration;
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

    public static AppConfig Config { get; private set; } = null!;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Sin config.json se crea con los valores por defecto y el idioma del sistema (si hay traducción).
        var detectedLanguage = Localization.DetectLanguage(CultureInfo.CurrentUICulture);
        Config = ConfigService.LoadOrCreate(detectedLanguage);
        Localization.Load(Config.Language);

        _window = new MainWindow();
        _window.Activate();
    }
}
