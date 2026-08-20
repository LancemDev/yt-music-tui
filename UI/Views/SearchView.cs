using Ratatui;
using YtMusicTui.App;

namespace YtMusicTui.UI.Views;

public sealed class SearchView : IView
{
    public Screen Screen => Screen.Search;

    public void HandleKey(in Event ev, AppState state)
    {
        if (ev.Kind != EventKind.Key) return;

        if (state.IsSearching)
        {
            HandleSearchInput(ev, state);
            return;
        }

        var count = state.SearchTracks.Count;
        if (ev.Key.Char == (uint)'/')
        {
            state.IsSearching = true;
            state.StatusMessage = "Search mode — type and press Enter";
            return;
        }

        if (count == 0) return;

        if (ev.Key.Char == (uint)'j' || ev.Key.CodeEnum == KeyCode.Down)
            state.SearchSelectedIndex = Math.Min(count - 1, state.SearchSelectedIndex + 1);
        else if (ev.Key.Char == (uint)'k' || ev.Key.CodeEnum == KeyCode.Up)
            state.SearchSelectedIndex = Math.Max(0, state.SearchSelectedIndex - 1);
    }

    private static void HandleSearchInput(in Event ev, AppState state)
    {
        if (ev.Key.CodeEnum == KeyCode.Esc)
        {
            state.IsSearching = false;
            state.StatusMessage = "Search cancelled";
            return;
        }

        if (ev.Key.CodeEnum == KeyCode.Enter)
        {
            state.IsSearching = false;
            state.StatusMessage = string.IsNullOrWhiteSpace(state.SearchQuery)
                ? "Empty query"
                : $"Search: {state.SearchQuery}";
            return;
        }

        if (ev.Key.CodeEnum == KeyCode.Backspace)
        {
            if (state.SearchQuery.Length > 0)
                state.SearchQuery = state.SearchQuery[..^1];
            return;
        }

        if (ev.Key.CodeEnum == KeyCode.Char && ev.Key.Char is >= 32 and <= 126)
            state.SearchQuery += (char)ev.Key.Char;
    }

    public void Draw(Terminal term, Rect area, AppState state)
    {
        var queryLine = state.IsSearching
            ? $"/{state.SearchQuery}█"
            : string.IsNullOrEmpty(state.SearchQuery)
                ? "Press / to search"
                : $"Query: {state.SearchQuery}";

        using var para = new Paragraph(queryLine)
            .Title("Search", border: true);

        if (state.SearchTracks.Count == 0)
        {
            para.NewLine().AppendSpan("No results.");
            term.Draw(para, area);
            return;
        }

        var (top, bottom) = area.SplitHorizontal(3);
        term.Draw(para, top);

        using var list = new List().Title("Results", border: true);
        foreach (var t in state.SearchTracks)
            list.AppendItem($"{t.Title}  ·  {t.Artist}");

        list.Selected(state.SearchSelectedIndex).HighlightSymbol("> ");
        term.Draw(list, bottom);
    }
}
