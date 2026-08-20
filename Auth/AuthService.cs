using System.Net;
using YouTubeMusicAPI.Client;

namespace YtMusicTui.Auth;

public sealed class AuthService
{
    private readonly string _cookiesPath;
    private readonly string _sessionPath;
    private readonly string _geographicalLocation;
    private readonly bool _validateWithApi;

    public AuthService(
        string? cookiesPath = null,
        string? sessionPath = null,
        string geographicalLocation = "US",
        bool validateWithApi = true)
    {
        _cookiesPath = cookiesPath ?? AuthPaths.DefaultCookiesPath;
        _sessionPath = sessionPath ?? AuthPaths.DefaultSessionPath;
        _geographicalLocation = geographicalLocation;
        _validateWithApi = validateWithApi;
    }

    public string CookiesPath => _cookiesPath;
    public string SessionPath => _sessionPath;

    public async Task<AuthSession> LoadAsync(CancellationToken ct = default)
    {
        var file = SessionStore.LoadOrDefault(_sessionPath);
        var geo = string.IsNullOrWhiteSpace(file.GeographicalLocation)
            ? _geographicalLocation
            : file.GeographicalLocation;

        var cookiesPath = ResolveCookiesPath(file.CookiesPath);
        if (!File.Exists(cookiesPath))
        {
            return new AuthSession
            {
                Cookies = [],
                VisitorData = file.VisitorData,
                PoToken = file.PoToken,
                GeographicalLocation = geo,
                CookiesPath = cookiesPath,
                Status = AuthStatus.Missing,
                StatusDetail = $"No cookies at {cookiesPath}"
            };
        }

        IReadOnlyList<Cookie> cookies;
        try
        {
            cookies = CookieFileParser.ParseFile(cookiesPath);
        }
        catch (Exception ex)
        {
            return new AuthSession
            {
                Cookies = [],
                VisitorData = file.VisitorData,
                PoToken = file.PoToken,
                GeographicalLocation = geo,
                CookiesPath = cookiesPath,
                Status = AuthStatus.InvalidCookies,
                StatusDetail = ex.Message
            };
        }

        if (cookies.Count == 0 || !CookieFileParser.HasAuthCookies(cookies))
        {
            return new AuthSession
            {
                Cookies = cookies,
                VisitorData = file.VisitorData,
                PoToken = file.PoToken,
                GeographicalLocation = geo,
                CookiesPath = cookiesPath,
                Status = AuthStatus.InvalidCookies,
                StatusDetail = "Cookies found but missing SAPISID / __Secure-3PAPISID (sign-in required)"
            };
        }

        var session = new AuthSession
        {
            Cookies = cookies,
            VisitorData = file.VisitorData,
            PoToken = file.PoToken,
            GeographicalLocation = geo,
            CookiesPath = cookiesPath,
            Status = AuthStatus.Authenticated,
            StatusDetail = $"{cookies.Count} cookies loaded"
        };

        if (!_validateWithApi)
            return session;

        return await ValidateAsync(session, ct);
    }

    public async Task<AuthSession> ValidateAsync(AuthSession session, CancellationToken ct = default)
    {
        try
        {
            var client = YouTubeClientFactory.Create(session);
            // Library endpoint requires authenticated cookies.
            _ = await client.GetLibrarySongsAsync(ct);

            return new AuthSession
            {
                Cookies = session.Cookies,
                VisitorData = session.VisitorData,
                PoToken = session.PoToken,
                GeographicalLocation = session.GeographicalLocation,
                CookiesPath = session.CookiesPath,
                Status = AuthStatus.Authenticated,
                StatusDetail = "Library access OK"
            };
        }
        catch (Exception ex)
        {
            return new AuthSession
            {
                Cookies = session.Cookies,
                VisitorData = session.VisitorData,
                PoToken = session.PoToken,
                GeographicalLocation = session.GeographicalLocation,
                CookiesPath = session.CookiesPath,
                Status = AuthStatus.ValidationFailed,
                StatusDetail = ex.Message
            };
        }
    }

    public AuthSession SaveCookiesFromHeader(string cookieHeader)
    {
        var cookies = CookieFileParser.ParseHeader(cookieHeader);
        if (cookies.Count == 0)
            throw new InvalidDataException("No cookies could be parsed from the header.");

        if (!CookieFileParser.HasAuthCookies(cookies))
            throw new InvalidDataException(
                "Header parsed but is missing SAPISID / __Secure-3PAPISID. Copy the Cookie header while signed into music.youtube.com.");

        AuthPaths.EnsureConfigDirectory();
        File.WriteAllText(_cookiesPath, cookieHeader.Trim() + Environment.NewLine);

        var file = SessionStore.LoadOrDefault(_sessionPath);
        file.CookiesPath = _cookiesPath;
        if (string.IsNullOrWhiteSpace(file.GeographicalLocation))
            file.GeographicalLocation = _geographicalLocation;
        SessionStore.Save(_sessionPath, file);

        return new AuthSession
        {
            Cookies = cookies,
            VisitorData = file.VisitorData,
            PoToken = file.PoToken,
            GeographicalLocation = file.GeographicalLocation,
            CookiesPath = _cookiesPath,
            Status = AuthStatus.Authenticated,
            StatusDetail = $"Saved {cookies.Count} cookies → {_cookiesPath}"
        };
    }

    public AuthSession ImportCookies(string sourcePath)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"Cookie file not found: {sourcePath}", sourcePath);

        // Verify parse before copying.
        var cookies = CookieFileParser.ParseFile(sourcePath);
        if (cookies.Count == 0)
            throw new InvalidDataException("No cookies could be parsed from the file.");

        if (!CookieFileParser.HasAuthCookies(cookies))
            throw new InvalidDataException(
                "File parsed but is missing SAPISID / __Secure-3PAPISID. Export cookies while signed into music.youtube.com.");

        AuthPaths.EnsureConfigDirectory();
        File.Copy(sourcePath, _cookiesPath, overwrite: true);

        var file = SessionStore.LoadOrDefault(_sessionPath);
        file.CookiesPath = _cookiesPath;
        if (string.IsNullOrWhiteSpace(file.GeographicalLocation))
            file.GeographicalLocation = _geographicalLocation;
        SessionStore.Save(_sessionPath, file);

        return new AuthSession
        {
            Cookies = cookies,
            VisitorData = file.VisitorData,
            PoToken = file.PoToken,
            GeographicalLocation = file.GeographicalLocation,
            CookiesPath = _cookiesPath,
            Status = AuthStatus.Authenticated,
            StatusDetail = $"Imported {cookies.Count} cookies → {_cookiesPath}"
        };
    }

    public void SaveSessionTokens(string? visitorData, string? poToken, string? geographicalLocation = null)
    {
        AuthPaths.EnsureConfigDirectory();
        var file = SessionStore.LoadOrDefault(_sessionPath);
        file.VisitorData = visitorData ?? file.VisitorData;
        file.PoToken = poToken ?? file.PoToken;
        file.GeographicalLocation = geographicalLocation ?? file.GeographicalLocation ?? _geographicalLocation;
        file.CookiesPath ??= _cookiesPath;
        SessionStore.Save(_sessionPath, file);
    }

    private string ResolveCookiesPath(string? configuredPath)
    {
        var fromEnv = Environment.GetEnvironmentVariable("YT_MUSIC_COOKIES");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return Path.GetFullPath(fromEnv);

        if (!string.IsNullOrWhiteSpace(configuredPath))
            return Path.GetFullPath(configuredPath);

        return Path.GetFullPath(_cookiesPath);
    }
}
