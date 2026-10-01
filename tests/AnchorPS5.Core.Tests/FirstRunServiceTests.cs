using AnchorPS5.Core.Configuration;

namespace AnchorPS5.Core.Tests;

public sealed class FirstRunServiceTests : IDisposable
{
    private readonly AppPaths _paths = new(Path.Combine(Path.GetTempPath(), "AnchorPS5Tests", Guid.NewGuid().ToString("N")));

    public void Dispose()
    {
        if (Directory.Exists(_paths.BaseDirectory))
            Directory.Delete(_paths.BaseDirectory, recursive: true);
    }

    [Theory]
    [InlineData(@"C:\Descargas", true)]
    [InlineData(@"  ""C:\Descargas\""  ", true)]
    [InlineData(@"\\servidor\compartida", true)]
    [InlineData(@"Descargas", false)]
    [InlineData(@"C:Descargas", false)]
    [InlineData(@"", false)]
    [InlineData(null, false)]
    public void TryNormalizeDownloadPath_AcceptsOnlyFullPaths(string? input, bool expected)
    {
        Assert.Equal(expected, FirstRunService.TryNormalizeDownloadPath(input, out _));
    }

    [Fact]
    public void TryNormalizeDownloadPath_TrimsQuotesAndTrailingSeparator()
    {
        FirstRunService.TryNormalizeDownloadPath(@" ""C:\Descargas\"" ", out var fullPath);

        Assert.Equal(@"C:\Descargas", fullPath);
    }

    [Fact]
    public void SaveSettings_ThenComplete_PersistsAndCreatesFolder()
    {
        var configService = new ConfigService(_paths);
        var config = configService.LoadOrCreate();
        var service = new FirstRunService(configService, config);
        var downloads = Path.Combine(_paths.BaseDirectory, "Bajadas");

        Assert.True(service.IsRequired);
        service.SaveSettings("en", downloads);

        Assert.True(Directory.Exists(downloads));
        var saved = new ConfigService(_paths).LoadOrCreate();
        Assert.Equal("en", saved.Language);
        Assert.Equal(downloads, saved.DownloadPath);
        Assert.False(saved.FirstRunCompleted);

        service.Complete();

        Assert.False(service.IsRequired);
        Assert.True(new ConfigService(_paths).LoadOrCreate().FirstRunCompleted);
    }

    [Fact]
    public void SaveSettings_RelativePath_Throws()
    {
        var configService = new ConfigService(_paths);
        var service = new FirstRunService(configService, configService.LoadOrCreate());

        Assert.Throws<ArgumentException>(() => service.SaveSettings("es", "Descargas"));
    }
}
