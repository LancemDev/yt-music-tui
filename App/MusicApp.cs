using Ratatui;
using YtMusicTui.Auth;
using YtMusicTui.Config;
using YtMusicTui.Services.Abstractions;
using YtMusicTui.UI.Layout;
using YtMusicTui.UI.Views;
using YtMusicTui.UI.Widgets;

namespace YtMusicTui.App;

public sealed class MusicApp : IDisposable
{
    private readonly AppConfig _config;
    private readonly IMusicService _music;
    private readonly IPlayerService _player;
    private readonly AuthSession _auth;
    private readonly AppState _state = new();
    private readonly Dictionary<Screen, IView> _views;
    private readonly Terminal _term = new();
    private readonly CancellationTokenSource _cts = new();
    private bool _needsRedraw = true;
    private DateTime _lastTick = DateTime.UtcNow;

    public MusicApp(AppConfig config, IMusicService music, IPlayerService player, AuthSession auth)
    {
        _config = config;
        _music = music;
        _player = player;
        _auth = auth;
        _views = new Dictionary<Screen, IView>
        {
            [Screen.Home] = new HomeView(),
            [Screen.Search] = new SearchView(),
            [Screen.Library] = new LibraryView(),
            [Screen.Queue] = new QueueView(),
        };

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
        _state.HomeTracks = await _music.GetHomeAsync(ct);
        _state.LibraryPlaylists = await _music.GetLibraryPlaylistsAsync(ct);
        _state.StatusMessage = _auth.IsAuthenticated
            ? $"Auth OK · {_auth.StatusDetail}"
            : $"Auth: {_auth.StatusLabel} · {_auth.StatusDetail}";
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
        _state.NowPlaying = _player.Current;
        _state.IsPlaying = _player.IsPlaying;
        _state.Position = _player.Position;
        _state.Duration = _player.Duration;
        _state.Queue = _player.Queue;
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

        // Global quit
        if (ev.Key.Char is (uint)'q' or (uint)'Q' || (ev.Key.Ctrl && ev.Key.Char is (uint)'c' or (uint)'C'))
        {
            _cts.Cancel();
            return;
        }

        // While typing a search query, only the search view handles keys
        if (_state.IsSearching)
        {
            _views[Screen.Search].HandleKey(ev, _state);
            if (!_state.IsSearching && !string.IsNullOrWhiteSpace(_state.SearchQuery))
                await RunSearchAsync(ct);
            _needsRedraw = true;
            return;
        }

        // Screen switching
        if (ev.Key.Char == (uint)'1') { SwitchScreen(Screen.Home); return; }
        if (ev.Key.Char == (uint)'2') { SwitchScreen(Screen.Search); return; }
        if (ev.Key.Char == (uint)'3') { SwitchScreen(Screen.Library); return; }
        if (ev.Key.Char == (uint)'4') { SwitchScreen(Screen.Queue); return; }

        // Playback
        if (ev.Key.Char == (uint)' ')
        {
            await _player.TogglePauseAsync(ct);
            SyncPlayerState();
            _state.StatusMessage = _state.IsPlaying ? "Resumed" : "Paused";
            _needsRedraw = true;
            return;
        }

        if (ev.Key.Char == (uint)'n')
        {
            await _player.NextAsync(ct);
            SyncPlayerState();
            _state.StatusMessage = "Next track";
            _needsRedraw = true;
            return;
        }

        if (ev.Key.Char == (uint)'p')
        {
            await _player.PreviousAsync(ct);
            SyncPlayerState();
            _state.StatusMessage = "Previous track";
            _needsRedraw = true;
            return;
        }

        if (ev.Key.CodeEnum == KeyCode.Enter)
        {
            await PlaySelectionAsync(ct);
            _needsRedraw = true;
            return;
        }

        _views[_state.CurrentScreen].HandleKey(ev, _state);
        _needsRedraw = true;
    }

    private void SwitchScreen(Screen screen)
    {
        _state.CurrentScreen = screen;
        _state.StatusMessage = screen.ToString();
        _needsRedraw = true;
    }

    private async Task RunSearchAsync(CancellationToken ct)
    {
        var results = await _music.SearchAsync(_state.SearchQuery, ct);
        _state.SearchTracks = results.Tracks;
        _state.SearchSelectedIndex = 0;
        _state.StatusMessage = $"{results.Tracks.Count} track(s)";
    }

    private async Task PlaySelectionAsync(CancellationToken ct)
    {
        switch (_state.CurrentScreen)
        {
            case Screen.Home when _state.HomeTracks.Count > 0:
                await _player.PlayQueueAsync(_state.HomeTracks, _state.HomeSelectedIndex, ct);
                break;
            case Screen.Search when _state.SearchTracks.Count > 0:
                await _player.PlayQueueAsync(_state.SearchTracks, _state.SearchSelectedIndex, ct);
                break;
            case Screen.Queue when _state.Queue.Count > 0:
                await _player.PlayQueueAsync(_state.Queue, _state.QueueSelectedIndex, ct);
                break;
            case Screen.Library when _state.LibraryPlaylists.Count > 0:
            {
                var playlist = _state.LibraryPlaylists[_state.LibrarySelectedIndex];
                var tracks = await _music.GetPlaylistTracksAsync(playlist.Id, ct);
                if (tracks.Count == 0)
                {
                    _state.StatusMessage = "Playlist has no tracks";
                    return;
                }

                await _player.PlayQueueAsync(tracks, 0, ct);
                break;
            }
            default:
                _state.StatusMessage = "Nothing to play";
                return;
        }

        SyncPlayerState();
        _state.StatusMessage = _state.NowPlaying is { } t ? $"Playing {t.Title}" : "Playing";
    }

    private void Draw()
    {
        var (w, h) = _term.Size();
        var layout = AppLayout.FromSize(w, h);

        _term.PushFrame();
        try
        {
            HeaderBar.Draw(_term, layout.Header, _state, _config.AppName);
            _views[_state.CurrentScreen].Draw(_term, layout.Content, _state);
            PlayerBar.Draw(_term, layout.Player, _state);
            HelpBar.Draw(_term, layout.Help);
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
    }
}
