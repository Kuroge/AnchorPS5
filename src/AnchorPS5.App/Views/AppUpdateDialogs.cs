using System.Diagnostics;
using System.Text;
using AnchorPS5.Core.Diagnostics;
using AnchorPS5.Core.Library;
using AnchorPS5.Core.Updates;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AnchorPS5.App.Views;

/// <summary>Aviso de versión nueva de AnchorPS5, descarga y "Actualizar ahora".</summary>
public static class AppUpdateDialogs
{
    private static bool _busy;

    /// <param name="interactive">
    /// Desde el botón "Buscar actualizaciones": también avisa si ya está al día o si no se ha
    /// podido comprobar. Al arrancar solo se muestra algo si hay versión nueva.
    /// </param>
    public static async Task CheckAsync(XamlRoot root, ElementTheme theme, bool interactive)
    {
        if (_busy)
            return;

        _busy = true;
        try
        {
            var releases = await App.GetAppReleasesAsync(forceRefresh: interactive);
            if (releases is null)
            {
                if (interactive)
                    await MessageAsync(root, theme, Loc("update.check"), Loc("update.checkFailed"));
                return;
            }

            var update = AppUpdates.FindUpdate(releases, AppVersion.Parse(App.Version));
            if (update is null)
            {
                if (interactive)
                    await MessageAsync(root, theme, Loc("update.check"), App.Localization.Format("update.upToDate", App.Version));
                return;
            }

            AppLog.Info($"Actualización disponible: {update.Version}");
            await OfferAsync(root, theme, update);
        }
        finally
        {
            _busy = false;
        }
    }

    private static async Task OfferAsync(XamlRoot root, ElementTheme theme, AppUpdate update)
    {
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock { Text = App.Localization.Format("update.current", App.Version), TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(update.Notes))
        {
            content.Children.Add(new TextBlock { Text = Loc("update.notesTitle"), Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"] });
            content.Children.Add(new ScrollViewer
            {
                MaxHeight = 280,
                Content = new TextBlock { Text = update.Notes.Trim(), TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true },
            });
        }

        var dialog = NewDialog(root, theme, App.Localization.Format("update.availableTitle", update.Version.Raw), content);
        dialog.PrimaryButtonText = Loc("update.now");
        dialog.CloseButtonText = Loc("update.later");
        dialog.DefaultButton = ContentDialogButton.Primary;
        if (update.PageUrl is not null)
            dialog.SecondaryButtonText = Loc("update.viewOnGitHub");

        switch (await ShowAsync(dialog))
        {
            case ContentDialogResult.Primary:
                await InstallAsync(root, theme, update);
                break;
            case ContentDialogResult.Secondary:
                OpenUrl(update.PageUrl);
                break;
        }
    }

    private static async Task InstallAsync(XamlRoot root, ElementTheme theme, AppUpdate update)
    {
        var installDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        if (!AppUpdates.CanWrite(installDir))
        {
            await FailAsync(root, theme, update, App.Localization.Format("update.notWritable", installDir));
            return;
        }

        var status = new TextBlock { Text = App.Localization.Format("update.downloading", 0), TextWrapping = TextWrapping.Wrap };
        var bar = new ProgressBar { Maximum = 1 };
        var progressDialog = NewDialog(root, theme, Loc("update.title"), new StackPanel { Spacing = 12, Width = 420, Children = { status, bar } });
        _ = progressDialog.ShowAsync();

        var staging = Path.Combine(Path.GetTempPath(), "AnchorPS5-update");
        var progress = new Progress<double>(value =>
        {
            bar.Value = value;
            status.Text = App.Localization.Format("update.downloading", (int)(value * 100));
        });

        string appDir;
        try
        {
            appDir = await AppUpdates.DownloadAndStageAsync(App.DownloadHttp, update, staging, progress);
        }
        catch (AppUpdateException ex)
        {
            AppLog.Error($"Actualización a {update.Version} fallida ({ex.Error})", ex);
            progressDialog.Hide();
            await FailAsync(root, theme, update, Loc(ex.Error switch
            {
                AppUpdateError.Network => "update.error.network",
                AppUpdateError.Verification => "update.error.verification",
                AppUpdateError.Disk => "update.error.disk",
                _ => "update.error.package",
            }));
            return;
        }

        status.Text = Loc("update.installing");
        bar.IsIndeterminate = true;

        var script = Path.Combine(staging, "actualizar.ps1");
        var log = AppLog.Directory is { } logs ? Path.Combine(logs, $"anchorps5-{DateTime.Now:yyyy-MM-dd}.log") : null;
        await File.WriteAllTextAsync(
            script,
            AppUpdates.CreateInstallScript(Environment.ProcessId, appDir, installDir, staging, log),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        AppLog.Info($"Instalando la actualización {update.Version}: la app se cierra y el actualizador copia los ficheros");
        Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{script}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        });

        await Task.Delay(1200);
        Application.Current.Exit();
    }

    private static async Task FailAsync(XamlRoot root, ElementTheme theme, AppUpdate update, string reason)
    {
        var dialog = NewDialog(
            root,
            theme,
            Loc("update.failedTitle"),
            new TextBlock { Text = App.Localization.Format("update.failedText", reason), TextWrapping = TextWrapping.Wrap });
        dialog.CloseButtonText = Loc("common.ok");
        if (update.PageUrl is not null)
            dialog.PrimaryButtonText = Loc("update.viewOnGitHub");

        if (await ShowAsync(dialog) == ContentDialogResult.Primary)
            OpenUrl(update.PageUrl);
    }

    private static async Task MessageAsync(XamlRoot root, ElementTheme theme, string title, string text)
    {
        var dialog = NewDialog(root, theme, title, new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });
        dialog.CloseButtonText = Loc("common.ok");
        await ShowAsync(dialog);
    }

    private static ContentDialog NewDialog(XamlRoot root, ElementTheme theme, string title, object content) => new()
    {
        XamlRoot = root,
        Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
        RequestedTheme = theme,
        Title = title,
        Content = content,
    };

    /// <summary>Si ya hay otro diálogo abierto, no se muestra (se volverá a ofrecer en el próximo arranque).</summary>
    private static async Task<ContentDialogResult> ShowAsync(ContentDialog dialog)
    {
        try
        {
            return await dialog.ShowAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return ContentDialogResult.None;
        }
    }

    private static void OpenUrl(string? url)
    {
        if (url is not null)
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static string Loc(string key) => App.Localization.Get(key);
}
