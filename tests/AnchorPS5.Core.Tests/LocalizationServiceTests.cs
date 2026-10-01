using System.Globalization;
using AnchorPS5.Core.Localization;

namespace AnchorPS5.Core.Tests;

public sealed class LocalizationServiceTests : IDisposable
{
    private readonly string _langDir = Path.Combine(Path.GetTempPath(), "AnchorPS5Tests", Guid.NewGuid().ToString("N"));

    public LocalizationServiceTests()
    {
        Directory.CreateDirectory(_langDir);
        Write("es", """{ "_meta": { "code": "es", "name": "Español" }, "nav.catalog": "Catálogo", "nav.guide": "Guía" }""");
        Write("en", """{ "_meta": { "code": "en", "name": "English" }, "nav.catalog": "Catalog" }""");
        Write("pt-BR", """{ "_meta": { "code": "pt-BR", "name": "Português (Brasil)" }, "nav.catalog": "Catálogo" }""");
    }

    public void Dispose() => Directory.Delete(_langDir, recursive: true);

    private void Write(string code, string json) => File.WriteAllText(Path.Combine(_langDir, code + ".json"), json);

    [Fact]
    public void DiscoverLanguages_ReadsMetaNames_AndSkipsInvalidFiles()
    {
        Write("broken", "{ not json");

        var languages = new LocalizationService(_langDir).DiscoverLanguages();

        Assert.Equal(["en", "es", "pt-BR"], languages.Select(l => l.Code).Order());
        Assert.Contains(languages, l => l.Code == "es" && l.Name == "Español");
    }

    [Theory]
    [InlineData("pt-BR", "pt-BR")] // coincidencia exacta
    [InlineData("en-US", "en")]    // por código de 2 letras
    [InlineData("pt-PT", "pt-BR")] // mismo idioma, otra región
    [InlineData("de-DE", "es")]    // sin traducción: idioma por defecto
    public void DetectLanguage_PicksBestMatch(string culture, string expected)
    {
        var service = new LocalizationService(_langDir);

        Assert.Equal(expected, service.DetectLanguage(new CultureInfo(culture)));
    }

    [Fact]
    public void Get_FallsBackToDefaultLanguage_ThenToKey()
    {
        var service = new LocalizationService(_langDir);
        service.Load("en");

        Assert.Equal("en", service.CurrentLanguage);
        Assert.Equal("Catalog", service.Get("nav.catalog"));
        Assert.Equal("Guía", service.Get("nav.guide"));
        Assert.Equal("missing.key", service.Get("missing.key"));
        Assert.Equal("_meta", service.Get("_meta")); // los metadatos no son textos
    }

    [Fact]
    public void Load_UnknownLanguage_UsesDefault()
    {
        var service = new LocalizationService(_langDir);
        service.Load("xx");

        Assert.Equal("es", service.CurrentLanguage);
        Assert.Equal("Catálogo", service.Get("nav.catalog"));
    }
}
