using AnchorPS5.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace AnchorPS5.App.Views;

public sealed partial class SetupPage : Page
{
    public SetupPage(SetupViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public SetupViewModel ViewModel { get; }
}
