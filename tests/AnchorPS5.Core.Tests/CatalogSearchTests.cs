using System.Globalization;
using AnchorPS5.Core.Catalog;
using AnchorPS5.Core.Models;

namespace AnchorPS5.Core.Tests;

public sealed class CatalogSearchTests
{
    private static readonly Source AnySource = new() { Name = "S" };

    private static readonly CatalogEntry[] Entries =
    [
        Entry("com.ej.reproductor", "Reproductor multimedia", "Ana", "Vídeo y música desde USB"),
        Entry("com.ej.explorador", "Explorador de archivos", "Luis", "Navega por las unidades"),
        Entry("com.ej.monitor", "Monitor de sistema", "Ana", "Temperatura y uso de CPU"),
    ];

    private static CatalogEntry Entry(string id, string name, string author, string description) =>
        new(new HomebrewApp { Id = id, Name = name, Author = author, Description = description }, AnySource, null);

    private static string[] Search(string? query) => CatalogSearch.Filter(Entries, query).Select(e => e.App.Id).ToArray();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyQuery_ReturnsAll(string? query) => Assert.Equal(3, Search(query).Length);

    [Fact]
    public void MatchesAuthor() => Assert.Equal(["com.ej.reproductor", "com.ej.monitor"], Search("ana"));

    [Fact]
    public void IgnoresAccentsAndCase() => Assert.Equal(["com.ej.reproductor"], Search("VIDEO"));

    [Fact]
    public void AllWordsMustMatch() => Assert.Equal(["com.ej.monitor"], Search("ana cpu"));

    [Fact]
    public void MatchesId() => Assert.Equal(["com.ej.explorador"], Search("ej.explo"));

    [Fact]
    public void ByteSize_FormatsWithCulture()
    {
        var es = new CultureInfo("es-ES");
        Assert.Equal("512 B", ByteSize.Format(512, es));
        Assert.Equal("11,8 MB", ByteSize.Format(12_345_678, es));
        Assert.Equal("1 GB", ByteSize.Format(1L << 30, es));
    }
}
