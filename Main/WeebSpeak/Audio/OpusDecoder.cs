using OpusSharp.Core;
using System;
using System.Diagnostics;

namespace WeebSpeak.Audio{
public class OpusVoiceDecoder : IDisposable
{
    private readonly OpusDecoder _decoder;
    private bool _disposed = false;
    private const int SampleRate = 48000;
    private const int Channels = 1;
    public const int FrameSize = 960; // 20ms at 48kHz

    public OpusVoiceDecoder()
    {
        try
        {
            _decoder = new OpusDecoder(SampleRate, Channels);
            Debug.WriteLine($"Opus decoder initialized for {SampleRate}Hz {Channels} channel(s)");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Opus decoder initialization failed: {ex.Message}");
            throw;
        }
    }

    public byte[] DecodeToBytes(byte[] opusData)
    {
        if (_disposed) throw new ObjectDisposedException("OpusVoiceDecoder");
        if (opusData == null || opusData.Length == 0) return Array.Empty<byte>();

        try
        {
            short[] pcmData = new short[FrameSize * Channels];
            int samplesDecoded = _decoder.Decode(opusData, opusData.Length, pcmData, FrameSize, false);

            if (samplesDecoded <= 0)
                throw new Exception($"Decoding failed with code: {samplesDecoded}");

            byte[] byteData = new byte[samplesDecoded * 2];
            Buffer.BlockCopy(pcmData, 0, byteData, 0, byteData.Length);
            
            Debug.WriteLine($"Decoded {opusData.Length} bytes Opus to {byteData.Length} bytes PCM");
            return byteData;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Decoding error: {ex.Message}");
            throw;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _decoder?.Dispose();
            Debug.WriteLine("Opus decoder disposed");
        }
    }
}}