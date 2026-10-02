using AnchorPS5.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace AnchorPS5.App.Views;

public sealed partial class SetupPage : Page
{
    public SetupPage(SetupViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Loaded += (_, _) =>
        {
            App.GitHubSession.Changed += OnSessionChanged;
            RefreshGitHub();
        };
        Unloaded += (_, _) => App.GitHubSession.Changed -= OnSessionChanged;
    }

    public SetupViewModel ViewModel { get; }

    private void OnSessionChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(RefreshGitHub);

    /// <summary>Tarjeta de GitHub: botón de iniciar sesión, o foto + usuario + cerrar sesión.</summary>
    private void RefreshGitHub()
    {
        var session = App.GitHubSession;
        var user = session.User;
        GitHubSignInButton.Visibility = session.IsSignedIn ? Visibility.Collapsed : Visibility.Visible;
        GitHubSignedInPanel.Visibility = session.IsSignedIn ? Visibility.Visible : Visibility.Collapsed;
        var picture = user?.AvatarUrl is { Length: > 0 } url ? new BitmapImage(new Uri(url)) : null;
        GitHubPicture.ProfilePicture = picture;
        // Las iniciales solo sin foto (si no, se ven por debajo).
        GitHubPicture.DisplayName = picture is null ? user?.Name ?? user?.Login ?? string.Empty : string.Empty;
        GitHubSignedInText.Text = user is not null
            ? App.Localization.Format("github.signedInAs", user.Login)
            : App.Localization.Get("github.account");
    }

    private async void OnGitHubSignInClick(object sender, RoutedEventArgs e) =>
        await GitHubAccountDialogs.SignInAsync(XamlRoot, ActualTheme);

    private async void OnGitHubSignOutClick(object sender, RoutedEventArgs e) =>
        await GitHubAccountDialogs.SignOutAsync(XamlRoot, ActualTheme);
}
