using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AnchorPS5.App.Views;

/// <summary>Pregunta común a todas las formas de actualizar: borrar o conservar las versiones anteriores.</summary>
public static class UpdateDialogs
{
    /// <summary>
    /// null = cancelar; true = borrar las anteriores (tras descargar y comprobar la nueva);
    /// false = conservarlas. Sin versiones anteriores, o con "no volver a preguntar", no pregunta y conserva.
    /// </summary>
    public static async Task<bool?> AskRemoveOldAsync(XamlRoot root, ElementTheme theme, int oldVersions)
    {
        if (oldVersions == 0 || App.Warnings.KeepOldVersionsWithoutAsking)
            return false;

        var dontAskAgain = new CheckBox { Content = Loc("update.dontAskAgain") };
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            RequestedTheme = theme,
            Title = Loc("update.removeOldTitle"),
            Content = dontAskAgain,
            PrimaryButtonText = Loc("update.removeOld"),
            SecondaryButtonText = Loc("update.keepOld"),
            CloseButtonText = Loc("action.cancel"),
            DefaultButton = ContentDialogButton.Secondary,
        };

        // Tres botones: el ancho por defecto los corta.
        dialog.Resources["ContentDialogMaxWidth"] = 720d;
        dialog.Resources["ContentDialogMinWidth"] = 560d;

        // "No volver a preguntar" = conservar siempre: borrar deja de tener sentido.
        dontAskAgain.Checked += (_, _) => dialog.IsPrimaryButtonEnabled = false;
        dontAskAgain.Unchecked += (_, _) => dialog.IsPrimaryButtonEnabled = true;

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.None)
            return null;

        if (dontAskAgain.IsChecked == true)
            App.Warnings.KeepOldVersionsFromNowOn();

        return result == ContentDialogResult.Primary;
    }

    private static string Loc(string key) => App.Localization.Get(key);
}
