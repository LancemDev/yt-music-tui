using Ratatui;
using YtMusicTui.App;

namespace YtMusicTui.UI.Widgets;

public static class HeaderBar
{
    public static void Draw(Terminal term, Rect area, AppState state, string appName)
    {
        var tabs = state.CurrentScreen switch
        {
            Screen.Home => 0,
            Screen.Search => 1,
            Screen.Library => 2,
            Screen.Queue => 3,
            _ => 0
        };

        using var widget = new Tabs()
            .Title($"{appName}  ·  [{state.AuthLabel}]  ·  {state.StatusMessage}", border: true)
            .Titles("1 Home", "2 Search", "3 Library", "4 Queue")
            .Selected(tabs);

        term.Draw(widget, area);
    }
}
