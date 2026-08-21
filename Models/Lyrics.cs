namespace YtMusicTui.Models;

public sealed record LyricsLine(TimeSpan? Time, string Text);

public sealed record Lyrics(IReadOnlyList<LyricsLine> Lines, bool IsSynced)
{
    public static readonly Lyrics NotFound = new([new LyricsLine(null, "No lyrics found for this track.")], false);
}
