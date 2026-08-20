using System.Diagnostics;
using System.Text.RegularExpressions;

namespace YtMusicTui.Auth;

/// <summary>
/// Interactive sign-in for the TUI: opens music.youtube.com directly (it redirects to Google's
/// own current sign-in flow if needed — no locally-hosted page, and no hand-built Google auth
/// URL to go stale), then auto-detects the resulting session cookie from the user's browser
/// profile. Falls back to a terminal paste prompt if auto-detection doesn't find anything.
/// </summary>
public sealed class BrowserAuthFlow
{
    public const string SignInUrl = "https://music.youtube.com/";

    private readonly AuthService _auth;

    public BrowserAuthFlow(AuthService auth) => _auth = auth;

    public async Task<AuthSession> RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine();
        Console.WriteLine("Authentication required for YouTube Music.");
        Console.WriteLine("Press Enter to open YouTube Music in your browser…");
        await WaitForKeyAsync(ct).ConfigureAwait(false);

        OpenBrowser(SignInUrl);

        Console.WriteLine();
        Console.WriteLine("Sign in there if prompted, then make sure the tab lands on music.youtube.com.");
        Console.WriteLine("Press Enter here once you're signed in…");
        await WaitForKeyAsync(ct).ConfigureAwait(false);

        Console.WriteLine("Looking for your session in Chrome, Chromium, Brave, Edge, and Firefox…");
        var detected = BrowserCookieReader.TryReadYouTubeCookies(out var source);

        AuthSession? imported = null;
        if (detected is { Count: > 0 })
        {
            try
            {
                Console.WriteLine($"Found a signed-in session via {source}.");
                imported = _auth.SaveCookies(detected);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Auto-detected session looked invalid: {ex.Message}");
            }
        }

        if (imported is null)
        {
            Console.WriteLine(detected is { Count: > 0 } ? "" : "Couldn't auto-detect a session from your browsers.");
            Console.WriteLine("Open DevTools (F12) → Network → any music.youtube.com request → copy the 'Cookie' request header.");

            for (var attempt = 0; attempt < 3 && imported is null; attempt++)
            {
                Console.Write("Paste it here: ");
                var header = await ReadLineAsync(ct).ConfigureAwait(false);
                try
                {
                    imported = _auth.SaveCookiesFromHeader(header ?? "");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ex.Message} Try again.");
                }
            }
        }

        if (imported is null)
        {
            Console.WriteLine("Giving up after repeated invalid input.");
            return await _auth.LoadAsync(ct).ConfigureAwait(false);
        }

        Console.WriteLine("Validating with YouTube Music…");
        var validated = await _auth.ValidateAsync(imported, ct).ConfigureAwait(false);

        Console.WriteLine(validated.IsAuthenticated
            ? "Signed in."
            : $"Sign-in saved but validation failed: {validated.StatusDetail}");

        return validated;
    }

    private static Task WaitForKeyAsync(CancellationToken ct) => ReadLineAsync(ct);

    private static async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        var readTask = Task.Run(Console.ReadLine, ct);
        var cancelSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = ct.Register(() => cancelSignal.TrySetResult());

        var finished = await Task.WhenAny(readTask, cancelSignal.Task).ConfigureAwait(false);
        if (finished != readTask)
            throw new OperationCanceledException(ct);

        return await readTask.ConfigureAwait(false);
    }

    private static readonly string[] KnownBrowserBinaries =
    [
        "zen", "brave-browser", "brave", "google-chrome", "google-chrome-stable",
        "chromium", "chromium-browser", "firefox", "microsoft-edge"
    ];

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
                if (TryLaunch("open", [url])) return;
            }
            else
            {
                // Linux/BSD: launch the real browser executable directly rather than going
                // through xdg-open/kde-open — on some desktop setups (e.g. KDE with a browser
                // that isn't a "known" system package) those route the URL through a kioexec/
                // portal detour instead of actually opening a browser tab.
                var defaultBrowser = ResolveDefaultBrowserCommand();
                if (defaultBrowser is not null && TryLaunch(defaultBrowser, [url]))
                    return;

                foreach (var binary in KnownBrowserBinaries)
                {
                    if (TryLaunch(binary, [url])) return;
                }

                if (TryLaunch("xdg-open", [url])) return;
                if (TryLaunch("gio", ["open", url])) return;
                if (TryLaunch("gnome-open", [url])) return;
                if (TryLaunch("kde-open", [url])) return;
            }

            Console.WriteLine($"Open this URL manually: {url}");
        }
        catch
        {
            Console.WriteLine($"Open this URL manually: {url}");
        }
    }

    private static bool TryLaunch(string command, string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(command)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            foreach (var arg in args) psi.ArgumentList.Add(arg);

            using var proc = Process.Start(psi);
            return proc is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Resolves the user's configured default browser (via xdg-settings + its .desktop
    /// Exec line) to an executable path, so we can launch it directly instead of relying
    /// on a desktop URL-open helper that may not handle it correctly.
    /// </summary>
    private static string? ResolveDefaultBrowserCommand()
    {
        try
        {
            var psi = new ProcessStartInfo("xdg-settings")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("get");
            psi.ArgumentList.Add("default-web-browser");

            using var proc = Process.Start(psi);
            if (proc is null) return null;

            var desktopFile = proc.StandardOutput.ReadToEnd().Trim();
            if (!proc.WaitForExit(2000))
            {
                try { proc.Kill(); } catch { /* best effort */ }
                return null;
            }
            if (string.IsNullOrEmpty(desktopFile)) return null;

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] searchDirs =
            [
                Path.Combine(home, ".local", "share", "applications"),
                "/usr/share/applications",
                "/usr/local/share/applications"
            ];

            foreach (var dir in searchDirs)
            {
                var path = Path.Combine(dir, desktopFile);
                if (!File.Exists(path)) continue;

                foreach (var line in File.ReadAllLines(path))
                {
                    if (!line.StartsWith("Exec=", StringComparison.Ordinal)) continue;

                    var exec = Regex.Replace(line["Exec=".Length..], "%[a-zA-Z%]", "").Trim();
                    var firstToken = exec.Split(' ', 2)[0].Trim('"');
                    return string.IsNullOrEmpty(firstToken) ? null : firstToken;
                }
            }
        }
        catch
        {
            // fall through to other strategies
        }

        return null;
    }
}
