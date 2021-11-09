using qs.Model;
using qs.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace qs
{
    enum TransferState
    {
        WaitForIncomingConnection,
        ConnectionAccepted,
        DoneConnectionProcess,
        WaitForFinishSending,
        FinishedSending,
        DoneSendingFiles
    }

    internal class FileSharer
    {
        private static UdpClient udpClient;
        private static string code;
        private static TransferState currentState = TransferState.WaitForIncomingConnection;

        internal static void Share(string[] files)
        {
            if (!files.All(f => File.Exists(f)))
            {
                string missingFiles = files.Where(f => !File.Exists(f)).Aggregate((a, b) => a + "\n\t" + b);
                Console.WriteLine($"Cannot Share files because some files are missing:\n\t{missingFiles}");
                return;
            }

            udpClient = new UdpClient();
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, Settings.BINDING_PORT));

            udpClient.BeginReceive(ReceiveRequest, files);
            code = CodeGenerator.GetCode();

            string filesToSend = files.Aggregate((a, b) => a + "\n\t" + b);

            Console.WriteLine($"Sending the following files:\n\t{filesToSend}");

            Console.WriteLine();
            Console.WriteLine($"Use Code {code} to receive the files.");

            int count = 0;

            Task.WaitAll(Task.Run(() =>
            {
                int origLeft = Console.CursorLeft;
                int origTop = Console.CursorTop;

                while (currentState == TransferState.WaitForIncomingConnection)
                {
                    Thread.Sleep(200);
                    Console.SetCursorPosition(origLeft, origTop);
                    Console.Write("Waiting for peer to connect." + "".PadLeft(count++ % 4, '.') + "    ");
                }

                currentState = TransferState.DoneConnectionProcess;
                while (currentState == TransferState.DoneConnectionProcess)
                {
                    Thread.Sleep(100);
                }

                origLeft = Console.CursorLeft;
                origTop = Console.CursorTop;
                while (currentState == TransferState.WaitForFinishSending)
                {
                    Thread.Sleep(200);
                    Console.SetCursorPosition(origLeft, origTop);
                    Console.Write("Sending Files." + "".PadLeft(count++ % 4, '.') + "    ");
                }
            }));
        }

        private static void ReceiveRequest(IAsyncResult ar)
        {
            IPEndPoint ipep = new IPEndPoint(0, 0);
            byte[] data = udpClient.EndReceive(ar, ref ipep);
            string[] files = ar.AsyncState as string[];
            currentState = TransferState.ConnectionAccepted;

            while (currentState == TransferState.ConnectionAccepted)
            {
                Thread.Sleep(100);
            }

            Console.WriteLine("\nConnection from: " + ipep);

            using (MemoryStream ms = new MemoryStream(data))
            {
                BinaryFormatter binaryFormatter = new BinaryFormatter();
                Peer peer = binaryFormatter.Deserialize(ms) as Peer;
                if (peer.Code == code)
                {
                    AcceptPeer(peer, files);
                }
                else
                {
                    Console.WriteLine($"\nMissmatching code... Expected {code}, got {peer.Code}");
                }
            }
        }

        private static void AcceptPeer(Peer peer, string[] files)
        {
            Console.WriteLine($"\nPeer: {peer.IPEndPoint} with code {peer.Code}");

            TcpClient tcpClient = new TcpClient();
            tcpClient.Connect(peer.IPEndPoint);

            if (!tcpClient.Connected)
            {
                Console.WriteLine("\nThe remote client declined the connection.");
                return;
            }


            NetworkStream ns = new NetworkStream(tcpClient.Client);

            BinaryReader reader = new BinaryReader(ns);
            BinaryWriter writer = new BinaryWriter(ns);

            // Receiving public key
            string publicKey = reader.ReadString();

            // Send AES Params
            (string encrData, byte[] key, byte[] iv) encrSymmetricKey = Encryption.GetEncryptedSymmetricKey(publicKey);
            writer.Write(encrSymmetricKey.encrData);

            SendFiles(writer, files, (key: encrSymmetricKey.key, iv: encrSymmetricKey.iv));

            reader.ReadBoolean();
            currentState = TransferState.DoneSendingFiles;
        }

        private static void SendFiles(BinaryWriter writer, string[] files, (byte[] key, byte[] iv) aesParams)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            List<FileInformation> fileInfos = files.Select(f => new FileInformation() { Path = f, FileName = new FileInfo(f).Name, Length = (int)new FileInfo(f).Length }).ToList();
            fileInfos.ForEach(fi =>
            {
                writer.Write(true);
                Console.WriteLine($"\tSending File: {fi.FileName}");

                using MemoryStream ms = new MemoryStream();
                formatter.Serialize(ms, fi);
                byte[] data = Encryption.Encrypt(ms.ToArray(), aesParams);

                var bytes = File.ReadAllBytes(fi.Path);
                var encrBytes = Encryption.Encrypt(bytes, aesParams);

                // Send FI
                writer.Write(data.Length);
                writer.Write(data);

                // Send File
                writer.Write(encrBytes.Length);
                writer.Write(encrBytes);
            });

            writer.Write(false);
        }
    }
}