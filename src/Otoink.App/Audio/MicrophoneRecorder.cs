using NAudio.Wave;

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
            BufferMilliseconds = 50
        };
        waveIn.DataAvailable += OnDataAvailable;
        waveIn.RecordingStopped += OnWaveInRecordingStopped;
        _waveIn = waveIn;
        waveIn.StartRecording();
        IsRecording = true;
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

        if (_waveIn is not null)
        {
            try
            {
                if (IsRecording)
                    _waveIn.StopRecording();
            }
            catch
            {
                // ignore teardown errors
            }

            CleanupWaveIn();
        }

        IsRecording = false;
    }
}
