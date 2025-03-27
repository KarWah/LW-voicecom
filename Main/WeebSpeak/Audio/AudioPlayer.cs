using NAudio.Wave;
using System;
using System.Diagnostics;
using WeebSpeak;

namespace WeebSpeak.Audio
{
    public class AudioPlayer : IDisposable
    {
        private readonly IWavePlayer _waveOut;
        private readonly BufferedWaveProvider _waveProvider;
        private bool _disposed = false;
        private const int SampleRate = 48000;
        private const int BitsPerSample = 16;
        private const int Channels = 1;

        public AudioPlayer()
        {
            try
            {
                // Configure for low-latency voice playback
                _waveOut = new WaveOutEvent()
                {
                    DesiredLatency = 100,  // 100ms latency
                    NumberOfBuffers = 3     // Triple buffering
                };

                _waveProvider = new BufferedWaveProvider(new WaveFormat(SampleRate, BitsPerSample, Channels))
                {
                    BufferDuration = TimeSpan.FromMilliseconds(500),  // 500ms buffer
                    DiscardOnBufferOverflow = true
                };

                _waveOut.Init(_waveProvider);
                _waveOut.Play();  // Start playback immediately
                
                Debug.WriteLine($"AudioPlayer initialized: {SampleRate}Hz, {BitsPerSample}-bit, {Channels} channel");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AudioPlayer initialization failed: {ex.Message}");
                throw;
            }
        }

        // In AudioPlayer class
       public void Play(byte[] pcmData)
{
    if (_disposed || pcmData == null || pcmData.Length == 0)
    {
        Debug.WriteLine("Play called with invalid parameters");
        return;
    }

    try
    {
        // Diagnostic logging
        Debug.WriteLine($"Attempting to play {pcmData.Length} bytes PCM");
        
        // Verify PCM data format (optional diagnostic)
        if (pcmData.Length % 2 != 0)
        {
            Debug.WriteLine("Invalid PCM data - odd number of bytes");
            return;
        }

        // Check buffer state
        int bufferedBytes = _waveProvider.BufferedBytes;
        Debug.WriteLine($"Buffer state: {bufferedBytes}/{_waveProvider.BufferLength} bytes");

        // More aggressive buffer management
        if (bufferedBytes > _waveProvider.BufferLength / 3)
        {
            Debug.WriteLine("Clearing buffer to prevent lag");
            _waveProvider.ClearBuffer();
        }

        // Add samples with validation
        if (pcmData.Length <= _waveProvider.BufferLength - bufferedBytes)
        {
            _waveProvider.AddSamples(pcmData, 0, pcmData.Length);
            Debug.WriteLine($"Successfully added {pcmData.Length} bytes to buffer");
        }
        else
        {
            Debug.WriteLine("Buffer overflow - packet dropped");
        }
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"Playback error: {ex.Message}");
        
        // Temporary diagnostic - save problematic audio to file
        try 
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "audio_debug.pcm");
            File.WriteAllBytes(tempPath, pcmData);
            Debug.WriteLine($"Saved problematic audio to {tempPath}");
        }
        catch { }
    }
}

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _waveOut?.Stop();
                _waveOut?.Dispose();
                Debug.WriteLine("AudioPlayer disposed");
            }
        }
    }
}