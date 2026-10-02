using AnchorPS5.App.Views;
using AnchorPS5.Core.GitHub;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace AnchorPS5.App.Controls;

/// <summary>Cuenta de GitHub en la barra de título: foto, resumen del perfil y sesión.</summary>
public sealed partial class AccountIndicator : UserControl
{
    private readonly GitHubSession _session = App.GitHubSession;

    public AccountIndicator()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _session.Changed += OnSessionChanged;
            Refresh();
        };
        Unloaded += (_, _) => _session.Changed -= OnSessionChanged;
    }

    private void OnSessionChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(Refresh);

    private void Refresh()
    {
        var user = _session.User;
        var signedIn = _session.IsSignedIn;
        var picture = user?.AvatarUrl is { Length: > 0 } url ? new BitmapImage(new Uri(url)) : null;

        SmallPicture.ProfilePicture = picture;
        LargePicture.ProfilePicture = picture;
        // PersonPicture pinta las iniciales debajo de la foto y se transparentan: solo sin foto.
        SmallPicture.DisplayName = LargePicture.DisplayName = picture is null ? user?.Name ?? user?.Login ?? string.Empty : string.Empty;

        DisplayNameText.Text = signedIn
            ? user?.Name ?? user?.Login ?? App.Localization.Get("github.account")
            : App.Localization.Get("github.signedOut");

        ProfileLink.Visibility = user is not null ? Visibility.Visible : Visibility.Collapsed;
        if (user is not null)
        {
            ProfileLink.Content = "@" + user.Login;
            ProfileLink.NavigateUri = user.HtmlUrl is { Length: > 0 } profile ? new Uri(profile) : null;
        }

        DetailText.Text = signedIn ? user?.Bio ?? string.Empty : App.Localization.Get("github.signedOutDescription");
        DetailText.Visibility = DetailText.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        SignInButton.Visibility = signedIn ? Visibility.Collapsed : Visibility.Visible;
        SignOutButton.Visibility = signedIn ? Visibility.Visible : Visibility.Collapsed;
        ToolTipService.SetToolTip(AccountButton, signedIn && user is not null
            ? App.Localization.Format("github.signedInAs", user.Login)
            : App.Localization.Get("github.account"));
    }

    private async void OnSignInClick(object sender, RoutedEventArgs e)
    {
        AccountFlyout.Hide();
        await GitHubAccountDialogs.SignInAsync(XamlRoot, ActualTheme);
    }

    private async void OnSignOutClick(object sender, RoutedEventArgs e)
    {
        AccountFlyout.Hide();
        await GitHubAccountDialogs.SignOutAsync(XamlRoot, ActualTheme);
    }
}
