using System.Net;
using AnchorPS5.Core.GitHub;
using AnchorPS5.Core.Models;
using AnchorPS5.Core.Packages;

namespace AnchorPS5.Core.Tests;

public sealed class ReleaseIndexTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AnchorPS5Tests", Guid.NewGuid().ToString("N"));
    private readonly Source _official = new() { Name = "Oficial", Type = SourceType.Official, Url = "https://example.test/repo/main/catalog.json" };
    private DateTimeOffset _now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    private readonly List<string> _requests = [];
    private bool _offline;

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string IndexJson(DateTimeOffset generatedAt) => $$"""
        {
          "schemaVersion": 1,
          "generatedAt": "{{generatedAt:O}}",
          "repos": {
            "autor/app": [
              { "tag_name": "v1.1", "prerelease": false, "published_at": "2026-09-01T00:00:00Z",
                "assets": [ { "name": "app.elf", "size": 10, "browser_download_url": "https://x/app.elf", "digest": "sha256:abc" } ] }
            ]
          }
        }
        """;

    private ReleaseIndex Index(string? json = null) =>
        new(new HttpClient(new Stub(request =>
        {
            _requests.Add(request.RequestUri!.AbsoluteUri);
            if (_offline)
                throw new HttpRequestException("sin red");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json ?? IndexJson(_now)) };
        })), _root, () => _now);

    [Fact]
    public async Task LoadsTheIndexNextToTheOfficialCatalog()
    {
        var index = Index();
        await index.LoadAsync([_official]);

        Assert.Equal(["https://example.test/repo/main/releases.json"], _requests);
        Assert.True(index.TryGet(new GitHubRepoRef("Autor", "APP"), out var releases));
        Assert.Equal("abc", releases[0].Assets[0].Sha256);
        Assert.False(index.TryGet(new GitHubRepoRef("otro", "repo"), out _));
    }

    [Fact]
    public async Task RecentCopy_IsUsedWithoutDownloading_AndOfflineFallsBackToIt()
    {
        await Index().LoadAsync([_official]);
        _now = _now.AddMinutes(10);
        var index = Index();
        await index.LoadAsync([_official]);
        Assert.Single(_requests);

        _now = _now.AddHours(2);
        _offline = true;
        var offline = Index();
        await offline.LoadAsync([_official]);
        Assert.True(offline.TryGet(new GitHubRepoRef("autor", "app"), out _));
    }

    [Fact]
    public async Task StaleIndex_IsIgnored()
    {
        var index = Index(IndexJson(_now.AddDays(-3)));
        await index.LoadAsync([_official]);

        Assert.False(index.TryGet(new GitHubRepoRef("autor", "app"), out _));
    }

    [Fact]
    public async Task InvalidIndex_IsIgnored()
    {
        var index = Index("{ esto no es json");
        await index.LoadAsync([_official]);

        Assert.False(index.TryGet(new GitHubRepoRef("autor", "app"), out _));
    }

    [Fact]
    public async Task OnlyOfficialSourcesHaveAnIndex()
    {
        var index = Index();
        await index.LoadAsync([new Source { Name = "Remoto", Type = SourceType.Remote, Url = "https://example.test/catalog.json" }]);

        Assert.Empty(_requests);
    }

    [Fact]
    public async Task Resolver_UsesTheIndex_AndOnlyAsksTheApiForTheRest()
    {
        var index = Index();
        var apiCalls = new List<string>();
        var api = new GitHubClient(new HttpClient(new Stub(request =>
        {
            apiCalls.Add(request.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") };
        })), Path.Combine(_root, "api"));
        var resolver = new PackageResolver(api, index);
        await resolver.LoadIndexAsync([_official]);

        var indexed = await resolver.ResolveAsync(new HomebrewApp { Id = "github:autor/app", Name = "App", Repo = "autor/app" });
        await resolver.ResolveAsync(new HomebrewApp { Id = "github:mio/propia", Name = "Propia", Repo = "mio/propia" });

        Assert.Equal("1.1", indexed.DisplayVersion);
        Assert.Equal(["/repos/mio/propia/releases"], apiCalls);
    }

    [Fact]
    public async Task SignedIn_AsksTheApi_AndFallsBackToTheIndexIfItFails()
    {
        var index = Index();
        var apiFails = false;
        var apiCalls = 0;
        var api = new GitHubClient(new HttpClient(new Stub(_ =>
        {
            apiCalls++;
            if (apiFails)
                throw new HttpRequestException("sin red");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{ "tag_name": "v1.2", "prerelease": false, "published_at": "2026-09-20T00:00:00Z", "assets": [ { "name": "app.elf", "size": 10, "browser_download_url": "https://x/app.elf" } ] }]"""),
            };
        })), Path.Combine(_root, "api")) { MaxAge = TimeSpan.Zero };
        var resolver = new PackageResolver(api, index, preferApi: () => true);
        await resolver.LoadIndexAsync([_official]);
        var app = new HomebrewApp { Id = "github:autor/app", Name = "App", Repo = "autor/app" };

        Assert.Equal("1.2", (await resolver.ResolveAsync(app)).DisplayVersion); // más al día que el índice (1.1)

        apiFails = true;
        Assert.Equal("1.1", (await resolver.ResolveAsync(app)).DisplayVersion); // respaldo: el índice
        Assert.Equal(2, apiCalls);
    }

    private sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
