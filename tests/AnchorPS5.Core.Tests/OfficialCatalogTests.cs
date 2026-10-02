using System.Net;
using System.Text.Json;
using AnchorPS5.Core.Catalog;
using AnchorPS5.Core.Configuration;
using AnchorPS5.Core.Models;

namespace AnchorPS5.Core.Tests;

public sealed class LocalizedTextTests
{
    private static HomebrewApp Read(string description) =>
        JsonSerializer.Deserialize<HomebrewApp>($$"""{ "id": "a", "name": "A", "description": {{description}} }""", JsonDefaults.Options)!;

    [Fact]
    public void PlainText_IsUsedForAnyLanguage()
    {
        var text = Read("\"Hola\"").Description;

        Assert.Equal("Hola", text.Get("en"));
        Assert.Equal("Hola", text.Get("es"));
    }

    [Theory]
    [InlineData("en", "Hello")]
    [InlineData("en-GB", "Hello")]
    [InlineData("fr", "Hola")] // sin traducción: español
    public void Translations_FallBackToSpanish(string language, string expected)
    {
        var text = Read("""{ "es": "Hola", "en": "Hello" }""").Description;

        Assert.Equal(expected, text.Get(language));
    }

    [Fact]
    public void WithoutSpanish_FallsBackToFirst()
    {
        Assert.Equal("Hello", Read("""{ "en": "Hello" }""").Description.Get("fr"));
    }

    [Fact]
    public void RoundTrip_KeepsTheShape()
    {
        var plain = JsonSerializer.Serialize(Read("\"Hola\""), JsonDefaults.Options);
        var translated = JsonSerializer.Serialize(Read("""{ "es": "Hola", "en": "Hello" }"""), JsonDefaults.Options);

        Assert.Contains("\"description\": \"Hola\"", plain);
        Assert.Contains("\"en\": \"Hello\"", translated);
    }

    [Fact]
    public void Search_LooksInEveryLanguage()
    {
        var entry = new CatalogEntry(Read("""{ "es": "Servidor", "en": "Server" }"""), new Source { Name = "x" }, null);

        Assert.Single(CatalogSearch.Filter([entry], "server"));
        Assert.Single(CatalogSearch.Filter([entry], "servidor"));
    }
}

public sealed class OfficialCatalogSyncTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AnchorPS5Tests", Guid.NewGuid().ToString("N"));
    private readonly Source _source = new() { Name = "Oficial", Type = SourceType.Official, Url = "https://example.test/catalog.json", Path = "catalog.json" };
    private string _remote = Catalog("ftpsrv");
    private int _requests;

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string LocalFile => Path.Combine(_root, "catalog.json");

    private static string Catalog(params string[] ids) =>
        JsonSerializer.Serialize(new CatalogManifest { Apps = ids.Select(id => new HomebrewApp { Id = id, Name = id, Repo = $"autor/{id}" }).ToList() }, JsonDefaults.Options);

    private OfficialCatalogSync Sync(bool offline = false) =>
        new(new HttpClient(new Stub(_ =>
        {
            _requests++;
            if (offline)
                throw new HttpRequestException("sin red");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_remote) };
        })), _root, Path.Combine(_root, "cache"));

    private List<string> LocalIds() =>
        JsonSerializer.Deserialize<CatalogManifest>(File.ReadAllText(LocalFile), JsonDefaults.Options)!.Apps.Select(a => a.Id).ToList();

    private void AddCustomApp(string id, string? repo = null)
    {
        var local = JsonSerializer.Deserialize<CatalogManifest>(File.ReadAllText(LocalFile), JsonDefaults.Options)!;
        local.Apps.Add(new HomebrewApp { Id = id, Name = id, Repo = repo });
        File.WriteAllText(LocalFile, JsonSerializer.Serialize(local, JsonDefaults.Options));
    }

    [Fact]
    public async Task FirstSync_CreatesLocalCopy()
    {
        var result = await Sync().CheckAsync(_source);

        Assert.Equal(OfficialSyncState.Updated, result.State);
        Assert.Equal(["ftpsrv"], LocalIds());
    }

    [Fact]
    public async Task SameVersion_IsUpToDate_AndKeepsLocalEdits()
    {
        await Sync().CheckAsync(_source);
        AddCustomApp("mia");

        var result = await Sync().CheckAsync(_source);

        Assert.Equal(OfficialSyncState.UpToDate, result.State);
        Assert.Equal(["ftpsrv", "mia"], LocalIds());
    }

    [Fact]
    public async Task NewVersion_WithoutCustomApps_IsAppliedSilently()
    {
        await Sync().CheckAsync(_source);
        _remote = Catalog("ftpsrv", "nanodns");

        var result = await Sync().CheckAsync(_source);

        Assert.Equal(OfficialSyncState.Updated, result.State);
        Assert.Equal(["ftpsrv", "nanodns"], LocalIds());
    }

    [Fact]
    public async Task NewVersion_WithCustomApps_AsksAndCanKeepThem()
    {
        await Sync().CheckAsync(_source);
        AddCustomApp("mia");
        _remote = Catalog("ftpsrv", "nanodns");

        var sync = Sync();
        var result = await sync.CheckAsync(_source);

        Assert.Equal(OfficialSyncState.NeedsDecision, result.State);
        Assert.Equal(["mia"], result.CustomApps.Select(a => a.Id));
        Assert.Equal(["ftpsrv", "mia"], LocalIds()); // aún sin tocar

        sync.Apply(result, OfficialSyncChoice.KeepMine);

        Assert.Equal(["ftpsrv", "nanodns", "mia"], LocalIds());
        Assert.Equal(OfficialSyncState.UpToDate, (await Sync().CheckAsync(_source)).State);
    }

    [Fact]
    public async Task NewVersion_WithCustomApps_CanBeReplaced()
    {
        await Sync().CheckAsync(_source);
        AddCustomApp("mia");
        _remote = Catalog("ftpsrv", "nanodns");

        var sync = Sync();
        sync.Apply(await sync.CheckAsync(_source), OfficialSyncChoice.ReplaceAll);

        Assert.Equal(["ftpsrv", "nanodns"], LocalIds());
    }

    [Fact]
    public async Task CustomApp_ThatBecomesOfficial_IsNoLongerCustom()
    {
        await Sync().CheckAsync(_source);
        AddCustomApp("github:autor/nanodns", repo: "https://github.com/autor/nanodns");
        _remote = Catalog("ftpsrv", "nanodns"); // mismo repo, otro id

        var result = await Sync().CheckAsync(_source);

        Assert.Equal(OfficialSyncState.Updated, result.State);
        Assert.Equal(["ftpsrv", "nanodns"], LocalIds());
    }

    [Fact]
    public async Task Offline_KeepsLocalCopy()
    {
        await Sync().CheckAsync(_source);
        AddCustomApp("mia");

        var result = await Sync(offline: true).CheckAsync(_source);

        Assert.Equal(OfficialSyncState.Failed, result.State);
        Assert.Equal(["ftpsrv", "mia"], LocalIds());
    }

    [Fact]
    public async Task InvalidRemote_IsIgnored()
    {
        await Sync().CheckAsync(_source);
        _remote = "{ esto no es json";

        Assert.Equal(OfficialSyncState.Failed, (await Sync().CheckAsync(_source)).State);
        Assert.Equal(["ftpsrv"], LocalIds());
    }

    [Fact]
    public async Task LocalFileUrl_WorksForTesting()
    {
        var official = Path.Combine(_root, "repo", "catalog.json");
        Directory.CreateDirectory(Path.GetDirectoryName(official)!);
        File.WriteAllText(official, Catalog("garlic"));
        var source = new Source { Name = "Oficial", Type = SourceType.Official, Url = new Uri(official).AbsoluteUri };

        await Sync().CheckAsync(source);

        Assert.Equal(0, _requests);
        Assert.Equal(["garlic"], LocalIds());
    }

    [Fact]
    public async Task Loader_ReadsTheLocalCopy()
    {
        await Sync().CheckAsync(_source);

        var result = await new SourceLoader(new HttpClient(), _root).LoadAllAsync([_source]);

        Assert.Equal(["ftpsrv"], result.Entries.Select(e => e.App.Id));
    }

    private sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}

public sealed class AppOriginTests
{
    private static readonly CatalogManifest Official = new() { Apps = [new HomebrewApp { Id = "ftpsrv", Name = "ftpsrv", Repo = "autor/ftpsrv" }] };

    private static CatalogEntry Entry(string id, SourceType type, string? repo = null) =>
        new(new HomebrewApp { Id = id, Name = id, Repo = repo }, new Source { Name = "x", Type = type }, null);

    [Fact]
    public void OfficialSource_DistinguishesOfficialFromCustom()
    {
        Assert.Equal(AppOrigin.Official, OfficialCatalogSync.GetOrigin(Entry("ftpsrv", SourceType.Official), Official));
        Assert.Equal(AppOrigin.Official, OfficialCatalogSync.GetOrigin(Entry("otro-id", SourceType.Official, "https://github.com/autor/ftpsrv"), Official));
        Assert.Equal(AppOrigin.Custom, OfficialCatalogSync.GetOrigin(Entry("mia", SourceType.Official), Official));
    }

    [Fact]
    public void WithoutOfficialVersionYet_EverythingInTheCopyIsOfficial()
    {
        Assert.Equal(AppOrigin.Official, OfficialCatalogSync.GetOrigin(Entry("mia", SourceType.Official), null));
    }

    [Fact]
    public void OtherSources_AreCustomOrExternal()
    {
        Assert.Equal(AppOrigin.Custom, OfficialCatalogSync.GetOrigin(Entry("a", SourceType.Local), Official));
        Assert.Equal(AppOrigin.External, OfficialCatalogSync.GetOrigin(Entry("a", SourceType.Remote), Official));
    }
}
