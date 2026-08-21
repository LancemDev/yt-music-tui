namespace YtMusicTui.Services.Audio;

/// <summary>
/// Turns raw 16-bit PCM audio into a small set of smoothed magnitude bars for the
/// TUI's bar visualizer. Self-contained radix-2 FFT — no external dependency.
/// </summary>
internal sealed class SpectrumAnalyzer
{
    private const int WindowSize = 1024; // must be a power of two
    private const int BarCount = 24;
    private const double Decay = 0.75;

    private readonly float[] _window = new float[WindowSize];
    private int _windowFill;
    private readonly double[] _bars = new double[BarCount];

    public IReadOnlyList<ulong> Bars { get; private set; } = new ulong[BarCount];

    /// <param name="pcm">Interleaved 16-bit signed little-endian stereo samples.</param>
    public void Feed(ReadOnlySpan<byte> pcm)
    {
        var frameCount = pcm.Length / 4; // 2 channels * 2 bytes/sample
        for (var i = 0; i < frameCount; i++)
        {
            var left = BitConverter.ToInt16(pcm.Slice(i * 4, 2));
            var right = BitConverter.ToInt16(pcm.Slice(i * 4 + 2, 2));
            var mono = (left + right) / 2f / short.MaxValue;

            _window[_windowFill++] = mono;
            if (_windowFill < WindowSize) continue;

            Analyze();
            _windowFill = 0;
        }
    }

    private void Analyze()
    {
        var real = new double[WindowSize];
        var imag = new double[WindowSize];
        for (var i = 0; i < WindowSize; i++)
        {
            // Hann window to reduce spectral leakage at the edges of the sample block.
            var w = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (WindowSize - 1)));
            real[i] = _window[i] * w;
        }

        Fft(real, imag);

        // Only the first half of bins carries information for real-valued input.
        var usableBins = WindowSize / 2;
        var binsPerBar = Math.Max(1, usableBins / BarCount);

        var next = new ulong[BarCount];
        for (var bar = 0; bar < BarCount; bar++)
        {
            var start = bar * binsPerBar;
            var end = Math.Min(usableBins, start + binsPerBar);

            var magnitude = 0.0;
            for (var bin = start; bin < end; bin++)
                magnitude = Math.Max(magnitude, Math.Sqrt(real[bin] * real[bin] + imag[bin] * imag[bin]));

            // Decay smoothing so bars fall gracefully instead of snapping to zero between windows.
            _bars[bar] = Math.Max(magnitude, _bars[bar] * Decay);
            next[bar] = (ulong)Math.Clamp(_bars[bar] * 4000, 0, 100);
        }

        Bars = next;
    }

    private static void Fft(double[] real, double[] imag)
    {
        var n = real.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            var bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1)
                j ^= bit;
            j ^= bit;

            if (i < j)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imag[i], imag[j]) = (imag[j], imag[i]);
            }
        }

        for (var len = 2; len <= n; len <<= 1)
        {
            var angle = -2 * Math.PI / len;
            var wr = Math.Cos(angle);
            var wi = Math.Sin(angle);

            for (var start = 0; start < n; start += len)
            {
                double curWr = 1, curWi = 0;
                for (var k = 0; k < len / 2; k++)
                {
                    var evenIdx = start + k;
                    var oddIdx = start + k + len / 2;

                    var tr = real[oddIdx] * curWr - imag[oddIdx] * curWi;
                    var ti = real[oddIdx] * curWi + imag[oddIdx] * curWr;

                    real[oddIdx] = real[evenIdx] - tr;
                    imag[oddIdx] = imag[evenIdx] - ti;
                    real[evenIdx] += tr;
                    imag[evenIdx] += ti;

                    var nextWr = curWr * wr - curWi * wi;
                    var nextWi = curWr * wi + curWi * wr;
                    curWr = nextWr;
                    curWi = nextWi;
                }
            }
        }
    }
}
