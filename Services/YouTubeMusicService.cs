using YouTubeMusicAPI.Client;
using YouTubeMusicAPI.Models;
using YouTubeMusicAPI.Models.Info;
using YouTubeMusicAPI.Models.Library;
using YouTubeMusicAPI.Models.Search;
using YouTubeMusicAPI.Models.Streaming;
using YtMusicTui.Auth;
using YtMusicTui.Models;
using YtMusicTui.Services.Abstractions;

namespace YtMusicTui.Services;

public sealed class YouTubeMusicService : IMusicService
{
    private readonly YouTubeMusicClient _client;

    public YouTubeMusicService(AuthSession session)
        => _client = YouTubeClientFactory.Create(session);

    public async Task<IReadOnlyList<Track>> GetLibraryTracksAsync(CancellationToken ct = default)
    {
        var songs = await _client.GetLibrarySongsAsync(ct);
        return songs.Select(ToTrack).ToList();
    }

    public async Task<IReadOnlyList<Playlist>> GetLibraryPlaylistsAsync(CancellationToken ct = default)
    {
        var playlists = await _client.GetLibraryCommunityPlaylistsAsync(ct);
        return playlists
            .Select(p => new Playlist(p.Id, p.Name, Description: null, TrackCount: p.SongCount))
            .ToList();
    }

    public async Task<SearchResults> SearchAsync(string query, CancellationToken ct = default)
    {
        var tracks = new List<Track>();
        await foreach (var result in _client.SearchAsync(query, SearchCategory.Songs).WithCancellation(ct))
        {
            if (result is SongSearchResult song)
                tracks.Add(ToTrack(song));

            if (tracks.Count >= 25)
                break;
        }

        return new SearchResults(tracks, [], [], []);
    }

    public async Task<IReadOnlyList<Track>> GetPlaylistTracksAsync(string playlistId, CancellationToken ct = default)
    {
        var browseId = _client.GetCommunityPlaylistBrowseId(playlistId);

        var tracks = new List<Track>();
        await foreach (var song in _client.GetCommunityPlaylistSongsAsync(browseId).WithCancellation(ct))
            tracks.Add(ToTrack(song));

        return tracks;
    }

    public async Task<string> GetStreamUrlAsync(string trackId, CancellationToken ct = default)
    {
        var data = await _client.GetStreamingDataAsync(trackId, ct);
        var best = data.StreamInfo
            .OfType<AudioStreamInfo>()
            .OrderByDescending(s => s.Bitrate)
            .FirstOrDefault();

        if (best is null)
            throw new InvalidOperationException($"No audio stream available for track {trackId}.");

        return best.Url;
    }

    private static Track ToTrack(LibrarySong s) =>
        new(s.Id, s.Name, ArtistNames(s.Artists), s.Album?.Name, s.Duration, ThumbnailUrl(s.Thumbnails));

    private static Track ToTrack(SongSearchResult s) =>
        new(s.Id, s.Name, ArtistNames(s.Artists), s.Album?.Name, s.Duration, ThumbnailUrl(s.Thumbnails));

    private static Track ToTrack(CommunityPlaylistSong s) =>
        new(s.Id, s.Name, ArtistNames(s.Artists), s.Album?.Name, s.Duration, ThumbnailUrl(s.Thumbnails));

    private static string ArtistNames(NamedEntity[]? artists) =>
        artists is { Length: > 0 } ? string.Join(", ", artists.Select(a => a.Name)) : "Unknown Artist";

    private static string? ThumbnailUrl(Thumbnail[]? thumbnails) =>
        thumbnails?.OrderByDescending(t => t.Width).FirstOrDefault()?.Url;
}
