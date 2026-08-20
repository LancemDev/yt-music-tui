using Ratatui;

namespace YtMusicTui.UI.Layout;

public readonly record struct AppLayout(Rect Header, Rect Content, Rect Player, Rect Help)
{
    public const int HeaderHeight = 3;
    public const int PlayerHeight = 4;
    public const int HelpHeight = 1;

    public static AppLayout FromSize(int width, int height)
    {
        var full = Rect.FromSize(width, Math.Max(height, HeaderHeight + PlayerHeight + HelpHeight + 1));
        var header = new Rect(full.X, full.Y, full.Width, HeaderHeight);

        var helpY = full.Y + full.Height - HelpHeight;
        var help = new Rect(full.X, helpY, full.Width, HelpHeight);

        var playerY = helpY - PlayerHeight;
        var player = new Rect(full.X, playerY, full.Width, PlayerHeight);

        var contentHeight = Math.Max(1, playerY - header.Y - header.Height);
        var content = new Rect(full.X, header.Y + header.Height, full.Width, contentHeight);

        return new AppLayout(header, content, player, help);
    }
}
