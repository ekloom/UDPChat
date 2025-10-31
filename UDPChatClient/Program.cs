namespace UDPChatClient
{

    public class MainClass
    {
        public static async Task Main(string[] args)
        {

            if (args.Length != 2)
            {
                Console.Error.WriteLine("Usage: <host name> <port number>");
                Environment.Exit(1);
            }

            string hostName = args[0];
            if (!int.TryParse(args[1], out int portNumber))
            {
                Console.Error.WriteLine("Port number moet een getal zijn.");
                Environment.Exit(1);
            }


            Console.WriteLine("Welkom bij UDPChat!");


            try
            {

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