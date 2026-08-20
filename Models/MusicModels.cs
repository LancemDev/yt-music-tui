namespace YtMusicTui.Models;

public sealed record Track(
    string Id,
    string Title,
    string Artist,
    string? Album = null,
    TimeSpan? Duration = null,
    string? ThumbnailUrl = null);

public sealed record Playlist(
    string Id,
    string Title,
    string? Description = null,
    int TrackCount = 0,
    IReadOnlyList<Track>? Tracks = null);

public sealed record Album(
    string Id,
    string Title,
    string Artist,
    int? Year = null,
    IReadOnlyList<Track>? Tracks = null);

public sealed record Artist(
    string Id,
    string Name,
    string? Description = null);

public sealed record SearchResults(
    IReadOnlyList<Track> Tracks,
    IReadOnlyList<Album> Albums,
    IReadOnlyList<Artist> Artists,
    IReadOnlyList<Playlist> Playlists);
