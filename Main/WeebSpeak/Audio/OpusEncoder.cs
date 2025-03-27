using OpusSharp.Core;
using System;
using System.Diagnostics;

namespace WeebSpeak.Audio
{
    public class OpusEncoderWrapper : IDisposable
    {
        private readonly OpusEncoder _encoder;
        private bool _disposed = false;
        public const int FrameSize = 960; // 20ms at 48kHz (public for external reference)
        private const int MaxPacketSize = 4000; // Maximum expected Opus packet size

        public OpusEncoderWrapper()
        {
            try
            {
                // Using the exact constructor format that works for you
                _encoder = new OpusEncoder(48000, 1, OpusPredefinedValues.OPUS_APPLICATION_VOIP);
                Debug.WriteLine("Opus encoder initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Opus encoder initialization failed: {ex.Message}");
                throw;
            }
        }

        public byte[] Encode(short[] pcmData)
        {
            return Encode(pcmData, FrameSize);
        }

        public byte[] Encode(short[] rawAudioData, int frameSize)
        {
            if (_disposed)
                throw new ObjectDisposedException("OpusEncoderWrapper");

            if (rawAudioData == null || rawAudioData.Length == 0)
            {
                Debug.WriteLine("Empty PCM data received for encoding");
                return Array.Empty<byte>();
            }

            try
            {
                // Validate frame size
                if (frameSize != FrameSize)
                {
                    throw new ArgumentException($"Frame size must be {FrameSize} samples (20ms at 48kHz)");
                }

                byte[] encodedData = new byte[MaxPacketSize];
                int bytesEncoded = _encoder.Encode(rawAudioData, frameSize, encodedData, encodedData.Length);

                if (bytesEncoded <= 0)
                {
                    Debug.WriteLine($"Encoding failed with error code: {bytesEncoded}");
                    throw new InvalidOperationException($"Opus encoding error: {bytesEncoded}");
                }

                Debug.WriteLine($"Encoded {frameSize} PCM samples to {bytesEncoded} bytes");
                
                byte[] result = new byte[bytesEncoded];
                Buffer.BlockCopy(encodedData, 0, result, 0, bytesEncoded);
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Encoding error: {ex.Message}");
                throw;
            }
        }

        public byte[] Encode(byte[] pcmBytes)
        {
            if (pcmBytes == null || pcmBytes.Length == 0)
                return Array.Empty<byte>();

            // Convert byte[] to short[]
            short[] pcmData = new short[pcmBytes.Length / 2];
            Buffer.BlockCopy(pcmBytes, 0, pcmData, 0, pcmBytes.Length);
            
            return Encode(pcmData);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _encoder?.Dispose();
                _disposed = true;
                Debug.WriteLine("Opus encoder disposed");
            }
        }
    }
}