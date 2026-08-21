using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using YtMusicTui.Models;

namespace YtMusicTui.Services;

/// <summary>
/// Fetches synced/plain lyrics from LRCLIB (lrclib.net) — free, keyless, community-sourced.
/// YouTubeMusicAPI has no lyrics endpoint of its own.
/// </summary>
public sealed partial class LyricsService
{
    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri("https://lrclib.net/"),
        Timeout = TimeSpan.FromSeconds(8)
    };

    public async Task<Lyrics> GetLyricsAsync(string title, string artist, TimeSpan? duration, CancellationToken ct = default)
    {
        try
        {
            var query = $"api/get?track_name={Uri.EscapeDataString(title)}&artist_name={Uri.EscapeDataString(artist)}";
            if (duration is { } d)
                query += $"&duration={(int)d.TotalSeconds}";

            var response = await _http.GetAsync(query, ct);
            if (!response.IsSuccessStatusCode)
                return Lyrics.NotFound;

            var payload = await response.Content.ReadFromJsonAsync<LrcLibResponse>(cancellationToken: ct);
            if (payload is null)
                return Lyrics.NotFound;

            if (!string.IsNullOrWhiteSpace(payload.SyncedLyrics))
                return new Lyrics(ParseSynced(payload.SyncedLyrics), IsSynced: true);

            if (!string.IsNullOrWhiteSpace(payload.PlainLyrics))
            {
                var lines = payload.PlainLyrics
                    .Split('\n')
                    .Select(l => new LyricsLine(null, l))
                    .ToList();
                return new Lyrics(lines, IsSynced: false);
            }

            return Lyrics.NotFound;
        }
        catch
        {
            return Lyrics.NotFound;
        }
    }

    [GeneratedRegex(@"^\[(\d{2}):(\d{2})\.(\d{2,3})\]\s*(.*)$")]
    private static partial Regex TimeTagRegex();

    private static IReadOnlyList<LyricsLine> ParseSynced(string lrc)
    {
        var lines = new List<LyricsLine>();
        foreach (var raw in lrc.Split('\n'))
        {
            var match = TimeTagRegex().Match(raw.TrimEnd('\r'));
            if (!match.Success) continue;

            var minutes = int.Parse(match.Groups[1].Value);
            var seconds = int.Parse(match.Groups[2].Value);
            var millis = int.Parse(match.Groups[3].Value.PadRight(3, '0')[..3]);
            var time = new TimeSpan(0, 0, minutes, seconds, millis);
            var text = match.Groups[4].Value;

            lines.Add(new LyricsLine(time, string.IsNullOrWhiteSpace(text) ? " " : text));
        }

        return lines.Count > 0 ? lines : Lyrics.NotFound.Lines;
    }

    private sealed record LrcLibResponse(
        [property: JsonPropertyName("plainLyrics")] string? PlainLyrics,
        [property: JsonPropertyName("syncedLyrics")] string? SyncedLyrics);
}
