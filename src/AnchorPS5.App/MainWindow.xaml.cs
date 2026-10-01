using AnchorPS5.App.ViewModels;
using AnchorPS5.App.Views;
using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage.Pickers;

namespace AnchorPS5.App;

/// <summary>
/// Ventana única. Primer arranque: setup → pregunta de la guía → (guía) → app.
/// Arranques siguientes: directo a la app.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        UpdateTitle();

        if (App.FirstRun.IsRequired)
            ShowSetup();
        else
            ShowShell();
    }

    private void ShowSetup()
    {
        var viewModel = new SetupViewModel(
            App.Localization, App.FirstRun, App.Config.Language, App.Config.DownloadPath, PickFolderAsync);

        viewModel.Completed += (_, _) =>
        {
            // Las pantallas siguientes se crean ya en el idioma elegido.
            App.Localization.Load(App.Config.Language);
            UpdateTitle();
            ShowIntroQuestion();
        };

        Content = new SetupPage(viewModel);
    }

    private void ShowIntroQuestion()
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

        Content = page;
    }

    private void ShowIntro()
    {
        var page = new IntroPage();
        page.Closed += (_, _) => ShowShell();
        Content = page;
    }

    private void ShowShell() => Content = new ShellPage();

    private void UpdateTitle() => Title = App.Localization.Get("app.title");

    private async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker(AppWindow.Id);
        var result = await picker.PickSingleFolderAsync();
        return result?.Path;
    }
}
