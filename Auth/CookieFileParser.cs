using System.Net;
using System.Text.Json;

namespace YtMusicTui.Auth;

/// <summary>
/// Loads YouTube cookies from Netscape cookies.txt, a Cookie header string, or JSON.
/// YouTubeMusicAPI expects <see cref="Cookie"/> values (typically domain .youtube.com).
/// </summary>
public static class CookieFileParser
{
    public static IReadOnlyList<Cookie> ParseFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Cookie file not found: {path}", path);

        var text = File.ReadAllText(path).Trim();
        if (string.IsNullOrWhiteSpace(text))
            return [];

        if (text.StartsWith('[') || text.StartsWith('{'))
            return ParseJson(text);

        if (LooksLikeNetscape(text))
            return ParseNetscape(text);

        return ParseHeader(text);
    }

    public static IReadOnlyList<Cookie> ParseHeader(string cookieHeader)
    {
        var cookies = new List<Cookie>();

        foreach (var part in cookieHeader.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0) continue;

            var name = part[..eq].Trim();
            var value = part[(eq + 1)..].Trim();
            if (string.IsNullOrEmpty(name)) continue;

            cookies.Add(CreateCookie(name, value));
        }

        return cookies;
    }

    public static IReadOnlyList<Cookie> ParseNetscape(string text)
    {
        var cookies = new List<Cookie>();

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                // Netscape httpOnly marker: #HttpOnly_.youtube.com ...
                if (!line.StartsWith("#HttpOnly_", StringComparison.OrdinalIgnoreCase))
                    continue;

                line = line["#HttpOnly_".Length..];
            }

            var cols = line.Split('\t');
            if (cols.Length < 7) continue;

            var domain = cols[0].Trim();
            var path = cols[2].Trim();
            var secure = cols[3].Equals("TRUE", StringComparison.OrdinalIgnoreCase);
            _ = long.TryParse(cols[4], out var expiresUnix);
            var name = cols[5].Trim();
            var value = cols[6].Trim();

            if (string.IsNullOrEmpty(name)) continue;

            var cookie = CreateCookie(name, value, domain, path, secure);
            if (expiresUnix > 0)
                cookie.Expires = DateTimeOffset.FromUnixTimeSeconds(expiresUnix).UtcDateTime;

            cookies.Add(cookie);
        }

        return cookies;
    }

    public static IReadOnlyList<Cookie> ParseJson(string text)
    {
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("cookies", out var nested))
            root = nested;

        if (root.ValueKind == JsonValueKind.String)
            return ParseHeader(root.GetString() ?? string.Empty);

        if (root.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Cookie JSON must be an array, {\"cookies\":[...]}, or a cookie header string.");

        var cookies = new List<Cookie>();
        foreach (var item in root.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;

            var name = GetString(item, "name", "Name");
            var value = GetString(item, "value", "Value");
            if (string.IsNullOrEmpty(name) || value is null) continue;

            var domain = GetString(item, "domain", "Domain") ?? ".youtube.com";
            var path = GetString(item, "path", "Path") ?? "/";
            var secure = GetBool(item, "secure", "Secure", "isSecure", "IsSecure");

            cookies.Add(CreateCookie(name, value, domain, path, secure));
        }

        return cookies;
    }

    public static bool HasAuthCookies(IEnumerable<Cookie> cookies)
        => cookies.Any(c =>
            c.Name is "SAPISID" or "__Secure-3PAPISID" or "__Secure-1PAPISID");

    private static bool LooksLikeNetscape(string text)
        => text.Contains('\t') ||
           text.Contains("Netscape", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("#HttpOnly_", StringComparison.OrdinalIgnoreCase);

    private static Cookie CreateCookie(
        string name,
        string value,
        string? domain = null,
        string path = "/",
        bool secure = true)
    {
        domain = string.IsNullOrWhiteSpace(domain) ? ".youtube.com" : domain.Trim();
        if (!domain.StartsWith('.') && domain.Contains("youtube", StringComparison.OrdinalIgnoreCase))
            domain = "." + domain.TrimStart('.');

        return new Cookie(name, value, path, domain)
        {
            Secure = secure,
            HttpOnly = name.Contains("SID", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static string? GetString(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();
        }

        return null;
    }

    private static bool GetBool(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (!el.TryGetProperty(name, out var prop)) continue;
            if (prop.ValueKind is JsonValueKind.True) return true;
            if (prop.ValueKind is JsonValueKind.False) return false;
            if (prop.ValueKind == JsonValueKind.String && bool.TryParse(prop.GetString(), out var b))
                return b;
        }

        return true;
    }
}
