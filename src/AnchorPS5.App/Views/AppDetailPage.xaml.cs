using AnchorPS5.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace AnchorPS5.App.Views;

/// <summary>Detalle de una app; recibe su <see cref="CatalogItemViewModel"/> al navegar.</summary>
public sealed partial class AppDetailPage : Page
{
    private const string DownloadGlyph = "";
    private const string WarningGlyph = "";

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

    private void OnDeleteAllConfirmed(object sender, RoutedEventArgs e)
    {
        DeleteAllFlyout.Hide();
        Item.DeleteAllCommand.Execute(null);
    }
}
