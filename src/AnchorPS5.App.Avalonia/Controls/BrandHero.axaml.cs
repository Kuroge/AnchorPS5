using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;

namespace AnchorPS5.App.Controls;

/// <summary>
/// Panel de marca: ancla flotante con halo azul, bokeh y chispas doradas (inspirado en el
/// arranque de PS5). Opcionalmente muestra el paso actual con puntos.
/// Los Storyboard de WinUI se sustituyen por un temporizador que calcula cada fotograma con
/// las mismas duraciones; solo corre mientras el panel está en pantalla.
/// </summary>
public partial class BrandHero : UserControl
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly DateTime _start = DateTime.UtcNow;
    private readonly ScaleTransform _glowScale = new();
    private readonly TranslateTransform _anchorMove = new(), _bokeh1 = new(), _bokeh2 = new(), _bokeh3 = new();

    public BrandHero()
    {
        InitializeComponent();
        VersionText.Text = App.Localization.Format("app.versionBy", App.Version, "cheyen2008");

        Glow.RenderTransform = _glowScale;
        Anchor.RenderTransform = _anchorMove;
        Bokeh1.RenderTransform = _bokeh1;
        Bokeh2.RenderTransform = _bokeh2;
        Bokeh3.RenderTransform = _bokeh3;

        _timer.Tick += (_, _) => Animate((DateTime.UtcNow - _start).TotalSeconds);
        AttachedToVisualTree += (_, _) => _timer.Start();
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    /// <summary>Muestra los puntos de progreso (0 o 1 = ocultos).</summary>
    public void SetSteps(int count, int current)
    {
        Pips.Children.Clear();
        Pips.IsVisible = count > 1;
        for (var i = 0; i < count; i++)
        {
            var dot = new Ellipse { Width = 8, Height = 8, Opacity = i == current ? 1 : 0.35 };
            dot.Bind(Shape.FillProperty, dot.GetResourceObservable(i == current ? "AnchorBlueBrush" : "TextFillColorSecondaryBrush"));
            Pips.Children.Add(dot);
        }
    }

    private void Animate(double t)
    {
        // Halo que "respira": escala 0.88→1.08 y opacidad 0.55→1, ida y vuelta en 2.8 s.
        var glow = PingPong(t, 2.8);
        _glowScale.ScaleX = _glowScale.ScaleY = Lerp(0.88, 1.08, glow);
        Glow.Opacity = Lerp(0.55, 1, glow);

        // El ancla flota: -6→6 px en 3.4 s.
        _anchorMove.Y = Lerp(-6, 6, PingPong(t, 3.4));

        // Bokeh a la deriva (lineal, ida y vuelta).
        _bokeh1.X = Lerp(-10, 14, PingPong(t, 7, ease: false)); _bokeh1.Y = Lerp(6, -12, PingPong(t, 9, ease: false));
        _bokeh2.X = Lerp(12, -10, PingPong(t, 8, ease: false)); _bokeh2.Y = Lerp(-8, 10, PingPong(t, 6, ease: false));
        _bokeh3.X = Lerp(-6, 10, PingPong(t, 10, ease: false)); _bokeh3.Y = Lerp(10, -6, PingPong(t, 7.5, ease: false));

        // Chispas: se encienden en 0.5 s y se apagan en 1.1 s, a destiempo (ciclo de 3.2 s).
        Spark(Spark1, t, 0.0, 1);
        Spark(Spark2, t, 0.8, 1);
        Spark(Spark3, t, 1.6, 1);
        Spark(Spark4, t, 0.4, 0.9);
        Spark(Spark5, t, 1.2, 1);
        Spark(Spark6, t, 2.0, 0.85);
    }

    private static void Spark(Control spark, double t, double offset, double peak)
    {
        var local = ((t % 3.2) - offset + 3.2) % 3.2;
        spark.Opacity = local switch
        {
            < 0.5 => peak * Ease(local / 0.5),
            < 1.6 => peak * (1 - (local - 0.5) / 1.1),
            _ => 0,
        };
    }

    /// <summary>0→1→0 con periodo 2·<paramref name="half"/> (AutoReverse), con o sin SineEase.</summary>
    private static double PingPong(double t, double half, bool ease = true)
    {
        var phase = t % (2 * half) / half;
        var linear = phase <= 1 ? phase : 2 - phase;
        return ease ? Ease(linear) : linear;
    }

    private static double Ease(double x) => (1 - Math.Cos(Math.PI * x)) / 2;

    private static double Lerp(double from, double to, double k) => from + (to - from) * k;
}
