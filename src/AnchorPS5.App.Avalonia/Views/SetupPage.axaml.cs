using AnchorPS5.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace AnchorPS5.App.Views;

public partial class SetupPage : UserControl
{
    public SetupPage(SetupViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = this; // el XAML se enlaza a "ViewModel.*"
        AttachedToVisualTree += (_, _) =>
        {
            App.GitHubSession.Changed += OnSessionChanged;
            RefreshGitHub();
        };
        DetachedFromVisualTree += (_, _) => App.GitHubSession.Changed -= OnSessionChanged;
    }

    public SetupViewModel ViewModel { get; }

    private void OnSessionChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(RefreshGitHub);

    /// <summary>Tarjeta de GitHub: botón de iniciar sesión, o foto + usuario + cerrar sesión.</summary>
    private void RefreshGitHub()
    {
        var session = App.GitHubSession;
        var user = session.User;
        GitHubSignInButton.IsVisible = !session.IsSignedIn;
        GitHubSignedInPanel.IsVisible = session.IsSignedIn;
        GitHubPicture.Set(user?.AvatarUrl, user?.Name ?? user?.Login);
        GitHubSignedInText.Text = user is not null
            ? App.Localization.Format("github.signedInAs", user.Login)
            : App.Localization.Get("github.account");
    }

    private async void OnGitHubSignInClick(object? sender, RoutedEventArgs e) =>
        await GitHubAccountDialogs.SignInAsync(this);

    private async void OnGitHubSignOutClick(object? sender, RoutedEventArgs e) =>
        await GitHubAccountDialogs.SignOutAsync(this);
}
