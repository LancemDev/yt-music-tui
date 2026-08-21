using Ratatui;

namespace YtMusicTui.UI.Widgets;

public static class QuickActionsBar
{
    public static void Draw(Terminal term, Rect area)
    {
        using var bar = new Paragraph(
            "Tab focus  |  j/k move  |  Enter play  |  Space pause  |  n/p next/prev  |  / search  |  f fullscreen  |  c collapse  |  q quit");
        term.Draw(bar, area);
    }
}
