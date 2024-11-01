using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;

namespace FileHora_server
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Server server = new Server();
            Task task = server.StartServer();
            task.Wait();
        }
    }
}
