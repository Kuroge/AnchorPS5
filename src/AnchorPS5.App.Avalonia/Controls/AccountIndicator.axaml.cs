using AnchorPS5.App.Services;
using AnchorPS5.App.Views;
using AnchorPS5.Core.GitHub;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace AnchorPS5.App.Controls;

/// <summary>Cuenta de GitHub en la barra de título: foto, resumen del perfil y sesión.</summary>
public partial class AccountIndicator : UserControl
{
    private readonly GitHubSession _session = App.GitHubSession;

    public AccountIndicator()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) =>
        {
            _session.Changed += OnSessionChanged;
            Refresh();
        };
        DetachedFromVisualTree += (_, _) => _session.Changed -= OnSessionChanged;
    }

    private void OnSessionChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(Refresh);

    private void Refresh()
    {
        var user = _session.User;
        var signedIn = _session.IsSignedIn;
        var name = user?.Name ?? user?.Login;

        SmallPicture.Set(signedIn ? user?.AvatarUrl : null, signedIn ? name : null);
        LargePicture.Set(signedIn ? user?.AvatarUrl : null, signedIn ? name : null);

        DisplayNameText.Text = signedIn
            ? name ?? App.Localization.Get("github.account")
            : App.Localization.Get("github.signedOut");

        ProfileLink.IsVisible = user is not null;
        if (user is not null)
        {
            ProfileLink.Content = "@" + user.Login;
            ProfileLink.Tag = user.HtmlUrl;
        }

        DetailText.Text = signedIn ? user?.Bio ?? string.Empty : App.Localization.Get("github.signedOutDescription");
        DetailText.IsVisible = DetailText.Text.Length > 0;
        SignInButton.IsVisible = !signedIn;
        SignOutButton.IsVisible = signedIn;
        ToolTip.SetTip(AccountButton, signedIn && user is not null
            ? App.Localization.Format("github.signedInAs", user.Login)
            : App.Localization.Get("github.account"));
    }

    private void OnProfileClick(object? sender, RoutedEventArgs e)
    {
        if (ProfileLink.Tag is string { Length: > 0 } url)
            ShellOpen.Open(url);
    }

    private async void OnSignInClick(object? sender, RoutedEventArgs e)
    {
        AccountButton.Flyout?.Hide();
        await GitHubAccountDialogs.SignInAsync(this);
    }

    private async void OnSignOutClick(object? sender, RoutedEventArgs e)
    {
        AccountButton.Flyout?.Hide();
        await GitHubAccountDialogs.SignOutAsync(this);
    }
}
