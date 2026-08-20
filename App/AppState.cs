namespace YtMusicTui.App;

public enum Screen
{
    Home,
    Search,
    Library,
    Queue
}

public sealed class AppState
{
    public Screen CurrentScreen { get; set; } = Screen.Home;
    public string StatusMessage { get; set; } = "Ready";
    public string SearchQuery { get; set; } = string.Empty;
    public bool IsSearching { get; set; }

    public int HomeSelectedIndex { get; set; }
    public int LibrarySelectedIndex { get; set; }
    public int QueueSelectedIndex { get; set; }
    public int SearchSelectedIndex { get; set; }

    public IReadOnlyList<Models.Track> HomeTracks { get; set; } = [];
    public IReadOnlyList<Models.Playlist> LibraryPlaylists { get; set; } = [];
    public IReadOnlyList<Models.Track> Queue { get; set; } = [];
    public IReadOnlyList<Models.Track> SearchTracks { get; set; } = [];

    public Models.Track? NowPlaying { get; set; }
    public bool IsPlaying { get; set; }
    public TimeSpan Position { get; set; }
    public TimeSpan Duration { get; set; }

    public string AuthLabel { get; set; } = "not signed in";
    public bool IsAuthenticated { get; set; }

    public float ProgressRatio =>
        Duration > TimeSpan.Zero
            ? Math.Clamp((float)(Position.TotalSeconds / Duration.TotalSeconds), 0f, 1f)
            : 0f;
}
