using AnchorPS5.App.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace AnchorPS5.App.Views;

/// <summary>Recibe su <see cref="CatalogViewModel"/> como parámetro de navegación.</summary>
public sealed partial class CatalogPage : Page
{
    public CatalogPage()
    {
        InitializeComponent();
    }

    public CatalogViewModel ViewModel { get; private set; } = null!;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is CatalogViewModel viewModel && !ReferenceEquals(viewModel, ViewModel))
        {
            ViewModel = viewModel;
            Bindings.Update();
        }

        if (!ViewModel.IsLoaded && ViewModel.LoadCommand.CanExecute(null))
            ViewModel.LoadCommand.Execute(null);
    }

    private void OnItemClick(object sender, ItemClickEventArgs e) =>
        Frame.Navigate(typeof(AppDetailPage), e.ClickedItem, new DrillInNavigationTransitionInfo());
}
