using System.Net;
using YouTubeMusicAPI.Client;

namespace YtMusicTui.Auth;

public static class YouTubeClientFactory
{
    public static YouTubeMusicClient Create(AuthSession session)
        => new(
            geographicalLocation: session.GeographicalLocation,
            visitorData: session.VisitorData,
            poToken: session.PoToken,
            cookies: session.Cookies.Count > 0 ? session.Cookies : null);
}
