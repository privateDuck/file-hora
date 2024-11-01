using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO.Enumeration;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FileHora_client
{
    internal class TCPClient
    {
        public readonly string FILE_DIRECTORY = AppDomain.CurrentDomain.BaseDirectory + "/storage";
        public const int CHUNK_SIZE = 1024 * 100;
        public TCPClient() { }

        public async Task ConnectToServer()
        {
            while(true)
            {
                await Console.Out.WriteLineAsync("Enter ip and port: ");
                string? sock = Console.ReadLine();
                if (sock != null)
                {
                    Console.Clear();
                    if (sock == "default")
                    {
                        await EstablishConnection("127.0.0.1", 13000);
                    }
                    else if (sock.StartsWith("exit"))
                    {
                        await Console.Out.WriteLineAsync("FileHora Client Closed\n\nPress any key to continue...");
                        Console.ReadKey();
                        break;
                    }
                    else
                    {
                        string[] args = sock.Split(' ');
                        await EstablishConnection(args[0], int.Parse(args[1]));
                    }
                }
            }
        }

        private async Task EstablishConnection(string host, int port)
        {
            try
            {
                Console.Clear();
                using TcpClient client = new TcpClient();
                await client.ConnectAsync(host, port);
                using NetworkStream stream = client.GetStream();
                int cid = stream.ReadByte();

                await Console.Out.WriteLineAsync($"Connected successfully with id: {cid}");
                Thread.Sleep(500);
                while (client.Connected)
                {
                    //Console.Clear();
                    Console.WriteLine("");
                    await Console.Out.WriteAsync("File Hora# ");
                    string? comm = Console.ReadLine();
                    if (comm != null)
                    {
                        string[] sttr = comm.Split(' ');
                        if (sttr[0] == "send")
                        {
                            string filepath = sttr[1];
                            if (!filepath.Contains('/')) filepath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filepath);
                            if (File.Exists(filepath))
                            {
                                FileInfo fileInfo = new FileInfo(filepath);
                                if (fileInfo.Length <= CHUNK_SIZE)
                                {
                                    SendFileWhole(stream, filepath);
                                }
                                else
                                {
                                    SendFileAsChunks(stream, filepath, (int)fileInfo.Length);
                                }
                            }
                        }
                        else if (sttr[0] == "view")
                        {
                            WriteToStream(stream, "view \r\n");
                            DecodeViewResponse(stream);
                        }
                        else if (sttr[0] == "get")
                        {
                            WriteToStream(stream, $"get {sttr[1]} \r\n");
                            int numBytes = GetReceivingFileSize(stream);

                            if (numBytes == 0) await Console.Out.WriteLineAsync("File doesn't exist");
                            else if (numBytes <= CHUNK_SIZE)
                                RecieveFileAsWhole(stream, sttr[1], numBytes);
                            else
                                ReceiveFileAsChunks(sttr[1], stream, numBytes);
                        }
                        else if (sttr[0] == "exit")
                        {
                            await Console.Out.WriteLineAsync("Disconnecting...");
                            WriteToStream(stream, "exit \r\n");
                            Thread.Sleep(500);
                            Console.Clear();
                            break;
                        }
                        else if (sttr[0] == "help")
                        {
                            await Console.Out.WriteLineAsync(helpString);
                        }
                    }
                    //await Console.Out.WriteLineAsync("\n\nPress any key to continue...");
                    //Console.ReadKey();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void RecieveFileAsWhole(NetworkStream stream, string filename, int numBytes)
        {
            byte[]? bytes = GetMessageBytes(stream, numBytes);

            if (bytes != null)
            {
                SaveFile(filename, bytes);
                Console.WriteLine($"{filename} retrieved successfully and saved to {FILE_DIRECTORY}");
            }
        }

        private void SendFileAsChunks(NetworkStream stream, string filepath, int length)
        {
            byte[] buffer = new byte[CHUNK_SIZE];

            string filename = Path.GetFileName(filepath);
            string sendComm = $"sendp {filename} {length} \r\n";
            WriteToStream(stream, sendComm);

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

            int rr = stream.ReadByte();
            if(rr == 0) { Console.WriteLine($"Error while transferring file {filename}"); }
            else { Console.WriteLine("File transferred successfuly!"); }
        }

        int GetReceivingFileSize(NetworkStream stream)
        {
            byte[] bytes = new byte[128];
            int rc = stream.Read(bytes, 0, bytes.Length);
            string? line = Encoding.UTF8.GetString(bytes, 0, rc - 2);
            stream.WriteByte(1);
            if (line != null) { return int.Parse(line); }
            return 0;
        }

        private void ReceiveFileAsChunks(string fileName, NetworkStream stream, int numBytes)
        {
            int numChunks = numBytes / CHUNK_SIZE;
            int lastChunkSize = numBytes - (numChunks * CHUNK_SIZE);
            Thread.Sleep(1000);
            byte[] chunk = new byte[CHUNK_SIZE];
            int bytes = 0;
            Storage.DeleteFile(fileName);
            while (stream.DataAvailable)
            {
                int dt = numBytes - bytes;
                int wn = dt < CHUNK_SIZE ? lastChunkSize : CHUNK_SIZE;
                bytes += stream.Read(chunk, 0, wn);
                Storage.AppendToFile(fileName, chunk, wn);
                Array.Clear(chunk, 0, CHUNK_SIZE);
            }
            if (bytes != numBytes)
            {
                Console.WriteLine($"Error : missing data while receiving file {fileName}");
                Storage.DeleteFile(fileName);
            }
            Console.WriteLine($"{fileName} retrieved successfully and saved to {FILE_DIRECTORY}");
        }

        private void SendFileWhole(NetworkStream stream, string filepath)
        {
            byte[] bytes = OpenFile(filepath);
            int numBytes = bytes.Length;

            string filename = Path.GetFileName(filepath);
            string sendComm = $"send {filename} {numBytes} \r\n";

            WriteToStream(stream, sendComm);
            stream.Write(bytes, 0, numBytes);
            stream.Flush();

            int rr = stream.ReadByte();
            if (rr == 1) Console.WriteLine("File sent successfully");
            else Console.WriteLine("Error occurred while transferring");
        }

        private void DecodeViewResponse(NetworkStream stream)
        {
            string msg = GetMessageString(stream);

            if (msg == "No Files") Console.WriteLine(msg);
            else
            {
                string[] strings = msg.Split('\n');
                Console.WriteLine($"\nShared Directory: {strings.Length} files\n\n");
                Console.WriteLine(String.Format("|{0,40}|{1,20}|{2,20}|", "File Name", "File Size", "Date Created"));
                Console.WriteLine(new string('-', 84));
                foreach (var s in strings)
                {
                    string[] attr = s.Split(' ');
                    int len = int.Parse(attr[1]);
                    long size = len < 10000 ? len : (len < 10000000 ? len / 1024 : len / 1024 / 1024);
                    string unit = len < 10000 ? "Bytes" : (len < 10000000 ? "KB" : "MB");
                    string ufmt = $"{size} {unit}";
                    Console.WriteLine(String.Format("|{0,40}|{1,20}|{2, 20}|", attr[0], ufmt, attr[2]));
                }
            }
        }

        private byte[] OpenFile(string filepath)
        {
            using var fs = new FileStream(filepath, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);
            long numB = new FileInfo(filepath).Length;

            return br.ReadBytes((int)numB);
        }

        private void SaveFile(string filename, byte[] bytes)
        {
            if (!Directory.Exists(FILE_DIRECTORY))
            {
                Directory.CreateDirectory(FILE_DIRECTORY);
            }
            File.WriteAllBytes(Path.Combine(FILE_DIRECTORY, filename), bytes);
        }

        private void WriteToStream(Stream stream, string msg)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(msg);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }

        string GetMessageString(NetworkStream stream)
        {
            int readBytes = 0;
            byte[] bytes = new byte[1024];
            Thread.Sleep(1000);

            if (stream.DataAvailable)
            {
                readBytes = stream.Read(bytes, 0, bytes.Length);
            }
            return Encoding.UTF8.GetString(bytes, 0, readBytes);
        }

        byte[]? GetMessageBytes(NetworkStream stream, int numBytes)
        {
            int readBytes = 0;
            byte[] bytes = new byte[numBytes];
            Thread.Sleep(500);

            while(stream.DataAvailable)
            {
                readBytes = stream.Read(bytes, 0, numBytes);
            }
            if (readBytes != numBytes) { Console.WriteLine("Error while receiving file"); }
            return bytes;
        }

        private const string helpString = @"
            send <file_path>    : sends a file to the server
            view <no_arguments> : view contents of the shared directory
            get  <filename>     : retreives a file with the given file name from the server
            exit <no_arguments> : ends the socket connection
        ";
    }
}
