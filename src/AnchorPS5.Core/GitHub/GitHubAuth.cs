using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AnchorPS5.Core.GitHub;

/// <summary>Perfil público del usuario con sesión iniciada.</summary>
public sealed record GitHubUser
{
    [JsonPropertyName("login")]
    public string Login { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; init; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; init; }

    [JsonPropertyName("bio")]
    public string? Bio { get; init; }
}

/// <summary>Código que el usuario introduce en GitHub para autorizar la app.</summary>
public sealed record GitHubDeviceCode
{
    [JsonPropertyName("device_code")]
    public string DeviceCode { get; init; } = string.Empty;

    [JsonPropertyName("user_code")]
    public string UserCode { get; init; } = string.Empty;

    [JsonPropertyName("verification_uri")]
    public string VerificationUri { get; init; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    [JsonPropertyName("interval")]
    public int Interval { get; init; }
}

public enum GitHubSignInError
{
    None,
    /// <summary>El código ha caducado sin que el usuario lo autorizara.</summary>
    Expired,
    /// <summary>El usuario ha rechazado la autorización.</summary>
    Denied,
    Network,
    /// <summary>GitHub no acepta la app (Client ID incorrecto o flujo de dispositivo desactivado).</summary>
    AppNotAllowed,
}

public sealed record GitHubSignInResult(string? Token, GitHubSignInError Error)
{
    public bool Succeeded => Token is not null;
}

/// <summary>
/// Inicio de sesión con el flujo de dispositivo de GitHub: la app pide un código, el
/// usuario lo introduce en github.com (la app nunca ve su contraseña) y la app recibe
/// un token sin permisos sobre la cuenta (solo lectura de lo público).
/// </summary>
public sealed class GitHubDeviceFlow
{
    private const string DeviceCodeUrl = "https://github.com/login/device/code";
    private const string TokenUrl = "https://github.com/login/oauth/access_token";

    private readonly HttpClient _http;
    private readonly string _clientId;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    /// <param name="delay">Espera entre consultas (para los tests).</param>
    public GitHubDeviceFlow(HttpClient http, string clientId, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _http = http;
        _clientId = clientId;
        _delay = delay ?? Task.Delay;
    }

    /// <summary>Pide un código nuevo. Null si no se puede (sin conexión o app no aceptada).</summary>
    public async Task<(GitHubDeviceCode? Code, GitHubSignInError Error)> RequestCodeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await PostAsync(DeviceCodeUrl, new() { ["client_id"] = _clientId }, cancellationToken);
            if (json is null)
                return (null, GitHubSignInError.AppNotAllowed);

            var code = JsonSerializer.Deserialize<GitHubDeviceCode>(json);
            return code is { DeviceCode.Length: > 0, UserCode.Length: > 0 }
                ? (code, GitHubSignInError.None)
                : (null, GitHubSignInError.AppNotAllowed);
        }
        catch (Exception ex) when (IsNetworkError(ex, cancellationToken))
        {
            return (null, GitHubSignInError.Network);
        }
    }

    /// <summary>Espera a que el usuario autorice el código y devuelve el token.</summary>
    public async Task<GitHubSignInResult> WaitForTokenAsync(GitHubDeviceCode code, CancellationToken cancellationToken = default)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(code.Interval, 5));
        var remaining = TimeSpan.FromSeconds(code.ExpiresIn > 0 ? code.ExpiresIn : 900);
        var networkFailures = 0;

        while (remaining > TimeSpan.Zero)
        {
            await _delay(interval, cancellationToken);
            remaining -= interval;

            try
            {
                var json = await PostAsync(TokenUrl, new()
                {
                    ["client_id"] = _clientId,
                    ["device_code"] = code.DeviceCode,
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                }, cancellationToken);
                if (json is null)
                    return new(null, GitHubSignInError.AppNotAllowed);

                networkFailures = 0;
                var response = JsonSerializer.Deserialize<TokenResponse>(json);
                if (response?.AccessToken is { Length: > 0 } token)
                    return new(token, GitHubSignInError.None);

                switch (response?.Error)
                {
                    case "authorization_pending":
                        break;
                    case "slow_down":
                        interval = response.Interval > 0 ? TimeSpan.FromSeconds(response.Interval) : interval + TimeSpan.FromSeconds(5);
                        break;
                    case "expired_token":
                        return new(null, GitHubSignInError.Expired);
                    case "access_denied":
                        return new(null, GitHubSignInError.Denied);
                    default:
                        return new(null, GitHubSignInError.AppNotAllowed);
                }
            }
            catch (Exception ex) when (IsNetworkError(ex, cancellationToken))
            {
                // Un corte puntual no cancela el inicio de sesión; varios seguidos, sí.
                if (++networkFailures >= 3)
                    return new(null, GitHubSignInError.Network);
            }
        }

        return new(null, GitHubSignInError.Expired);
    }

    private async Task<string?> PostAsync(string url, Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static bool IsNetworkError(Exception ex, CancellationToken cancellationToken) =>
        ex is HttpRequestException or JsonException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    private sealed record TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }

        [JsonPropertyName("interval")]
        public int Interval { get; init; }
    }
}

/// <summary>Dónde se guarda el token (en Windows, el Administrador de credenciales).</summary>
public interface ITokenStore
{
    string? Read();

    void Save(string token);

    void Delete();
}

/// <summary>
/// Sesión de GitHub: token guardado + perfil público del usuario. El perfil se guarda
/// en caché para mostrarlo también sin conexión.
/// </summary>
public sealed class GitHubSession
{
    private const string UserUrl = "https://api.github.com/user";

    private readonly ITokenStore _store;
    private readonly HttpClient _http;
    private readonly string _userCacheFile;

    public GitHubSession(ITokenStore store, HttpClient http, string cacheDirectory)
    {
        _store = store;
        _http = http;
        _userCacheFile = Path.Combine(cacheDirectory, "user.json");
    }

    /// <summary>Cambia al iniciar o cerrar sesión, o al recibir el perfil.</summary>
    public event EventHandler? Changed;

    public string? Token { get; private set; }

    public GitHubUser? User { get; private set; }

    public bool IsSignedIn => Token is not null;

    /// <summary>Recupera la sesión guardada. Si GitHub ya no acepta el token (revocado), se cierra.</summary>
    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        Token = _store.Read() is { Length: > 0 } token ? token : null;
        if (Token is null)
            return;

        User = ReadCachedUser();
        Changed?.Invoke(this, EventArgs.Empty);
        await RefreshUserAsync(cancellationToken);
    }

    /// <summary>Guarda el token recién obtenido y carga el perfil.</summary>
    public async Task SignInAsync(string token, CancellationToken cancellationToken = default)
    {
        _store.Save(token);
        Token = token;
        User = null;
        Changed?.Invoke(this, EventArgs.Empty);
        await RefreshUserAsync(cancellationToken);
    }

    /// <summary>Borra el token de este equipo (revocarlo del todo se hace desde GitHub).</summary>
    public void SignOut()
    {
        _store.Delete();
        Token = null;
        User = null;
        try
        {
            File.Delete(_userCacheFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task RefreshUserAsync(CancellationToken cancellationToken)
    {
        if (Token is not { } token)
            return;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, UserUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await _http.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                SignOut();
                return;
            }

            if (!response.IsSuccessStatusCode)
                return;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (JsonSerializer.Deserialize<GitHubUser>(json) is not { Login.Length: > 0 } user || Token != token)
                return;

            User = user;
            WriteCachedUser(json);
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // Sin conexión: se queda el perfil guardado.
        }
    }

    private GitHubUser? ReadCachedUser()
    {
        try
        {
            return File.Exists(_userCacheFile) ? JsonSerializer.Deserialize<GitHubUser>(File.ReadAllText(_userCacheFile)) : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void WriteCachedUser(string json)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_userCacheFile)!);
            File.WriteAllText(_userCacheFile, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
