using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace YtMusicTui.Auth;

/// <summary>
/// Decrypts Chrome-family (Chrome/Chromium/Brave/Edge) cookie values on Linux.
/// The AES key is derived from a password stored in the OS keyring via libsecret
/// (falling back to Chromium's well-known "peanuts" password when no keyring
/// entry exists, e.g. no desktop keyring is running).
/// </summary>
internal static class ChromeCookieCrypto
{
    private const string FallbackPassword = "peanuts";
    private static readonly byte[] Salt = Encoding.UTF8.GetBytes("saltysalt");
    private static readonly byte[] Iv = Encoding.ASCII.GetBytes(new string(' ', 16));

    public static byte[] DeriveKey(string[] secretToolApplicationNames)
    {
        var password = secretToolApplicationNames
            .Select(TryLibSecretLookup)
            .FirstOrDefault(pw => !string.IsNullOrEmpty(pw)) ?? FallbackPassword;

        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            Salt,
            iterations: 1,
            HashAlgorithmName.SHA1,
            outputLength: 16);
    }

    public static string? Decrypt(byte[] encryptedValue, byte[] key)
    {
        if (encryptedValue.Length == 0) return "";
        if (encryptedValue.Length <= 3) return null;

        var prefix = Encoding.ASCII.GetString(encryptedValue, 0, 3);
        if (prefix is not ("v10" or "v11"))
            return Encoding.UTF8.GetString(encryptedValue);

        try
        {
            var cipherText = encryptedValue[3..];
            var padded = AesCbcDecrypt(cipherText, key);
            var unpadded = RemovePkcs7Padding(padded);
            return Encoding.UTF8.GetString(unpadded);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] AesCbcDecrypt(byte[] cipherText, byte[] key)
    {
        using var aes = Aes.Create();
        aes.KeySize = 128;
        aes.Key = key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        return aes.DecryptCbc(cipherText, Iv, PaddingMode.None);
    }

    private static byte[] RemovePkcs7Padding(byte[] data)
    {
        if (data.Length == 0) return data;
        var pad = data[^1];
        if (pad is <= 0 or > 16 || pad > data.Length) return data;
        return data[..^pad];
    }

    private static string? TryLibSecretLookup(string applicationName)
    {
        try
        {
            var psi = new ProcessStartInfo("secret-tool")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            psi.ArgumentList.Add("lookup");
            psi.ArgumentList.Add("application");
            psi.ArgumentList.Add(applicationName);

            using var proc = Process.Start(psi);
            if (proc is null) return null;

            var output = proc.StandardOutput.ReadToEnd();
            if (!proc.WaitForExit(3000))
            {
                try { proc.Kill(); } catch { /* best effort */ }
                return null;
            }

            return proc.ExitCode == 0 && !string.IsNullOrEmpty(output) ? output.TrimEnd('\n') : null;
        }
        catch
        {
            return null;
        }
    }
}
