using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace AnchorPS5.App.Controls;

/// <summary>El ancla del logo de la app, en vectorial. Usa el Foreground heredado de UserControl.</summary>
public partial class AnchorMark : UserControl
{
    public AnchorMark()
    {
        InitializeComponent();
    }

    public static readonly Avalonia.StyledProperty<IBrush?> RingBrushProperty =
        Avalonia.AvaloniaProperty.Register<AnchorMark, IBrush?>(nameof(RingBrush), Brushes.Goldenrod);

    public IBrush? RingBrush
    {
        get => GetValue(RingBrushProperty);
        set => SetValue(RingBrushProperty, value);
    }
}
