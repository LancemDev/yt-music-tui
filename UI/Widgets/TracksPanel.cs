using Ratatui;
using YtMusicTui.App;

namespace YtMusicTui.UI.Widgets;

public static class TracksPanel
{
    public static void Draw(Terminal term, Rect area, AppState state)
    {
        var title = state.IsSearching
            ? $"Search: /{state.SearchQuery}█"
            : state.IsShowingSearchResults
                ? $"Tracks · Search: {state.SearchQuery}"
                : "Tracks · Library";

        if (state.LeftFocus == LeftFocus.Tracks) title = $"▶ {title}";

        using var list = new List().Title(title, border: true);

        var tracks = state.DisplayedTracks;
        if (tracks.Count == 0)
        {
            list.AppendItem(state.IsShowingSearchResults ? "No results." : "No tracks yet.");
        }
        else
        {
            foreach (var t in tracks)
            {
                var duration = t.Duration is { } d ? d.ToString(@"m\:ss") : "--:--";
                var marker = state.NowPlaying?.Id == t.Id ? "♪ " : "  ";
                list.AppendItem($"{marker}{t.Title}  ·  {t.Artist}  [{duration}]");
            }

            list.Selected(state.TracksSelectedIndex).HighlightSymbol("> ");
        }

        term.Draw(list, area);
    }
}
