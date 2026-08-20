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
    var code = await RunCliAsync(args, authService);
    Environment.Exit(code);
    return;
}

var session = await authService.LoadAsync();
var music = new MockMusicService();
var player = new MockPlayerService();

using var app = new MusicApp(config, music, player, session);
await app.RunAsync();

static async Task<int> RunCliAsync(string[] args, AuthService auth)
{
    switch (args[0])
    {
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

          (no args)                 Start the TUI
          --auth-status             Load cookies and validate auth
          --import-cookies <path>   Copy cookie file into config and validate
          --help                    Show this help

        Cookie file formats: Netscape cookies.txt, Cookie header string, or JSON array.
        Default path: ~/.config/yt-music-tui/cookies.txt
        Override with YT_MUSIC_COOKIES.
        """);
}
