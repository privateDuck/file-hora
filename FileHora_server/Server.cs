using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.FileIO;
using System.Drawing;

namespace FileHora_server
{
    internal class Server
    {
        public const int CHUNK_SIZE = 1024 * 100;
        public Server() { }
        public async Task StartServer()
        {
            using TcpListener listener = new TcpListener(IPAddress.Any, 13000);
            
            try
            {
                List<Task> tasks = [];
                listener.Start();
                while (true)
                {
                    await Console.Out.WriteLineAsync("Ready");

                    TcpClient client = await listener.AcceptTcpClientAsync();
                    int cid = GenerateRandID();
                    /*tasks.Add(Task.Factory.StartNew(() => HandleClient(client), TaskCreationOptions.LongRunning));

                    _ = Task.Factory.ContinueWhenAll([.. tasks], compl =>
                    {
                        Console.WriteLine("All Clients Handled");
                    });*/
                    Thread clientThread = new Thread(() => HandleClient(client, cid));
                    clientThread.Start();

                    /*var rr = await Console.In.ReadLineAsync();
                    if (rr == "exit")
                    {
                        break;
                    }*/
                }
            }
            finally
            {
                listener.Stop();
            }
        }

        private void HandleClient(TcpClient client, int cid)
        {
            Console.WriteLine($"Client Connected (user {cid})");

            try
            {
                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new(stream))
                //using (StreamWriter writer = new StreamWriter(stream) { AutoFlush = true })
                {
                    stream.WriteByte((byte)cid);
                    stream.Flush();
                    string? command;
                    while (client.Connected && (command = reader.ReadLine()) != null)
                    {
                        if (command.StartsWith("send "))
                        {
                            string[] comms = command.Split(' ');
                            string fileName = comms[1];
                            int numBytes = Convert.ToInt32(comms[2]);

                            Console.WriteLine($"Received (user {cid}): file {fileName} with size {numBytes} bytes");
                            SaveFile(stream, numBytes, $"{fileName}");
                        }
                        else if (command.StartsWith("get "))
                        {
                            string[] comms = command.Split(' ');
                            string fileName = comms[1];
                            Console.WriteLine($"Received (user {cid}): get {fileName}");
                            SendFile(stream, fileName);
                        }
                        else if (command.StartsWith("view"))
                        {
                            Console.WriteLine($"Received (user {cid}): view");
                            SendFileIndex(stream);
                        }
                        else if (command.StartsWith("sendp "))
                        {
                            string[] comms = command.Split(' ');
                            string fileName = comms[1];
                            int numBytes = Convert.ToInt32(comms[2]);
                            int numChunks = numBytes / CHUNK_SIZE;
                            int lastChunkSize = numBytes - (numChunks * CHUNK_SIZE);
                            numChunks++;

                            ReceiveFileAsChunks(fileName, stream, numBytes, numChunks, lastChunkSize, cid);
                            Console.WriteLine($"Received (user {cid}): file {fileName} with size {numBytes} bytes in {numChunks} chunks");
                        }
                        else if (command.StartsWith("exit"))
                        {
                            Console.WriteLine($"Exited (user {cid})");
                            stream.WriteByte(1);
                            client.Close();
                            break;
                        }
                        else
                        {
                            Console.WriteLine($"Incorrect Command (user {cid})");
                            stream.WriteByte(0);
                            client.Close();
                            break;
                        }
                    }
                }
                Console.WriteLine($"Client Disconnected (user {cid})");
            }
            catch (Exception e)
            {
                client.Close();
                Console.WriteLine($"An error occurred: (user {cid} forcibly disconnected)");
                Console.WriteLine($"Error: {e.Message}");
            }
        }

        private void ReceiveFileAsChunks(string fileName, NetworkStream stream, int numBytes, int numChunks, int lastChunkSize, int cid)
        {
            Thread.Sleep(1000);
            byte[] chunk = new byte[CHUNK_SIZE];
            int bytes = 0;
            Storage.DeleteFile(fileName);
            while(stream.DataAvailable)
            {
                int dt = numBytes - bytes;
                int wn = dt < CHUNK_SIZE ? lastChunkSize : CHUNK_SIZE;
                bytes += stream.Read(chunk, 0, wn);
                Storage.AppendToFile(fileName, chunk, wn);
                Array.Clear(chunk, 0, CHUNK_SIZE);
            }
            if (bytes != numBytes)
            {
                Console.WriteLine($"Error (user {cid}): missing data while receiving file {fileName}");
                Storage.DeleteFile(fileName);
                stream.WriteByte(0);
            }
            stream.WriteByte(1);
            stream.Flush();
        }

        private void SendFileAsChunks(NetworkStream stream, string filepath)
        {
            byte[] buffer = new byte[CHUNK_SIZE];

            using (FileStream fs = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int bytesRead = 0;
                while ((bytesRead = fs.Read(buffer, 0, CHUNK_SIZE)) > 0)
                {
                    stream.Write(buffer, 0, bytesRead);
                    stream.Flush();
                    Array.Clear(buffer, 0, CHUNK_SIZE);
                }
            }
        }

        private void SendFileIndex(NetworkStream stream)
        {
            string[] files = Storage.GetFilesInStorage();
            string sendTo = "No Files";
            if (files.Length > 0)
            {
                for (int i = 0; i < files.Length; i++)
                {
                    var info = new FileInfo(files[i]);
                    string infStr = $"{info.Name} {info.Length} {info.CreationTime}";
                    files[i] = infStr;
                }
                sendTo = string.Join('\n', files);
            }
            byte[] bytes = Encoding.UTF8.GetBytes(sendTo);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }

        byte[] GetDataBytes(NetworkStream stream, int numBytes)
        {
            byte[] bytes = new byte[numBytes];

            if (stream.DataAvailable)
            {
                stream.Read(bytes, 0, bytes.Length);
            }

            return bytes;
        }

        void SaveFile(NetworkStream stream, int numBytes, string filename)
        {
            byte[] bytes = GetDataBytes(stream, numBytes);

            if (bytes == null) return;
            if (Storage.AddFile(filename, bytes))
            {
                stream.WriteByte(1);
            }
            else
                stream.WriteByte(0);
            stream.Flush();
        }

        void SendFile(NetworkStream stream,  string filename)
        {
            // Check if file exists
            if (Storage.FileExists(filename))
            {
                // Fetch file info
                int size = (int)new FileInfo(Storage.GetLoc(filename)).Length;
                WriteStream(stream, $"{size}\r\n");
                stream.ReadByte();
                if (size <= CHUNK_SIZE)
                {
                    byte[] fileData = Storage.GetFileBytes(filename);
                    int numBytes = fileData.Length;
                    stream.Write(fileData, 0, numBytes);
                    stream.Flush();
                }
                else
                {
                    SendFileAsChunks(stream, Storage.GetLoc(filename));
                }
            }
            else
            {
                WriteStream(stream, "0\r\n");
                Console.WriteLine("File doesn't exists");
            }
            stream.Flush();
        }

        void WriteStream(NetworkStream stream, string msg)
        {
            byte[] data = Encoding.UTF8.GetBytes(msg);
            stream.Write(data, 0, data.Length);
            stream.Flush();
        }

        static int GenerateRandID()
        {
            return rand.Next(0, 256);
        }
        static Random rand = new Random();
    }
}
