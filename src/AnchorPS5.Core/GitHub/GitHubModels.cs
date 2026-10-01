using System.Text.Json.Serialization;

namespace AnchorPS5.Core.GitHub;

/// <summary>Repositorio de GitHub: acepta "owner/repo" o una URL https://github.com/owner/repo.</summary>
public sealed record GitHubRepoRef(string Owner, string Name)
{
    public static bool TryParse(string? text, out GitHubRepoRef repo)
    {
        repo = null!;
        var value = (text ?? string.Empty).Trim();
        if (value.Length == 0)
            return false;

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                && !uri.Host.Equals("www.github.com", StringComparison.OrdinalIgnoreCase))
                return false;
            value = uri.AbsolutePath;
        }

        var parts = value.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return false;

        var name = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? parts[1][..^4] : parts[1];
        if (!IsValidPart(parts[0]) || !IsValidPart(name))
            return false;

        repo = new GitHubRepoRef(parts[0], name);
        return true;
    }

    private static bool IsValidPart(string part) =>
        part.Length > 0 && part.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');

    public override string ToString() => $"{Owner}/{Name}";
}

/// <summary>Release de GitHub (solo los campos que usa la app).</summary>
public sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("draft")]
    public bool Draft { get; set; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; set; }

    [JsonPropertyName("published_at")]
    public DateTimeOffset? PublishedAt { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; set; } = [];

    /// <summary>Versión a partir de la etiqueta, sin la "v" inicial.</summary>
    [JsonIgnore]
    public string Version => TagName.StartsWith('v') || TagName.StartsWith('V') ? TagName[1..] : TagName;
}

public sealed class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;

    /// <summary>"sha256:…" (GitHub lo calcula para cada fichero subido).</summary>
    [JsonPropertyName("digest")]
    public string? Digest { get; set; }

    [JsonIgnore]
    public string? Sha256 => Digest is { } d && d.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? d[7..] : null;
}

public enum GitHubStatus
{
    Ok,
    /// <summary>Sin conexión o error: se usa lo último guardado en caché.</summary>
    FromCache,
    /// <summary>Límite de la API alcanzado (puede venir con datos de caché).</summary>
    RateLimited,
    NotFound,
    Error,
}

public sealed record GitHubResult<T>(T? Value, GitHubStatus Status, DateTimeOffset? RateLimitReset = null)
    where T : class
{
    public bool HasValue => Value is not null;
}
