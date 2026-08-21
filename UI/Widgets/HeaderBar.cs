using Ratatui;
using YtMusicTui.App;

namespace YtMusicTui.UI.Widgets;

public static class HeaderBar
{
    public static void Draw(Terminal term, Rect area, AppState state, string appName)
    {
        using var para = new Paragraph($"[{state.AuthLabel}]  ·  {state.StatusMessage}")
            .Title(appName, border: true);

        term.Draw(para, area);
    }
}
