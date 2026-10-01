using System.Net;
using System.Text;
using AnchorPS5.Core.Catalog;
using AnchorPS5.Core.Models;

namespace AnchorPS5.Core.Tests;

public sealed class SourceLoaderTests : IDisposable
{
    private readonly string _configDir = Path.Combine(Path.GetTempPath(), "AnchorPS5Tests", Guid.NewGuid().ToString("N"));

    public SourceLoaderTests() => Directory.CreateDirectory(_configDir);

    public void Dispose() => Directory.Delete(_configDir, recursive: true);

    private const string TwoApps = """
        {
          "schemaVersion": 1,
          "apps": [
            { "id": "com.a.uno", "name": "Uno", "version": "1.0", "author": "A", "iconUrl": "icons/uno.png" },
            { "id": "com.a.dos", "name": "Dos", "version": "2.0", "author": "A", "iconUrl": "https://cdn.example.com/dos.png" },
            { "id": "", "name": "Sin id" }
          ]
        }
        """;

    private static Source Local(string path) => new() { Name = "Local", Type = SourceType.Local, Path = path };

    private static Source Remote(string url) => new() { Name = "Remota", Type = SourceType.Remote, Url = url };

    private SourceLoader CreateLoader(Func<HttpRequestMessage, HttpResponseMessage>? handler = null) =>
        new(new HttpClient(new StubHandler(handler ?? (_ => new HttpResponseMessage(HttpStatusCode.NotFound)))), _configDir);

    [Fact]
    public async Task Local_RelativePath_LoadsValidAppsAndResolvesIcons()
    {
        File.WriteAllText(Path.Combine(_configDir, "catalog.json"), TwoApps);

        var result = await CreateLoader().LoadAsync(Local("catalog.json"));

        Assert.True(result.Succeeded);
        Assert.Equal(["com.a.uno", "com.a.dos"], result.Entries.Select(e => e.App.Id));
        var unoIcon = result.Entries[0].IconUri!;
        Assert.True(unoIcon.IsFile);
        Assert.Equal(Path.Combine(_configDir, "icons", "uno.png"), unoIcon.LocalPath);
        Assert.Equal("https://cdn.example.com/dos.png", result.Entries[1].IconUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Local_MissingFile_ReportsNotFound()
    {
        var result = await CreateLoader().LoadAsync(Local("no-existe.json"));

        Assert.Equal(SourceErrorKind.NotFound, result.Error);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public async Task Local_InvalidJson_ReportsInvalidJson()
    {
        File.WriteAllText(Path.Combine(_configDir, "roto.json"), "{ \"apps\": [ ");

        var result = await CreateLoader().LoadAsync(Local("roto.json"));

        Assert.Equal(SourceErrorKind.InvalidJson, result.Error);
    }

    [Fact]
    public async Task Remote_LoadsManifest_AndResolvesRelativeIconsAgainstUrl()
    {
        var loader = CreateLoader(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(TwoApps, Encoding.UTF8, "application/json"),
        });

        var result = await loader.LoadAsync(Remote("https://repo.example.com/ps5/catalog.json"));

        Assert.True(result.Succeeded);
        Assert.Equal("https://repo.example.com/ps5/icons/uno.png", result.Entries[0].IconUri!.AbsoluteUri);
    }

    [Theory]
    [InlineData("ftp://repo.example.com/catalog.json")]
    [InlineData("file:///C:/catalog.json")]
    [InlineData("no es una url")]
    [InlineData(null)]
    public async Task Remote_NonHttpUrl_IsInvalidSource(string? url)
    {
        var result = await CreateLoader().LoadAsync(Remote(url!));

        Assert.Equal(SourceErrorKind.InvalidSource, result.Error);
    }

    [Fact]
    public async Task Remote_NetworkFailure_ReportsNetwork()
    {
        var loader = CreateLoader(_ => throw new HttpRequestException("sin red"));

        var result = await loader.LoadAsync(Remote("https://repo.example.com/catalog.json"));

        Assert.Equal(SourceErrorKind.Network, result.Error);
    }

    [Fact]
    public async Task LoadAll_SkipsDisabled_DeduplicatesById_AndCollectsFailures()
    {
        File.WriteAllText(Path.Combine(_configDir, "a.json"), TwoApps);
        File.WriteAllText(Path.Combine(_configDir, "b.json"), """
            { "apps": [ { "id": "COM.A.UNO", "name": "Uno (copia)" }, { "id": "com.b.tres", "name": "Tres" } ] }
            """);
        File.WriteAllText(Path.Combine(_configDir, "off.json"), """{ "apps": [ { "id": "com.off", "name": "Off" } ] }""");

        var sources = new[]
        {
            Local("a.json"),
            Local("b.json"),
            new Source { Name = "Off", Type = SourceType.Local, Path = "off.json", Enabled = false },
            Local("falta.json"),
        };

        var result = await CreateLoader().LoadAllAsync(sources);

        Assert.Equal(["com.a.uno", "com.a.dos", "com.b.tres"], result.Entries.Select(e => e.App.Id));
        Assert.Equal("Uno", result.Entries[0].App.Name);
        Assert.Equal(SourceErrorKind.NotFound, Assert.Single(result.FailedSources).Error);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = respond(request);
            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }
}
