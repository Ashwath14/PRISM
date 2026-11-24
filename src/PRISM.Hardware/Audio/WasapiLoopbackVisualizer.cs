using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PRISM.Hardware.Audio;

public class WasapiLoopbackVisualizer : IDisposable
{
    private const int FFT_SIZE = 1024;
    private const int NUM_BANDS = 32;

    private readonly float[] _fftReal = new float[FFT_SIZE];
    private readonly float[] _fftImag = new float[FFT_SIZE];
    private readonly float[] _hannWindow = new float[FFT_SIZE];
    private readonly float[] _sampleBuffer = new float[FFT_SIZE];
    private int _sampleBufferIndex = 0;

    private readonly float[] _currentBands = new float[NUM_BANDS];
    private readonly float[] _targetBands = new float[NUM_BANDS];
    private readonly object _bandsLock = new();

    private CancellationTokenSource? _cts;
    private Task? _captureTask;
    private bool _disposed;
    private bool _simulateWhenIdle = false;
    private double _simPhase = 0.0;

    public int BandCount => NUM_BANDS;
    public bool IsRunning => _captureTask != null && !_captureTask.IsCompleted;

    public bool SimulateWhenIdle
    {
        get => _simulateWhenIdle;
        set => _simulateWhenIdle = value;
    }

    public event Action<float[]>? SpectrumUpdated;

    public WasapiLoopbackVisualizer()
    {
        // Pre-compute Hann window: w[n] = 0.5 * (1 - cos(2*pi*n / (N-1)))
        for (int i = 0; i < FFT_SIZE; i++)
        {
            _hannWindow[i] = 0.5f * (1.0f - (float)Math.Cos(2.0 * Math.PI * i / (FFT_SIZE - 1)));
        }
    }

    public void Start(string? targetDeviceId = null)
    {
        Stop();
        _cts = new CancellationTokenSource();
        _captureTask = Task.Run(() => CaptureLoop(_cts.Token, targetDeviceId));
    }

    public void Stop()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            try { _captureTask?.Wait(500); } catch { }
            _cts.Dispose();
            _cts = null;
        }
    }

    public float[] GetCurrentBands()
    {
        lock (_bandsLock)
        {
            return (float[])_currentBands.Clone();
        }
    }

    private void CaptureLoop(CancellationToken token, string? targetDeviceId)
    {
        while (!token.IsCancellationRequested)
        {
            IntPtr pFormat = IntPtr.Zero;
            CoreAudioNative.IAudioClient? audioClient = null;
            CoreAudioNative.IAudioCaptureClient? captureClient = null;

            try
            {
                var enumerator = (CoreAudioNative.IMMDeviceEnumerator)new CoreAudioNative.MMDeviceEnumeratorComObject();
                CoreAudioNative.IMMDevice? device = null;

                if (!string.IsNullOrEmpty(targetDeviceId))
                {
                    enumerator.GetDevice(targetDeviceId, out device);
                }

                if (device == null)
                {
                    // Default Console Render Endpoint
                    if (enumerator.GetDefaultAudioEndpoint(0, 0, out device) != 0 || device == null)
                    {
                        Task.Delay(1000, token).Wait(token);
                        continue;
                    }
                }

                var iidClient = CoreAudioNative.IID_IAudioClient;
                if (device.Activate(ref iidClient, CoreAudioNative.CLSCTX_ALL, IntPtr.Zero, out var clientObj) != 0 || clientObj == null)
                {
                    Task.Delay(1000, token).Wait(token);
                    continue;
                }

                audioClient = (CoreAudioNative.IAudioClient)clientObj;
                if (audioClient.GetMixFormat(out pFormat) != 0 || pFormat == IntPtr.Zero)
                {
                    Task.Delay(1000, token).Wait(token);
                    continue;
                }

                var waveFormat = Marshal.PtrToStructure<CoreAudioNative.WAVEFORMATEX>(pFormat);
                int channels = waveFormat.nChannels;
                int sampleRate = (int)waveFormat.nSamplesPerSec;
                int bitsPerSample = waveFormat.wBitsPerSample;
                bool isFloat = (waveFormat.wFormatTag == 3); // WAVE_FORMAT_IEEE_FLOAT

                if (waveFormat.wFormatTag == 0xFFFE) // WAVE_FORMAT_EXTENSIBLE
                {
                    var ext = Marshal.PtrToStructure<CoreAudioNative.WAVEFORMATEXTENSIBLE>(pFormat);
                    isFloat = ext.SubFormat == CoreAudioNative.KSDATAFORMAT_SUBTYPE_IEEE_FLOAT;
                }

                // 100ms buffer in 100ns units = 1,000,000
                long bufferDuration = 1000000;
                var sessionGuid = Guid.Empty;
                int hr = audioClient.Initialize(
                    CoreAudioNative.AUDCLNT_SHAREMODE_SHARED,
                    CoreAudioNative.AUDCLNT_STREAMFLAGS_LOOPBACK,
                    bufferDuration,
                    0,
                    pFormat,
                    ref sessionGuid);

                if (hr != 0)
                {
                    Task.Delay(1000, token).Wait(token);
                    continue;
                }

                var iidCapture = CoreAudioNative.IID_IAudioCaptureClient;
                if (audioClient.GetService(ref iidCapture, out var captureObj) != 0 || captureObj == null)
                {
                    Task.Delay(1000, token).Wait(token);
                    continue;
                }

                captureClient = (CoreAudioNative.IAudioCaptureClient)captureObj;
                audioClient.Start();

                var timer = new Stopwatch();
                timer.Start();

                bool hadRealAudio = false;

                while (!token.IsCancellationRequested)
                {
                    uint packetSize;
                    hr = captureClient.GetNextPacketSize(out packetSize);
                    if (hr != 0) break;

                    if (packetSize > 0)
                    {
                        IntPtr pData;
                        uint numFramesRead;
                        uint flags;
                        ulong devPos, qpcPos;

                        if (captureClient.GetBuffer(out pData, out numFramesRead, out flags, out devPos, out qpcPos) == 0)
                        {
                            bool isSilent = (flags & CoreAudioNative.AUDCLNT_BUFFERFLAGS_SILENT) != 0;

                            if (!isSilent && pData != IntPtr.Zero && numFramesRead > 0)
                            {
                                hadRealAudio = true;
                                ProcessFrames(pData, (int)numFramesRead, channels, bitsPerSample, isFloat, sampleRate);
                            }

                            captureClient.ReleaseBuffer(numFramesRead);
                        }
                    }
                    else
                    {
                        // No packets ready yet
                        Thread.Sleep(10);
                    }

                    // Refresh ballistics and notify at ~60Hz (every 16ms)
                    if (timer.ElapsedMilliseconds >= 16)
                    {
                        timer.Restart();
                        UpdateBallistics(!hadRealAudio);
                    }
                }

                try { audioClient.Stop(); } catch { }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WASAPI Loopback Exception: {ex.Message}");
                Thread.Sleep(500);
            }
            finally
            {
                if (pFormat != IntPtr.Zero) Marshal.FreeCoTaskMem(pFormat);
                captureClient = null;
                audioClient = null;
            }
        }
    }

    public void PushPcmSamples(float[] samples, int sampleRate = 44100)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            _sampleBuffer[_sampleBufferIndex++] = samples[i];
            if (_sampleBufferIndex >= FFT_SIZE)
            {
                _sampleBufferIndex = 0;
                ComputeFftAndBin(sampleRate);
            }
        }
        UpdateBallistics(false);
    }

    private void ProcessFrames(IntPtr pData, int frameCount, int channels, int bitsPerSample, bool isFloat, int sampleRate)
    {
        int bytesPerSample = bitsPerSample / 8;
        int frameStride = channels * bytesPerSample;

        for (int i = 0; i < frameCount; i++)
        {
            float monoSample = 0.0f;
            IntPtr framePtr = IntPtr.Add(pData, i * frameStride);

            if (isFloat && bytesPerSample == 4)
            {
                float left = Marshal.PtrToStructure<float>(framePtr);
                float right = (channels > 1) ? Marshal.PtrToStructure<float>(IntPtr.Add(framePtr, 4)) : left;
                monoSample = 0.5f * (left + right);
            }
            else if (bytesPerSample == 2) // 16-bit PCM
            {
                short left = Marshal.PtrToStructure<short>(framePtr);
                short right = (channels > 1) ? Marshal.PtrToStructure<short>(IntPtr.Add(framePtr, 2)) : left;
                monoSample = 0.5f * (left + right) / 32768.0f;
            }

            _sampleBuffer[_sampleBufferIndex++] = monoSample;

            if (_sampleBufferIndex >= FFT_SIZE)
            {
                _sampleBufferIndex = 0;
                ComputeFftAndBin(sampleRate);
            }
        }
    }

    private void ComputeFftAndBin(int sampleRate)
    {
        // 1. Apply Hann window and copy to real buffer
        for (int i = 0; i < FFT_SIZE; i++)
        {
            _fftReal[i] = _sampleBuffer[i] * _hannWindow[i];
            _fftImag[i] = 0.0f;
        }

        // 2. Perform in-place Radix-2 FFT
        Radix2Fft(_fftReal, _fftImag, FFT_SIZE);

        // 3. Bin into 32 Logarithmic Bands (32 Hz to 16,000 Hz)
        double minFreq = 32.0;
        double maxFreq = 16000.0;
        double binWidth = (double)sampleRate / FFT_SIZE;

        for (int b = 0; b < NUM_BANDS; b++)
        {
            double fLow = minFreq * Math.Pow(maxFreq / minFreq, (double)b / NUM_BANDS);
            double fHigh = minFreq * Math.Pow(maxFreq / minFreq, (double)(b + 1) / NUM_BANDS);

            int iStart = Math.Max(1, (int)(fLow / binWidth));
            int iEnd = Math.Min(FFT_SIZE / 2 - 1, (int)Math.Ceiling(fHigh / binWidth));
            if (iEnd < iStart) iEnd = iStart;

            float maxMag = 0.0f;
            for (int k = iStart; k <= iEnd; k++)
            {
                float mag = (float)Math.Sqrt(_fftReal[k] * _fftReal[k] + _fftImag[k] * _fftImag[k]);
                if (mag > maxMag) maxMag = mag;
            }

            // Convert to dB scale (-60 dB to 0 dBFS mapped to 0.0 to 1.0)
            float db = (maxMag > 1e-5f) ? (20.0f * (float)Math.Log10(maxMag * 4.0f)) : -60.0f;
            float norm = Math.Clamp((db + 55.0f) / 55.0f, 0.0f, 1.0f);

            // Shape with slight power curve for musical responsiveness
            norm = (float)Math.Pow(norm, 1.3);

            lock (_bandsLock)
            {
                if (norm > _targetBands[b])
                {
                    _targetBands[b] = norm; // Fast attack
                }
            }
        }
    }

    private void UpdateBallistics(bool isSilent)
    {
        lock (_bandsLock)
        {
            if (isSilent && _simulateWhenIdle)
            {
                // Organic ambient shimmer when idle
                _simPhase += 0.08;
                for (int b = 0; b < NUM_BANDS; b++)
                {
                    double wave = 0.15 * Math.Sin(_simPhase + b * 0.25) + 0.10 * Math.Cos(_simPhase * 0.7 - b * 0.18);
                    float target = Math.Clamp((float)Math.Abs(wave) + 0.05f, 0.02f, 0.35f);
                    _currentBands[b] = _currentBands[b] * 0.85f + target * 0.15f;
                }
            }
            else
            {
                for (int b = 0; b < NUM_BANDS; b++)
                {
                    if (_targetBands[b] > _currentBands[b])
                    {
                        // Instant fast attack
                        _currentBands[b] = _currentBands[b] * 0.3f + _targetBands[b] * 0.7f;
                    }
                    else
                    {
                        // Smooth analog exponential release / decay
                        _currentBands[b] *= 0.88f;
                        if (_currentBands[b] < 0.005f) _currentBands[b] = 0.0f;
                    }
                    _targetBands[b] *= 0.80f; // decay target peak
                }
            }
        }

        SpectrumUpdated?.Invoke(GetCurrentBands());
    }

    // Classic Radix-2 Cooley-Tukey In-Place Decimation-in-Time FFT
    private static void Radix2Fft(float[] real, float[] imag, int n)
    {
        // Bit reversal
        int j = 0;
        for (int i = 0; i < n - 1; i++)
        {
            if (i < j)
            {
                float tr = real[i]; real[i] = real[j]; real[j] = tr;
                float ti = imag[i]; imag[i] = imag[j]; imag[j] = ti;
            }
            int k = n >> 1;
            while (k <= j)
            {
                j -= k;
                k >>= 1;
            }
            j += k;
        }

        // Butterflies
        for (int len = 2; len <= n; len <<= 1)
        {
            double angle = -2.0 * Math.PI / len;
            float wstepR = (float)Math.Cos(angle);
            float wstepI = (float)Math.Sin(angle);

            for (int i = 0; i < n; i += len)
            {
                float wR = 1.0f;
                float wI = 0.0f;

                for (int k = 0; k < len / 2; k++)
                {
                    int u = i + k;
                    int v = i + k + len / 2;

                    float vR = real[v] * wR - imag[v] * wI;
                    float vI = real[v] * wI + imag[v] * wR;

                    real[v] = real[u] - vR;
                    imag[v] = imag[u] - vI;
                    real[u] += vR;
                    imag[u] += vI;

                    float nextWR = wR * wstepR - wI * wstepI;
                    wI = wR * wstepI + wI * wstepR;
                    wR = nextWR;
                }
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            Stop();
        }
    }
}
