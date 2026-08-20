using YtMusicTui.Models;
using YtMusicTui.Services.Abstractions;

namespace YtMusicTui.Services;

/// <summary>
/// In-memory player stub. Swap for mpv / ffplay / libvlc later.
/// </summary>
public sealed class MockPlayerService : IPlayerService
{
    private readonly List<Track> _queue = [];
    private int _index = -1;

    public Track? Current { get; private set; }
    public bool IsPlaying { get; private set; }
    public TimeSpan Position { get; private set; }
    public TimeSpan Duration { get; private set; }
    public IReadOnlyList<Track> Queue => _queue;

    public Task PlayAsync(Track track, CancellationToken ct = default)
    {
        _queue.Clear();
        _queue.Add(track);
        _index = 0;
        StartCurrent();
        return Task.CompletedTask;
    }

    public Task PlayQueueAsync(IReadOnlyList<Track> tracks, int startIndex = 0, CancellationToken ct = default)
    {
        _queue.Clear();
        _queue.AddRange(tracks);
        _index = Math.Clamp(startIndex, 0, Math.Max(0, _queue.Count - 1));
        if (_queue.Count == 0)
        {
            Current = null;
            IsPlaying = false;
            Position = TimeSpan.Zero;
            Duration = TimeSpan.Zero;
            return Task.CompletedTask;
        }

        StartCurrent();
        return Task.CompletedTask;
    }

    public Task TogglePauseAsync(CancellationToken ct = default)
    {
        if (Current is null) return Task.CompletedTask;
        IsPlaying = !IsPlaying;
        return Task.CompletedTask;
    }

    public Task NextAsync(CancellationToken ct = default)
    {
        if (_queue.Count == 0) return Task.CompletedTask;
        _index = (_index + 1) % _queue.Count;
        StartCurrent();
        return Task.CompletedTask;
    }

    public Task PreviousAsync(CancellationToken ct = default)
    {
        if (_queue.Count == 0) return Task.CompletedTask;
        _index = (_index - 1 + _queue.Count) % _queue.Count;
        StartCurrent();
        return Task.CompletedTask;
    }

    public void Tick(TimeSpan delta)
    {
        if (!IsPlaying || Current is null) return;

        Position += delta;
        if (Position < Duration) return;

        Position = Duration;
        _ = NextAsync();
    }

    private void StartCurrent()
    {
        Current = _queue[_index];
        Duration = Current.Duration ?? TimeSpan.FromMinutes(3);
        Position = TimeSpan.Zero;
        IsPlaying = true;
    }
}
