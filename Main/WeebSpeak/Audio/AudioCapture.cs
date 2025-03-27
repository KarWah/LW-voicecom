using NAudio.Wave;
using System;

namespace WeebSpeak.Audio
{
    public class AudioCapture : IDisposable
    {
        private WaveInEvent waveIn;
        private bool _disposed = false;
        public const int DesiredFrameSize = 960; // 20ms at 48kHz (960 samples)
        public const int BytesPerFrame = DesiredFrameSize * 2; // 1920 bytes (16-bit mono)

        public event Action<byte[]> OnAudioCaptured = delegate { };
        public event Action<string> OnError = delegate { };

        public AudioCapture()
        {
            try
            {
                int deviceIndex = 0;
                if (WaveInEvent.DeviceCount == 0)
                {
                    throw new Exception("No audio input devices found.");
                }

                Console.WriteLine($"Using input device: {WaveInEvent.GetCapabilities(deviceIndex).ProductName}");

                waveIn = new WaveInEvent
                {
                    DeviceNumber = deviceIndex,
                    WaveFormat = new WaveFormat(48000, 16, 1), // 48kHz, 16-bit, Mono
                    BufferMilliseconds = 20, // Matches our desired frame size
                    NumberOfBuffers = 3
                };

                waveIn.DataAvailable += OnDataAvailable;
                waveIn.RecordingStopped += (s, e) => 
                {
                    if (e.Exception != null)
                        OnError?.Invoke($"Recording error: {e.Exception.Message}");
                };
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Capture initialization failed: {ex.Message}");
                throw;
            }
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            try
            {
                // Only process if we have a full frame (960 samples = 1920 bytes)
                if (e.BytesRecorded == BytesPerFrame)
                {
                    byte[] audioData = new byte[BytesPerFrame];
                    Buffer.BlockCopy(e.Buffer, 0, audioData, 0, BytesPerFrame);
                    Console.WriteLine($"Captured {audioData.Length} bytes of audio");
                    OnAudioCaptured?.Invoke(audioData);
                }
                else
                {
                    Console.WriteLine($"Incomplete frame: {e.BytesRecorded} bytes");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Capture error: {ex.Message}");
            }
}

        public void StartCapture()
        {
            if (!_disposed)
                waveIn.StartRecording();
        }

        public void StopCapture()
        {
            if (!_disposed)
                waveIn.StopRecording();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                waveIn?.Dispose();
            }
        }
    }
}