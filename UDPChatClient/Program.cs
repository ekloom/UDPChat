namespace UDPChatClient
{

    public class MainClass
    {
        public static async Task Main(string[] args)
        {

            string? hostName = null;
            int portNumber;

            if (args.Length != 2)
            {
                Console.Write("Enter the hostname: ");
                hostName = Console.ReadLine();

                Console.Write("Enter the port number: ");
                if (!int.TryParse(Console.ReadLine(), out portNumber))
                {
                    Console.Error.WriteLine("Port number moet een getal zijn.");
                    Console.ReadLine();
                    Environment.Exit(1);
                }
            }
            else
            {

                hostName = args[0];
                if (!int.TryParse(args[1], out portNumber))
                {
                    Console.Error.WriteLine("Usage: <host name> <port number>");
                    Console.Error.WriteLine("Port number moet een getal zijn.");
                    Console.ReadLine();
                    Environment.Exit(1);
                }
            }

            if (string.IsNullOrEmpty(hostName))
            {
                Console.WriteLine("No host name was provided");
                Console.ReadLine();
                Environment.Exit(1);
            }

            Console.WriteLine("Welkom bij UDPChat!");


            try
            {

                // TODO, First check if hostname + port exist

                ClientHandler clientHandler = new ClientHandler(hostName, portNumber);
                await clientHandler.ConnectAsync();

                await ConsoleHandler.ReadInputLoop(clientHandler);

            }
            catch (Exception)
            {
                throw;
            }

        }
    }
}