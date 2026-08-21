using Ratatui;
using YtMusicTui.App;

namespace YtMusicTui.UI.Widgets;

public static class BarVisualizerPanel
{
    public static void Draw(Terminal term, Rect area, AppState state)
    {
        using var chart = new BarChart()
            .Values(state.VisualizerLevels.ToArray())
            .Title("Bar Visualization", border: true);

        term.Draw(chart, area);
    }
}
