using Ratatui;
using YtMusicTui.App;

namespace YtMusicTui.UI.Views;

public sealed class QueueView : IView
{
    public Screen Screen => Screen.Queue;

    public void HandleKey(in Event ev, AppState state)
    {
        if (ev.Kind != EventKind.Key) return;

        var count = state.Queue.Count;
        if (count == 0) return;

        if (ev.Key.Char == (uint)'j' || ev.Key.CodeEnum == KeyCode.Down)
            state.QueueSelectedIndex = Math.Min(count - 1, state.QueueSelectedIndex + 1);
        else if (ev.Key.Char == (uint)'k' || ev.Key.CodeEnum == KeyCode.Up)
            state.QueueSelectedIndex = Math.Max(0, state.QueueSelectedIndex - 1);
    }

    public void Draw(Terminal term, Rect area, AppState state)
    {
        using var list = new List()
            .Title("Queue", border: true);

        if (state.Queue.Count == 0)
        {
            list.AppendItem("Queue is empty — play something from Home.");
        }
        else
        {
            for (var i = 0; i < state.Queue.Count; i++)
            {
                var t = state.Queue[i];
                var marker = state.NowPlaying?.Id == t.Id ? "♪ " : "  ";
                list.AppendItem($"{marker}{i + 1}. {t.Title}  ·  {t.Artist}");
            }

            list.Selected(state.QueueSelectedIndex).HighlightSymbol("> ");
        }

        term.Draw(list, area);
    }
}
