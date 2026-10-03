using AnchorPS5.Core.Diagnostics;
using AnchorPS5.Core.Library;
using AnchorPS5.Core.Updates;
using AnchorPS5.App.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AnchorPS5.App.Views;

/// <summary>
/// Aviso de versión nueva de AnchorPS5. En Windows la app se actualiza sola (zip de la
/// release + script de PowerShell); esas releases son de Windows, así que en Linux solo se
/// avisa, se enseñan las novedades y se lleva a la página de la release.
/// </summary>
public static class AppUpdateDialogs
{
    private static bool _busy;

    /// <param name="interactive">
    /// Desde el botón "Buscar actualizaciones": también avisa si ya está al día o si no se ha
    /// podido comprobar. Al arrancar solo se muestra algo si hay versión nueva.
    /// </param>
    public static async Task CheckAsync(Control root, bool interactive)
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
                    await MessageAsync(root, Loc("update.check"), Loc("update.checkFailed"));
                return;
            }

            var update = AppUpdates.FindUpdate(releases, AppVersion.Parse(App.Version));
            if (update is null)
            {
                if (interactive)
                    await MessageAsync(root, Loc("update.check"), App.Localization.Format("update.upToDate", App.Version));
                return;
            }

            AppLog.Info($"Actualización disponible: {update.Version}");
            await OfferAsync(root, update);
        }
        catch (Exception ex)
        {
            AppLog.Error("Error buscando actualizaciones", ex);
            if (interactive)
                await MessageAsync(root, Loc("update.check"), Loc("update.checkFailed"));
        }
        finally
        {
            _busy = false;
        }
    }

    private static async Task OfferAsync(Control root, AppUpdate update)
    {
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(Text(App.Localization.Format("update.current", App.Version)));
        content.Children.Add(Text(Loc("update.linuxManual")));
        if (!string.IsNullOrWhiteSpace(update.Notes))
        {
            content.Children.Add(new TextBlock { Text = Loc("update.notesTitle"), FontWeight = FontWeight.SemiBold });
            content.Children.Add(new ScrollViewer
            {
                MaxHeight = 280,
                Content = new SelectableTextBlock { Text = update.Notes.Trim(), TextWrapping = TextWrapping.Wrap },
            });
        }

        var title = App.Localization.Format("update.availableTitle", update.Version.Raw);
        if (update.PageUrl is null)
        {
            await Dialogs.AskAsync(root, title, content, Loc("update.later"));
            return;
        }

        if (await Dialogs.AskAsync(root, title, content, Loc("update.viewOnGitHub"), cancel: Loc("update.later")) == Dialogs.Result.Primary)
            ShellOpen.Open(update.PageUrl);
    }

    private static Task MessageAsync(Control root, string title, string text) =>
        Dialogs.AskAsync(root, title, Text(text), Loc("common.ok"));

    private static TextBlock Text(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };

    private static string Loc(string key) => App.Localization.Get(key);
}
