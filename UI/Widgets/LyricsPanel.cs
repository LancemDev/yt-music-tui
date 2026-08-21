using Ratatui;
using YtMusicTui.App;
using YtMusicTui.Models;

namespace YtMusicTui.UI.Widgets;

public static class LyricsPanel
{
    public static void Draw(Terminal term, Rect area, AppState state)
    {
        using var list = new List().Title("Lyrics", border: true);

        var lyrics = state.Lyrics;
        if (lyrics is null || lyrics.Lines.Count == 0)
        {
            list.AppendItem("Loading lyrics…");
            term.Draw(list, area);
            return;
        }

        foreach (var line in lyrics.Lines)
            list.AppendItem(line.Text);

        if (lyrics.IsSynced)
        {
            var currentIndex = CurrentLineIndex(lyrics, state.Position);
            if (currentIndex >= 0)
                list.Selected(currentIndex).HighlightSymbol("▶ ");
        }

        term.Draw(list, area);
    }

    private static int CurrentLineIndex(Lyrics lyrics, TimeSpan position)
    {
        var index = -1;
        for (var i = 0; i < lyrics.Lines.Count; i++)
        {
            if (lyrics.Lines[i].Time is { } t && t <= position)
                index = i;
        }

        return index;
    }
}
