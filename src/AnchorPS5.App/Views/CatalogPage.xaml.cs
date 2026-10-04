using AnchorPS5.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace AnchorPS5.App.Views;

/// <summary>Recibe su <see cref="CatalogViewModel"/> como parámetro de navegación.</summary>
public sealed partial class CatalogPage : Page
{
    public CatalogPage()
    {
        InitializeComponent();
    }

    public CatalogViewModel ViewModel { get; private set; } = null!;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is CatalogViewModel viewModel && !ReferenceEquals(viewModel, ViewModel))
        {
            ViewModel = viewModel;
            ViewModel.ConfirmReload = ConfirmReloadAsync;
            Bindings.Update();
        }

        if (!ViewModel.IsLoaded && ViewModel.LoadCommand.CanExecute(null))
            ViewModel.LoadCommand.Execute(null);
    }

    private void OnItemClick(object sender, ItemClickEventArgs e) =>
        Frame.Navigate(typeof(AppDetailPage), e.ClickedItem, new DrillInNavigationTransitionInfo());

    // Al iniciar sesión, el armazón refresca el catálogo con el nuevo límite.
    private async void OnGitHubSignInClick(object sender, RoutedEventArgs e) =>
        await GitHubAccountDialogs.SignInAsync(XamlRoot, ActualTheme);

    /// <summary>
    /// Recargar sin sesión: avisa de que puede gastar el límite de GitHub y ofrece iniciar
    /// sesión, recargar igualmente o cancelar, con "no volver a mostrar".
    /// </summary>
    private async Task<bool> ConfirmReloadAsync(int requests)
    {
        var dontShowAgain = new CheckBox { Content = Loc("reload.dontShowAgain") };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            RequestedTheme = ActualTheme,
            Title = Loc("reload.title"),
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = App.Localization.Format("reload.text", requests), TextWrapping = TextWrapping.Wrap },
                    dontShowAgain,
                },
            },
            PrimaryButtonText = Loc("reload.confirm"),
            SecondaryButtonText = Loc("github.signIn"),
            CloseButtonText = Loc("action.cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };

        // Tres botones de texto largo: el ancho por defecto corta "Iniciar sesión con GitHub".
        dialog.Resources["ContentDialogMaxWidth"] = 980d;
        dialog.Resources["ContentDialogMinWidth"] = 760d;

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.None)
            return false;

        if (dontShowAgain.IsChecked == true)
            App.Warnings.HideReloadWarningFromNowOn();

        // "Iniciar sesión": tras iniciarla se recarga ya con el límite alto; si se cancela, no.
        return result == ContentDialogResult.Primary || await GitHubAccountDialogs.SignInAsync(XamlRoot, ActualTheme);
    }

    /// <summary>"Actualizar todo" de Actualizaciones: una sola pregunta para todas las apps.</summary>
    private async void OnUpdateAllAppsClick(object sender, RoutedEventArgs e)
    {
        var files = ViewModel.UpdatableItems.SelectMany(i => i.OutdatedFiles).ToList();
        var removeOld = await UpdateDialogs.AskRemoveOldAsync(XamlRoot, ActualTheme, CatalogItemViewModel.OldVersionCount(files));
        if (removeOld is { } remove)
            ViewModel.UpdateAllApps(remove);
    }

    private static string Loc(string key) => App.Localization.Get(key);
}
