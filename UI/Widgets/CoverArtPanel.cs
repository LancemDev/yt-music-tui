using Ratatui;
using YtMusicTui.App;
using YtMusicTui.Models;

namespace YtMusicTui.UI.Widgets;

/// <summary>
/// Styled text placeholder for album art — no terminal image protocol (Kitty/Sixel)
/// detection, just a centered title/artist/album block matching the wireframe's box.
/// </summary>
public static class CoverArtPanel
{
    public static void Draw(Terminal term, Rect area, AppState state)
    {
        var lines = state.NowPlaying is { } track
            ? BuildTrackLines(track)
            : ["Nothing playing", "Press Enter on a track to start"];

        var topPad = Math.Max(0, (area.Height - 2 - lines.Count) / 2);
        var body = string.Concat(Enumerable.Repeat("\n", topPad)) + string.Join('\n', lines);

        using var para = new Paragraph(body).Title("Now Playing", border: true).Align(Alignment.Center);
        term.Draw(para, area);
    }

    private static List<string> BuildTrackLines(Track track)
    {
        var lines = new List<string> { track.Title, track.Artist };
        if (!string.IsNullOrWhiteSpace(track.Album))
            lines.Add(track.Album);
        return lines;
    }
}
