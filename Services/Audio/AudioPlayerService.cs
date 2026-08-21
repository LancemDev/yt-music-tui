using System.Diagnostics;
using YtMusicTui.Models;
using YtMusicTui.Services.Abstractions;

namespace YtMusicTui.Services.Audio;

/// <summary>
/// Real playback: ffmpeg decodes the resolved stream URL to raw PCM, which is forwarded
/// to a system audio sink (see <see cref="AudioSink"/>) and simultaneously fed into a
/// <see cref="SpectrumAnalyzer"/> so the bar visualizer reflects the actual audio, not a
/// simulated animation. Position is derived from samples actually consumed, not wall-clock
/// time, so it stays accurate across pauses.
/// </summary>
public sealed class AudioPlayerService : IPlayerService, IDisposable
{
    private const int SampleRate = 44100;
    private const int Channels = 2;
    private const int BytesPerFrame = Channels * 2; // 16-bit samples

    private readonly IMusicService _music;
    private readonly SpectrumAnalyzer _analyzer = new();
    private readonly List<Track> _queue = [];
    private int _index = -1;

    private Process? _ffmpeg;
    private Process? _sink;
    private CancellationTokenSource? _pipelineCts;
    private Task? _pumpTask;
    private long _samplesWritten;
    private volatile bool _isPlaying;
    private volatile bool _trackEnded;

    public Track? Current { get; private set; }
    public bool IsPlaying => _isPlaying;
    public TimeSpan Position => TimeSpan.FromSeconds(_samplesWritten / (double)SampleRate);
    public TimeSpan Duration { get; private set; }
    public IReadOnlyList<Track> Queue => _queue;
    public IReadOnlyList<ulong> VisualizerLevels => _analyzer.Bars;
    public string? LastError { get; private set; }

    public AudioPlayerService(IMusicService music) => _music = music;

    public Task PlayAsync(Track track, CancellationToken ct = default)
    {
        _queue.Clear();
        _queue.Add(track);
        _index = 0;
        return StartCurrentAsync(ct);
    }

    public Task PlayQueueAsync(IReadOnlyList<Track> tracks, int startIndex = 0, CancellationToken ct = default)
    {
        _queue.Clear();
        _queue.AddRange(tracks);
        _index = Math.Clamp(startIndex, 0, Math.Max(0, _queue.Count - 1));

        if (_queue.Count == 0)
        {
            StopPipeline();
            Current = null;
            Duration = TimeSpan.Zero;
            return Task.CompletedTask;
        }

        return StartCurrentAsync(ct);
    }

    public Task TogglePauseAsync(CancellationToken ct = default)
    {
        if (Current is null) return Task.CompletedTask;
        _isPlaying = !_isPlaying;
        return Task.CompletedTask;
    }

    public Task NextAsync(CancellationToken ct = default)
    {
        if (_queue.Count == 0) return Task.CompletedTask;
        _index = (_index + 1) % _queue.Count;
        return StartCurrentAsync(ct);
    }

    public Task PreviousAsync(CancellationToken ct = default)
    {
        if (_queue.Count == 0) return Task.CompletedTask;
        _index = (_index - 1 + _queue.Count) % _queue.Count;
        return StartCurrentAsync(ct);
    }

    public void Tick(TimeSpan delta)
    {
        if (!_trackEnded) return;
        _trackEnded = false;
        _ = NextAsync();
    }

    private async Task StartCurrentAsync(CancellationToken ct)
    {
        StopPipeline();

        Current = _queue[_index];
        Duration = Current.Duration ?? TimeSpan.Zero;
        _samplesWritten = 0;
        LastError = null;

        string url;
        try
        {
            url = await _music.GetStreamUrlAsync(Current.Id, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LastError = $"Couldn't resolve stream for {Current.Title}: {ex.Message}";
            _isPlaying = false;
            return;
        }

        if (!TryStartFfmpeg(url, out var ffmpeg))
        {
            LastError = "ffmpeg not found — install it to enable playback.";
            _isPlaying = false;
            return;
        }

        var sink = AudioSink.Start(SampleRate, Channels);
        if (sink is null)
        {
            LastError = "No audio output found (tried pw-play, paplay, aplay).";
            KillQuietly(ffmpeg);
            _isPlaying = false;
            return;
        }

        _ffmpeg = ffmpeg;
        _sink = sink;
        _pipelineCts = new CancellationTokenSource();
        _isPlaying = true;

        _pumpTask = Task.Run(() => PumpAsync(ffmpeg, sink, _pipelineCts.Token));
    }

    private static bool TryStartFfmpeg(string url, out Process ffmpeg)
    {
        try
        {
            var psi = new ProcessStartInfo("ffmpeg")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("-hide_banner");
            psi.ArgumentList.Add("-loglevel");
            psi.ArgumentList.Add("error");
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(url);
            psi.ArgumentList.Add("-vn");
            psi.ArgumentList.Add("-ac");
            psi.ArgumentList.Add(Channels.ToString());
            psi.ArgumentList.Add("-ar");
            psi.ArgumentList.Add(SampleRate.ToString());
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("s16le");
            psi.ArgumentList.Add("pipe:1");

            var proc = Process.Start(psi);
            if (proc is null)
            {
                ffmpeg = null!;
                return false;
            }

            ffmpeg = proc;
            return true;
        }
        catch
        {
            ffmpeg = null!;
            return false;
        }
    }

    private async Task PumpAsync(Process ffmpeg, Process sink, CancellationToken ct)
    {
        var buffer = new byte[16384];
        try
        {
            var stdout = ffmpeg.StandardOutput.BaseStream;
            var stdin = sink.StandardInput.BaseStream;

            while (!ct.IsCancellationRequested)
            {
                while (!_isPlaying && !ct.IsCancellationRequested)
                    await Task.Delay(50, ct).ConfigureAwait(false);

                if (ct.IsCancellationRequested) break;

                var read = await stdout.ReadAsync(buffer, ct).ConfigureAwait(false);
                if (read <= 0)
                {
                    _trackEnded = true;
                    break;
                }

                await stdin.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                _analyzer.Feed(buffer.AsSpan(0, read));
                _samplesWritten += read / BytesPerFrame;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on stop/seek/next — the pipeline is being torn down deliberately.
        }
        catch
        {
            _trackEnded = true;
        }
    }

    private void StopPipeline()
    {
        _pipelineCts?.Cancel();
        try { _pumpTask?.Wait(TimeSpan.FromMilliseconds(500)); } catch { /* best effort */ }

        KillQuietly(_ffmpeg);
        KillQuietly(_sink);
        _ffmpeg = null;
        _sink = null;
        _pipelineCts?.Dispose();
        _pipelineCts = null;
        _pumpTask = null;
    }

    private static void KillQuietly(Process? process)
    {
        if (process is null) return;
        try { if (!process.HasExited) process.Kill(true); } catch { /* best effort */ }
        process.Dispose();
    }

    public void Dispose() => StopPipeline();
}
