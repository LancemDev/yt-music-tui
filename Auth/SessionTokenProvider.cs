using YouTubeSessionGenerator;
using YouTubeSessionGenerator.Js.Environments;

namespace YtMusicTui.Auth;

/// <summary>
/// Generates the VisitorData + PoToken YouTube now requires for streaming requests
/// (a BotGuard anti-bot check) via the same-author companion to YouTubeMusicAPI.
/// Solves Google's JS challenge through a local Node.js process — no browser needed,
/// but Node.js must be on PATH.
/// </summary>
public static class SessionTokenProvider
{
    public static async Task<(string VisitorData, string PoToken)?> TryGenerateAsync(CancellationToken ct = default)
    {
        try
        {
            using var jsEnvironment = new NodeEnvironment();
            var config = new YouTubeSessionConfig { JsEnvironment = jsEnvironment };
            var creator = new YouTubeSessionCreator(config);

            var visitorData = await creator.VisitorDataAsync(ct);
            var poToken = await creator.ProofOfOriginTokenAsync(visitorData, cancellationToken: ct);

            return (visitorData, poToken);
        }
        catch
        {
            return null;
        }
    }
}
