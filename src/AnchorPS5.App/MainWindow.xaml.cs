using System.Runtime.InteropServices;
using AnchorPS5.App.Controls;
using AnchorPS5.App.ViewModels;
using AnchorPS5.App.Views;
using AnchorPS5.Core.Models;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.Storage.Pickers;
using Windows.Graphics;

namespace AnchorPS5.App;

/// <summary>
/// Ventana única. Primer arranque: setup → pregunta de la guía → (guía) → app.
/// Arranques siguientes: directo a la app.
/// </summary>
public sealed partial class MainWindow : Window
{
    // Tamaños en píxeles lógicos (a 100 % de escala).
    private const int DefaultWindowWidth = 1180, DefaultWindowHeight = 780;
    private const int MinWindowWidth = 720, MinWindowHeight = 540;

    private ShellPage? _shell;
    private ColumnDefinition? _rightPaddingColumn;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureWindow();
        ApplyTheme(App.Config.Theme);
        UpdateTitle();

        if (App.FirstRun.IsRequired)
            ShowSetup();
        else
            ShowShell();

        if (App.ConfigProblem is not null)
            Root.Loaded += OnRootLoadedShowConfigProblem;
    }

    /// <summary>config.json estaba mal escrito: se avisa de dónde quedó el original.</summary>
    private async void OnRootLoadedShowConfigProblem(object sender, RoutedEventArgs e)
    {
        Root.Loaded -= OnRootLoadedShowConfigProblem;
        if (App.ConfigProblem is not { } problem)
            return;

        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            RequestedTheme = Root.ActualTheme,
            Title = App.Localization.Get("config.brokenTitle"),
            Content = new TextBlock
            {
                Text = App.Localization.Format("config.brokenText", problem.Line, Path.GetFileName(problem.BrokenCopy)),
                TextWrapping = TextWrapping.Wrap,
            },
            CloseButtonText = App.Localization.Get("common.ok"),
        };
        await dialog.ShowAsync();
    }

    private void ConfigureWindow()
    {
        // Contenido bajo la barra de título, con el control TitleBar de WinUI.
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppTitleBar.LayoutUpdated += (_, _) => FixTitleBarRightPadding();

        // Tamaño inicial escalado según los PPP, centrado y sin salirse de la pantalla.
        var scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        var work = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Min((int)(DefaultWindowWidth * scale), work.Width);
        var height = Math.Min((int)(DefaultWindowHeight * scale), work.Height);
        AppWindow.MoveAndResize(new RectInt32(
            work.X + (work.Width - width) / 2,
            work.Y + (work.Height - height) / 2,
            width,
            height));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = (int)(MinWindowWidth * scale);
            presenter.PreferredMinimumHeight = (int)(MinWindowHeight * scale);
        }

        var icon = Path.Combine(AppContext.BaseDirectory, "Assets", "AnchorPS5.ico");
        if (File.Exists(icon))
            AppWindow.SetIcon(icon);
    }

    private void ApplyTheme(AppTheme theme)
    {
        Root.RequestedTheme = theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }

    private void ShowSetup()
    {
        var firstRun = new FirstRunPage();
        var viewModel = new SetupViewModel(
            App.Localization, App.FirstRun, App.Config.Language, App.Config.DownloadPath, PickFolderAsync);

        // Cambiar el idioma en el selector cambia la pantalla al momento.
        viewModel.LanguageChanged += (_, _) => DispatcherQueue.TryEnqueue(() =>
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
            // La pregunta de la guía vuelve con el tour; de momento, directo a la app.
            App.FirstRun.Complete();
            ShowShell();
        };

        firstRun.ShowStep(new SetupPage(viewModel), 0);
        ShowScreen(firstRun);
    }

    private void ShowIntroQuestion(FirstRunPage firstRun)
    {
        var page = new IntroQuestionPage();
        page.Answered += (_, showIntro) =>
        {
            App.FirstRun.Complete();
            if (showIntro)
                ShowIntro();
            else
                ShowShell();
        };

        firstRun.ShowStep(page, 1);
    }

    private void ShowIntro()
    {
        var page = new IntroPage();
        page.Closed += (_, _) => ShowShell();
        ShowScreen(page);
    }

    private void ShowShell()
    {
        _shell = new ShellPage();
        // El botón atrás solo se ve cuando hay a dónde volver.
        _shell.HomeAvailableChanged += (_, available) =>
            HomeButton.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
        HomeButton.Visibility = Visibility.Collapsed;
        AppTitleBar.IsPaneToggleButtonVisible = true;
        // Descargas y cuenta de GitHub a la derecha del todo, pegados a los botones de la ventana.
        AppTitleBar.RightHeader = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { _shell.DownloadsIndicator, new AccountIndicator() },
        };

        // Al arrancar se mira si hay versión nueva de AnchorPS5 (sin molestar si no la hay).
        var shell = _shell;
        shell.Loaded += async (_, _) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(4));
            if (shell.XamlRoot is { } root)
                await AppUpdateDialogs.CheckAsync(root, Root.ActualTheme, interactive: false);
        };
        ShowScreen(_shell);
    }

    private void ShowScreen(UIElement screen)
    {
        if (!ReferenceEquals(screen, _shell))
        {
            _shell = null;
            HomeButton.Visibility = Visibility.Collapsed;
            AppTitleBar.IsPaneToggleButtonVisible = false;
            AppTitleBar.RightHeader = null;
        }

        ScreenHost.Content = screen;
    }

    /// <summary>
    /// El TitleBar reserva el hueco de los botones de la ventana en píxeles físicos sin
    /// aplicar la escala (a 150 % deja 69 px de más). Se corrige la columna a su ancho real.
    /// </summary>
    private void FixTitleBarRightPadding()
    {
        if (AppTitleBar.XamlRoot is not { RasterizationScale: > 0 } root)
            return;

        _rightPaddingColumn ??= VisualTreeHelper.GetChildrenCount(AppTitleBar) > 0
            && VisualTreeHelper.GetChild(AppTitleBar, 0) is FrameworkElement templateRoot
                ? templateRoot.FindName("RightPaddingColumn") as ColumnDefinition
                : null;
        if (_rightPaddingColumn is null)
            return;

        // Al cerrarse la ventana (p. ej. al actualizar) ya no hay AppWindow.
        if (AppWindow?.TitleBar is not { } titleBar)
            return;

        var width = titleBar.RightInset / root.RasterizationScale;
        if (Math.Abs(_rightPaddingColumn.Width.Value - width) > 0.5)
            _rightPaddingColumn.Width = new GridLength(width);
    }

    private void OnHomeClick(object sender, RoutedEventArgs e) => _shell?.GoHome();

    private void OnTitleBarPaneToggleRequested(TitleBar sender, object args) => _shell?.TogglePane();

    private void UpdateTitle()
    {
        Title = App.Localization.Get("app.title");
        AppTitleBar.Title = Title;
        AppTitleBar.Subtitle = App.Version;
    }

    private async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker(AppWindow.Id) { SuggestedStartLocation = PickerLocationId.Downloads };
        var result = await picker.PickSingleFolderAsync();
        return result?.Path;
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
