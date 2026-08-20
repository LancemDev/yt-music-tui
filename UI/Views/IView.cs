using Ratatui;
using YtMusicTui.App;

namespace YtMusicTui.UI.Views;

public interface IView
{
    Screen Screen { get; }
    void HandleKey(in Event ev, AppState state);
    void Draw(Terminal term, Rect area, AppState state);
}
