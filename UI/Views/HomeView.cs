using Ratatui;
using YtMusicTui.App;

namespace YtMusicTui.UI.Views;

public sealed class HomeView : IView
{
    public Screen Screen => Screen.Home;

    public void HandleKey(in Event ev, AppState state)
    {
        if (ev.Kind != EventKind.Key) return;

        var count = state.HomeTracks.Count;
        if (count == 0) return;

        if (ev.Key.Char == (uint)'j' || ev.Key.CodeEnum == KeyCode.Down)
            state.HomeSelectedIndex = Math.Min(count - 1, state.HomeSelectedIndex + 1);
        else if (ev.Key.Char == (uint)'k' || ev.Key.CodeEnum == KeyCode.Up)
            state.HomeSelectedIndex = Math.Max(0, state.HomeSelectedIndex - 1);
    }

    public void Draw(Terminal term, Rect area, AppState state)
    {
        using var list = new List()
            .Title("Home · Quick picks", border: true);

        if (state.HomeTracks.Count == 0)
        {
            list.AppendItem("No tracks loaded yet.");
        }
        else
        {
            for (var i = 0; i < state.HomeTracks.Count; i++)
            {
                var t = state.HomeTracks[i];
                var duration = t.Duration is { } d ? d.ToString(@"m\:ss") : "--:--";
                list.AppendItem($"{t.Title}  ·  {t.Artist}  [{duration}]");
            }

            list.Selected(state.HomeSelectedIndex).HighlightSymbol("> ");
        }

        term.Draw(list, area);
    }
}
