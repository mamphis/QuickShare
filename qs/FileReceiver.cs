using qs.Model;
using qs.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;

namespace qs
{
    internal class FileReceiver
    {
        private UdpClient udpClient;
        private readonly Random dice = new Random();
        private TcpListener listener;
        private readonly object writeLock = new object();

        internal void Receive(string v)
        {
            this.udpClient = new UdpClient();

            IPAddress ip = Dns.GetHostEntry(Dns.GetHostName()).AddressList.Where(addr => addr.AddressFamily == AddressFamily.InterNetwork).FirstOrDefault();
            Peer peer = new Peer()
            {
                Code = v,
                IP = ip.ToString(),
                TcpPort = this.dice.Next(10000, 25000)
            };

            byte[] data;
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(ms, peer);

                data = ms.ToArray();
            }

            this.listener = new TcpListener(peer.IPEndPoint);
            this.listener.Start();
            this.listener.BeginAcceptSocket(this.AcceptSocket, null);

            this.udpClient.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, Settings.BINDING_PORT));

            Task.WaitAll(Task.Run(() =>
            {
                lock (this.writeLock)
                {
                    Console.WriteLine("Press Enter to exit.");
                }
                Console.ReadLine();
            }));
        }

        private void AcceptSocket(IAsyncResult ar)
        {
            Socket socket = this.listener.EndAcceptSocket(ar);
            NetworkStream ns = new NetworkStream(socket);

            BinaryReader reader = new BinaryReader(ns);
            BinaryWriter writer = new BinaryWriter(ns);

            (string privateKey, string publicKey) keypair = Encryption.GetKeyPair();

            // Sending public Key
            writer.Write(keypair.publicKey);

            // Receive AES Params
            string encrSymmetricKey = reader.ReadString();
            (byte[] key, byte[] iv) aesParams = Encryption.GetDecryptedSymmetricKey(encrSymmetricKey, keypair.privateKey);

            // Receiving Files
            this.ReceiveFiles(reader, aesParams);

            writer.Write(true);

            Environment.Exit(0);
        }

        private void ReceiveFiles(BinaryReader reader, (byte[] key, byte[] iv) aesParams)
        {
            while (reader.ReadBoolean())
            {
                this.ReceiveFile(reader, aesParams);
            }

            lock (this.writeLock)
            {
                Console.WriteLine("All Files Received.");
            }
        }

        private void ReceiveFile(BinaryReader reader, (byte[] key, byte[] iv) aesParams)
        {
            int fiLength = reader.ReadInt32();
            byte[] encrFiArr = reader.ReadBytes(fiLength);
            byte[] fileInfoArr = Encryption.Decrypt(encrFiArr, aesParams);
            using MemoryStream msfi = new MemoryStream(fileInfoArr);
            FileInformation fileInfo = new BinaryFormatter().Deserialize(msfi) as FileInformation;
            int left, top;

            lock (this.writeLock)
            {
                Console.WriteLine("Receiving: " + fileInfo.FileName + " (" + fileInfo.Length + ")");

                left = Console.CursorLeft;
                top = Console.CursorTop;
            }

            void writePercentage(float progress, TimeSpan duration)
            {
                int totalWidth = Console.BufferWidth;
                int barWidth = totalWidth - 2 /*Brackets*/ - 9 /*Progress*/ - 8 /*Duration*/;
                int filledBarWidth = (int)Math.Floor(barWidth * progress);

                lock (this.writeLock)
                {
                    Console.SetCursorPosition(left, top);
                    Console.Write($"{duration:mm\\:ss\\.f} [{"".PadLeft(filledBarWidth, '▒')}{"".PadLeft(barWidth - filledBarWidth, ' ')}]{progress * 100,7:#.00}%");
                }
            }

            int dataLength = reader.ReadInt32();
            int bytesReceived = 0;
            List<byte> bytes = new List<byte>();
            DateTime start = DateTime.Now;

            int maxChunkSize = 16 * 1024;

            do
            {
                byte[] part = reader.ReadBytes(Math.Min(maxChunkSize, dataLength - bytesReceived));
                bytesReceived += part.Length;

                bytes.AddRange(part);
                writePercentage(bytesReceived / (float)dataLength, DateTime.Now - start);
            } while (bytesReceived < dataLength);

            lock (this.writeLock)
            {
                Console.WriteLine();
            }

            byte[] decryptedBytes = Encryption.Decrypt(bytes.ToArray(), aesParams);
            byte[] hash = Encryption.GetHash(decryptedBytes);

            lock (this.writeLock)
                {
                    Console.SetCursorPosition(left + 10, top);

                    if (hash.SequenceEqual(fileInfo.FileHash))
                    {
                        Console.Write(" Checksum OK \n");

                        File.WriteAllBytes(fileInfo.FileName, decryptedBytes);
                    }
                    else
                    {
                        Console.Write(" Checksum Failed \n");
                    }
                }
        }
    }
}