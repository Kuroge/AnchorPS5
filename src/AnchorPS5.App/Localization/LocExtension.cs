using Microsoft.UI.Xaml.Markup;

namespace AnchorPS5.App.Localization;

/// <summary>
/// Texto traducido en XAML: <c>Text="{l:Loc Key=nav.catalog}"</c>.
/// El idioma se fija al arrancar, así que basta con resolverlo una vez.
/// </summary>
[MarkupExtensionReturnType(ReturnType = typeof(string))]
public sealed partial class LocExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    protected override object ProvideValue() => App.Localization.Get(Key);
}
