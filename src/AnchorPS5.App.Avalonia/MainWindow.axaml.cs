using AnchorPS5.App.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Styling;

namespace AnchorPS5.App;

/// <summary>
/// Ventana única. Primer arranque: setup → app. Arranques siguientes: directo a la app.
/// </summary>
public partial class MainWindow : Window
{
    private ShellPage? _shell;

    public MainWindow()
    {
        InitializeComponent();
        ApplyTheme(App.Config.Theme);
        UpdateTitle();

        if (App.FirstRun.IsRequired)
            ShowSetup();
        else
            ShowShell();

        if (App.ConfigProblem is not null)
            Opened += OnShowConfigProblem;
    }

    private void OnShowConfigProblem(object? sender, EventArgs e)
    {
        Opened -= OnShowConfigProblem;
        if (App.ConfigProblem is not { } problem)
            return;

        var okButton = new Button
        {
            Content = App.Localization.Get("common.ok"),
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var dialog = new Window
        {
            Title = App.Localization.Get("config.brokenTitle"),
            Width = 420,
            Height = 200,
            Content = new StackPanel
            {
                Spacing = 12,
                Margin = new Thickness(20),
                Children =
                {
                    new TextBlock
                    {
                        Text = App.Localization.Format("config.brokenText", problem.Line, Path.GetFileName(problem.BrokenCopy)),
                        TextWrapping = TextWrapping.Wrap,
                    },
                    okButton,
                },
            },
        };
        okButton.Click += (_, _) => dialog.Close();
        dialog.ShowDialog(this);
    }

    private void ApplyTheme(Core.Models.AppTheme theme)
    {
        var request = theme switch
        {
            Core.Models.AppTheme.Light => ThemeVariant.Light,
            Core.Models.AppTheme.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
        RequestedThemeVariant = request;
    }

    private void ShowShell()
    {
        _shell = new ShellPage();
        ShowScreen(_shell);
        // Descargas y cuenta de GitHub a la derecha de la barra de título.
        TitleRightHost.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children = { _shell.DownloadsIndicator, new Controls.AccountIndicator() },
        };

        // Al arrancar se mira si hay versión nueva de AnchorPS5 (sin molestar si no la hay).
        var shell = _shell;
        Avalonia.Threading.DispatcherTimer.RunOnce(
            async () => await AppUpdateDialogs.CheckAsync(shell, interactive: false),
            TimeSpan.FromSeconds(4));
    }

    private void ShowSetup()
    {
        var firstRun = new FirstRunPage();
        var viewModel = new ViewModels.SetupViewModel(
            App.Localization, App.FirstRun, App.Config.Language, App.Config.DownloadPath, PickFolderAsync);

        // Cambiar el idioma en el selector cambia la pantalla al momento.
        viewModel.LanguageChanged += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            UpdateTitle();
            var translated = new FirstRunPage();
            translated.ShowStep(new SetupPage(viewModel), 0);
            ShowScreen(translated);
        });

        viewModel.Completed += (_, _) =>
        {
            // Las pantallas siguientes se crean ya en el idioma elegido.
            App.Localization.Load(App.Config.Language);
            UpdateTitle();
            // La pregunta de la guía vuelve con el tour; de momento, directo a la app (como en Windows).
            App.FirstRun.Complete();
            ShowShell();
        };

        firstRun.ShowStep(new SetupPage(viewModel), 0);
        ShowScreen(firstRun);
    }

    private void ShowScreen(Control screen)
    {
        if (!ReferenceEquals(screen, _shell))
        {
            _shell = null;
            TitleRightHost.Content = null;
        }

        ScreenHost.Content = screen;
    }

    private async Task<string?> PickFolderAsync()
    {
        var start = await StorageProvider.TryGetWellKnownFolderAsync(Avalonia.Platform.Storage.WellKnownFolder.Downloads);
        var folders = await StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            AllowMultiple = false,
            SuggestedStartLocation = start,
            Title = App.Localization.Get("setup.downloadFolder"),
        });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    private void UpdateTitle()
    {
        Title = App.Localization.Get("app.title");
    }
}
