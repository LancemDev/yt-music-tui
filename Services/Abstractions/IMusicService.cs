using YtMusicTui.Models;

namespace YtMusicTui.Services.Abstractions;

public interface IMusicService
{
    Task<IReadOnlyList<Track>> GetHomeAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Playlist>> GetLibraryPlaylistsAsync(CancellationToken ct = default);
    Task<SearchResults> SearchAsync(string query, CancellationToken ct = default);
    Task<IReadOnlyList<Track>> GetPlaylistTracksAsync(string playlistId, CancellationToken ct = default);
}
