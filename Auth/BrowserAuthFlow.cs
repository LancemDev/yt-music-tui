using System.Diagnostics;
using System.Net;
using System.Text;
using System.Web;

namespace YtMusicTui.Auth;

/// <summary>
/// Interactive browser auth for Linux/macOS/Windows TUIs.
/// Opens a local page that sends the user to YouTube Music sign-in, then accepts
/// a pasted Cookie header (HttpOnly cookies cannot be read from page JS).
/// </summary>
public sealed class BrowserAuthFlow : IAsyncDisposable
{
    public const string SignInUrl =
        "https://accounts.google.com/ServiceLogin?service=youtube&uilel=3&passive=1&continue=https%3A%2F%2Fmusic.youtube.com%2F&hl=en";

    private readonly AuthService _auth;
    private HttpListener? _listener;
    private int _port;

    public BrowserAuthFlow(AuthService auth) => _auth = auth;

    public async Task<AuthSession> RunAsync(CancellationToken ct = default)
    {
        _port = FindFreePort();
        var baseUrl = $"http://127.0.0.1:{_port}/";

        _listener = new HttpListener();
        _listener.Prefixes.Add(baseUrl);
        _listener.Start();

        var completion = new TaskCompletionSource<AuthSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = ct.Register(() => completion.TrySetCanceled(ct));

        Console.WriteLine();
        Console.WriteLine("Authentication required for YouTube Music.");
        Console.WriteLine($"Opening browser → {baseUrl}");
        Console.WriteLine("Complete sign-in in the browser, then paste your Cookie header on that page.");
        Console.WriteLine("Press Ctrl+C to cancel.");
        Console.WriteLine();

        OpenBrowser(baseUrl);

        var listenTask = ListenAsync(completion, ct);
        var session = await completion.Task.ConfigureAwait(false);
        await listenTask.ConfigureAwait(false);
        return session;
    }

    private async Task ListenAsync(TaskCompletionSource<AuthSession> completion, CancellationToken ct)
    {
        if (_listener is null) return;

        try
        {
            while (!ct.IsCancellationRequested && !completion.Task.IsCompleted)
            {
                var contextTask = _listener.GetContextAsync();
                var finished = await Task.WhenAny(contextTask, completion.Task).ConfigureAwait(false);
                if (finished != contextTask)
                    break;

                var context = await contextTask.ConfigureAwait(false);
                _ = Task.Run(() => HandleRequestAsync(context, completion), ct);
            }
        }
        catch (ObjectDisposedException)
        {
            // Listener stopped.
        }
        catch (HttpListenerException)
        {
            // Listener stopped.
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, TaskCompletionSource<AuthSession> completion)
    {
        try
        {
            var req = context.Request;
            var res = context.Response;
            var path = req.Url?.AbsolutePath.TrimEnd('/') ?? "";

            if (req.HttpMethod == "GET" && (path is "" or "/"))
            {
                await WriteHtmlAsync(res, BuildLandingHtml()).ConfigureAwait(false);
                return;
            }

            if (req.HttpMethod == "GET" && path == "/signin")
            {
                res.StatusCode = 302;
                res.RedirectLocation = SignInUrl;
                res.Close();
                return;
            }

            if (req.HttpMethod == "POST" && path == "/submit")
            {
                using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                var body = await reader.ReadToEndAsync().ConfigureAwait(false);
                var form = HttpUtility.ParseQueryString(body);
                var cookieHeader = form["cookies"]?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(cookieHeader))
                {
                    await WriteHtmlAsync(res, BuildLandingHtml(error: "Cookie header was empty.")).ConfigureAwait(false);
                    return;
                }

                try
                {
                    var imported = _auth.SaveCookiesFromHeader(cookieHeader);
                    Console.WriteLine("Cookies received. Validating with YouTube Music…");
                    var validated = await _auth.ValidateAsync(imported).ConfigureAwait(false);

                    if (!validated.IsAuthenticated)
                    {
                        await WriteHtmlAsync(
                            res,
                            BuildLandingHtml(error: $"Validation failed: {validated.StatusDetail}")).ConfigureAwait(false);
                        return;
                    }

                    await WriteHtmlAsync(res, BuildSuccessHtml()).ConfigureAwait(false);
                    completion.TrySetResult(validated);
                }
                catch (Exception ex)
                {
                    await WriteHtmlAsync(res, BuildLandingHtml(error: ex.Message)).ConfigureAwait(false);
                }

                return;
            }

            res.StatusCode = 404;
            await WriteTextAsync(res, "Not found").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Auth server error: {ex.Message}");
            try { context.Response.Abort(); } catch { /* ignore */ }
        }
    }

    private static async Task WriteHtmlAsync(HttpListenerResponse res, string html)
    {
        var bytes = Encoding.UTF8.GetBytes(html);
        res.StatusCode = 200;
        res.ContentType = "text/html; charset=utf-8";
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        res.Close();
    }

    private static async Task WriteTextAsync(HttpListenerResponse res, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        res.ContentType = "text/plain; charset=utf-8";
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        res.Close();
    }

    private static string BuildLandingHtml(string? error = null)
    {
        var errorBlock = string.IsNullOrEmpty(error)
            ? ""
            : $"<p class=\"error\">{HttpUtility.HtmlEncode(error)}</p>";

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>YT Music TUI · Sign in</title>
              <style>
                :root { color-scheme: dark; }
                body { font-family: ui-sans-serif, system-ui, sans-serif; max-width: 42rem; margin: 2rem auto; padding: 0 1rem; background: #111; color: #eee; line-height: 1.5; }
                a.button, button { display: inline-block; background: #f00; color: #fff; border: 0; padding: .7rem 1.1rem; border-radius: 6px; text-decoration: none; font-weight: 600; cursor: pointer; }
                a.button.secondary { background: #333; margin-left: .5rem; }
                ol { padding-left: 1.2rem; }
                textarea { width: 100%; min-height: 9rem; margin: .75rem 0; padding: .75rem; border-radius: 6px; border: 1px solid #444; background: #1a1a1a; color: #eee; font-family: ui-monospace, monospace; }
                .error { color: #ff8a8a; background: #3a1515; padding: .75rem; border-radius: 6px; }
                code { background: #222; padding: .1rem .35rem; border-radius: 4px; }
                .hint { color: #aaa; font-size: .95rem; }
              </style>
            </head>
            <body>
              <h1>Sign in to YouTube Music</h1>
              <p>This page bridges browser login into the TUI. Google cookies are HttpOnly, so they must be pasted once after sign-in.</p>
              {{errorBlock}}
              <ol>
                <li>Click <strong>Open YouTube Music sign-in</strong> and finish login.</li>
                <li>On <code>music.youtube.com</code>, open DevTools → <strong>Network</strong>.</li>
                <li>Refresh, click any request to <code>music.youtube.com</code>.</li>
                <li>Copy the full <code>Cookie</code> request header value.</li>
                <li>Paste it below and submit.</li>
              </ol>
              <p>
                <a class="button" href="/signin" target="_blank" rel="noopener">Open YouTube Music sign-in</a>
              </p>
              <form method="post" action="/submit">
                <label for="cookies">Cookie header</label>
                <textarea id="cookies" name="cookies" placeholder="SAPISID=...; __Secure-1PSID=...; ..." required></textarea>
                <button type="submit">Save &amp; continue</button>
              </form>
              <p class="hint">Cookies are stored only on this machine under ~/.config/yt-music-tui/</p>
            </body>
            </html>
            """;
    }

    private static string BuildSuccessHtml() => """
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <title>Signed in</title>
          <style>
            :root { color-scheme: dark; }
            body { font-family: ui-sans-serif, system-ui, sans-serif; max-width: 36rem; margin: 3rem auto; padding: 0 1rem; background: #111; color: #eee; }
          </style>
        </head>
        <body>
          <h1>Signed in</h1>
          <p>You can close this tab and return to the terminal. The TUI is starting.</p>
        </body>
        </html>
        """;

    public static void OpenBrowser(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return;
            }

            if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", url);
                return;
            }

            // Linux / BSD
            foreach (var opener in new[] { "xdg-open", "gio", "gnome-open", "kde-open" })
            {
                try
                {
                    Process.Start(new ProcessStartInfo(opener, url)
                    {
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    });
                    return;
                }
                catch
                {
                    // try next
                }
            }

            Console.WriteLine($"Open this URL manually: {url}");
        }
        catch
        {
            Console.WriteLine($"Open this URL manually: {url}");
        }
    }

    private static int FindFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        if (_listener is null) return;
        try { _listener.Stop(); } catch { /* ignore */ }
        _listener.Close();
        _listener = null;
        await Task.CompletedTask;
    }
}
