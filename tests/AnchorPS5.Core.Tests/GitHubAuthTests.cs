using System.Net;
using AnchorPS5.Core.GitHub;

namespace AnchorPS5.Core.Tests;

public sealed class GitHubDeviceFlowTests
{
    private static readonly GitHubDeviceCode Code = new()
    {
        DeviceCode = "dev", UserCode = "ABCD-1234", VerificationUri = "https://github.com/login/device", ExpiresIn = 900, Interval = 5,
    };

    private static GitHubDeviceFlow Flow(Func<HttpRequestMessage, HttpResponseMessage> respond, List<TimeSpan>? waits = null) =>
        new(new HttpClient(new Stub(respond)), "client-id", (delay, _) =>
        {
            waits?.Add(delay);
            return Task.CompletedTask;
        });

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json) };

    [Fact]
    public async Task RequestCode_SendsClientId_AndParsesCode()
    {
        string? form = null;
        var (code, error) = await Flow(request =>
        {
            form = request.Content!.ReadAsStringAsync().Result;
            return Json("""{ "device_code": "dev", "user_code": "ABCD-1234", "verification_uri": "https://github.com/login/device", "expires_in": 900, "interval": 5 }""");
        }).RequestCodeAsync();

        Assert.Equal(GitHubSignInError.None, error);
        Assert.Equal("ABCD-1234", code!.UserCode);
        Assert.Contains("client_id=client-id", form);
    }

    [Fact]
    public async Task RequestCode_RejectedApp_IsReported()
    {
        var (code, error) = await Flow(_ => new HttpResponseMessage(HttpStatusCode.NotFound)).RequestCodeAsync();

        Assert.Null(code);
        Assert.Equal(GitHubSignInError.AppNotAllowed, error);
    }

    [Fact]
    public async Task WaitForToken_PollsUntilAuthorized_AndSlowsDownWhenAsked()
    {
        var waits = new List<TimeSpan>();
        var calls = 0;
        var result = await Flow(_ => ++calls switch
        {
            1 => Json("""{ "error": "authorization_pending" }"""),
            2 => Json("""{ "error": "slow_down", "interval": 10 }"""),
            _ => Json("""{ "access_token": "gho_token", "token_type": "bearer" }"""),
        }, waits).WaitForTokenAsync(Code);

        Assert.True(result.Succeeded);
        Assert.Equal("gho_token", result.Token);
        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)], waits);
    }

    [Theory]
    [InlineData("access_denied", GitHubSignInError.Denied)]
    [InlineData("expired_token", GitHubSignInError.Expired)]
    [InlineData("device_flow_disabled", GitHubSignInError.AppNotAllowed)]
    public async Task WaitForToken_ReportsErrors(string error, GitHubSignInError expected)
    {
        var result = await Flow(_ => Json($$"""{ "error": "{{error}}" }""")).WaitForTokenAsync(Code);

        Assert.False(result.Succeeded);
        Assert.Equal(expected, result.Error);
    }

    [Fact]
    public async Task WaitForToken_GivesUpWhenCodeExpires()
    {
        var calls = 0;
        var result = await Flow(_ =>
        {
            calls++;
            return Json("""{ "error": "authorization_pending" }""");
        }).WaitForTokenAsync(Code with { ExpiresIn = 20 });

        Assert.Equal(GitHubSignInError.Expired, result.Error);
        Assert.Equal(4, calls);
    }

    private sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}

public sealed class GitHubSessionTests : IDisposable
{
    private readonly string _cache = Path.Combine(Path.GetTempPath(), "AnchorPS5Tests", Guid.NewGuid().ToString("N"));
    private readonly MemoryStore _store = new();

    public void Dispose()
    {
        if (Directory.Exists(_cache))
            Directory.Delete(_cache, recursive: true);
    }

    private GitHubSession Session(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(_store, new HttpClient(new Stub(respond)), _cache);

    private static HttpResponseMessage UserJson() => new(HttpStatusCode.OK)
    {
        Content = new StringContent("""{ "login": "cheyen2008", "name": "Cheyen", "avatar_url": "https://avatars/x", "html_url": "https://github.com/cheyen2008" }"""),
    };

    [Fact]
    public async Task SignIn_SavesToken_AndLoadsProfile()
    {
        string? auth = null;
        var session = Session(request =>
        {
            auth = request.Headers.Authorization?.ToString();
            return UserJson();
        });

        await session.SignInAsync("gho_token");

        Assert.True(session.IsSignedIn);
        Assert.Equal("gho_token", _store.Token);
        Assert.Equal("Bearer gho_token", auth);
        Assert.Equal("cheyen2008", session.User!.Login);
    }

    [Fact]
    public async Task Restore_Offline_KeepsSession_WithCachedProfile()
    {
        await Session(_ => UserJson()).SignInAsync("gho_token");

        var restored = Session(_ => throw new HttpRequestException("sin red"));
        await restored.RestoreAsync();

        Assert.True(restored.IsSignedIn);
        Assert.Equal("cheyen2008", restored.User!.Login);
    }

    [Fact]
    public async Task Restore_RevokedToken_SignsOut()
    {
        _store.Save("revocado");
        var session = Session(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        await session.RestoreAsync();

        Assert.False(session.IsSignedIn);
        Assert.Null(_store.Token);
    }

    [Fact]
    public async Task SignOut_ForgetsTokenAndProfile()
    {
        var session = Session(_ => UserJson());
        await session.SignInAsync("gho_token");
        var changes = 0;
        session.Changed += (_, _) => changes++;

        session.SignOut();

        Assert.False(session.IsSignedIn);
        Assert.Null(session.User);
        Assert.Null(_store.Token);
        Assert.Equal(1, changes);
    }

    private sealed class MemoryStore : ITokenStore
    {
        public string? Token { get; private set; }

        public string? Read() => Token;

        public void Save(string token) => Token = token;

        public void Delete() => Token = null;
    }

    private sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
