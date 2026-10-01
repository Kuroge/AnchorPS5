using System.Text.Json;
using AnchorPS5.Core.Catalog;
using AnchorPS5.Core.Configuration;
using AnchorPS5.Core.Library;
using AnchorPS5.Core.Models;

namespace AnchorPS5.Core.Tests;

public sealed class AppVersionTests
{
    [Theory]
    [InlineData("1.2", "v1.2.0", 0)]
    [InlineData("1.10", "1.9", 1)]
    [InlineData("0.1.3", "0.2.3", -1)]
    [InlineData("2.0", "1.9.9", 1)]
    [InlineData("1.0.0-beta", "1.0.0", -1)]
    [InlineData("1.0.0beta", "1.0.0-beta", 0)]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.10", -1)]
    [InlineData("1.0.0-alpha", "1.0.0-beta", -1)]
    [InlineData("1.0.0+build.5", "1.0.0", 0)]
    [InlineData("nightly", "NIGHTLY", 0)]
    public void Compares(string a, string b, int expected)
    {
        Assert.Equal(expected, Math.Sign(AppVersion.Parse(a).CompareTo(AppVersion.Parse(b))));
        Assert.Equal(-expected, Math.Sign(AppVersion.Parse(b).CompareTo(AppVersion.Parse(a))));
    }

    [Fact]
    public void EqualVersions_HaveEqualHashes() =>
        Assert.Equal(AppVersion.Parse("1.2").GetHashCode(), AppVersion.Parse("v1.2.0").GetHashCode());
}

public sealed class PathNamesTests
{
    [Theory]
    [InlineData("Hola Mundo", "Hola Mundo")]
    [InlineData("App: v2/beta?", "App_ v2_beta_")]
    [InlineData("Nombre. ", "Nombre")]
    [InlineData("CON", "_CON")]
    [InlineData("con.txt", "_con.txt")]
    [InlineData("   ", "_")]
    [InlineData(null, "_")]
    public void Sanitize(string? input, string expected) => Assert.Equal(expected, PathNames.Sanitize(input));
}

public sealed class LibraryServiceTests : IDisposable
{
    private readonly string _downloads = Path.Combine(Path.GetTempPath(), "AnchorPS5Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_downloads))
            Directory.Delete(_downloads, recursive: true);
    }

    private static HomebrewApp App(string id, string name, string version) => new() { Id = id, Name = name, Version = version };

    private void CreateVersion(string appFolder, string versionFolder, VersionMetadata? metadata = null)
    {
        var dir = Path.Combine(_downloads, appFolder, versionFolder);
        Directory.CreateDirectory(dir);
        if (metadata is not null)
            File.WriteAllText(Path.Combine(dir, VersionMetadata.FileName), JsonSerializer.Serialize(metadata, JsonDefaults.Options));
    }

    [Fact]
    public void Scan_MissingFolder_IsEmpty()
    {
        var library = new LibraryService(Path.Combine(_downloads, "no-existe")).Scan();

        Assert.Empty(library.GetVersions(App("a", "A", "1.0")));
    }

    [Fact]
    public void ManualCopies_MatchByFolderName_SortedNewestFirst()
    {
        CreateVersion("Hola Mundo", "0.9");
        CreateVersion("Hola Mundo", "1.0.0");
        CreateVersion("hola mundo", "0.10");

        var versions = new LibraryService(_downloads).Scan().GetVersions(App("com.ej.hola", "Hola Mundo", "1.0.0"));

        Assert.Equal(["1.0.0", "0.10", "0.9"], versions.Select(v => v.Version));
    }

    [Fact]
    public void Metadata_MatchesById_EvenInRenamedFolder()
    {
        CreateVersion("Carpeta renombrada", "x", new VersionMetadata { Id = "com.ej.rep", Version = "2.1.0", DownloadedAt = DateTimeOffset.UnixEpoch });
        // Misma carpeta de nombre pero de otra app (por id): no cuenta.
        CreateVersion("Reproductor", "9.9", new VersionMetadata { Id = "com.otro", Version = "9.9" });

        var versions = new LibraryService(_downloads).Scan().GetVersions(App("com.ej.rep", "Reproductor", "2.3.1"));

        var version = Assert.Single(versions);
        Assert.Equal("2.1.0", version.Version);
        Assert.Equal(DateTimeOffset.UnixEpoch, version.DownloadedAt);
    }

    [Fact]
    public void Status_DetectsUpdate()
    {
        CreateVersion("Reproductor", "2.1.0");
        CreateVersion("Monitor", "1.4");
        var library = new LibraryService(_downloads).Scan();

        var reproductor = App("r", "Reproductor", "2.3.1");
        var monitor = App("m", "Monitor", "1.4.0");
        var explorador = App("e", "Explorador", "0.9.4");

        Assert.Equal(PackageState.UpdateAvailable, PackageStatus.Compute(reproductor, library.GetVersions(reproductor)).State);
        Assert.Equal(PackageState.Downloaded, PackageStatus.Compute(monitor, library.GetVersions(monitor)).State);
        Assert.Equal(PackageState.NotDownloaded, PackageStatus.Compute(explorador, library.GetVersions(explorador)).State);
    }

    [Fact]
    public void Status_LocalNewerThanCatalog_IsDownloaded()
    {
        CreateVersion("App", "3.0");
        var app = App("a", "App", "2.0");

        Assert.Equal(PackageState.Downloaded, PackageStatus.Compute(app, new LibraryService(_downloads).Scan().GetVersions(app)).State);
    }

    [Fact]
    public void VersionFolder_IsSanitized()
    {
        var service = new LibraryService(_downloads);

        Assert.Equal(Path.Combine(_downloads, "App_ Pro", "1.0_beta"), service.GetVersionFolder(App("a", "App: Pro", "1.0"), "1.0/beta"));
    }
}

public sealed class SeenAppsServiceTests : IDisposable
{
    private readonly AppPaths _paths = new(Path.Combine(Path.GetTempPath(), "AnchorPS5Tests", Guid.NewGuid().ToString("N")));

    public void Dispose()
    {
        if (Directory.Exists(_paths.BaseDirectory))
            Directory.Delete(_paths.BaseDirectory, recursive: true);
    }

    [Fact]
    public void FirstRun_MarksNothingAsNew_ThenDetectsAdditions()
    {
        var service = new SeenAppsService(_paths);

        Assert.Empty(service.RegisterAndGetNew(["a", "b"]));
        Assert.Equal(["c"], service.RegisterAndGetNew(["a", "b", "c"]));
        // Ya vista: deja de ser nueva en la siguiente apertura.
        Assert.Empty(service.RegisterAndGetNew(["a", "b", "c"]));
    }

    [Fact]
    public void MissingSource_DoesNotForgetApps()
    {
        var service = new SeenAppsService(_paths);
        service.RegisterAndGetNew(["a", "b"]);

        service.RegisterAndGetNew(["a"]);

        Assert.Empty(service.RegisterAndGetNew(["a", "b"]));
    }
}

public sealed class CatalogViewTests
{
    [Theory]
    [InlineData(CatalogFilter.All, PackageState.NotDownloaded, false, true)]
    [InlineData(CatalogFilter.Downloaded, PackageState.NotDownloaded, false, false)]
    [InlineData(CatalogFilter.Downloaded, PackageState.UpdateAvailable, false, true)]
    [InlineData(CatalogFilter.Updates, PackageState.Downloaded, false, false)]
    [InlineData(CatalogFilter.Updates, PackageState.UpdateAvailable, false, true)]
    [InlineData(CatalogFilter.New, PackageState.NotDownloaded, true, true)]
    [InlineData(CatalogFilter.New, PackageState.Downloaded, false, false)]
    public void Filter(CatalogFilter filter, PackageState state, bool isNew, bool expected) =>
        Assert.Equal(expected, CatalogView.Matches(filter, state, isNew));

    [Fact]
    public void WhatsNew_PutsNewThenUpdatesFirst()
    {
        var items = new[]
        {
            ("Zeta", PackageState.NotDownloaded, false),
            ("Beta", PackageState.UpdateAvailable, false),
            ("Alfa", PackageState.Downloaded, false),
            ("Omega", PackageState.NotDownloaded, true),
        };

        var sorted = CatalogView.Sort(items, CatalogSort.WhatsNew, i => i.Item1, i => i.Item2, i => i.Item3).Select(i => i.Item1);

        Assert.Equal(["Omega", "Beta", "Alfa", "Zeta"], sorted);
    }
}
