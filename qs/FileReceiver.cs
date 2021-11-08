using qs.Model;
using qs.Utils;
using System;
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
        private static UdpClient udpClient;
        private static readonly Random dice = new Random();
        private static TcpListener listener;
        internal static void Receive(string v)
        {
            udpClient = new UdpClient();

            IPAddress ip = Dns.GetHostEntry(Dns.GetHostName()).AddressList.Where(addr => addr.AddressFamily == AddressFamily.InterNetwork).FirstOrDefault();
            Peer peer = new Peer()
            {
                Code = v,
                IP = ip.ToString(),
                TcpPort = dice.Next(10000, 25000)
            };

            byte[] data;
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(ms, peer);

                data = ms.ToArray();
            }

            listener = new TcpListener(peer.IPEndPoint);
            listener.Start();
            listener.BeginAcceptSocket(AcceptSocket, null);

            udpClient.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, Settings.BINDING_PORT));
            Task.WaitAll(Task.Run(() => {
                Console.WriteLine("Press Enter to exit.");
                Console.ReadLine();
            }));
        }

        private static void AcceptSocket(IAsyncResult ar)
        {
            Socket socket = listener.EndAcceptSocket(ar);
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
            ReceiveFiles(reader, aesParams);
        }

        private static void ReceiveFiles(BinaryReader reader, (byte[] key, byte[] iv) aesParams)
        {
            while (true)
            {
                bool shouldReceiveFile = reader.ReadBoolean();
                if (!shouldReceiveFile)
                {
                    break;
                }

                int fiLength = reader.ReadInt32();
                byte[] encrFiArr = reader.ReadBytes(fiLength);
                byte[] fileInfoArr = Encryption.Decrypt(encrFiArr, aesParams);
                using MemoryStream msfi = new MemoryStream(fileInfoArr);
                FileInformation fileInfo = new BinaryFormatter().Deserialize(msfi) as FileInformation;

                Console.WriteLine("Receiving: " + fileInfo.FileName + " (" + fileInfo.Length + ")");
                int dataLength = reader.ReadInt32();
                byte[] encrDataArr = reader.ReadBytes(dataLength);
                File.WriteAllBytes(fileInfo.FileName, Encryption.Decrypt(encrDataArr, aesParams));
            }

            Console.WriteLine("All Files Received.");
            Environment.Exit(0);
        }
    }
}