using Ratatui;
using YtMusicTui.App;

namespace YtMusicTui.UI.Layout;

public readonly record struct AppLayout(
    Rect Header,
    Rect? TracksPanel,
    Rect? PlaylistsPanel,
    Rect? CoverArt,
    Rect? BarVisualizer,
    Rect? Lyrics,
    Rect Player,
    Rect QuickActions)
{
    private const int HeaderHeight = 3;
    private const int PlayerHeight = 4;
    private const int QuickActionsHeight = 1;
    private const int LeftWidth = 28;
    private const int RightWidth = 34;

    public static AppLayout FromSize(int width, int height, FullScreenMode fullScreen, bool sidebarCollapsed)
    {
        var minHeight = HeaderHeight + PlayerHeight + QuickActionsHeight + 4;
        var full = Rect.FromSize(width, Math.Max(height, minHeight));

        var header = new Rect(full.X, full.Y, full.Width, HeaderHeight);

        var quickActionsY = full.Y + full.Height - QuickActionsHeight;
        var quickActions = new Rect(full.X, quickActionsY, full.Width, QuickActionsHeight);

        var playerY = quickActionsY - PlayerHeight;
        var player = new Rect(full.X, playerY, full.Width, PlayerHeight);

        var contentHeight = Math.Max(1, playerY - header.Y - header.Height);
        var content = new Rect(full.X, header.Y + header.Height, full.Width, contentHeight);

        if (fullScreen == FullScreenMode.Lyrics)
            return new AppLayout(header, null, null, null, null, content, player, quickActions);

        if (fullScreen == FullScreenMode.CoverBar || sidebarCollapsed)
        {
            var (coverOnly, barOnly) = SplitCenter(content);
            return new AppLayout(header, null, null, coverOnly, barOnly, null, player, quickActions);
        }

        var leftWidth = Math.Min(LeftWidth, content.Width / 3);
        var rightWidth = Math.Min(RightWidth, content.Width / 3);
        var centerWidth = Math.Max(1, content.Width - leftWidth - rightWidth);

        var left = new Rect(content.X, content.Y, leftWidth, content.Height);
        var center = new Rect(left.X + left.Width, content.Y, centerWidth, content.Height);
        var right = new Rect(center.X + center.Width, content.Y, rightWidth, content.Height);

        var tracksHeight = Math.Max(1, (int)(left.Height * 0.6));
        var tracks = new Rect(left.X, left.Y, left.Width, tracksHeight);
        var playlists = new Rect(left.X, left.Y + tracksHeight, left.Width, left.Height - tracksHeight);

        var (coverArt, barVisualizer) = SplitCenter(center);

        return new AppLayout(header, tracks, playlists, coverArt, barVisualizer, right, player, quickActions);
    }

    private static (Rect Cover, Rect Bar) SplitCenter(Rect center)
    {
        var barHeight = Math.Max(3, (int)(center.Height * 0.35));
        var coverHeight = Math.Max(1, center.Height - barHeight);
        var cover = new Rect(center.X, center.Y, center.Width, coverHeight);
        var bar = new Rect(center.X, center.Y + coverHeight, center.Width, barHeight);
        return (cover, bar);
    }
}
