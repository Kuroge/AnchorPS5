using AnchorPS5.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;

namespace AnchorPS5.App.Controls;

/// <summary>Indicador de descargas de la barra de título.</summary>
public partial class DownloadsIndicator : UserControl
{
    public DownloadsIndicator(DownloadsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        // El XAML se enlaza a "ViewModel.*"; sin DataContext propio no se resuelve y el
        // anillo/contador se quedan visibles. Lo fijamos a la propia vista.
        DataContext = this;
        RootButton.Click += (_, _) =>
        {
            if (Menu is { IsOpen: true })
                Menu.Close();
            else
                Menu.Open(RootButton);
        };
    }

    public DownloadsViewModel ViewModel { get; }
}
