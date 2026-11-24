using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using PRISM.Core.Models;

namespace PRISM.Hardware.Audio;

public enum AuditionTrack
{
    SynthGroove,
    AcousticMelody,
    SpeechVoice,
    PinkNoise
}

public class PrismAudioEngine : IDisposable
{
    private const int SAMPLE_RATE = 44100;
    private const int CHANNELS = 2;
    private const int BUFFER_SAMPLES = 2048;
    private const int NUM_BUFFERS = 4;

    private static readonly double[] EqFrequencies = { 31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 };

    [StructLayout(LayoutKind.Sequential)]
    private struct WAVEFORMATEX
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WAVEHDR
    {
        public IntPtr lpData;
        public uint dwBufferLength;
        public uint dwBytesRecorded;
        public IntPtr dwUser;
        public uint dwFlags;
        public uint dwLoops;
        public IntPtr lpNext;
        public IntPtr reserved;
    }

    private const int WHDR_DONE = 0x00000001;
    private const int WAVE_FORMAT_PCM = 1;
    private const int MMSYSERR_NOERROR = 0;

    [DllImport("winmm.dll")]
    private static extern int waveOutOpen(out IntPtr hWaveOut, uint uDeviceID, ref WAVEFORMATEX lpFormat, IntPtr dwCallback, IntPtr dwInstance, uint dwFlags);

    [DllImport("winmm.dll")]
    private static extern int waveOutPrepareHeader(IntPtr hWaveOut, ref WAVEHDR lpWaveHdr, uint uSize);

    [DllImport("winmm.dll")]
    private static extern int waveOutWrite(IntPtr hWaveOut, ref WAVEHDR lpWaveHdr, uint uSize);

    [DllImport("winmm.dll")]
    private static extern int waveOutUnprepareHeader(IntPtr hWaveOut, ref WAVEHDR lpWaveHdr, uint uSize);

    [DllImport("winmm.dll")]
    private static extern int waveOutReset(IntPtr hWaveOut);

    [DllImport("winmm.dll")]
    private static extern int waveOutClose(IntPtr hWaveOut);

    private readonly object _dspLock = new();
    private IntPtr _hWaveOut = IntPtr.Zero;
    private Thread? _playbackThread;
    private volatile bool _isPlaying = false;
    private volatile bool _isDisposed = false;

    // DSP Filter Stages
    private readonly BiquadFilter[] _eqFilters = new BiquadFilter[10];
    private readonly BiquadFilter _bassShelf = new();
    private readonly BiquadFilter _trebleShelf = new();

    private double _preampLinear = 1.0;
    private double _targetPreampLinear = 1.0;
    private double _stereoWidthFactor = 1.0;
    private float _balanceLeft = 1.0f;
    private float _balanceRight = 1.0f;
    private bool _isBypassed = false;
    private AuditionTrack _currentTrack = AuditionTrack.SynthGroove;

    // Track synthesis state
    private double _phase1 = 0;
    private double _phase2 = 0;
    private double _phase3 = 0;
    private double _sampleCounter = 0;
    private readonly Random _rand = new(42);

    public bool IsPlaying => _isPlaying;
    public bool IsBypassed => _isBypassed;
    public AuditionTrack CurrentTrack
    {
        get => _currentTrack;
        set => _currentTrack = value;
    }

    public event Action<float[]>? AudioPcmRendered;

    public PrismAudioEngine()
    {
        for (int i = 0; i < _eqFilters.Length; i++)
        {
            _eqFilters[i] = new BiquadFilter();
            _eqFilters[i].ConfigurePeaking(EqFrequencies[i], 0.0, 1.414, SAMPLE_RATE);
        }
        _bassShelf.ConfigureShelf(100.0, 0.0, true, SAMPLE_RATE);
        _trebleShelf.ConfigureShelf(10000.0, 0.0, false, SAMPLE_RATE);
    }

    public void UpdateDspParameters(AudioProfile profile, float leftBalance, float rightBalance)
    {
        lock (_dspLock)
        {
            _isBypassed = profile.IsBypassed;
            _balanceLeft = leftBalance;
            _balanceRight = rightBalance;
            _stereoWidthFactor = profile.IsStereoWideningEnabled ? (profile.StereoWidthPercent / 100.0) : 1.0;

            // Preamp and auto-preamp headroom
            double effectivePreamp = profile.PreampDb;
            if (profile.IsLoudnessNormalizationEnabled)
            {
                double maxPositive = 0;
                if (profile.BandGains != null)
                {
                    foreach (var g in profile.BandGains) if (g > maxPositive) maxPositive = g;
                }
                if (profile.BassGainDb > maxPositive) maxPositive = profile.BassGainDb;
                if (profile.TrebleGainDb > maxPositive) maxPositive = profile.TrebleGainDb;
                if (maxPositive > 0) effectivePreamp -= maxPositive;
            }
            _targetPreampLinear = Math.Pow(10.0, effectivePreamp / 20.0);

            // Configure 10-band peaking biquads
            for (int i = 0; i < 10; i++)
            {
                double gain = (profile.BandGains != null && i < profile.BandGains.Length) ? profile.BandGains[i] : 0.0;
                _eqFilters[i].ConfigurePeaking(EqFrequencies[i], gain, 1.414, SAMPLE_RATE);
            }

            // Configure Bass and Treble Shelves
            _bassShelf.ConfigureShelf(100.0, profile.BassGainDb, true, SAMPLE_RATE);
            _trebleShelf.ConfigureShelf(10000.0, profile.TrebleGainDb, false, SAMPLE_RATE);
        }
    }

    public void SetBypass(bool isBypassed)
    {
        lock (_dspLock)
        {
            _isBypassed = isBypassed;
        }
    }

    public void StartAudition(AuditionTrack track = AuditionTrack.SynthGroove)
    {
        if (_isPlaying)
        {
            _currentTrack = track;
            return;
        }

        _currentTrack = track;
        _isPlaying = true;

        _playbackThread = new Thread(PlaybackLoop)
        {
            IsBackground = true,
            Name = "PRISM_Audio_Audition_Engine",
            Priority = ThreadPriority.AboveNormal
        };
        _playbackThread.Start();
    }

    public void StopAudition()
    {
        _isPlaying = false;
        if (_playbackThread != null && _playbackThread.IsAlive)
        {
            _playbackThread.Join(500);
        }
        _playbackThread = null;
    }

    private void PlaybackLoop()
    {
        WAVEFORMATEX wfx = new WAVEFORMATEX
        {
            wFormatTag = WAVE_FORMAT_PCM,
            nChannels = CHANNELS,
            nSamplesPerSec = SAMPLE_RATE,
            wBitsPerSample = 16,
            nBlockAlign = (ushort)(CHANNELS * (16 / 8)),
            cbSize = 0
        };
        wfx.nAvgBytesPerSec = wfx.nSamplesPerSec * wfx.nBlockAlign;

        int res = waveOutOpen(out _hWaveOut, 0, ref wfx, IntPtr.Zero, IntPtr.Zero, 0);
        if (res != MMSYSERR_NOERROR || _hWaveOut == IntPtr.Zero)
        {
            _isPlaying = false;
            return;
        }

        short[][] buffers = new short[NUM_BUFFERS][];
        GCHandle[] bufferHandles = new GCHandle[NUM_BUFFERS];
        WAVEHDR[] headers = new WAVEHDR[NUM_BUFFERS];
        GCHandle[] headerHandles = new GCHandle[NUM_BUFFERS];

        int bufferSize = BUFFER_SAMPLES * CHANNELS;

        for (int i = 0; i < NUM_BUFFERS; i++)
        {
            buffers[i] = new short[bufferSize];
            bufferHandles[i] = GCHandle.Alloc(buffers[i], GCHandleType.Pinned);
            headers[i] = new WAVEHDR
            {
                lpData = bufferHandles[i].AddrOfPinnedObject(),
                dwBufferLength = (uint)(bufferSize * sizeof(short)),
                dwFlags = 0
            };
            headerHandles[i] = GCHandle.Alloc(headers[i], GCHandleType.Pinned);
            waveOutPrepareHeader(_hWaveOut, ref headers[i], (uint)Marshal.SizeOf<WAVEHDR>());
        }

        float[] visualizerFloatPcm = new float[BUFFER_SAMPLES];
        int bufIdx = 0;

        try
        {
            while (_isPlaying && !_isDisposed)
            {
                ref WAVEHDR hdr = ref headers[bufIdx];
                if ((hdr.dwFlags & WHDR_DONE) != 0 || hdr.dwFlags == 0)
                {
                    GenerateAudioFrame(buffers[bufIdx], visualizerFloatPcm);

                    waveOutWrite(_hWaveOut, ref hdr, (uint)Marshal.SizeOf<WAVEHDR>());
                    AudioPcmRendered?.Invoke(visualizerFloatPcm);

                    bufIdx = (bufIdx + 1) % NUM_BUFFERS;
                }
                else
                {
                    Thread.Sleep(5);
                }
            }
        }
        catch { }
        finally
        {
            waveOutReset(_hWaveOut);
            for (int i = 0; i < NUM_BUFFERS; i++)
            {
                try
                {
                    waveOutUnprepareHeader(_hWaveOut, ref headers[i], (uint)Marshal.SizeOf<WAVEHDR>());
                    if (bufferHandles[i].IsAllocated) bufferHandles[i].Free();
                    if (headerHandles[i].IsAllocated) headerHandles[i].Free();
                }
                catch { }
            }
            waveOutClose(_hWaveOut);
            _hWaveOut = IntPtr.Zero;
        }
    }

    private void GenerateAudioFrame(short[] pcmBuffer, float[] visualizerPcm)
    {
        lock (_dspLock)
        {
            for (int i = 0; i < BUFFER_SAMPLES; i++)
            {
                _sampleCounter++;

                // 1. Synthesize Audio Sample
                (double rawL, double rawR) = SynthesizeTrackSample(_currentTrack);

                // Smooth preamp interpolation to prevent volume zippering
                _preampLinear += (_targetPreampLinear - _preampLinear) * 0.005;

                double outL, outR;

                if (_isBypassed)
                {
                    // A/B Bypass: Pure bit-exact raw sound (0 dB neutral reference)
                    outL = rawL * 0.707 * _balanceLeft;
                    outR = rawR * 0.707 * _balanceRight;
                }
                else
                {
                    // DSP Processed Path
                    double l = rawL * _preampLinear;
                    double r = rawR * _preampLinear;

                    // Cascaded 10-Band EQ
                    for (int b = 0; b < 10; b++)
                    {
                        (l, r) = _eqFilters[b].Process(l, r);
                    }

                    // Low and High Shelves
                    (l, r) = _bassShelf.Process(l, r);
                    (l, r) = _trebleShelf.Process(l, r);

                    // Stereo Widening (Mid/Side Matrix)
                    if (Math.Abs(_stereoWidthFactor - 1.0) > 0.01)
                    {
                        double mid = (l + r) * 0.5;
                        double side = (l - r) * 0.5 * _stereoWidthFactor;
                        l = mid + side;
                        r = mid - side;
                    }

                    // Balance
                    l *= _balanceLeft;
                    r *= _balanceRight;

                    // Peak Soft-Knee Limiter (tanh saturation safeguard)
                    outL = SoftKneeLimit(l);
                    outR = SoftKneeLimit(r);
                }

                // Convert to 16-bit PCM integer
                short sL = (short)Math.Clamp((int)(outL * 32767.0), -32768, 32767);
                short sR = (short)Math.Clamp((int)(outR * 32767.0), -32768, 32767);

                pcmBuffer[i * 2] = sL;
                pcmBuffer[i * 2 + 1] = sR;

                // Mono downmix for visualizer FFT
                visualizerPcm[i] = (float)((outL + outR) * 0.5);
            }
        }
    }

    private (double, double) SynthesizeTrackSample(AuditionTrack track)
    {
        switch (track)
        {
            case AuditionTrack.SynthGroove:
            {
                // Dynamic Bass + Kick + Lead Chord + Hi-Hat
                // Kick drum (50 Hz sweep) every 0.5s
                double beatSec = (_sampleCounter % 22050) / 22050.0;
                double kick = Math.Sin(2.0 * Math.PI * (120.0 * (1.0 - beatSec) + 45.0) * beatSec) * Math.Exp(-beatSec * 10.0);

                // Sub-Bass groove (55 Hz A1 / 65 Hz C2 alternating)
                double bassFreq = ((int)(_sampleCounter / 22050) % 2 == 0) ? 55.0 : 65.4;
                _phase1 += 2.0 * Math.PI * bassFreq / SAMPLE_RATE;
                double bass = Math.Sin(_phase1) * 0.5;

                // Mid Synth Lead (440 Hz / 523 Hz / 659 Hz chords)
                _phase2 += 2.0 * Math.PI * 440.0 / SAMPLE_RATE;
                _phase3 += 2.0 * Math.PI * 659.2 / SAMPLE_RATE;
                double lead = (Math.Sin(_phase2) + Math.Sin(_phase3)) * 0.18;

                // Hi-Hat noise burst on 16th notes
                double hatBeat = (_sampleCounter % 5512) / 5512.0;
                double hat = (_rand.NextDouble() * 2.0 - 1.0) * Math.Exp(-hatBeat * 25.0) * 0.12;

                double l = (kick * 0.7 + bass * 0.6 + lead * 0.5 + hat * 0.4);
                double r = (kick * 0.7 + bass * 0.6 + lead * 0.4 + hat * 0.6);
                return (l, r);
            }

            case AuditionTrack.AcousticMelody:
            {
                // Acoustic guitar / piano harmonics
                _phase1 += 2.0 * Math.PI * 196.0 / SAMPLE_RATE; // G3
                _phase2 += 2.0 * Math.PI * 293.6 / SAMPLE_RATE; // D4
                _phase3 += 2.0 * Math.PI * 392.0 / SAMPLE_RATE; // G4

                double note = Math.Sin(_phase1) * 0.35 +
                              Math.Sin(_phase2) * 0.25 +
                              Math.Sin(_phase3) * 0.20 +
                              Math.Sin(_phase1 * 3.0) * 0.10 +
                              Math.Sin(_phase2 * 2.0) * 0.08;

                // Subtle tape warmth
                double air = (_rand.NextDouble() * 2.0 - 1.0) * 0.01;
                return (note * 0.55 + air, note * 0.55 - air);
            }

            case AuditionTrack.SpeechVoice:
            {
                // Voice / Dialogue Formant Synthesis (130 Hz fundamental with 750 Hz, 1200 Hz, 2400 Hz formants)
                _phase1 += 2.0 * Math.PI * 130.0 / SAMPLE_RATE;
                double f0 = Math.Sin(_phase1);

                _phase2 += 2.0 * Math.PI * 750.0 / SAMPLE_RATE;
                _phase3 += 2.0 * Math.PI * 2400.0 / SAMPLE_RATE;
                double formants = Math.Sin(_phase2) * 0.35 + Math.Sin(_phase3) * 0.25;

                // Speech cadence modulation (sentence pacing)
                double cadence = 0.5 + 0.5 * Math.Sin(2.0 * Math.PI * 3.0 * _sampleCounter / SAMPLE_RATE);
                double voice = (f0 * 0.3 + formants * 0.45) * cadence;
                return (voice * 0.6, voice * 0.6);
            }

            case AuditionTrack.PinkNoise:
            default:
            {
                // Full-band pink noise (equal energy per octave 20Hz-20kHz)
                double white = _rand.NextDouble() * 2.0 - 1.0;
                double pink = (white + (_rand.NextDouble() * 2.0 - 1.0) * 0.5) * 0.25;
                return (pink, pink);
            }
        }
    }

    private static double SoftKneeLimit(double x)
    {
        if (x > 0.85)
        {
            return 0.85 + 0.15 * Math.Tanh((x - 0.85) / 0.15);
        }
        if (x < -0.85)
        {
            return -0.85 + 0.15 * Math.Tanh((x + 0.85) / 0.15);
        }
        return x;
    }

    public void Dispose()
    {
        _isDisposed = true;
        StopAudition();
    }

    private class BiquadFilter
    {
        public double B0 = 1, B1 = 0, B2 = 0, A1 = 0, A2 = 0;
        private double _z1L, _z2L, _z1R, _z2R;

        public void ConfigurePeaking(double freq, double gainDb, double q, double sampleRate)
        {
            if (Math.Abs(gainDb) < 0.05)
            {
                B0 = 1; B1 = 0; B2 = 0; A1 = 0; A2 = 0;
                return;
            }
            double w0 = 2.0 * Math.PI * freq / sampleRate;
            double alpha = Math.Sin(w0) / (2.0 * q);
            double A = Math.Pow(10.0, gainDb / 40.0);
            double cosW0 = Math.Cos(w0);

            double a0 = 1.0 + alpha / A;
            B0 = (1.0 + alpha * A) / a0;
            B1 = (-2.0 * cosW0) / a0;
            B2 = (1.0 - alpha * A) / a0;
            A1 = (-2.0 * cosW0) / a0;
            A2 = (1.0 - alpha / A) / a0;
        }

        public void ConfigureShelf(double freq, double gainDb, bool isLowShelf, double sampleRate)
        {
            if (Math.Abs(gainDb) < 0.05)
            {
                B0 = 1; B1 = 0; B2 = 0; A1 = 0; A2 = 0;
                return;
            }
            double w0 = 2.0 * Math.PI * freq / sampleRate;
            double A = Math.Pow(10.0, gainDb / 40.0);
            double cosW0 = Math.Cos(w0);
            double sinW0 = Math.Sin(w0);
            double alpha = sinW0 / (2.0 * 0.707);

            if (isLowShelf)
            {
                double a0 = (A + 1.0) + (A - 1.0) * cosW0 + 2.0 * Math.Sqrt(A) * alpha;
                B0 = (A * ((A + 1.0) - (A - 1.0) * cosW0 + 2.0 * Math.Sqrt(A) * alpha)) / a0;
                B1 = (2.0 * A * ((A - 1.0) - (A + 1.0) * cosW0)) / a0;
                B2 = (A * ((A + 1.0) - (A - 1.0) * cosW0 - 2.0 * Math.Sqrt(A) * alpha)) / a0;
                A1 = (-2.0 * ((A - 1.0) + (A + 1.0) * cosW0)) / a0;
                A2 = ((A + 1.0) + (A - 1.0) * cosW0 - 2.0 * Math.Sqrt(A) * alpha) / a0;
            }
            else
            {
                double a0 = (A + 1.0) - (A - 1.0) * cosW0 + 2.0 * Math.Sqrt(A) * alpha;
                B0 = (A * ((A + 1.0) + (A - 1.0) * cosW0 + 2.0 * Math.Sqrt(A) * alpha)) / a0;
                B1 = (-2.0 * A * ((A - 1.0) + (A + 1.0) * cosW0)) / a0;
                B2 = (A * ((A + 1.0) + (A - 1.0) * cosW0 - 2.0 * Math.Sqrt(A) * alpha)) / a0;
                A1 = (2.0 * ((A - 1.0) - (A + 1.0) * cosW0)) / a0;
                A2 = ((A + 1.0) - (A - 1.0) * cosW0 - 2.0 * Math.Sqrt(A) * alpha) / a0;
            }
        }

        public (double left, double right) Process(double inL, double inR)
        {
            double outL = B0 * inL + _z1L;
            _z1L = B1 * inL - A1 * outL + _z2L;
            _z2L = B2 * inL - A2 * outL;

            double outR = B0 * inR + _z1R;
            _z1R = B1 * inR - A1 * outR + _z2R;
            _z2R = B2 * inR - A2 * outR;

            return (outL, outR);
        }
    }
}
