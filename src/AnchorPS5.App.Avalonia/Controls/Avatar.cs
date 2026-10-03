using AnchorPS5.App.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AnchorPS5.App.Controls;

/// <summary>
/// Foto redonda de un usuario (equivalente al PersonPicture de WinUI): la imagen si la hay
/// y, si no, las iniciales sobre un círculo; sin nombre, una silueta.
/// </summary>
public sealed class Avatar : Border
{
    private readonly Image _image = new() { Stretch = Stretch.UniformToFill };
    private readonly TextBlock _initials = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        FontWeight = FontWeight.SemiBold,
        Foreground = Brushes.White,
    };
    private Uri? _source;

    public Avatar()
    {
        ClipToBounds = true;
        Background = new SolidColorBrush(Color.Parse("#0070D1"));
        Child = new Grid { Children = { _initials, _image } };
        SizeChanged += (_, e) =>
        {
            CornerRadius = new CornerRadius(e.NewSize.Width / 2);
            _initials.FontSize = Math.Max(9, e.NewSize.Width * 0.4);
        };
        Set(null, null);
    }

    /// <summary>Foto (url) y nombre para las iniciales cuando no hay foto.</summary>
    public async void Set(string? imageUrl, string? displayName)
    {
        _initials.Text = Initials(displayName);
        var uri = imageUrl is { Length: > 0 } && Uri.TryCreate(imageUrl, UriKind.Absolute, out var u) ? u : null;
        _source = uri;
        _image.Source = null;
        if (uri is null)
            return;

        var bitmap = await IconLoader.LoadAsync(uri);
        if (ReferenceEquals(_source, uri))
            _image.Source = bitmap;
    }

    private static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "\u263A"; // silueta
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
    }
}
