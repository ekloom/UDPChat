
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace UDPChatClient
{
    internal class ClientHandler
    {
        ConcurrentDictionary<int, TaskCompletionSource<bool>> pendingAcks;

        SecurityHandler encryptionHandler;

        UdpClient client;
        IPEndPoint serverEndpoint;
        string username;
        int ackID;

        public bool ClientIsOnline { get; private set; }

        public ClientHandler(string serverIp, int serverPort, string name)
        {

            client = new UdpClient(0);
            serverEndpoint = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
            username = name;
            pendingAcks = new ConcurrentDictionary<int, TaskCompletionSource<bool>>();
            encryptionHandler = new SecurityHandler();
        }

        public async Task ConnectAsync()
        {
            _ = Task.Run(() => ListenForMessages());
            await VerifyConnectionWithServer();
            ClientIsOnline = true;
        }

        private async Task ListenForMessages()
        {
            while (true)
            {
                var result = await client.ReceiveAsync();
                string txt = Encoding.UTF8.GetString(result.Buffer);

                string[] parts = txt.Split(" ");
                string command = parts[0];

                switch (command)
                {
                    case "MSG":
                        // MSG <sender> <message...>
                        string sender = parts[1];
                        string encryptedBase64 = string.Join(" ", parts, 2, parts.Length - 2);
                        byte[] encryptedBytes = Convert.FromBase64String(encryptedBase64);
                        string message = encryptionHandler.DecryptMessage(encryptedBytes);
                        ConsoleHandler.WriteToConsole($"{sender}: {message}");
                        break;

                    case "ACK":
                        // ACK <ackId>
                        int ackId = int.Parse(parts[1]);
                        if (pendingAcks.TryRemove(ackId, out var tcs))
                        {
                            tcs.SetResult(true);
                        }
                        break;

                    case "STATUS":
                        // STATUS <username> <status>
                        string statusUser = parts[1];
                        string status = parts[2];
                        ConsoleHandler.WriteToConsole($"{statusUser} is nu {status}");
                        break;
                    case "PING":
                        await SendDataToServer($"PONG {username}");
                        break;
                    case "SETKEY":
                        byte[] encKey = Convert.FromBase64String(parts[1]);
                        byte[] encIV = Convert.FromBase64String(parts[2]);

                        byte[] aesKey = encryptionHandler.DecryptWithPrivateKey(encKey);
                        byte[] aesIV = encryptionHandler.DecryptWithPrivateKey(encIV);

                        encryptionHandler.SetAESKeyAndIV(aesKey, aesIV);
                        break;
                    default:
                        ConsoleHandler.WriteToConsole("Onbekend bericht ontvangen: " + txt);
                        break;
                }
            }

        }

        public async Task ProcessPrompt(string text)
        {
            switch (text)
            {
                case string status when text.StartsWith("/"):
                    string command = text.Substring(1);

                    if (command.ToLower() == "beschikbaar")
                    {
                        await VerifyConnectionWithServer();
                    }
                    else
                    {
                        string fullMsg = $"STATUS {username} {command}";
                        await SendDataToServer(fullMsg);
                    }


                    break;
                default:
                    await SendMessage(text);
                    break;
            }
        }


        async Task VerifyConnectionWithServer()
        {
            int _ackID = ackID++;

            int timeout = 5000; // 5 seconden

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            pendingAcks[_ackID] = tcs;

            await SendDataToServer($"CONNECT {username} {_ackID}");

            bool isSuccess = await HandleACK(tcs,
                            timeout,
                            _ackID,
                            "Verbinding met de server is niet gelukt!",
                            "Je bent verbonden met de server!");

            if (isSuccess)
            {
                string pubKeyString = Convert.ToBase64String(encryptionHandler.PublicKey);
                await SendDataToServer($"PUBKEY {username} {pubKeyString}");
            }

        }


        async Task SendMessage(string message)
        {
            int messageID = ackID++;

            string cipher = Convert.ToBase64String(encryptionHandler.EncryptMessage(message));

            string fullMsg = $"MSG {username} {messageID} {cipher}";

            int timeout = 5000; // 5 seconden

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            pendingAcks[messageID] = tcs;

            await SendDataToServer(fullMsg);

            await HandleACK(tcs, timeout, messageID, "Bericht is niet verstuurd naar server!");
        }

        async Task<bool> HandleACK(TaskCompletionSource<bool> tcs, int timeout, int ackID, string failedMessage, string successMessage = "")
        {
            try
            {
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeout));

                if (completedTask != tcs.Task)
                {
                    ConsoleHandler.WriteToConsole(failedMessage, ConsoleColor.Red);
                }
                else if (tcs.Task.IsCompletedSuccessfully)
                {
                    if (!string.IsNullOrEmpty(successMessage))
                        ConsoleHandler.WriteToConsole(successMessage);
                    return true;
                }
                else if (tcs.Task.IsFaulted)
                {
                    throw new Exception(tcs.Task.Exception?.InnerException?.Message);
                }
            }
            finally
            {
                pendingAcks.TryRemove(ackID, out _);
            }

            return false;
        }


        private async Task SendDataToServer(string data)
        {
            byte[] Bytes = Encoding.UTF8.GetBytes(data);
            await client.SendAsync(Bytes, Bytes.Length, serverEndpoint);
        }

    }
}
