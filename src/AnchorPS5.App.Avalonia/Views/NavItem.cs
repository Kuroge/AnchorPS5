using System.ComponentModel;
using System.Windows.Input;
using AnchorPS5.Core.Catalog;
using AnchorPS5.Core.Downloads;
using AnchorPS5.Core.Library;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;

namespace AnchorPS5.App.Views;

/// <summary>Un elemento del menú lateral (sección del catálogo o Acerca de).</summary>
public sealed partial class NavItem : ObservableObject
{
    public NavItem(string glyph, string label, string tag)
    {
        Glyph = glyph;
        Label = label;
        Tag = tag;
        NavigateCommand = new RelayCommand(Navigate);
    }

    public string Glyph { get; }
    public string Label { get; }
    public string Tag { get; }

    public ICommand NavigateCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCount))]
    [NotifyPropertyChangedFor(nameof(CountText))]
    public partial int Count { get; set; }

    public bool HasCount => Count > 0;
    public string CountText => Count.ToString();

    private void Navigate() => ShellPage.Current?.NavigateTo(Tag);
}
