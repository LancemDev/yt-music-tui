using Ratatui;
using YtMusicTui.App;

namespace YtMusicTui.UI.Widgets;

public static class PlayerBar
{
    public static void Draw(Terminal term, Rect area, AppState state)
    {
        var status = state.IsPlaying ? "Playing" : "Paused";
        var line = state.NowPlaying is { } track
            ? $"{status}  ·  {track.Title} — {track.Artist}"
            : "Nothing playing  ·  Enter play · Space pause · n/p skip";

        var pos = Format(state.Position);
        var dur = Format(state.Duration);

        if (area.Height <= 2)
        {
            using var compact = new Paragraph($"{line}  [{pos}/{dur}]")
                .Title("Now Playing", border: true);
            term.Draw(compact, area);
            return;
        }

        var (infoArea, gaugeArea) = area.SplitHorizontal(area.Height - 1);

        using var info = new Paragraph(line)
            .Title("Now Playing", border: true);
        term.Draw(info, infoArea);

        using var gauge = new Gauge()
            .Ratio(state.ProgressRatio)
            .Label($"{pos} / {dur}");
        term.Draw(gauge, gaugeArea);
    }

    private static string Format(TimeSpan t) =>
        t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
}
