namespace YtMusicTui.Auth;

public static class AuthPaths
{
    public const string AppFolderName = "yt-music-tui";
    public const string CookiesFileName = "cookies.txt";
    public const string SessionFileName = "session.json";

    public static string ConfigDirectory
    {
        get
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            var root = !string.IsNullOrWhiteSpace(xdg)
                ? xdg
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config");

            return Path.Combine(root, AppFolderName);
        }
    }

    public static string DefaultCookiesPath => Path.Combine(ConfigDirectory, CookiesFileName);
    public static string DefaultSessionPath => Path.Combine(ConfigDirectory, SessionFileName);

    public static void EnsureConfigDirectory()
        => Directory.CreateDirectory(ConfigDirectory);
}
