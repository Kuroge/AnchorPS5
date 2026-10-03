using AnchorPS5.App.Services;
using AnchorPS5.Core.GitHub;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AnchorPS5.App.Views;

/// <summary>Diálogos de iniciar y cerrar sesión en GitHub (configuración inicial, barra superior y avisos).</summary>
public static class GitHubAccountDialogs
{
    private const string AuthorizedAppsUrl = "https://github.com/settings/applications";

    /// <summary>Flujo de dispositivo: muestra el código, abre GitHub y espera la autorización.</summary>
    /// <returns>true si la sesión ha quedado iniciada.</returns>
    public static async Task<bool> SignInAsync(Control root)
    {
        using var cancel = new CancellationTokenSource();
        var flow = new GitHubDeviceFlow(App.Http, App.GitHubClientId);
        GitHubDeviceCode? code = null;
        var signedIn = false;

        var codeText = new SelectableTextBlock
        {
            FontFamily = new FontFamily("monospace"),
            FontSize = 32,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 4,
            HorizontalAlignment = HorizontalAlignment.Center,
            Text = "····-····",
        };
        var codeCard = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Child = codeText,
        };
        codeCard.Bind(Border.BackgroundProperty, codeCard.GetResourceObservable("CardBackgroundFillColorDefaultBrush"));
        codeCard.Bind(Border.BorderBrushProperty, codeCard.GetResourceObservable("CardStrokeColorDefaultBrush"));

        var openButton = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            IsEnabled = false,
            Content = Loc("github.copyAndOpen"),
        };
        openButton.Classes.Add("accent");
        var progress = new ProgressBar { IsIndeterminate = true, Width = 60, VerticalAlignment = VerticalAlignment.Center };
        var statusText = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        var status = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { progress, statusText } };
        var errorText = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        var retryButton = new Button { Content = Loc("github.newCode") };
        var error = new Border
        {
            IsVisible = false,
            Padding = new Thickness(12, 8),
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.Parse("#33C42B1C")),
            Child = new DockPanel { Children = { retryButton, errorText } },
        };
        DockPanel.SetDock(retryButton, Dock.Right);
        retryButton.Margin = new Thickness(12, 0, 0, 0);

        var cancelButton = new Button { Content = Loc("action.cancel"), HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 90 };
        var window = new Window
        {
            Title = Loc("github.signInTitle"),
            Width = 480,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = Loc("github.signInIntro"), TextWrapping = TextWrapping.Wrap },
                    codeCard,
                    openButton,
                    status,
                    error,
                    cancelButton,
                },
            },
        };
        cancelButton.Click += (_, _) => window.Close();

        void ShowError(GitHubSignInError reason)
        {
            status.IsVisible = false;
            openButton.IsEnabled = false;
            errorText.Text = Loc(reason switch
            {
                GitHubSignInError.Expired => "github.error.expired",
                GitHubSignInError.Denied => "github.error.denied",
                GitHubSignInError.Network => "github.error.network",
                _ => "github.error.appNotAllowed",
            });
            error.IsVisible = true;
        }

        async void Start()
        {
            code = null;
            codeText.Text = "····-····";
            openButton.IsEnabled = false;
            error.IsVisible = false;
            status.IsVisible = true;
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
                window.Close();
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

            if (TopLevel.GetTopLevel(window)?.Clipboard is { } clipboard)
                await clipboard.SetTextAsync(code.UserCode);
            ShellOpen.Open(code.VerificationUri);
        };
        retryButton.Click += (_, _) => Start();
        window.Opened += (_, _) => Start();

        await window.ShowDialog(Dialogs.OwnerOf(root));
        cancel.Cancel();
        return signedIn;
    }

    /// <summary>Pide confirmación y borra el token de este equipo.</summary>
    public static async Task SignOutAsync(Control root)
    {
        var manage = new Button
        {
            Content = Loc("github.manageApps"),
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };
        manage.Bind(Button.ForegroundProperty, manage.GetResourceObservable("AnchorBlueBrush"));
        manage.Click += (_, _) => ShellOpen.Open(AuthorizedAppsUrl);

        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = Loc("github.signOutText"), TextWrapping = TextWrapping.Wrap },
                manage,
            },
        };

        if (await Dialogs.AskAsync(root, Loc("github.signOutTitle"), content, Loc("github.signOut"), cancel: Loc("action.cancel")) == Dialogs.Result.Primary)
            App.GitHubSession.SignOut();
    }

    private static string Loc(string key) => App.Localization.Get(key);
}
