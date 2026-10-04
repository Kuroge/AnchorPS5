using AnchorPS5.App.ViewModels;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace AnchorPS5.App.Views;

/// <summary>Recibe su <see cref="CatalogViewModel"/> por parámetro y muestra la cuadrícula.</summary>
public partial class CatalogPage : UserControl
{
    public CatalogPage(CatalogViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        // El XAML se enlaza a "ViewModel.*": sin esto ningún binding resuelve y todos los IsVisible quedan en true.
        DataContext = this;
        ViewModel.ConfirmReload = ConfirmReloadAsync;
        if (!ViewModel.IsLoaded && ViewModel.LoadCommand.CanExecute(null))
            ViewModel.LoadCommand.Execute(null);
    }

    public CatalogViewModel ViewModel { get; }

    public void EnsureLoaded()
    {
        if (!ViewModel.IsLoaded && ViewModel.LoadCommand.CanExecute(null))
            ViewModel.LoadCommand.Execute(null);
    }

    private void OnCardPointerOver(object? sender, PointerEventArgs e)
    {
        if (sender is Button b)
            b.Opacity = 0.92;
    }

    private void OnCardPointerLeave(object? sender, PointerEventArgs e)
    {
        if (sender is Button b)
            b.Opacity = 1.0;
    }

    private void OnCardClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CatalogItemViewModel item })
            ShellPage.Current?.ShowDetail(item);
    }

    private void OnLearnMoreClick(object? sender, RoutedEventArgs e)
        => AnchorPS5.App.Services.ShellOpen.Open("https://docs.github.com/rest/using-the-rest-api/rate-limits-for-the-rest-api");

    // Al iniciar sesión, el armazón refresca el catálogo con el nuevo límite.
    private async void OnGitHubSignInClick(object? sender, RoutedEventArgs e) =>
        await Dialogs.GitHubSignInAsync(this);

    /// <summary>
    /// Recargar sin sesión: avisa de que puede gastar el límite de GitHub y ofrece iniciar
    /// sesión, recargar igualmente o cancelar, con "no volver a mostrar".
    /// </summary>
    private async Task<bool> ConfirmReloadAsync(int requests)
    {
        var dontShowAgain = new CheckBox
        {
            Content = App.Localization.Get("reload.dontShowAgain"),
        };
        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = App.Localization.Format("reload.text", requests), TextWrapping = TextWrapping.Wrap },
                dontShowAgain,
            },
        };

        var primary = App.Localization.Get("reload.confirm");
        var secondary = App.Localization.Get("github.signIn");
        var cancel = App.Localization.Get("action.cancel");

        var result = await Dialogs.AskAsync(this, App.Localization.Get("reload.title"), content, primary, secondary, cancel);
        if (result == Dialogs.Result.None)
            return false;

        if (dontShowAgain.IsChecked == true)
            App.Warnings.HideReloadWarningFromNowOn();

        return result == Dialogs.Result.Primary || await Dialogs.GitHubSignInAsync(this);
    }
}
