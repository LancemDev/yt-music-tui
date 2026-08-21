using YtMusicTui.Models;

namespace YtMusicTui.Services.Abstractions;

public interface IPlayerService
{
    Track? Current { get; }
    bool IsPlaying { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    IReadOnlyList<Track> Queue { get; }
    IReadOnlyList<ulong> VisualizerLevels { get; }
    string? LastError { get; }

    Task PlayAsync(Track track, CancellationToken ct = default);
    Task PlayQueueAsync(IReadOnlyList<Track> tracks, int startIndex = 0, CancellationToken ct = default);
    Task TogglePauseAsync(CancellationToken ct = default);
    Task NextAsync(CancellationToken ct = default);
    Task PreviousAsync(CancellationToken ct = default);
    void Tick(TimeSpan delta);
}
