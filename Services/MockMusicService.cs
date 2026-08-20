using YtMusicTui.Models;
using YtMusicTui.Services.Abstractions;

namespace YtMusicTui.Services;

/// <summary>
/// Local stub data so UI work can proceed before a real YouTube Music client exists.
/// </summary>
public sealed class MockMusicService : IMusicService
{
    private static readonly Track[] DemoTracks =
    [
        new("t1", "Midnight City", "M83", "Hurry Up, We're Dreaming", TimeSpan.FromMinutes(4).Add(TimeSpan.FromSeconds(3))),
        new("t2", "Nightcall", "Kavinsky", "OutRun", TimeSpan.FromMinutes(4).Add(TimeSpan.FromSeconds(18))),
        new("t3", "Blinding Lights", "The Weeknd", "After Hours", TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(20))),
        new("t4", "Instant Crush", "Daft Punk", "Random Access Memories", TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(37))),
        new("t5", "Sunset", "The Midnight", "Endless Summer", TimeSpan.FromMinutes(4).Add(TimeSpan.FromSeconds(45))),
        new("t6", "Resonance", "HOME", "Odyssey", TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(32))),
    ];

    private static readonly Playlist[] DemoPlaylists =
    [
        new("p1", "Liked Music", "Songs you liked", 128, DemoTracks),
        new("p2", "Focus Flow", "Instrumental / ambient", 42),
        new("p3", "Late Night Drive", "Synthwave & chill", 36, DemoTracks[..3]),
        new("p4", "Workout", null, 55),
    ];

    public Task<IReadOnlyList<Track>> GetHomeAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Track>>(DemoTracks);

    public Task<IReadOnlyList<Playlist>> GetLibraryPlaylistsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Playlist>>(DemoPlaylists);

    public Task<SearchResults> SearchAsync(string query, CancellationToken ct = default)
    {
        var q = query.Trim();
        var tracks = string.IsNullOrEmpty(q)
            ? Array.Empty<Track>()
            : DemoTracks
                .Where(t =>
                    t.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    t.Artist.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (t.Album?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToArray();

        return Task.FromResult(new SearchResults(
            tracks,
            [],
            [],
            DemoPlaylists.Where(p => p.Title.Contains(q, StringComparison.OrdinalIgnoreCase)).ToArray()));
    }

    public Task<IReadOnlyList<Track>> GetPlaylistTracksAsync(string playlistId, CancellationToken ct = default)
    {
        var playlist = DemoPlaylists.FirstOrDefault(p => p.Id == playlistId);
        IReadOnlyList<Track> tracks = playlist?.Tracks ?? DemoTracks;
        return Task.FromResult(tracks);
    }
}
