namespace YtMusicTui.Config;

public sealed class AppConfig
{
    public string AppName { get; init; } = "YT Music TUI";
    public int TickMs { get; init; } = 50;
    public string GeographicalLocation { get; init; } = "US";
    public string? CookiesPath { get; init; }
    public string? SessionPath { get; init; }
    public bool ValidateAuthOnStartup { get; init; } = true;

    public static AppConfig Load()
    {
        return new AppConfig
        {
            CookiesPath = Environment.GetEnvironmentVariable("YT_MUSIC_COOKIES"),
            SessionPath = Environment.GetEnvironmentVariable("YT_MUSIC_SESSION"),
            GeographicalLocation = Environment.GetEnvironmentVariable("YT_MUSIC_GEO") ?? "US",
            ValidateAuthOnStartup = !string.Equals(
                Environment.GetEnvironmentVariable("YT_MUSIC_SKIP_AUTH_CHECK"),
                "1",
                StringComparison.Ordinal)
        };
    }
}
