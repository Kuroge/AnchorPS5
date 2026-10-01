using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using AnchorPS5.Core.Downloads;
using AnchorPS5.Core.Library;
using AnchorPS5.Core.Models;

namespace AnchorPS5.Core.Tests;

public sealed class DownloadManagerTests : IDisposable
{
    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("contenido del homebrew");
    private static readonly string PayloadSha = Convert.ToHexStringLower(SHA256.HashData(Payload));

    private readonly string _downloads = Path.Combine(Path.GetTempPath(), "AnchorPS5Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_downloads))
            Directory.Delete(_downloads, recursive: true);
    }

    private static HomebrewApp App(string url = "https://example.com/files/hola.elf", string? sha = null) => new()
    {
        Id = "com.ej.hola",
        Name = "Hola Mundo",
        Version = "1.0.0",
        DownloadUrl = url,
        Sha256 = sha ?? PayloadSha,
    };

    private (DownloadManager Manager, LibraryService Library, FakeExtractor Extractor) Create(Func<HttpRequestMessage, Task<HttpResponseMessage>>? respond = null)
    {
        var library = new LibraryService(_downloads);
        var extractor = new FakeExtractor();
        var http = new HttpClient(new StubHandler(respond ?? (_ => Task.FromResult(Ok(Payload)))));
        return (new DownloadManager(http, library, extractor, 2), library, extractor);
    }

    private static HttpResponseMessage Ok(byte[] body) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };

    private static async Task<DownloadJob> RunToEnd(DownloadManager manager, HomebrewApp app)
    {
        var done = new TaskCompletionSource<DownloadJob>(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.JobFinished += (_, job) => done.TrySetResult(job);
        manager.Enqueue(app);
        return await done.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task PlainFile_IsVerified_MovedToVersionFolder_WithMetadata()
    {
        var (manager, library, extractor) = Create();

        var job = await RunToEnd(manager, App());

        Assert.Equal(DownloadPhase.Completed, job.Phase);
        Assert.Equal(Path.Combine(_downloads, "Hola Mundo", "1.0.0"), job.InstalledPath);
        Assert.Equal(Payload, File.ReadAllBytes(Path.Combine(job.InstalledPath!, "hola.elf")));
        Assert.False(extractor.Called);

        var installed = Assert.Single(library.Scan().GetVersions(App()));
        Assert.Equal("com.ej.hola", installed.AppId);
        Assert.True(installed.Verified);
        Assert.False(Directory.Exists(library.TempRoot) && Directory.EnumerateFileSystemEntries(library.TempRoot).Any());
    }

    [Fact]
    public async Task HashMismatch_FailsAndLeavesNothing()
    {
        var (manager, library, _) = Create();

        var job = await RunToEnd(manager, App(sha: new string('0', 64)));

        Assert.Equal(DownloadPhase.Failed, job.Phase);
        Assert.Equal(DownloadError.HashMismatch, job.Error);
        Assert.Empty(library.Scan().GetVersions(App()));
        Assert.False(Directory.Exists(Path.Combine(_downloads, "Hola Mundo")));
    }

    [Fact]
    public async Task NoHashInCatalog_DownloadsButNotVerified()
    {
        var (manager, library, _) = Create();

        var job = await RunToEnd(manager, App(sha: ""));

        Assert.Equal(DownloadPhase.Completed, job.Phase);
        Assert.False(Assert.Single(library.Scan().GetVersions(App())).Verified);
    }

    [Fact]
    public async Task Archive_IsExtracted()
    {
        var (manager, _, extractor) = Create();

        var job = await RunToEnd(manager, App("https://example.com/files/hola-1.0.0.zip"));

        Assert.Equal(DownloadPhase.Completed, job.Phase);
        Assert.True(extractor.Called);
        Assert.True(File.Exists(Path.Combine(job.InstalledPath!, "extraido.txt")));
    }

    [Fact]
    public async Task HttpError_IsNetworkError()
    {
        var (manager, _, _) = Create(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        var job = await RunToEnd(manager, App());

        Assert.Equal(DownloadError.Network, job.Error);
        Assert.Contains("404", job.ErrorDetail);
    }

    [Theory]
    [InlineData("ftp://example.com/a.zip")]
    [InlineData("no es una url")]
    [InlineData("")]
    public async Task InvalidUrl_Fails(string url)
    {
        var (manager, _, _) = Create();

        var job = await RunToEnd(manager, App(url));

        Assert.Equal(DownloadError.InvalidUrl, job.Error);
    }

    [Fact]
    public async Task Cancel_StopsAndLeavesNothing()
    {
        var gate = new TaskCompletionSource();
        var (manager, library, _) = Create(async request =>
        {
            await gate.Task;
            return Ok(Payload);
        });

        var done = new TaskCompletionSource<DownloadJob>();
        manager.JobFinished += (_, j) => done.TrySetResult(j);
        var job = manager.Enqueue(App());
        job.Cancel();
        gate.SetResult();

        Assert.Equal(DownloadPhase.Canceled, (await done.Task.WaitAsync(TimeSpan.FromSeconds(10))).Phase);
        Assert.Empty(library.Scan().GetVersions(App()));
    }

    [Fact]
    public async Task SameApp_EnqueuedTwice_ReturnsSameJob()
    {
        var gate = new TaskCompletionSource();
        var (manager, _, _) = Create(async _ =>
        {
            await gate.Task;
            return Ok(Payload);
        });

        var done = new TaskCompletionSource<DownloadJob>(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.JobFinished += (_, j) => done.TrySetResult(j);

        var first = manager.Enqueue(App());
        var second = manager.Enqueue(App());
        gate.SetResult();

        Assert.Same(first, second);
        // Esperar al final para no borrar la carpeta con la descarga aún escribiendo.
        Assert.Equal(DownloadPhase.Completed, (await done.Task.WaitAsync(TimeSpan.FromSeconds(10))).Phase);
    }

    [Fact]
    public async Task Retry_AfterFailure_Succeeds()
    {
        var attempts = 0;
        var (manager, _, _) = Create(_ => Task.FromResult(++attempts == 1 ? new HttpResponseMessage(HttpStatusCode.BadGateway) : Ok(Payload)));

        var job = await RunToEnd(manager, App());
        Assert.Equal(DownloadPhase.Failed, job.Phase);

        var done = new TaskCompletionSource<DownloadJob>();
        manager.JobFinished += (_, j) => done.TrySetResult(j);
        manager.Retry(job);

        Assert.Equal(DownloadPhase.Completed, (await done.Task.WaitAsync(TimeSpan.FromSeconds(10))).Phase);
    }

    [Fact]
    public async Task DeleteVersion_RemovesFolder_AndEmptyAppFolder()
    {
        var (manager, library, _) = Create();
        await RunToEnd(manager, App());

        library.DeleteVersion(Assert.Single(library.Scan().GetVersions(App())));

        Assert.False(Directory.Exists(Path.Combine(_downloads, "Hola Mundo")));
    }

    [Fact]
    public void DeleteVersion_OutsideDownloads_IsRefused()
    {
        var library = new LibraryService(_downloads);
        var outside = new InstalledVersion("1.0", Path.GetTempPath(), null, null);

        Assert.Throws<InvalidOperationException>(() => library.DeleteVersion(outside));
    }

    private sealed class FakeExtractor : IArchiveExtractor
    {
        public bool Called { get; private set; }

        public Task ExtractAsync(string archivePath, string destination, CancellationToken cancellationToken)
        {
            Called = true;
            File.WriteAllText(Path.Combine(destination, "extraido.txt"), "ok");
            return Task.CompletedTask;
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await respond(request);
            response.RequestMessage = request;
            return response;
        }
    }
}

/// <summary>Con el 7-Zip real incluido en la app.</summary>
public sealed class SevenZipExtractorTests : IDisposable
{
    private static readonly string SevenZip = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "AnchorPS5.App", "tools", "7zip", "7z.exe"));

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "AnchorPS5Tests", Guid.NewGuid().ToString("N"));

    public SevenZipExtractorTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task Zip_IsExtractedWithFolders()
    {
        var zip = Path.Combine(_dir, "app.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            using (var w = new StreamWriter(archive.CreateEntry("app/eboot.bin").Open()))
                w.Write("bin");
            using (var w = new StreamWriter(archive.CreateEntry("LEEME.txt").Open()))
                w.Write("hola");
        }

        var output = Path.Combine(_dir, "out");
        await new SevenZipExtractor(SevenZip).ExtractAsync(zip, output, CancellationToken.None);

        Assert.Equal("bin", File.ReadAllText(Path.Combine(output, "app", "eboot.bin")));
        Assert.Equal("hola", File.ReadAllText(Path.Combine(output, "LEEME.txt")));
    }

    [Fact]
    public async Task TarGz_IsExtractedInOneStep()
    {
        var source = Path.Combine(_dir, "src");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "a.txt"), "tar");
        var tarGz = Path.Combine(_dir, "app.tar.gz");
        await using (var file = File.Create(tarGz))
        await using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
            await TarFile.CreateFromDirectoryAsync(source, gzip, includeBaseDirectory: false);

        var output = Path.Combine(_dir, "out");
        await new SevenZipExtractor(SevenZip).ExtractAsync(tarGz, output, CancellationToken.None);

        Assert.Equal("tar", File.ReadAllText(Path.Combine(output, "a.txt")));
        Assert.False(File.Exists(Path.Combine(output, "app.tar")));
    }

    [Fact]
    public async Task ZipWithParentPaths_StaysInsideDestination()
    {
        var zip = Path.Combine(_dir, "malo.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        using (var w = new StreamWriter(archive.CreateEntry("../../fuera.txt").Open()))
            w.Write("x");

        var output = Path.Combine(_dir, "out");
        try
        {
            await new SevenZipExtractor(SevenZip).ExtractAsync(zip, output, CancellationToken.None);
        }
        catch (ExtractionException)
        {
            // Rechazarlo también es válido.
        }

        Assert.False(File.Exists(Path.Combine(_dir, "fuera.txt")));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(_dir)!, "fuera.txt")));
    }

    [Fact]
    public async Task CorruptArchive_Throws()
    {
        var zip = Path.Combine(_dir, "roto.zip");
        File.WriteAllText(zip, "esto no es un zip");

        await Assert.ThrowsAsync<ExtractionException>(() =>
            new SevenZipExtractor(SevenZip).ExtractAsync(zip, Path.Combine(_dir, "out"), CancellationToken.None));
    }
}
