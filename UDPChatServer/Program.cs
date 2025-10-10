using System.Net.Sockets;

namespace UDPChatServer
{
    public class MainClass
    {
        public static async Task Main(string[] args)
        {
            int portNumber = 5000;
            using UdpClient server = new UdpClient(portNumber);

            Console.WriteLine($"UDPChat is gestart op poort {portNumber}");

            ServerHandler serverHandler = new ServerHandler(server);
            CancellationTokenSource cts = new CancellationTokenSource();

            // Om de server goed af te sluiten moet je de combinatie 'Ctrl+C' in de console uitvoeren
            Console.CancelKeyPress += (sender, e) =>
            {
                Console.WriteLine("Server wordt afgesloten...");
                cts.Cancel();
                e.Cancel = true;  
                Environment.Exit(0);    
            };

            await serverHandler.StartAsync(cts);

            
        }
    }
}
