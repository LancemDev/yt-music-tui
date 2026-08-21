namespace YtMusicTui.App;

public enum LeftFocus
{
    Tracks,
    Playlists
}

public enum FullScreenMode
{
    None,
    CoverBar,
    Lyrics
}

public sealed class AppState
{
    public string StatusMessage { get; set; } = "Ready";
    public string SearchQuery { get; set; } = string.Empty;
    public bool IsSearching { get; set; }
    public bool IsShowingSearchResults { get; set; }

    public LeftFocus LeftFocus { get; set; } = LeftFocus.Tracks;
    public FullScreenMode FullScreenMode { get; set; } = FullScreenMode.None;
    public bool IsSidebarCollapsed { get; set; }

    public int TracksSelectedIndex { get; set; }
    public int PlaylistsSelectedIndex { get; set; }

    public IReadOnlyList<Models.Track> LibraryTracks { get; set; } = [];
    public IReadOnlyList<Models.Track> SearchResults { get; set; } = [];
    public IReadOnlyList<Models.Playlist> Playlists { get; set; } = [];
    public IReadOnlyList<Models.Track> Queue { get; set; } = [];

    public IReadOnlyList<Models.Track> DisplayedTracks => IsShowingSearchResults ? SearchResults : LibraryTracks;

    public Models.Track? NowPlaying { get; set; }
    public bool IsPlaying { get; set; }
    public TimeSpan Position { get; set; }
    public TimeSpan Duration { get; set; }
    public IReadOnlyList<ulong> VisualizerLevels { get; set; } = [];
    public Models.Lyrics? Lyrics { get; set; }

    public string AuthLabel { get; set; } = "not signed in";
    public bool IsAuthenticated { get; set; }

    public float ProgressRatio =>
        Duration > TimeSpan.Zero
            ? Math.Clamp((float)(Position.TotalSeconds / Duration.TotalSeconds), 0f, 1f)
            : 0f;
}
