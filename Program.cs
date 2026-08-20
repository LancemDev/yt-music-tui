using YtMusicTui.App;
using YtMusicTui.Auth;
using YtMusicTui.Config;
using YtMusicTui.Services;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var config = AppConfig.Load();
var authService = new AuthService(
    cookiesPath: config.CookiesPath,
    sessionPath: config.SessionPath,
    geographicalLocation: config.GeographicalLocation,
    validateWithApi: config.ValidateAuthOnStartup);

if (args.Length > 0)
{
    var code = await RunCliAsync(args, authService, config);
    Environment.Exit(code);
    return;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var session = await EnsureAuthenticatedAsync(authService, config, forceLogin: false, cts.Token);
var music = new MockMusicService();
var player = new MockPlayerService();

using var app = new MusicApp(config, music, player, session);
await app.RunAsync(cts.Token);

static async Task<AuthSession> EnsureAuthenticatedAsync(
    AuthService auth,
    AppConfig config,
    bool forceLogin,
    CancellationToken ct)
{
    if (!forceLogin && !config.ForceBrowserLogin)
    {
        var existing = await auth.LoadAsync(ct);
        if (existing.IsAuthenticated)
            return existing;

        if (!config.PromptLoginOnStartup)
        {
            Console.WriteLine($"Starting without auth ({existing.StatusLabel}): {existing.StatusDetail}");
            return existing;
        }

        Console.WriteLine($"Not authenticated ({existing.StatusLabel}). Starting browser sign-in…");
    }

    var flow = new BrowserAuthFlow(auth);
    return await flow.RunAsync(ct);
}

static async Task<int> RunCliAsync(string[] args, AuthService auth, AppConfig config)
{
    switch (args[0])
    {
        case "--login":
        case "login":
        {
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
            try
            {
                var session = await EnsureAuthenticatedAsync(auth, config, forceLogin: true, cts.Token);
                PrintSession(session);
                return session.IsAuthenticated ? 0 : 1;
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Login cancelled.");
                return 130;
            }
        }
        case "--auth-status":
        case "auth-status":
        {
            var session = await auth.LoadAsync();
            PrintSession(session);
            return session.IsAuthenticated ? 0 : 1;
        }
        case "--import-cookies":
        case "import-cookies":
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: yt-music-tui --import-cookies <path-to-cookies>");
                return 2;
            }

            try
            {
                var imported = auth.ImportCookies(args[1]);
                Console.WriteLine(imported.StatusDetail);
                Console.WriteLine("Validating against YouTube Music library…");
                var validated = await auth.ValidateAsync(imported);
                PrintSession(validated);
                return validated.IsAuthenticated ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }
        case "--help":
        case "-h":
        case "help":
            PrintHelp();
            return 0;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[0]}");
            PrintHelp();
            return 2;
    }
}

static void PrintSession(AuthSession session)
{
    Console.WriteLine($"Status:   {session.StatusLabel}");
    Console.WriteLine($"Detail:   {session.StatusDetail}");
    Console.WriteLine($"Cookies:  {session.CookiesPath}");
    Console.WriteLine($"Count:    {session.Cookies.Count}");
    Console.WriteLine($"Geo:      {session.GeographicalLocation}");
    Console.WriteLine($"Visitor:  {(string.IsNullOrEmpty(session.VisitorData) ? "(none)" : "set")}");
    Console.WriteLine($"PoToken:  {(string.IsNullOrEmpty(session.PoToken) ? "(none)" : "set")}");
}

static void PrintHelp()
{
    Console.WriteLine("""
        yt-music-tui

          (no args)                 Start TUI (opens browser login if needed)
          --login                   Force browser sign-in flow
          --auth-status             Load cookies and validate auth
          --import-cookies <path>   Copy cookie file into config and validate
          --help                    Show this help

        On first run (or expired cookies), the app opens YouTube Music sign-in
        directly in your browser, then auto-detects the session from your
        browser's cookies. If that fails, it asks you to paste the Cookie
        header instead.

        Env:
          YT_MUSIC_COOKIES          Cookie file path
          YT_MUSIC_SKIP_LOGIN=1     Do not open browser login on startup
        """);
}
