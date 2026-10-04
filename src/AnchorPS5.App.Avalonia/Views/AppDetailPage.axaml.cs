using AnchorPS5.Core.Packages;
using AnchorPS5.App.ViewModels;
using AnchorPS5.Core.Library;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace AnchorPS5.App.Views;

/// <summary>Detalle de una app; recibe su <see cref="CatalogItemViewModel"/> al construirse.</summary>
public partial class AppDetailPage : UserControl
{
    public AppDetailPage(CatalogItemViewModel item)
    {
        Item = item;
        InitializeComponent();
        DataContext = this; // el XAML se enlaza a "Item.*"
    }

    public CatalogItemViewModel Item { get; }

    /// <summary>Menú "Descargar ▾": una opción por fichero.</summary>
    private async void OnDownloadMenuClick(object? sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        foreach (var file in Item.DownloadableFiles)
        {
            var header = file.IsUpToDate ? "✓  " : "⬇  ";
            var button = new Button
            {
                Content = header + file.MenuText,
                IsEnabled = !file.IsUpToDate && !file.IsBusy,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(8, 6, 8, 6),
                Background = Avalonia.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };
            var f = file;
            button.Click += async (_, _) =>
            {
                menu.Close();
                await DownloadWithWarningAsync(f);
            };
            menu.Items.Add(button);
        }
        if (menu.Items.Count > 0)
            menu.Open(sender as Control ?? this);
    }

    private async void OnFileDownloadClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is PackageFileViewModel file)
            await DownloadWithWarningAsync(file);
    }

    /// <summary>Descargar una beta por primera vez (fichero solo-beta) pide confirmación.</summary>
    private async Task DownloadWithWarningAsync(PackageFileViewModel file)
    {
        if (file.NeedsBetaWarning && !await ConfirmBetaAsync(file, file.File?.Version ?? string.Empty))
            return;
        file.Download();
    }

    private async void OnTryBetaClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is PackageFileViewModel file && await ConfirmBetaAsync(file, file.BetaVersion))
            file.TryBeta();
    }

    /// <summary>Volver a la estable: si hay betas descargadas, pregunta si se borran o se conservan.</summary>
    private async void OnBackToStableClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is not PackageFileViewModel file)
            return;

        var betas = file.BetaVersions;
        if (betas.Count == 0)
        {
            file.BackToStable(deleteBeta: false);
            return;
        }

        var versions = string.Join(", ", betas.Select(v => VersionLabel.Format(v.Version)));
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock
        {
            Text = Loc("dialog.backToStableText", file.FileName, versions),
            TextWrapping = TextWrapping.Wrap,
        });

        var result = await Dialogs.AskAsync(this, Loc("dialog.backToStableTitle"), content,
            Loc("action.deleteBeta"), Loc("action.keepBeta"), Loc("action.cancel"));
        switch (result)
        {
            case Dialogs.Result.Primary:
                file.BackToStable(deleteBeta: true);
                break;
            case Dialogs.Result.Secondary:
                file.BackToStable(deleteBeta: false);
                break;
        }
    }

    /// <summary>Aviso de beta: alerta arriba y cómo volver a la estable.</summary>
    private async Task<bool> ConfirmBetaAsync(PackageFileViewModel file, string version)
    {
        var alreadyDownloaded = file.BetaDownloaded && version == file.BetaVersion;
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock
        {
            Text = Loc(alreadyDownloaded ? "dialog.betaTextDownloaded" : "dialog.betaText", VersionLabel.Format(version), file.FileName),
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(new TextBlock { Text = Loc("dialog.betaHowToReturn"), TextWrapping = TextWrapping.Wrap });

        var result = await Dialogs.AskAsync(this, Loc(alreadyDownloaded ? "dialog.useBetaTitle" : "dialog.betaTitle", VersionLabel.Format(version)), content,
            Loc(alreadyDownloaded ? "action.useBeta" : "action.tryBeta"), null, Loc("action.cancel"));
        return result == Dialogs.Result.Primary;
    }

    /// <summary>"Eliminar todas" de la app.</summary>
    private async void OnDeleteAllClick(object? sender, RoutedEventArgs e)
    {
        var all = Item.Versions;
        var extra = LibrarySnapshot.AllButLatestPerFile(all);
        var choice = await Dialogs.AskAsync(
            this,
            Loc("dialog.deleteAppTitle", Item.Name),
            new TextBlock { Text = extra.Count > 0 ? Loc("dialog.deleteAppMany") : Loc("dialog.deleteAppText"), TextWrapping = TextWrapping.Wrap },
            Loc("action.deleteAllConfirm"),
            extra.Count > 0 ? Loc("action.deleteAllButLatestPerFile") : null);

        if (choice == Dialogs.Result.Primary)
            Item.DeleteVersions(all);
        else if (choice == Dialogs.Result.Secondary)
            Item.DeleteVersions(extra);
    }

    /// <summary>Borrar desde la ficha de un fichero.</summary>
    private async void OnDeleteFileClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is not PackageFileViewModel file || !file.HasVersions)
            return;

        if (!file.HasManyVersions)
        {
            var single = file.Versions[0];
            if (await Dialogs.AskAsync(this, Loc("dialog.deleteVersionTitle", single.VersionText, file.FileName),
                new TextBlock { Text = Loc("dialog.deleteVersionText"), TextWrapping = TextWrapping.Wrap },
                Loc("action.confirmDelete"), null, Loc("action.cancel")) == Dialogs.Result.Primary)
                single.Delete();
            return;
        }

        var versions = string.Join(", ", file.Versions.Select(v => v.VersionText));
        var choice = await Dialogs.AskAsync(
            this,
            Loc("dialog.manyVersionsTitle"),
            new TextBlock { Text = Loc("dialog.manyVersionsFile", file.Versions.Count, file.FileName, versions), TextWrapping = TextWrapping.Wrap },
            Loc("action.deleteAllConfirm"),
            Loc("action.deleteAllButLatest"));

        if (choice == Dialogs.Result.Primary)
            Item.DeleteVersions(file.InstalledVersions);
        else if (choice == Dialogs.Result.Secondary)
            Item.DeleteVersions(file.InstalledVersions.Skip(1).ToList());
    }

    /// <summary>Borrar una versión concreta desde el histórico.</summary>
    private async void OnDeleteVersionClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is not FileVersionViewModel version)
            return;

        var file = Item.Files.FirstOrDefault(f => f.Versions.Contains(version));
        if (await Dialogs.AskAsync(this, Loc("dialog.deleteVersionTitle", version.VersionText, file?.FileName ?? Item.Name),
            new TextBlock { Text = Loc("dialog.deleteVersionText"), TextWrapping = TextWrapping.Wrap },
            Loc("action.confirmDelete"), null, Loc("action.cancel")) == Dialogs.Result.Primary)
            version.Delete();
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) =>
        ShellPage.Current?.NavigateTo(nameof(Core.Catalog.CatalogFilter.All));

    private static string Loc(string key, params object?[] args) =>
        args.Length == 0 ? App.Localization.Get(key) : App.Localization.Format(key, args);
}
