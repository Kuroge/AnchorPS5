using AnchorPS5.Core.GitHub;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace AnchorPS5.App.Views;

/// <summary>Diálogos de iniciar y cerrar sesión en GitHub (configuración inicial, barra superior y avisos).</summary>
public static class GitHubAccountDialogs
{
    private static readonly Uri AuthorizedAppsUrl = new("https://github.com/settings/applications");

    /// <summary>Flujo de dispositivo: muestra el código, abre GitHub y espera la autorización.</summary>
    /// <returns>true si la sesión ha quedado iniciada.</returns>
    public static async Task<bool> SignInAsync(XamlRoot root, ElementTheme theme)
    {
        using var cancel = new CancellationTokenSource();
        var flow = new GitHubDeviceFlow(App.Http, App.GitHubClientId);
        GitHubDeviceCode? code = null;
        var signedIn = false;

        var codeText = new TextBlock
        {
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 32,
            FontWeight = FontWeights.SemiBold,
            CharacterSpacing = 150,
            HorizontalAlignment = HorizontalAlignment.Center,
            IsTextSelectionEnabled = true,
            Text = "····-····",
        };
        var codeCard = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(8),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            Child = codeText,
        };
        var openButton = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            IsEnabled = false,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new FontIcon { FontSize = 14, Glyph = "" },
                    new TextBlock { Text = Loc("github.copyAndOpen") },
                },
            },
        };
        var progress = new ProgressRing { Width = 18, Height = 18, IsActive = true };
        var statusText = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        var status = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { progress, statusText } };
        var retryButton = new Button { Content = Loc("github.newCode") };
        var error = new InfoBar { Severity = InfoBarSeverity.Error, IsClosable = false, ActionButton = retryButton };

        var content = new StackPanel
        {
            Width = 440,
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = Loc("github.signInIntro"), TextWrapping = TextWrapping.Wrap },
                codeCard,
                openButton,
                status,
                error,
            },
        };

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            RequestedTheme = theme,
            Title = Loc("github.signInTitle"),
            Content = content,
            CloseButtonText = Loc("action.cancel"),
            DefaultButton = ContentDialogButton.None,
        };

        void ShowError(GitHubSignInError reason)
        {
            status.Visibility = Visibility.Collapsed;
            openButton.IsEnabled = false;
            error.Message = Loc(reason switch
            {
                GitHubSignInError.Expired => "github.error.expired",
                GitHubSignInError.Denied => "github.error.denied",
                GitHubSignInError.Network => "github.error.network",
                _ => "github.error.appNotAllowed",
            });
            error.IsOpen = true;
        }

        async void Start()
        {
            code = null;
            codeText.Text = "····-····";
            openButton.IsEnabled = false;
            error.IsOpen = false;
            status.Visibility = Visibility.Visible;
            statusText.Text = Loc("github.requestingCode");

            try
            {
                if (App.GitHubClientId.Length == 0)
                {
                    ShowError(GitHubSignInError.AppNotAllowed);
                    return;
                }

                var (requested, requestError) = await flow.RequestCodeAsync(cancel.Token);
                if (requested is null)
                {
                    ShowError(requestError);
                    return;
                }

                code = requested;
                codeText.Text = requested.UserCode;
                openButton.IsEnabled = true;
                statusText.Text = Loc("github.waiting");

                var result = await flow.WaitForTokenAsync(requested, cancel.Token);
                if (!result.Succeeded)
                {
                    ShowError(result.Error);
                    return;
                }

                await App.GitHubSession.SignInAsync(result.Token!);
                signedIn = true;
                statusText.Text = Loc("github.signedInDone");
                dialog.Hide();
            }
            catch (OperationCanceledException)
            {
                // Cancelado por el usuario.
            }
        }

        openButton.Click += async (_, _) =>
        {
            if (code is null)
                return;

            var package = new DataPackage();
            package.SetText(code.UserCode);
            Clipboard.SetContent(package);
            await Launcher.LaunchUriAsync(new Uri(code.VerificationUri));
        };
        retryButton.Click += (_, _) => Start();
        dialog.Opened += (_, _) => Start();

        await dialog.ShowAsync();
        cancel.Cancel();
        return signedIn;
    }

    /// <summary>Pide confirmación y borra el token de este equipo.</summary>
    public static async Task SignOutAsync(XamlRoot root, ElementTheme theme)
    {
        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = Loc("github.signOutText"), TextWrapping = TextWrapping.Wrap },
                new HyperlinkButton { Content = Loc("github.manageApps"), NavigateUri = AuthorizedAppsUrl, Padding = new Thickness(0) },
            },
        };
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            RequestedTheme = theme,
            Title = Loc("github.signOutTitle"),
            Content = content,
            PrimaryButtonText = Loc("github.signOut"),
            CloseButtonText = Loc("action.cancel"),
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            App.GitHubSession.SignOut();
    }

    private static string Loc(string key) => App.Localization.Get(key);
}
