using AnchorPS5.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace AnchorPS5.App.Controls;

public sealed partial class DownloadsIndicator : UserControl
{
    public DownloadsIndicator(DownloadsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public DownloadsViewModel ViewModel { get; }
}
