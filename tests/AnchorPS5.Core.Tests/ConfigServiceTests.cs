using AnchorPS5.Core.Configuration;
using AnchorPS5.Core.Models;

namespace AnchorPS5.Core.Tests;

public sealed class ConfigServiceTests : IDisposable
{
    private readonly AppPaths _paths = new(Path.Combine(Path.GetTempPath(), "AnchorPS5Tests", Guid.NewGuid().ToString("N")));

    public void Dispose()
    {
        if (Directory.Exists(_paths.BaseDirectory))
            Directory.Delete(_paths.BaseDirectory, recursive: true);
    }

    [Fact]
    public void LoadOrCreate_WithoutFile_CreatesDefaultsWithInitialLanguage()
    {
        var config = new ConfigService(_paths).LoadOrCreate("en");

        Assert.True(File.Exists(_paths.ConfigFile));
        Assert.Equal("en", config.Language);
        Assert.EndsWith("AnchorPS5_Downloads", config.DownloadPath);
        Assert.False(config.FirstRunCompleted);
    }

    [Fact]
    public void LoadOrCreate_ExistingFile_KeepsValues_AndFillsMissing()
    {
        Directory.CreateDirectory(_paths.ConfigDirectory);
        File.WriteAllText(_paths.ConfigFile, """
            {
              // comentario permitido
              "language": "pt-BR",
              "theme": "dark",
              "sources": [ { "name": "R", "type": "remote", "url": "https://example.com/catalog.json" } ],
            }
            """);

        var config = new ConfigService(_paths).LoadOrCreate("es");

        Assert.Equal("pt-BR", config.Language);
        Assert.Equal(AppTheme.Dark, config.Theme);
        Assert.Equal(SourceType.Remote, Assert.Single(config.Sources).Type);
        Assert.Equal(3, config.MaxConcurrentDownloads);
        Assert.False(string.IsNullOrEmpty(config.DownloadPath));
    }
}
