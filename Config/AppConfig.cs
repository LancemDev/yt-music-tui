namespace YtMusicTui.Config;

public sealed class AppConfig
{
    public string AppName { get; init; } = "YT Music TUI";
    public int TickMs { get; init; } = 50;
    public string? CookiesPath { get; init; }

    public static AppConfig Load()
    {
        // Placeholder for future config file / env loading.
        return new AppConfig
        {
            CookiesPath = Environment.GetEnvironmentVariable("YT_MUSIC_COOKIES")
        };
    }
}
