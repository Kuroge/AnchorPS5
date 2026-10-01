using AnchorPS5.App.ViewModels;
using AnchorPS5.Core.Library;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace AnchorPS5.App.Views;

/// <summary>Detalle de una app; recibe su <see cref="CatalogItemViewModel"/> al navegar.</summary>
public sealed partial class AppDetailPage : Page
{
    private const string DownloadGlyph = "\uE896";
    private const string WarningGlyph = "\uE7BA";

    public AppDetailPage()
    {
        InitializeComponent();
    }

    public CatalogItemViewModel Item { get; private set; } = null!;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        Item = (CatalogItemViewModel)e.Parameter;
        Bindings.Update();
    }

    /// <summary>Menú "Descargar ▾": una opción por fichero publicado (PS4 y beta indicados).</summary>
    private void OnDownloadMenuOpening(object? sender, object e)
    {
        DownloadMenu.Items.Clear();
        foreach (var file in Item.DownloadableFiles)
        {
            var option = new MenuFlyoutItem
            {
                Text = file.MenuText,
                Icon = new FontIcon { Glyph = file.IsBeta ? WarningGlyph : DownloadGlyph },
            };
            if (file.IsBeta && Application.Current.Resources.TryGetValue("BetaBrush", out var beta))
                option.Foreground = (Brush)beta;
            ToolTipService.SetToolTip(option, file.HasDescription ? file.Description : file.FileName);
            option.Click += (_, _) => Item.DownloadFile(file);
            DownloadMenu.Items.Add(option);
        }
    }

    // ---- Borrar ----

    /// <summary>"Eliminar todas" de la app: si algún fichero tiene varias versiones, ofrece conservar la última de cada uno.</summary>
    private async void OnDeleteAllClick(object sender, RoutedEventArgs e)
    {
        var all = Item.Versions;
        var extra = LibrarySnapshot.AllButLatestPerFile(all);
        var choice = await AskAsync(
            Loc("dialog.deleteAppTitle", Item.Name),
            extra.Count > 0 ? Loc("dialog.deleteAppMany") : Loc("dialog.deleteAppText"),
            Loc("action.deleteAllConfirm"),
            extra.Count > 0 ? Loc("action.deleteAllButLatestPerFile") : null);

        if (choice == ContentDialogResult.Primary)
            Item.DeleteVersions(all);
        else if (choice == ContentDialogResult.Secondary)
            Item.DeleteVersions(extra);
    }

    /// <summary>Borrar desde la ficha de un fichero: con varias versiones, aviso con las tres opciones.</summary>
    private async void OnDeleteFileClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not PackageFileViewModel file || !file.HasVersions)
            return;

        if (!file.HasManyVersions)
        {
            var single = file.Versions[0];
            if (await AskAsync(Loc("dialog.deleteVersionTitle", single.VersionText, file.FileName), Loc("dialog.deleteVersionText"), Loc("action.confirmDelete")) == ContentDialogResult.Primary)
                single.Delete();
            return;
        }

        var versions = string.Join(", ", file.Versions.Select(v => v.VersionText));
        var choice = await AskAsync(
            Loc("dialog.manyVersionsTitle"),
            Loc("dialog.manyVersionsFile", file.Versions.Count, file.FileName, versions),
            Loc("action.deleteAllConfirm"),
            Loc("action.deleteAllButLatest"));

        if (choice == ContentDialogResult.Primary)
            Item.DeleteVersions(file.InstalledVersions);
        else if (choice == ContentDialogResult.Secondary)
            Item.DeleteVersions(file.InstalledVersions.Skip(1).ToList()); // ya vienen de la más nueva a la más antigua
    }

    /// <summary>Borrar una versión concreta desde el histórico.</summary>
    private async void OnDeleteVersionClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not FileVersionViewModel version)
            return;

        var file = Item.Files.FirstOrDefault(f => f.Versions.Contains(version));
        if (await AskAsync(Loc("dialog.deleteVersionTitle", version.VersionText, file?.FileName ?? Item.Name), Loc("dialog.deleteVersionText"), Loc("action.confirmDelete")) == ContentDialogResult.Primary)
            version.Delete();
    }

    /// <summary>Confirmación con "Cancelar" siempre como opción de cierre.</summary>
    private async Task<ContentDialogResult> AskAsync(string title, string text, string primary, string? secondary = null)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            RequestedTheme = ActualTheme,
            Title = title,
            Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = primary,
            CloseButtonText = Loc("action.cancel"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (secondary is not null)
        {
            dialog.SecondaryButtonText = secondary;
            // Con tres botones de texto largo, el ancho por defecto (548) los corta: los tres
            // botones se reparten el ancho a partes iguales, así que se fija uno suficiente.
            dialog.Resources["ContentDialogMaxWidth"] = 980d;
            dialog.Resources["ContentDialogMinWidth"] = 920d;
        }

        return await dialog.ShowAsync();
    }

    private static string Loc(string key, params object?[] args) =>
        args.Length == 0 ? App.Localization.Get(key) : App.Localization.Format(key, args);
}
