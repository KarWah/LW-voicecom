using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;

namespace WeebSpeak.Networking
{
    public class UdpComm : IDisposable
    {
        private UdpClient udpClient;
        private Thread? receiveThread = null;

        private bool _disposed = false;
        public const int MaxPacketSize = 2048;

        public event Action<byte[], IPEndPoint> OnDataReceived = delegate { };

        public IPEndPoint LocalEndPoint { get; private set; }

        public UdpComm(int port)
        {
            udpClient = new UdpClient(port);
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
            
            // Throw an exception if LocalEndPoint is unexpectedly null
            LocalEndPoint = (IPEndPoint)(udpClient.Client.LocalEndPoint 
                            ?? throw new InvalidOperationException("LocalEndPoint is null."));
            
            Console.WriteLine($"UDP comm initialized on {LocalEndPoint}");
        }


        public void StartReceiving()
        {
            if (_disposed)
                throw new ObjectDisposedException("UdpComm");

            receiveThread = new Thread(ReceiveData)
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
            receiveThread.Start();
            Console.WriteLine("UDP receiving thread started.");
        }

        private void ReceiveData()
        {
            try
            {
                // Use a separate variable for the remote endpoint so that our LocalEndPoint remains unchanged.
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                while (!_disposed)
                {
                    byte[] data = udpClient.Receive(ref remoteEP);
                    Console.WriteLine($"Received {data.Length} bytes from {remoteEP}");
                    OnDataReceived?.Invoke(data, remoteEP);
                }
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted)
            {
                // Normal shutdown
            }
            catch (Exception ex)
            {
                if (!_disposed)
                    Console.WriteLine($"Receive error: {ex.Message}");
            }
        }

        public void SendData(byte[] data, IPEndPoint target)
        {
            if (_disposed)
                throw new ObjectDisposedException("UdpComm");

            try
            {
                int sent = udpClient.Send(data, data.Length, target);
                Console.WriteLine($"Sent {sent} bytes to {target}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Send error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                try
                {
                    udpClient?.Close();
                    receiveThread?.Join(100);
                }
                catch { /* Ignore */ }
                Console.WriteLine("UDP comm disposed.");
            }
        }
    }
}
