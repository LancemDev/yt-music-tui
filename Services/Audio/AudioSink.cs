using System.Diagnostics;

namespace YtMusicTui.Services.Audio;

/// <summary>
/// Launches a system audio-output process that accepts raw 16-bit PCM on stdin.
/// Tries PipeWire, then PulseAudio, then plain ALSA — same auto-detect fallback
/// pattern used for opening a browser in BrowserAuthFlow.
/// </summary>
internal static class AudioSink
{
    public static Process? Start(int sampleRate, int channels)
        => TryStart("pw-play",
               ["--container", "raw", "--format", "s16", "--rate", sampleRate.ToString(), "--channels", channels.ToString(), "-"])
           ?? TryStart("paplay",
               ["--raw", $"--rate={sampleRate}", $"--channels={channels}", "--format=s16le"])
           ?? TryStart("aplay",
               ["-q", "-f", "S16_LE", "-r", sampleRate.ToString(), "-c", channels.ToString()]);

    private static Process? TryStart(string command, string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(command)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var arg in args) psi.ArgumentList.Add(arg);

            return Process.Start(psi);
        }
        catch
        {
            return null;
        }
    }
}
