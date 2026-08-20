using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YtMusicTui.Auth;

public enum AuthStatus
{
    Missing,
    InvalidCookies,
    Authenticated,
    ValidationFailed
}

public sealed class AuthSession
{
    public required IReadOnlyList<Cookie> Cookies { get; init; }
    public string? VisitorData { get; init; }
    public string? PoToken { get; init; }
    public string GeographicalLocation { get; init; } = "US";
    public string? CookiesPath { get; init; }
    public AuthStatus Status { get; init; } = AuthStatus.Missing;
    public string? StatusDetail { get; init; }

    public bool IsAuthenticated => Status == AuthStatus.Authenticated;

    public string StatusLabel => Status switch
    {
        AuthStatus.Authenticated => "signed in",
        AuthStatus.Missing => "not signed in",
        AuthStatus.InvalidCookies => "invalid cookies",
        AuthStatus.ValidationFailed => "auth failed",
        _ => "unknown"
    };
}

public sealed class SessionFile
{
    [JsonPropertyName("geographicalLocation")]
    public string GeographicalLocation { get; set; } = "US";

    [JsonPropertyName("visitorData")]
    public string? VisitorData { get; set; }

    [JsonPropertyName("poToken")]
    public string? PoToken { get; set; }

    [JsonPropertyName("cookiesPath")]
    public string? CookiesPath { get; set; }
}

public static class SessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static SessionFile LoadOrDefault(string path)
    {
        if (!File.Exists(path))
            return new SessionFile();

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SessionFile>(json, JsonOptions) ?? new SessionFile();
    }

    public static void Save(string path, SessionFile session)
    {
        AuthPaths.EnsureConfigDirectory();
        var json = JsonSerializer.Serialize(session, JsonOptions);
        File.WriteAllText(path, json);
    }
}
