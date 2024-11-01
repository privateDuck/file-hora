using System.Net.Sockets;
using System.Text;

namespace FileHora_client
{
    internal class Program
    {
        static void Main(string[] args)
        {
            TCPClient client = new TCPClient();
            Task task = client.ConnectToServer();
            task.Wait();

            Console.ReadLine();
            Console.ReadLine();
        }

        
    }
}
