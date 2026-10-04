using Avalonia.Markup.Xaml;

namespace AnchorPS5.App.Localization;

/// <summary>
/// Texto traducido en XAML: <c>Text="{l:Loc Key=nav.catalog}"</c>.
/// El idioma se fija al arrancar, así que basta con resolverlo una vez.
/// </summary>
public sealed class LocExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => App.Localization.Get(Key);
}
