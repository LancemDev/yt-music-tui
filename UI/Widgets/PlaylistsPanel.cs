using Ratatui;
using YtMusicTui.App;

namespace YtMusicTui.UI.Widgets;

public static class PlaylistsPanel
{
    public static void Draw(Terminal term, Rect area, AppState state)
    {
        var title = state.LeftFocus == LeftFocus.Playlists ? "▶ Playlists" : "Playlists";
        using var list = new List().Title(title, border: true);

        if (state.Playlists.Count == 0)
        {
            list.AppendItem("No playlists yet.");
        }
        else
        {
            foreach (var p in state.Playlists)
                list.AppendItem($"{p.Title}  ({p.TrackCount})");

            list.Selected(state.PlaylistsSelectedIndex).HighlightSymbol("> ");
        }

        term.Draw(list, area);
    }
}
