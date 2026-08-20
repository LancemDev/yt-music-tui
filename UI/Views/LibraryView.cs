using Ratatui;
using YtMusicTui.App;

namespace YtMusicTui.UI.Views;

public sealed class LibraryView : IView
{
    public Screen Screen => Screen.Library;

    public void HandleKey(in Event ev, AppState state)
    {
        if (ev.Kind != EventKind.Key) return;

        var count = state.LibraryPlaylists.Count;
        if (count == 0) return;

        if (ev.Key.Char == (uint)'j' || ev.Key.CodeEnum == KeyCode.Down)
            state.LibrarySelectedIndex = Math.Min(count - 1, state.LibrarySelectedIndex + 1);
        else if (ev.Key.Char == (uint)'k' || ev.Key.CodeEnum == KeyCode.Up)
            state.LibrarySelectedIndex = Math.Max(0, state.LibrarySelectedIndex - 1);
    }

    public void Draw(Terminal term, Rect area, AppState state)
    {
        using var list = new List()
            .Title("Library · Playlists", border: true);

        if (state.LibraryPlaylists.Count == 0)
        {
            list.AppendItem("No playlists yet.");
        }
        else
        {
            foreach (var p in state.LibraryPlaylists)
            {
                var desc = string.IsNullOrWhiteSpace(p.Description) ? "" : $" — {p.Description}";
                list.AppendItem($"{p.Title}  ({p.TrackCount}){desc}");
            }

            list.Selected(state.LibrarySelectedIndex).HighlightSymbol("> ");
        }

        term.Draw(list, area);
    }
}
