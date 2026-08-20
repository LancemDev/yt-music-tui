using Ratatui;

namespace YtMusicTui.UI.Widgets;

public static class HelpBar
{
    public static void Draw(Terminal term, Rect area)
    {
        using var help = new Paragraph(
            "1-4 screens  |  j/k move  |  Enter play  |  Space pause  |  n/p next/prev  |  / search  |  q quit");
        term.Draw(help, area);
    }
}
