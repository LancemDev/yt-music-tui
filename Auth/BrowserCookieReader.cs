using System.Net;
using Microsoft.Data.Sqlite;

namespace YtMusicTui.Auth;

/// <summary>
/// Auto-detects a signed-in YouTube Music session by reading cookies directly out of
/// the user's local browser profiles (Firefox, Chrome, Chromium, Brave, Edge) — the same
/// approach tools like yt-dlp's --cookies-from-browser use. Linux only for now.
/// </summary>
public static class BrowserCookieReader
{
    private sealed record Candidate(string Label, string DbPath, bool IsFirefox, string[] SecretToolApps);

    public static IReadOnlyList<Cookie>? TryReadYouTubeCookies(out string? source)
    {
        source = null;
        if (!OperatingSystem.IsLinux())
            return null;

        var candidates = FindCandidates()
            .OrderByDescending(c => SafeLastWriteUtc(c.DbPath))
            .ToList();

        foreach (var candidate in candidates)
        {
            IReadOnlyList<Cookie> cookies;
            try
            {
                cookies = candidate.IsFirefox
                    ? ReadFirefoxCookies(candidate.DbPath)
                    : ReadChromeCookies(candidate.DbPath, candidate.SecretToolApps);
            }
            catch
            {
                continue;
            }

            if (cookies.Count > 0 && CookieFileParser.HasAuthCookies(cookies))
            {
                source = candidate.Label;
                return cookies;
            }
        }

        return null;
    }

    private static List<Candidate> FindCandidates()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var config = Path.Combine(home, ".config");
        var candidates = new List<Candidate>();

        AddChromeFamily(candidates, "Chrome", Path.Combine(config, "google-chrome"), ["chrome", "Chrome", "Google Chrome"]);
        AddChromeFamily(candidates, "Chromium", Path.Combine(config, "chromium"), ["chromium", "Chromium"]);
        AddChromeFamily(candidates, "Brave", Path.Combine(config, "BraveSoftware", "Brave-Browser"), ["brave", "Brave"]);
        AddChromeFamily(candidates, "Edge", Path.Combine(config, "microsoft-edge"), ["microsoft-edge", "Microsoft Edge", "Chromium"]);
        AddFirefox(candidates, "Firefox", Path.Combine(home, ".mozilla", "firefox"));
        AddFirefox(candidates, "Zen", Path.Combine(home, ".zen"));

        return candidates;
    }

    private static void AddChromeFamily(List<Candidate> candidates, string label, string root, string[] secretApps)
    {
        if (!Directory.Exists(root)) return;

        foreach (var profileDir in Directory.EnumerateDirectories(root))
        {
            var name = Path.GetFileName(profileDir);
            if (name != "Default" && !name.StartsWith("Profile ", StringComparison.Ordinal) && name != "Guest Profile")
                continue;

            var db = Path.Combine(profileDir, "Cookies");
            if (File.Exists(db))
                candidates.Add(new Candidate(label, db, IsFirefox: false, secretApps));
        }
    }

    private static void AddFirefox(List<Candidate> candidates, string label, string root)
    {
        if (!Directory.Exists(root)) return;

        foreach (var profileDir in Directory.EnumerateDirectories(root))
        {
            var db = Path.Combine(profileDir, "cookies.sqlite");
            if (File.Exists(db))
                candidates.Add(new Candidate(label, db, IsFirefox: true, SecretToolApps: []));
        }
    }

    private static IReadOnlyList<Cookie> ReadFirefoxCookies(string dbPath)
    {
        var temp = CopyToTempFile(dbPath);
        try
        {
            using var conn = new SqliteConnection($"Data Source={temp};Mode=ReadOnly;");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT host, name, value, path, isSecure FROM moz_cookies WHERE host LIKE '%youtube.com'";
            using var reader = cmd.ExecuteReader();

            var cookies = new List<Cookie>();
            while (reader.Read())
            {
                var host = reader.GetString(0);
                var name = reader.GetString(1);
                var value = reader.GetString(2);
                var path = reader.IsDBNull(3) ? "/" : reader.GetString(3);
                var secure = !reader.IsDBNull(4) && reader.GetInt64(4) != 0;

                cookies.Add(CookieFileParser.CreateCookie(name, value, host, path, secure));
            }

            return cookies;
        }
        finally
        {
            DeleteTempFile(temp);
        }
    }

    private static IReadOnlyList<Cookie> ReadChromeCookies(string dbPath, string[] secretToolApps)
    {
        var temp = CopyToTempFile(dbPath);
        try
        {
            using var conn = new SqliteConnection($"Data Source={temp};Mode=ReadOnly;");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT host_key, name, path, encrypted_value, is_secure FROM cookies WHERE host_key LIKE '%youtube.com'";
            using var reader = cmd.ExecuteReader();

            byte[]? key = null;
            var cookies = new List<Cookie>();
            while (reader.Read())
            {
                var host = reader.GetString(0);
                var name = reader.GetString(1);
                var path = reader.IsDBNull(2) ? "/" : reader.GetString(2);
                var encrypted = reader.GetFieldValue<byte[]>(3);
                var secure = !reader.IsDBNull(4) && reader.GetInt64(4) != 0;

                key ??= ChromeCookieCrypto.DeriveKey(secretToolApps);
                var value = ChromeCookieCrypto.Decrypt(encrypted, key);
                if (value is null || !LooksDecrypted(value)) continue;

                cookies.Add(CookieFileParser.CreateCookie(name, value, host, path, secure));
            }

            return cookies;
        }
        finally
        {
            DeleteTempFile(temp);
        }
    }

    private static string CopyToTempFile(string dbPath)
    {
        var temp = Path.Combine(Path.GetTempPath(), $"ytmusic-cookies-{Guid.NewGuid():N}.sqlite");
        File.Copy(dbPath, temp, overwrite: true);
        if (OperatingSystem.IsLinux())
            File.SetUnixFileMode(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        var wal = dbPath + "-wal";
        if (File.Exists(wal))
        {
            try
            {
                File.Copy(wal, temp + "-wal", overwrite: true);
                if (OperatingSystem.IsLinux())
                    File.SetUnixFileMode(temp + "-wal", UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch
            {
                // best effort: without the WAL, very recent cookie writes may be missing
            }
        }

        return temp;
    }

    private static void DeleteTempFile(string temp)
    {
        try { File.Delete(temp); } catch { /* best effort */ }
        try { File.Delete(temp + "-wal"); } catch { /* best effort */ }
        try { File.Delete(temp + "-shm"); } catch { /* best effort */ }
    }

    /// <summary>
    /// A wrong AES key (e.g. a stale profile whose keyring secret has since rotated) still
    /// "decrypts" without throwing — it just produces garbage bytes. Real cookie values are
    /// text, so reject anything containing control characters before it gets treated as valid.
    /// </summary>
    private static bool LooksDecrypted(string value)
    {
        foreach (var ch in value)
        {
            if (ch < 0x20 && ch != '\t') return false;
        }

        return true;
    }

    private static DateTime SafeLastWriteUtc(string path)
    {
        try { return File.GetLastWriteTimeUtc(path); }
        catch { return DateTime.MinValue; }
    }
}
