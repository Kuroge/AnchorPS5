using AnchorPS5.Core.Packages;
using AnchorPS5.App.Controls;
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
    private const string CheckGlyph = "\uE73E";
    private const string UpdateGlyph = "\uE74A";

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

    /// <summary>Menú "Descargar ▾": todos los ficheros; los que ya tienes (al día o no) salen deshabilitados.</summary>
    private void OnDownloadMenuOpening(object? sender, object e)
    {
        DownloadMenu.Items.Clear();

        // Si falta alguno: primero "Descargar todo".
        var missing = Item.DownloadAllFiles.Count;
        if (missing > 0)
        {
            var all = new MenuFlyoutItem
            {
                Text = Loc("action.downloadAll", missing),
                Icon = new FontIcon { Glyph = DownloadGlyph },
            };
            all.Click += (_, _) => Item.DownloadAll();
            DownloadMenu.Items.Add(all);
            DownloadMenu.Items.Add(new MenuFlyoutSeparator());
        }

        foreach (var file in Item.DownloadableFiles)
        {
            var missingFile = file.Status.State == FileState.NotDownloaded;
            var option = FileOption(file, file.MenuText, missingFile ? DownloadGlyph : CheckGlyph);
            if (missingFile)
                option.Click += (_, _) => DownloadWithWarningAsync(file);
            else
                option.IsEnabled = false;
            DownloadMenu.Items.Add(option);
        }
    }

    /// <summary>Flecha de "Actualizar todo (N) ▾": actualizar fichero a fichero.</summary>
    private void OnUpdateMenuOpening(object? sender, object e)
    {
        UpdateMenu.Items.Clear();
        foreach (var file in Item.OutdatedFiles)
        {
            var option = FileOption(file, file.UpdateMenuText, UpdateGlyph);
            option.Click += async (_, _) => await UpdateAsync([file]);
            UpdateMenu.Items.Add(option);
        }
    }

    private static MenuFlyoutItem FileOption(PackageFileViewModel file, string text, string glyph)
    {
        var option = new MenuFlyoutItem
        {
            Text = text,
            Icon = file.IsBeta && glyph != CheckGlyph ? BetaIcon.CreateIcon() : new FontIcon { Glyph = glyph },
            IsEnabled = !file.IsBusy,
        };
        if (file.IsBeta && glyph != CheckGlyph && Application.Current.Resources.TryGetValue("BetaBrush", out var beta))
            option.Foreground = (Brush)beta;
        ToolTipService.SetToolTip(option, file.HasDescription ? file.Description : file.FileName);
        return option;
    }

    // ---- Descargar y betas ----

    private async void OnFileDownloadClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not PackageFileViewModel file)
            return;
        if (file.HasUpdate)
            await UpdateAsync([file]);
        else
            DownloadWithWarningAsync(file);
    }

    // ---- Actualizar (siempre con la pregunta de borrar o conservar lo anterior) ----

    private async void OnUpdateAllClick(object sender, RoutedEventArgs e) => await UpdateAsync(Item.OutdatedFiles);

    private async void OnUpdateAllSplitClick(SplitButton sender, SplitButtonClickEventArgs e) => await UpdateAsync(Item.OutdatedFiles);

    private async Task UpdateAsync(IReadOnlyList<PackageFileViewModel> files)
    {
        if (files.Count == 0)
            return;
        var removeOld = await UpdateDialogs.AskRemoveOldAsync(XamlRoot, ActualTheme, CatalogItemViewModel.OldVersionCount(files));
        if (removeOld is { } remove)
            Item.Update(files, remove);
    }

    /// <summary>Descargar una beta por primera vez (fichero solo-beta) pide confirmación.</summary>
    private async void DownloadWithWarningAsync(PackageFileViewModel file)
    {
        if (file.NeedsBetaWarning && !await ConfirmBetaAsync(file, file.File?.Version ?? string.Empty))
            return;
        file.Download();
    }

    private async void OnTryBetaClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is PackageFileViewModel file && await ConfirmBetaAsync(file, file.BetaVersion))
            file.TryBeta();
    }

    /// <summary>
    /// Volver a la estable: si hay betas descargadas de ese fichero, pregunta si se borran o se
    /// conservan, con un enlace para abrir su ubicación sin cerrar el aviso.
    /// </summary>
    private async void OnBackToStableClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not PackageFileViewModel file)
            return;

        var betas = file.BetaVersions;
        if (betas.Count == 0)
        {
            file.BackToStable(deleteBeta: false);
            return;
        }

        var versions = string.Join(", ", betas.Select(v => VersionLabel.Format(v.Version)));
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock { Text = Loc("dialog.backToStableText", file.FileName, versions), TextWrapping = TextWrapping.Wrap });
        var openLocation = new HyperlinkButton { Content = Loc("action.openBetaLocation"), Padding = new Thickness(0) };
        openLocation.Click += (_, _) => Item.OpenBetaFolder(file);
        content.Children.Add(openLocation);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            RequestedTheme = ActualTheme,
            Title = Loc("dialog.backToStableTitle"),
            Content = content,
            PrimaryButtonText = Loc("action.deleteBeta"),
            SecondaryButtonText = Loc("action.keepBeta"),
            CloseButtonText = Loc("action.cancel"),
            DefaultButton = ContentDialogButton.Close,
        };

        switch (await dialog.ShowAsync())
        {
            case ContentDialogResult.Primary:
                file.BackToStable(deleteBeta: true);
                break;
            case ContentDialogResult.Secondary:
                file.BackToStable(deleteBeta: false);
                break;
        }
    }

    /// <summary>Aviso de beta: alerta arriba y cómo volver a la estable.</summary>
    private async Task<bool> ConfirmBetaAsync(PackageFileViewModel file, string version)
    {
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new InfoBar
        {
            IsOpen = true,
            IsClosable = false,
            Severity = InfoBarSeverity.Warning,
            Title = Loc("dialog.betaWarningTitle"),
            Message = Loc("dialog.betaWarning"),
        });
        var alreadyDownloaded = file.BetaDownloaded && version == file.BetaVersion;
        content.Children.Add(new TextBlock
        {
            Text = Loc(alreadyDownloaded ? "dialog.betaTextDownloaded" : "dialog.betaText", VersionLabel.Format(version), file.FileName),
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(new TextBlock { Text = Loc("dialog.betaHowToReturn"), TextWrapping = TextWrapping.Wrap });

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            RequestedTheme = ActualTheme,
            Title = Loc(alreadyDownloaded ? "dialog.useBetaTitle" : "dialog.betaTitle", VersionLabel.Format(version)),
            Content = content,
            PrimaryButtonText = Loc(alreadyDownloaded ? "action.useBeta" : "action.tryBeta"),
            CloseButtonText = Loc("action.cancel"),
            DefaultButton = ContentDialogButton.Close,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
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

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
            Frame.GoBack(new Microsoft.UI.Xaml.Media.Animation.SlideNavigationTransitionInfo
            {
                Effect = Microsoft.UI.Xaml.Media.Animation.SlideNavigationTransitionEffect.FromLeft,
            });
    }

    private static string Loc(string key, params object?[] args) =>
        args.Length == 0 ? App.Localization.Get(key) : App.Localization.Format(key, args);
}
