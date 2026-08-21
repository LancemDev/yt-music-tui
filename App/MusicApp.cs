using Ratatui;
using YtMusicTui.Auth;
using YtMusicTui.Config;
using YtMusicTui.Models;
using YtMusicTui.Services;
using YtMusicTui.Services.Abstractions;
using YtMusicTui.UI.Layout;
using YtMusicTui.UI.Widgets;

namespace YtMusicTui.App;

public sealed class MusicApp : IDisposable
{
    private readonly AppConfig _config;
    private readonly IMusicService _music;
    private readonly IPlayerService _player;
    private readonly LyricsService _lyrics;
    private readonly AuthSession _auth;
    private readonly AppState _state = new();
    private readonly Terminal _term = new();
    private readonly CancellationTokenSource _cts = new();
    private bool _needsRedraw = true;
    private DateTime _lastTick = DateTime.UtcNow;

    public MusicApp(AppConfig config, IMusicService music, IPlayerService player, LyricsService lyrics, AuthSession auth)
    {
        _config = config;
        _music = music;
        _player = player;
        _lyrics = lyrics;
        _auth = auth;

        _state.IsAuthenticated = auth.IsAuthenticated;
        _state.AuthLabel = auth.StatusLabel;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        var token = linked.Token;

        Console.CancelKeyPress += OnCancel;

        _term.Raw(true).AltScreen(true).ShowCursor(false);
        _term.Clear();

        await LoadInitialDataAsync(token);

        var poll = TimeSpan.FromMilliseconds(_config.TickMs);

        while (!token.IsCancellationRequested)
        {
            TickPlayer();

            if (_needsRedraw)
            {
                Draw();
                _needsRedraw = false;
            }

            if (!_term.NextEvent(poll, out var ev))
                continue;

            await HandleEventAsync(ev, token);
        }
    }

    private async Task LoadInitialDataAsync(CancellationToken ct)
    {
        try
        {
            _state.LibraryTracks = await _music.GetLibraryTracksAsync(ct);
            _state.Playlists = await _music.GetLibraryPlaylistsAsync(ct);
            _state.StatusMessage = _auth.IsAuthenticated
                ? $"Auth OK · {_auth.StatusDetail}"
                : $"Auth: {_auth.StatusLabel} · {_auth.StatusDetail}";
        }
        catch (Exception ex)
        {
            _state.StatusMessage = $"Couldn't load library: {ex.Message}";
        }

        _needsRedraw = true;
    }

    private void TickPlayer()
    {
        var now = DateTime.UtcNow;
        var delta = now - _lastTick;
        _lastTick = now;

        var before = (_player.Position, _player.IsPlaying, _player.Current?.Id);
        _player.Tick(delta);
        SyncPlayerState();

        var after = (_player.Position, _player.IsPlaying, _player.Current?.Id);
        if (before != after)
            _needsRedraw = true;
    }

    private void SyncPlayerState()
    {
        var previousTrackId = _state.NowPlaying?.Id;

        _state.NowPlaying = _player.Current;
        _state.IsPlaying = _player.IsPlaying;
        _state.Position = _player.Position;
        _state.Duration = _player.Duration;
        _state.Queue = _player.Queue;
        _state.VisualizerLevels = _player.VisualizerLevels;

        if (_state.NowPlaying?.Id != previousTrackId)
            TriggerLyricsFetch(_state.NowPlaying);
    }

    private void TriggerLyricsFetch(Track? track)
    {
        _state.Lyrics = null;
        if (track is null) return;

        var targetId = track.Id;
        _ = Task.Run(async () =>
        {
            var lyrics = await _lyrics.GetLyricsAsync(track.Title, track.Artist, track.Duration);
            if (_player.Current?.Id == targetId)
            {
                _state.Lyrics = lyrics;
                _needsRedraw = true;
            }
        });
    }

    private async Task HandleEventAsync(Event ev, CancellationToken ct)
    {
        if (ev.Kind == EventKind.Resize)
        {
            _needsRedraw = true;
            return;
        }

        if (ev.Kind != EventKind.Key)
            return;

        if (_state.IsSearching)
        {
            HandleSearchInput(ev);
            if (!_state.IsSearching && _state.IsShowingSearchResults)
                await RunSearchAsync(ct);
            _needsRedraw = true;
            return;
        }

        // Global quit
        if (ev.Key.Char is (uint)'q' or (uint)'Q' || (ev.Key.Ctrl && ev.Key.Char is (uint)'c' or (uint)'C'))
        {
            _cts.Cancel();
            return;
        }

        if (ev.Key.CodeEnum == KeyCode.Esc)
        {
            if (_state.IsShowingSearchResults)
            {
                _state.IsShowingSearchResults = false;
                _state.TracksSelectedIndex = 0;
                _state.StatusMessage = "Back to library";
            }
            _needsRedraw = true;
            return;
        }

        if (ev.Key.Char == (uint)'/')
        {
            _state.IsSearching = true;
            _state.SearchQuery = "";
            _state.StatusMessage = "Search — type and press Enter";
            _needsRedraw = true;
            return;
        }

        if (ev.Key.CodeEnum == KeyCode.Tab)
        {
            _state.LeftFocus = _state.LeftFocus == LeftFocus.Tracks ? LeftFocus.Playlists : LeftFocus.Tracks;
            _needsRedraw = true;
            return;
        }

        if (ev.Key.Char == (uint)'f')
        {
            _state.FullScreenMode = _state.FullScreenMode switch
            {
                FullScreenMode.None => FullScreenMode.CoverBar,
                FullScreenMode.CoverBar => FullScreenMode.Lyrics,
                _ => FullScreenMode.None
            };
            _needsRedraw = true;
            return;
        }

        if (ev.Key.Char == (uint)'c')
        {
            _state.IsSidebarCollapsed = !_state.IsSidebarCollapsed;
            _needsRedraw = true;
            return;
        }

        if (ev.Key.Char == (uint)'j' || ev.Key.CodeEnum == KeyCode.Down)
        {
            MoveSelection(1);
            _needsRedraw = true;
            return;
        }

        if (ev.Key.Char == (uint)'k' || ev.Key.CodeEnum == KeyCode.Up)
        {
            MoveSelection(-1);
            _needsRedraw = true;
            return;
        }

        if (ev.Key.Char == (uint)' ')
        {
            await _player.TogglePauseAsync(ct);
            SyncPlayerState();
            _state.StatusMessage = _player.LastError ?? (_state.IsPlaying ? "Resumed" : "Paused");
            _needsRedraw = true;
            return;
        }

        if (ev.Key.Char == (uint)'n')
        {
            await _player.NextAsync(ct);
            SyncPlayerState();
            _state.StatusMessage = _player.LastError ?? "Next track";
            _needsRedraw = true;
            return;
        }

        if (ev.Key.Char == (uint)'p')
        {
            await _player.PreviousAsync(ct);
            SyncPlayerState();
            _state.StatusMessage = _player.LastError ?? "Previous track";
            _needsRedraw = true;
            return;
        }

        if (ev.Key.CodeEnum == KeyCode.Enter)
        {
            await PlaySelectionAsync(ct);
            _needsRedraw = true;
        }
    }

    private void HandleSearchInput(in Event ev)
    {
        if (ev.Key.CodeEnum == KeyCode.Esc)
        {
            _state.IsSearching = false;
            _state.StatusMessage = "Search cancelled";
            return;
        }

        if (ev.Key.CodeEnum == KeyCode.Enter)
        {
            _state.IsSearching = false;
            if (string.IsNullOrWhiteSpace(_state.SearchQuery))
            {
                _state.StatusMessage = "Empty query";
                _state.IsShowingSearchResults = false;
            }
            else
            {
                _state.IsShowingSearchResults = true;
            }
            return;
        }

        if (ev.Key.CodeEnum == KeyCode.Backspace)
        {
            if (_state.SearchQuery.Length > 0)
                _state.SearchQuery = _state.SearchQuery[..^1];
            return;
        }

        if (ev.Key.CodeEnum == KeyCode.Char && ev.Key.Char is >= 32 and <= 126)
            _state.SearchQuery += (char)ev.Key.Char;
    }

    private void MoveSelection(int delta)
    {
        if (_state.LeftFocus == LeftFocus.Tracks)
        {
            var count = _state.DisplayedTracks.Count;
            if (count == 0) return;
            _state.TracksSelectedIndex = Math.Clamp(_state.TracksSelectedIndex + delta, 0, count - 1);
        }
        else
        {
            var count = _state.Playlists.Count;
            if (count == 0) return;
            _state.PlaylistsSelectedIndex = Math.Clamp(_state.PlaylistsSelectedIndex + delta, 0, count - 1);
        }
    }

    private async Task RunSearchAsync(CancellationToken ct)
    {
        try
        {
            var results = await _music.SearchAsync(_state.SearchQuery, ct);
            _state.SearchResults = results.Tracks;
            _state.TracksSelectedIndex = 0;
            _state.LeftFocus = LeftFocus.Tracks;
            _state.StatusMessage = $"{results.Tracks.Count} track(s)";
        }
        catch (Exception ex)
        {
            _state.SearchResults = [];
            _state.StatusMessage = $"Search failed: {ex.Message}";
        }
    }

    private async Task PlaySelectionAsync(CancellationToken ct)
    {
        if (_state.LeftFocus == LeftFocus.Playlists)
        {
            if (_state.Playlists.Count == 0)
            {
                _state.StatusMessage = "No playlists";
                return;
            }

            var playlist = _state.Playlists[_state.PlaylistsSelectedIndex];
            IReadOnlyList<Track> playlistTracks;
            try
            {
                playlistTracks = await _music.GetPlaylistTracksAsync(playlist.Id, ct);
            }
            catch (Exception ex)
            {
                _state.StatusMessage = $"Couldn't load playlist: {ex.Message}";
                return;
            }

            if (playlistTracks.Count == 0)
            {
                _state.StatusMessage = "Playlist has no tracks";
                return;
            }

            await _player.PlayQueueAsync(playlistTracks, 0, ct);
        }
        else
        {
            var tracks = _state.DisplayedTracks;
            if (tracks.Count == 0)
            {
                _state.StatusMessage = "Nothing to play";
                return;
            }

            await _player.PlayQueueAsync(tracks, _state.TracksSelectedIndex, ct);
        }

        SyncPlayerState();
        _state.StatusMessage = _player.LastError
            ?? (_state.NowPlaying is { } t ? $"Playing {t.Title}" : "Playing");
    }

    private void Draw()
    {
        var (w, h) = _term.Size();
        var layout = AppLayout.FromSize(w, h, _state.FullScreenMode, _state.IsSidebarCollapsed);

        _term.PushFrame();
        try
        {
            HeaderBar.Draw(_term, layout.Header, _state, _config.AppName);

            if (layout.TracksPanel is { } tracksArea) TracksPanel.Draw(_term, tracksArea, _state);
            if (layout.PlaylistsPanel is { } playlistsArea) PlaylistsPanel.Draw(_term, playlistsArea, _state);
            if (layout.CoverArt is { } coverArea) CoverArtPanel.Draw(_term, coverArea, _state);
            if (layout.BarVisualizer is { } barArea) BarVisualizerPanel.Draw(_term, barArea, _state);
            if (layout.Lyrics is { } lyricsArea) LyricsPanel.Draw(_term, lyricsArea, _state);

            PlayerBar.Draw(_term, layout.Player, _state);
            QuickActionsBar.Draw(_term, layout.QuickActions);
        }
        finally
        {
            _term.PopFrame();
        }
    }

    private void OnCancel(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        _cts.Cancel();
    }

    public void Dispose()
    {
        Console.CancelKeyPress -= OnCancel;
        _cts.Cancel();
        try
        {
            _term.ShowCursor(true).AltScreen(false).Raw(false);
        }
        catch
        {
            // Terminal may already be torn down.
        }

        _term.Dispose();
        _cts.Dispose();

        if (_player is IDisposable disposablePlayer)
            disposablePlayer.Dispose();
    }
}
