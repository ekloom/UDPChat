

namespace UDPChatClient
{
    internal class ConsoleHandler
    {
        /* Lock wordt gebruikt voor prioriteit 
         * Wanneer een van deze functies opgeroepen wordt,
         * dan hebben ze even de console in macht en wordt de andere methode geblockt) */
        private static readonly object consoleLock = new object();
        private static string currentInput = "";

        public static void WriteToConsole(string message, ConsoleColor consoleColor = ConsoleColor.White)
        {
            lock (consoleLock)
            {
                // Verwijder huidige input prompt tijdelijk
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write(new string(' ', currentInput.Length + 17));
                Console.SetCursorPosition(0, Console.CursorTop);
                // Schrijf het nieuwe bericht
                Console.ForegroundColor = consoleColor;
                Console.WriteLine(message);
                if (consoleColor != ConsoleColor.White) Console.ResetColor();


                Console.Write("Verstuur bericht: " + currentInput);
            }
        }


        public static async Task ReadInputLoop(ClientHandler clientHandler)
        {
            while (clientHandler.ClientIsOnline)
            {
                string input = await Task.Run(() => Console.ReadLine());

                if (input == null) break;

                //Dit staat niet in de lock zodat 'WriteToConsole' nog kans heeft om opgeroepen tw worden
                if (string.IsNullOrEmpty(input))
                {
                    int top = Console.CursorTop - 1;
                    Console.SetCursorPosition(0, top);
                    Console.Write(new string(' ', 18 + input.Length));
                    Console.SetCursorPosition(0, top);
                    Console.Write("Verstuur bericht: ");
                    continue;
                }

                // Console wordt gelockt totdat deze process is voltooid
                lock (consoleLock)
                {
                    int top = Console.CursorTop - 1;
                    Console.SetCursorPosition(0, top);
                    Console.Write(new string(' ', 18 + input.Length));
                    Console.SetCursorPosition(0, top);

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("Jij: ");
                    Console.ResetColor();
                    Console.Write(input + '\n');

                    currentInput = "";
                    Console.Write("Verstuur bericht: ");
                }


                await clientHandler.ProcessPrompt(input);

            }
        }

    }
}
