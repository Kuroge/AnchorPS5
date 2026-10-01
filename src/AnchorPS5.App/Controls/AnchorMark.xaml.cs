using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AnchorPS5.App.Controls;

/// <summary>Ancla de AnchorPS5. El trazo usa Foreground y el aro superior, RingBrush.</summary>
public sealed partial class AnchorMark : UserControl
{
    public static readonly DependencyProperty RingBrushProperty =
        DependencyProperty.Register(nameof(RingBrush), typeof(Brush), typeof(AnchorMark), new PropertyMetadata(null));

    public AnchorMark()
    {
        InitializeComponent();
    }

    public Brush? RingBrush
    {
        get => (Brush?)GetValue(RingBrushProperty);
        set => SetValue(RingBrushProperty, value);
    }
}
