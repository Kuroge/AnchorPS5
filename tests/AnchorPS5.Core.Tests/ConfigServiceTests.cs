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
    public void LoadOrCreate_BrokenJson_KeepsACopy_AndStartsWithDefaults()
    {
        Directory.CreateDirectory(_paths.ConfigDirectory);
        // Falta la coma al final de la línea 3: el error está en la línea 4.
        File.WriteAllText(_paths.ConfigFile, """
            {
              "language": "es",
              "downloadPath": "C:/x"
              "theme": "dark"
            }
            """);
        var service = new ConfigService(_paths);

        var config = service.LoadOrCreate("en");

        Assert.Equal("en", config.Language);
        Assert.False(config.FirstRunCompleted);
        Assert.NotNull(service.LastLoadProblem);
        Assert.Equal(4, service.LastLoadProblem!.Line);
        Assert.True(File.Exists(service.LastLoadProblem.BrokenCopy));
        Assert.Contains("\"theme\": \"dark\"", File.ReadAllText(service.LastLoadProblem.BrokenCopy));
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
