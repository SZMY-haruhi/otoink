using NAudio.Wave;
using Otoink.Core;

namespace Otoink.App.Audio;

public sealed class MicrophoneRecorder : IDisposable
{
    private const int SampleRateHz = 16000;

    private WaveInEvent? _waveIn;
    private readonly List<byte> _buffer = new();
    private readonly object _sync = new();
    private bool _disposed;

    public bool IsRecording { get; private set; }

    public event Action<float[], int>? Stopped;
    public event Action<float>? Level;

    public void Start(int deviceNumber)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsRecording)
            return;

        lock (_sync)
            _buffer.Clear();

        var waveIn = new WaveInEvent
        {
            DeviceNumber = deviceNumber,
            WaveFormat = new WaveFormat(SampleRateHz, 16, 1),
            BufferMilliseconds = 20
        };
        waveIn.DataAvailable += OnDataAvailable;
        waveIn.RecordingStopped += OnWaveInRecordingStopped;
        _waveIn = waveIn;
        try
        {
            waveIn.StartRecording();
            IsRecording = true;
        }
        catch
        {
            CleanupWaveIn();
            IsRecording = false;
            throw;
        }
    }

    public void Stop()
    {
        if (!IsRecording || _waveIn is null)
            return;

        _waveIn.StopRecording();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0)
            return;

        lock (_sync)
        {
            var needed = _buffer.Count + e.BytesRecorded;
            if (_buffer.Capacity < needed)
                _buffer.Capacity = needed;
            for (var i = 0; i < e.BytesRecorded; i++)
                _buffer.Add(e.Buffer[i]);
        }

        var peak = 0f;
        var sumSq = 0d;
        var count = 0;
        var limit = e.BytesRecorded - 1;
        for (var i = 0; i < limit; i += 2)
        {
            var sample = BitConverter.ToInt16(e.Buffer, i) / 32768f;
            var abs = Math.Abs(sample);
            if (abs > peak)
                peak = abs;
            sumSq += sample * sample;
            count++;
        }

        var rms = count == 0 ? 0f : (float)Math.Sqrt(sumSq / count);
        Level?.Invoke(AudioVu.FromPeakAndRms(peak, rms));
    }

    private void OnWaveInRecordingStopped(object? sender, StoppedEventArgs e)
    {
        byte[] pcm;
        lock (_sync)
        {
            pcm = _buffer.ToArray();
            _buffer.Clear();
        }

        CleanupWaveIn();
        IsRecording = false;

        var samples = Pcm16ToFloat32(pcm);
        Stopped?.Invoke(samples, SampleRateHz);
    }

    private static float[] Pcm16ToFloat32(byte[] pcm)
    {
        var count = pcm.Length / 2;
        var samples = new float[count];
        for (var i = 0; i < count; i++)
        {
            var sample = BitConverter.ToInt16(pcm, i * 2);
            samples[i] = sample / 32768f;
        }

        return samples;
    }

    private void CleanupWaveIn()
    {
        if (_waveIn is null)
            return;

        _waveIn.DataAvailable -= OnDataAvailable;
        _waveIn.RecordingStopped -= OnWaveInRecordingStopped;
        _waveIn.Dispose();
        _waveIn = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        // Unsubscribe before Dispose so RecordingStopped cannot race into a disposed WaveIn.
        if (_waveIn is not null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.RecordingStopped -= OnWaveInRecordingStopped;
            try
            {
                _waveIn.Dispose();
            }
            catch
            {
                // ignore teardown errors
            }

            _waveIn = null;
        }

        IsRecording = false;
    }
}
