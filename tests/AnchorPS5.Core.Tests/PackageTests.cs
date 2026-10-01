using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AnchorPS5.Core.Configuration;
using AnchorPS5.Core.Downloads;
using AnchorPS5.Core.GitHub;
using AnchorPS5.Core.Library;
using AnchorPS5.Core.Models;
using AnchorPS5.Core.Packages;

namespace AnchorPS5.Core.Tests;

public sealed class AssetClassifierTests
{
    [Theory]
    [InlineData("ftpsrv-ps5.elf", false)]
    [InlineData("app-1.2.zip", false)]
    [InlineData("app-1.2.zip.sha256", true)]
    [InlineData("checksums.txt", true)]
    [InlineData("SHA256SUMS", true)]
    [InlineData("app.elf.sig", true)]
    [InlineData("app-src.zip", true)]
    [InlineData("source-1.2.tar.gz", true)]
    [InlineData("resources.zip", false)]
    public void Excluded(string name, bool expected) => Assert.Equal(expected, AssetClassifier.IsExcluded(name));

    [Theory]
    [InlineData("ftpsrv-ps4.elf", ConsolePlatform.PS4)]
    [InlineData("FTPSRV-PS5.elf", ConsolePlatform.PS5)]
    [InlineData("nanodns.elf", ConsolePlatform.Unknown)]
    public void Platform(string name, ConsolePlatform expected) => Assert.Equal(expected, AssetClassifier.DetectPlatform(name));

    [Theory]
    [InlineData("app-v1.2.0.zip", "1.2.0", "app.zip")]
    [InlineData("app-v1.3.0.zip", "v1.3.0", "app.zip")]
    [InlineData("app_1.2.0-beta.3.7z", "1.2.0-beta.3", "app.7z")]
    [InlineData("ftpsrv-ps5.elf", "0.21.1", "ftpsrv-ps5.elf")]
    [InlineData("ftpsrv-ps5-install.elf", "0.21.1", "ftpsrv-ps5-install.elf")]
    [InlineData("Tool-2.0-ps5.zip", "2.0", "tool-ps5.zip")]
    public void Key_RemovesVersion(string name, string version, string expected) =>
        Assert.Equal(expected, AssetClassifier.GetKey(name, version));
}

public sealed class PackageResolverTests
{
    private static GitHubRelease Release(string tag, bool pre, int day, params string[] assets) => new()
    {
        TagName = tag,
        Prerelease = pre,
        PublishedAt = new DateTimeOffset(2026, 9, day, 0, 0, 0, TimeSpan.Zero),
        Assets = assets.Select(a => new GitHubAsset { Name = a, Size = 10, BrowserDownloadUrl = "https://example.com/" + a, Digest = "sha256:" + new string('a', 64) }).ToList(),
    };

    private static readonly HomebrewApp Ftpsrv = new()
    {
        Id = "github:ps5-payload-dev/ftpsrv",
        Name = "ftpsrv",
        Repo = "ps5-payload-dev/ftpsrv",
        Assets = [new AssetRule { Match = "*-install.elf", Label = "Instalador" }],
    };

    [Fact]
    public void LatestStable_AllFilesExceptAuxiliary_WithRulesAndPlatforms()
    {
        var package = PackageResolver.FromReleases(Ftpsrv,
        [
            Release("v0.21.1", false, 20, "ftpsrv-ps4.elf", "ftpsrv-ps5.elf", "ftpsrv-ps5-install.elf", "checksums.txt"),
            Release("v0.21", false, 2, "ftpsrv-ps5.elf"),
        ], GitHubStatus.Ok);

        Assert.Equal("0.21.1", package.StableVersion);
        Assert.Null(package.BetaVersion);
        Assert.Equal(["ftpsrv-ps4.elf", "ftpsrv-ps5.elf", "ftpsrv-ps5-install.elf"], package.Files.Select(f => f.FileName));
        Assert.Equal(ConsolePlatform.PS4, package.Files[0].Platform);
        Assert.Equal("Instalador", package.Files[2].Label);
        Assert.Equal(new string('a', 64), package.Files[1].Sha256);
    }

    [Fact]
    public void NewerBeta_IsAddedAndMarked_OlderBetaIgnored()
    {
        var withNewer = PackageResolver.FromReleases(Ftpsrv,
        [
            Release("v0.22-beta1", true, 25, "ftpsrv-ps5.elf"),
            Release("v0.21.1", false, 20, "ftpsrv-ps5.elf"),
        ], GitHubStatus.Ok);
        Assert.Equal("0.22-beta1", withNewer.BetaVersion);
        Assert.Contains(withNewer.Files, f => f.IsPrerelease && f.Version == "0.22-beta1");

        var withOlder = PackageResolver.FromReleases(Ftpsrv,
        [
            Release("v0.21.1", false, 20, "ftpsrv-ps5.elf"),
            Release("v0.21-rc1", true, 10, "ftpsrv-ps5.elf"),
        ], GitHubStatus.Ok);
        Assert.Null(withOlder.BetaVersion);
        Assert.DoesNotContain(withOlder.Files, f => f.IsPrerelease);
    }

    [Fact]
    public void DecimalNumbering_OlderBeta_IsNotShown()
    {
        // Caso real de Garlic: 1.6 → 1.61b → 1.62b (betas) → 1.7. 1.62 > 1.7 por números, pero es anterior.
        var garlic = new HomebrewApp { Id = "garlic", Repo = "earthonion/garlic-savemgr" };
        var releases = new[]
        {
            Release("v1.7", false, 16, "garlic-savemgr.elf"),
            Release("v1.62b", true, 15, "garlic-savemgr.elf"),
            Release("v1.61b", true, 14, "garlic-savemgr.elf"),
            Release("v1.6", false, 13, "garlic-savemgr.elf"),
        };

        var package = PackageResolver.FromReleases(garlic, releases, GitHubStatus.Ok);

        Assert.Equal("1.7", package.StableVersion);
        Assert.Null(package.BetaVersion);
        Assert.Single(package.Files);
    }

    [Fact]
    public void BetaPublishedAfterStable_IsShown_EvenWithLowerNumber()
    {
        var app = new HomebrewApp { Id = "a", Repo = "o/r" };

        var package = PackageResolver.FromReleases(app,
            [Release("v1.7", false, 16, "a.elf"), Release("v1.71b", true, 20, "a.elf")], GitHubStatus.Ok);

        Assert.Equal("1.71b", package.BetaVersion);
    }

    [Fact]
    public void HiddenRule_RemovesFile()
    {
        var app = new HomebrewApp { Id = "a", Repo = "o/r", Assets = [new AssetRule { Match = "*debug*", Hidden = true }] };

        var package = PackageResolver.FromReleases(app, [Release("1.0", false, 1, "app.elf", "app-debug.elf")], GitHubStatus.Ok);

        Assert.Equal(["app.elf"], package.Files.Select(f => f.FileName));
    }

    [Theory]
    [InlineData("ps5-payload-dev/ftpsrv", "ps5-payload-dev", "ftpsrv")]
    [InlineData("https://github.com/drakmor/nanoDNS", "drakmor", "nanoDNS")]
    [InlineData("https://github.com/earthonion/garlic-savemgr.git/", "earthonion", "garlic-savemgr")]
    public void RepoRef_Parses(string text, string owner, string name)
    {
        Assert.True(GitHubRepoRef.TryParse(text, out var repo));
        Assert.Equal((owner, name), (repo.Owner, repo.Name));
    }

    [Theory]
    [InlineData("https://gitlab.com/a/b")]
    [InlineData("solo-un-nombre")]
    [InlineData("")]
    public void RepoRef_RejectsOthers(string text) => Assert.False(GitHubRepoRef.TryParse(text, out _));
}

public sealed class FileStatusTests
{
    private static PackageFile File(string name, string version, bool pre = false) =>
        new(AssetClassifier.GetKey(name, version), name, "https://example.com/" + name, 1, null, version, pre, AssetClassifier.DetectPlatform(name));

    private static InstalledVersion Installed(string version, params (string Name, bool Pre)[] files) =>
        new(version, @"C:\d\app\" + version, "app", null, Files: files.Select(f => new InstalledFile(AssetClassifier.GetKey(f.Name, version), f.Name, f.Name, null, true, null, f.Pre)).ToList());

    [Fact]
    public void EachFileHasItsOwnVersion()
    {
        // Tienes el elf de la 1.0.0 y el instalador de la 1.2.0; la última es 1.2.0 con un fichero nuevo.
        var package = new ResolvedPackage(
            [File("ftpsrv-ps5.elf", "1.2.0"), File("ftpsrv-ps5-install.elf", "1.2.0"), File("ftpsrv-ps5-extra.elf", "1.2.0")],
            "1.2.0", null, null, GitHubStatus.Ok);
        var versions = new[] { Installed("1.2.0", ("ftpsrv-ps5-install.elf", false)), Installed("1.0.0", ("ftpsrv-ps5.elf", false)) };

        var status = PackageStatus.Compute(package, versions);

        var elf = status.Files.Single(f => f.Key == "ftpsrv-ps5.elf");
        Assert.Equal(FileState.UpdateAvailable, elf.State);
        Assert.Equal("1.0.0", elf.Installed!.Version.Version);

        Assert.Equal(FileState.UpToDate, status.Files.Single(f => f.Key == "ftpsrv-ps5-install.elf").State);
        Assert.Equal(FileState.NotDownloaded, status.Files.Single(f => f.Key == "ftpsrv-ps5-extra.elf").State);
        Assert.Equal(PackageState.UpdateAvailable, status.State);
    }

    [Fact]
    public void ReleaseDates_DecideUpdates_OverVersionNumbers()
    {
        var march = (int day) => new DateTimeOffset(2026, 3, day, 0, 0, 0, TimeSpan.Zero);
        var latest = new PackageFile("garlic-savemgr.elf", "garlic-savemgr.elf", "https://x", 1, null, "1.7", false, ConsolePlatform.Unknown, ReleasedAt: march(16));
        var package = new ResolvedPackage([latest], "1.7", null, null, GitHubStatus.Ok);

        InstalledVersion With(string version, int day) => new(version, @"C:\d\" + version, "garlic", null,
            Files: [new InstalledFile("garlic-savemgr.elf", "garlic-savemgr.elf", "garlic-savemgr.elf", null, true, null, false, march(day))]);

        // Tienes la 1.62b (15 mar): la 1.7 (16 mar) es más nueva aunque 62 > 7.
        Assert.Equal(PackageState.UpdateAvailable, PackageStatus.Compute(package, [With("1.62b", 15)]).State);

        // Tienes la 1.7 y la 1.62b: la última que tienes es la 1.7 → al día.
        var both = PackageStatus.Compute(package, [With("1.62b", 15), With("1.7", 16)]);
        Assert.Equal(PackageState.Downloaded, both.State);
        Assert.Equal("1.7", both.Files.Single().Installed!.Version.Version);
    }

    [Theory]
    [InlineData("1.7", 16, "1.62b", 15, 1)]
    [InlineData("1.62b", 15, "1.7", 16, -1)]
    [InlineData("v1.7", 16, "1.7", 10, 0)]   // misma versión: iguales aunque cambie la fecha
    public void ReleaseOrder_UsesDates(string a, int dayA, string b, int dayB, int expected)
    {
        var march = (int day) => new DateTimeOffset(2026, 3, day, 0, 0, 0, TimeSpan.Zero);
        Assert.Equal(expected, Math.Sign(ReleaseOrder.Compare(a, march(dayA), b, march(dayB))));
    }

    [Fact]
    public void ReleaseOrder_WithoutDates_UsesVersion() =>
        Assert.True(ReleaseOrder.IsNewer("1.10", null, "1.9", null));

    [Fact]
    public void FileNoLongerPublished_IsKept()
    {
        var package = new ResolvedPackage([File("app.elf", "2.0")], "2.0", null, null, GitHubStatus.Ok);

        var status = PackageStatus.Compute(package, [Installed("2.0", ("app.elf", false), ("viejo.elf", false))]);

        Assert.Equal(FileState.NoLongerPublished, status.Files.Single(f => f.Key == "viejo.elf").State);
        Assert.Equal(PackageState.Downloaded, status.State);
    }

    [Fact]
    public void Beta_OnlyCountsAsUpdate_IfYouUseTheBeta()
    {
        var package = new ResolvedPackage([File("app.elf", "1.0"), File("app.elf", "1.1-beta", pre: true)], "1.0", "1.1-beta", null, GitHubStatus.Ok);

        var onStable = PackageStatus.Compute(package, [Installed("1.0", ("app.elf", false))]);
        Assert.Equal(PackageState.Downloaded, onStable.State);
        Assert.Contains(onStable.Files, f => f.State == FileState.UpdateAvailable && !f.CountsForUpdate);

        var onBeta = PackageStatus.Compute(package, [Installed("1.1-alpha", ("app.elf", true))]);
        Assert.Equal(PackageState.UpdateAvailable, onBeta.State);
    }
}

public sealed class GitHubClientTests : IDisposable
{
    private readonly string _cache = Path.Combine(Path.GetTempPath(), "AnchorPS5Tests", Guid.NewGuid().ToString("N"));
    private static readonly GitHubRepoRef Repo = new("o", "r");
    private const string Body = """[{ "tag_name": "v1.0", "prerelease": false, "assets": [ { "name": "a.elf", "size": 3, "browser_download_url": "https://x/a.elf", "digest": "sha256:abc" } ] }]""";

    public void Dispose()
    {
        if (Directory.Exists(_cache))
            Directory.Delete(_cache, recursive: true);
    }

    private GitHubClient Client(Func<HttpRequestMessage, HttpResponseMessage> respond, string? token = null) =>
        new(new HttpClient(new Stub(respond)), _cache, () => token);

    private static HttpResponseMessage Ok(string etag)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Body) };
        response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(etag);
        return response;
    }

    [Fact]
    public async Task SecondRequest_SendsETag_AndUsesCacheOn304()
    {
        string? sentEtag = null;
        var calls = 0;
        var client = Client(request =>
        {
            calls++;
            sentEtag = request.Headers.IfNoneMatch.FirstOrDefault()?.Tag;
            return calls == 1 ? Ok("\"v1\"") : new HttpResponseMessage(HttpStatusCode.NotModified);
        });

        await client.GetReleasesAsync(Repo);
        var second = await client.GetReleasesAsync(Repo);

        Assert.Equal("\"v1\"", sentEtag);
        Assert.Equal(GitHubStatus.Ok, second.Status);
        Assert.Equal("abc", second.Value![0].Assets[0].Sha256);
    }

    [Fact]
    public async Task RateLimit_IsReported_WithCachedData()
    {
        await Client(_ => Ok("\"v1\"")).GetReleasesAsync(Repo);

        var limited = await Client(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
            response.Headers.Add("X-RateLimit-Remaining", "0");
            response.Headers.Add("X-RateLimit-Reset", "1790000000");
            return response;
        }).GetReleasesAsync(Repo);

        Assert.Equal(GitHubStatus.RateLimited, limited.Status);
        Assert.NotNull(limited.Value);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1790000000), limited.RateLimitReset);
    }

    [Fact]
    public async Task Offline_UsesCache()
    {
        await Client(_ => Ok("\"v1\"")).GetReleasesAsync(Repo);

        var offline = await Client(_ => throw new HttpRequestException("sin red")).GetReleasesAsync(Repo);

        Assert.Equal(GitHubStatus.FromCache, offline.Status);
        Assert.Single(offline.Value!);
    }

    [Fact]
    public async Task Token_IsSentAsBearer()
    {
        string? auth = null;
        await Client(request =>
        {
            auth = request.Headers.Authorization?.ToString();
            return Ok("\"v1\"");
        }, token: "abc123").GetReleasesAsync(Repo);

        Assert.Equal("Bearer abc123", auth);
    }

    private sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}

public sealed class MultiFileDownloadTests : IDisposable
{
    private readonly string _downloads = Path.Combine(Path.GetTempPath(), "AnchorPS5Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_downloads))
            Directory.Delete(_downloads, recursive: true);
    }

    [Fact]
    public async Task TwoFilesOfSameVersion_LiveTogether_EachWithItsMetadata()
    {
        var bodies = new Dictionary<string, byte[]>
        {
            ["ftpsrv-ps5.elf"] = Encoding.UTF8.GetBytes("payload"),
            ["ftpsrv-ps5-install.elf"] = Encoding.UTF8.GetBytes("installer"),
        };
        var http = new HttpClient(new Stub(request =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bodies[Path.GetFileName(request.RequestUri!.AbsolutePath)]) }));
        var library = new LibraryService(_downloads);
        var manager = new DownloadManager(http, library, new NoExtractor(), 2);
        var app = new HomebrewApp { Id = "github:ps5-payload-dev/ftpsrv", Name = "ftpsrv" };

        PackageFile File(string name) => new(AssetClassifier.GetKey(name, "0.21.1"), name, "https://example.com/" + name,
            bodies[name].Length, Convert.ToHexStringLower(SHA256.HashData(bodies[name])), "0.21.1", false, ConsolePlatform.PS5);

        await RunAsync(manager, app, File("ftpsrv-ps5.elf"));
        await RunAsync(manager, app, File("ftpsrv-ps5-install.elf"));

        var version = Assert.Single(library.Scan().GetVersions(app));
        Assert.Equal(["ftpsrv-ps5-install.elf", "ftpsrv-ps5.elf"], version.Files.Select(f => f.FileName).Order());
        Assert.All(version.Files, f => Assert.True(f.Verified));
        Assert.True(System.IO.File.Exists(Path.Combine(version.FolderPath, "ftpsrv-ps5.elf")));
        Assert.True(System.IO.File.Exists(Path.Combine(version.FolderPath, "ftpsrv-ps5-install.elf")));
    }

    [Fact]
    public void LegacyMetadata_IsReadAsSingleFile()
    {
        var dir = Path.Combine(_downloads, "Hola", "1.0.0");
        Directory.CreateDirectory(dir);
        System.IO.File.WriteAllText(Path.Combine(dir, "hola.elf"), "x");
        System.IO.File.WriteAllText(Path.Combine(dir, VersionMetadata.FileName),
            """{ "id": "hola", "version": "1.0.0", "sha256": "abc", "verified": true, "downloadedAt": "2026-09-28T18:30:00+02:00" }""");

        var version = Assert.Single(new LibraryService(_downloads).Scan().GetVersions(new HomebrewApp { Id = "hola", Name = "Hola" }));

        var file = Assert.Single(version.Files);
        Assert.Equal(("hola.elf", "abc", true), (file.Key, file.Sha256, file.Verified));
    }

    private static async Task RunAsync(DownloadManager manager, HomebrewApp app, PackageFile file)
    {
        var done = new TaskCompletionSource<DownloadJob>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(object? s, DownloadJob j) => done.TrySetResult(j);
        manager.JobFinished += Handler;
        manager.Enqueue(app, file);
        var job = await done.Task.WaitAsync(TimeSpan.FromSeconds(10));
        manager.JobFinished -= Handler;
        Assert.Equal(DownloadPhase.Completed, job.Phase);
    }

    private sealed class NoExtractor : IArchiveExtractor
    {
        public Task ExtractAsync(string archivePath, string destination, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
