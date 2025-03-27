using System;
using WeebSpeak.Networking;
using WeebSpeak.Audio;
using System.Net;
using System.Threading;
using System.Collections.Concurrent;
using System.Linq;
using NAudio.Wave;

namespace WeebSpeak
{
    class Program
    {
        public const int FrameSize = 960;
        static string? serverIpAddress;
        static UdpComm? udpComm;
        static AudioCapture? audioCapture;
        static AudioPlayer? audioPlayer;
        static OpusEncoderWrapper? opusEncoder;

        static void Main(string[] args)
        {
            Console.WriteLine("Select Mode: ");
            Console.WriteLine("1. Host (Server)");
            Console.WriteLine("2. Connect (Client)");
            string? modeChoice = Console.ReadLine();

            if (modeChoice != "1" && modeChoice != "2")
            {
                Console.WriteLine("Invalid choice. Exiting.");
                return;
            }

            if (modeChoice == "1")
            {
                StartServer();
            }
            else
            {
                StartClient();
            }
        }

        static void StartServer()
        {
            var clients = new ConcurrentDictionary<IPEndPoint, DateTime>();
            udpComm = new UdpComm(12345);

            Task.Run(() =>
            {
                while (true)
                {
                    Thread.Sleep(30000);
                    var cutoff = DateTime.Now.AddSeconds(-15);

                    foreach (var client in clients.Keys.ToList())
                    {
                        if (clients[client] < cutoff)
                        {
                            try
                            {
                                byte[] pingPacket = { 0x03 };
                                udpComm.SendData(pingPacket, client);
                            }
                            catch
                            {
                                clients.TryRemove(client, out _);
                                Console.WriteLine($"Client {client} removed due to inactivity.");
                            }
                        }
                    }
                    Console.WriteLine($"Active clients: {clients.Count}");
                }
            });

            udpComm.OnDataReceived += (data, sender) =>
            {
                if (data.Length == 0) return;

                clients.AddOrUpdate(sender, DateTime.Now, (_, _) => DateTime.Now);
                Console.WriteLine($"Received {data.Length} bytes from {sender}");

                if (data[0] == 0x02)
                {
                    foreach (var client in clients.Keys.ToList())
                    {
                        if (!client.Equals(sender) && clients.ContainsKey(client))
                        {
                            try
                            {
                                udpComm.SendData(data, client);
                            }
                            catch
                            {
                                clients.TryRemove(client, out _);
                            }
                        }
                    }
                }
                else if (data[0] == 0x01)
                {
                    if (clients.TryRemove(sender, out _))
                    {
                        Console.WriteLine($"Client {sender} disconnected.");
                    }
                }
            };

            AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) => CleanupAndDisconnect();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                CleanupAndDisconnect();
            };

            udpComm.StartReceiving();
            Console.WriteLine("Server running. Press any key to stop...");
            Console.ReadKey();
            CleanupAndDisconnect();
        }

        static void StartClient()
        {
            Console.Write("Enter your username: ");
            string username = Console.ReadLine() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(username))
            {
                Console.WriteLine("Username cannot be empty. Exiting.");
                return;
            }

            Console.Write("Enter the server IP address: ");
            serverIpAddress = Console.ReadLine() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(serverIpAddress))
            {
                Console.WriteLine("IP Address cannot be empty. Exiting.");
                return;
            }

            udpComm = new UdpComm(0);
            udpComm.StartReceiving();

            IPEndPoint localEP = udpComm.LocalEndPoint;
            Console.WriteLine($"Client local endpoint: {localEP}");

            audioCapture = new AudioCapture();
            audioPlayer = new AudioPlayer();
            opusEncoder = new OpusEncoderWrapper();
            var opusDecoder = new OpusVoiceDecoder();

            byte[] localEndPointMessage = System.Text.Encoding.UTF8.GetBytes(localEP.ToString());
            udpComm.SendData(localEndPointMessage, new IPEndPoint(IPAddress.Parse(serverIpAddress), 12345));

            audioCapture.OnAudioCaptured += (pcmData) =>
            {
                try
                {
                    byte[] encoded = opusEncoder.Encode(pcmData);
                    byte[] packet = new byte[encoded.Length + 1];
                    packet[0] = 0x02;
                    Buffer.BlockCopy(encoded, 0, packet, 1, encoded.Length);
                    udpComm.SendData(packet, new IPEndPoint(IPAddress.Parse(serverIpAddress), 12345));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Encoding error: {ex.Message}");
                }
            };

            udpComm.OnDataReceived += (data, sender) =>
            {
                if (data.Length == 0) return;

                if (data[0] == 0x02)
                {
                    try
                    {
                        byte[] opusData = new byte[data.Length - 1];
                        Buffer.BlockCopy(data, 1, opusData, 0, opusData.Length);

                        byte[] pcmData = opusDecoder.DecodeToBytes(opusData);
                        if (pcmData.Length == FrameSize * 2)
                        {
                            audioPlayer.Play(pcmData);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing audio packet: {ex}");
                    }
                }
            };

            audioCapture.StartCapture();

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                CleanupAndDisconnect();
            };
            AppDomain.CurrentDomain.ProcessExit += (sender, e) => CleanupAndDisconnect();

            Console.WriteLine($"Connected as {username}. Press any key to stop...");
            Console.ReadKey();
            CleanupAndDisconnect();
        }

        static void CleanupAndDisconnect()
        {
            Console.WriteLine("\nClosing client...");

            if (!string.IsNullOrWhiteSpace(serverIpAddress) && udpComm != null)
            {
                try
                {
                    byte[] disconnectMsg = { 0x01 };
                    udpComm.SendData(disconnectMsg, new IPEndPoint(IPAddress.Parse(serverIpAddress), 12345));
                    Console.WriteLine("Sent disconnect message to server.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending disconnect message: {ex.Message}");
                }
            }

            audioCapture?.StopCapture();
            audioCapture?.Dispose();
            opusEncoder?.Dispose();
            audioPlayer?.Dispose();
            udpComm?.Dispose();

            Console.WriteLine("Client cleanup complete.");
        }
    }
}
