using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AnchorPS5.App.Controls;

/// <summary>Icono de beta (matraz), para señalar versiones experimentales. Usa el Foreground heredado.</summary>
public partial class BetaIcon : UserControl
{
    public BetaIcon()
    {
        InitializeComponent();
        IsHitTestVisible = false;
    }
}
