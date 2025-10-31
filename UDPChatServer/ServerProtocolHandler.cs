using System.Net;
using System.Security.Cryptography;
using UDPChatServer.DataInterfaces;
using UDPChatServer.Datamodels;
using UDPChatServer.Security;

namespace UDPChatServer
{
    internal class ServerProtocolHandler
    {
        ServerHandler ServerHandler { get; set; }

        private static readonly HashSet<string> NonEncryptedCommands = new() { "CONNECT", "PUBKEY", "PONG", "STATUS" };
        private static readonly HashSet<string> EncryptedCommands = new() { "MSG" };

        public ServerProtocolHandler(ServerHandler serverHandler)
        {
            ServerHandler = serverHandler;
        }

        public async Task ExecuteCommand(IPEndPoint senderEndpoint, ClientState clientInfo, string protocolMessage)
        {
            if (string.IsNullOrWhiteSpace(protocolMessage))
            {
                Console.WriteLine("Leeg/ongeldig protocolbericht ontvangen van {0}", senderEndpoint);
                return;
            }

            // Split only the first word to identify the command
            int firstSpace = protocolMessage.IndexOf(' ');
            string command = firstSpace == -1 ? protocolMessage : protocolMessage[..firstSpace];

            // Handle non-encrypted commands directly
            if (NonEncryptedCommands.Contains(command))
            {
                switch (command)
                {
                    case "CONNECT":
                    {
                        // CONNECT <ack> <username ...>
                        var parts = protocolMessage.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                        string ack = parts.Length > 1 ? parts[1] : "";
                        string username = parts.Length > 2 ? parts[2] : "";

                        string[] parsedData = { command, ack, username };
                        await HandleConnect(senderEndpoint, clientInfo, parsedData);
                        break;
                    }

                    case "PUBKEY":
                    {
                        // PUBKEY <ack> <key...>
                        var parts = protocolMessage.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                        string ack = parts.Length > 1 ? parts[1] : "";
                        string key = parts.Length > 2 ? parts[2] : "";

                        await HandlePubKey(senderEndpoint, clientInfo, key);
                        break;
                    }

                    case "PONG":
                    {
                        // PONG <ack>
                        var parts = protocolMessage.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                        string ack = parts.Length > 1 ? parts[1] : "";

                        if (ServerHandler.ClientDataInterface.GetPendingPongs().TryRemove(senderEndpoint, out var tcs))
                            tcs.SetResult(true);

                        clientInfo.LastPingTime = DateTime.UtcNow;
                        break;
                    }

                    case "STATUS":
                    {
                        // STATUS <ack> <status text...>
                        var parts = protocolMessage.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                        string ack = parts.Length > 1 ? parts[1] : "";
                        string statusText = parts.Length > 2 ? parts[2] : "";

                        await HandleStatus(senderEndpoint, clientInfo, statusText);
                        break;
                    }

                    default:
                        Console.WriteLine($"Onbekend niet-versleuteld commando ontvangen: {command}");
                        break;
                }

                return;
            }

            // Handle encrypted commands
            if (EncryptedCommands.Contains(command))
            {
                // MSG <identifier> <messageId> <Base64(blob)>
                var parts = protocolMessage.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries);
                string identifier = parts.Length > 1 ? parts[1] : "";
                string messageId = parts.Length > 2 ? parts[2] : "";
                string content = parts.Length > 3 ? parts[3] : "";

                byte[] aesKey = await ServerHandler.EncryptionKeyHandler.GetAesKey(senderEndpoint);
                byte[] blob;

                try
                {
                    blob = Convert.FromBase64String(content);
                }
                catch (FormatException)
                {
                    Console.WriteLine($"[WARN] Ongeldige Base64 payload van {senderEndpoint}");
                    return;
                }

                string plaintext;
                try
                {
                    plaintext = SecurityHandler.DecryptMessage(aesKey, blob);
                }
                catch (CryptographicException)
                {
                    Console.WriteLine($"[WARN] Bericht authenticatie van {senderEndpoint} is gefaald!");
                    return;
                }

                switch (command)
                {
                    case "MSG":
                        await HandleMessage(senderEndpoint, clientInfo, command, identifier, messageId, plaintext);
                        break;

                    default:
                        Console.WriteLine($"Onbekend versleuteld commando ontvangen: {command}");
                        break;
                }

                return;
            }

            Console.WriteLine($"Onbekend commando ontvangen: {command}");
        }

        private async Task HandleStatus(IPEndPoint senderEndpoint, ClientState clientInfo, string content)
        {
            // Update de status van de client
            clientInfo.clientStatus = ParseClientStatus(content);
            clientInfo.LastSeen = DateTime.UtcNow;
            ServerHandler.ClientDataInterface.UpdateClientStatus(senderEndpoint, clientInfo);
        }

        private async Task HandleMessage(IPEndPoint senderEndpoint, ClientState clientInfo, string command, string clientName, string messageId, string plaintext)
        {
            if (clientInfo.clientStatus == ClientStatus.Offline)
                return;

            // ACK terugsturen 
            string ackMessage = $"ACK {messageId}";
            await ServerHandler.MessageDataInterface.SendPacketToClient(senderEndpoint, ackMessage);

            // Bericht broadcasten naar andere clients 
            string broadcastMessage = $"MSG {clientName} {plaintext}";
            await ServerHandler.MessageDataInterface.SendPacketToClients(ServerHandler.ClientDataInterface, senderEndpoint, broadcastMessage, encrypt: true, 2);
        }

        private async Task HandlePubKey(IPEndPoint senderEndpoint, ClientState clientInfo, string content)
        {
            byte[] clientPubKey = Convert.FromBase64String(content);

            KeyExchangeMessage keys = await ServerHandler.EncryptionKeyHandler.GenAESKey(senderEndpoint, clientPubKey);
            string msg = $"SETKEY {keys.AESKey}";
            await ServerHandler.MessageDataInterface.SendPacketToClient(senderEndpoint, msg);

        }

        private async Task HandleConnect(IPEndPoint senderEndpoint, ClientState clientInfo, string[] parsedData)
        {
            string Username = parsedData[2];
            string ackId = parsedData[1];

            string formattedName = Username.ToLower();


            if (formattedName == "server" || formattedName.Contains("server"))
            {
                Console.WriteLine("{0} heeft verboden naam ingevoerd. (FLAG)", senderEndpoint);
                await ServerHandler.MessageDataInterface.SendPacketToClient(senderEndpoint, $"SERVERMSG (SERVER) VERBODEN NAAM");
                return;
            }
            else if (formattedName.Contains("(") || formattedName.Contains(")") || formattedName.Contains(" "))
            {
                Console.WriteLine("{0} heeft verboden tekens ingevoerd.", senderEndpoint);
                await ServerHandler.MessageDataInterface.SendPacketToClient(senderEndpoint, $"SERVERMSG (SERVER) Deze tekens zijn niet toegstaan! Dit zijn de enigste toegestane charaters (\"_\",\"-\")");
                return;
            }

            if (!ServerHandler.ClientDataInterface.GetAllClients().ContainsKey(senderEndpoint))
            {
                await ServerHandler.MessageDataInterface.SendPacketToClient(senderEndpoint, $"ACK {ackId}");

                clientInfo.Username = Username;
                clientInfo.clientStatus = ClientStatus.Available;
                clientInfo.LastSeen = DateTime.UtcNow;
                clientInfo.JoinTime = DateTime.UtcNow;
                ServerHandler.ClientDataInterface.UpdateClientStatus(senderEndpoint, clientInfo);

                Console.WriteLine($"Nieuw client toegevoegd: {Username} ({senderEndpoint})");

            }
            else
            {
                await ServerHandler.MessageDataInterface.SendPacketToClient(senderEndpoint, $"ACK {ackId}");

                // voeg client toe als nieuw
                clientInfo.Username = Username;
                clientInfo.clientStatus = ClientStatus.Available;
                clientInfo.LastSeen = DateTime.UtcNow;
                clientInfo.JoinTime = DateTime.UtcNow;
                ServerHandler.ClientDataInterface.UpdateClientStatus(senderEndpoint, clientInfo);

            }
        }

        public ClientStatus ParseClientStatus(string status)
        {
            return status.ToUpper() switch
            {
                "BESCHIKBAAR" => ClientStatus.Available,
                "BUSY" => ClientStatus.Busy,
                "OFFLINE" => ClientStatus.Offline,
                _ => ClientStatus.Offline
            };
        }


    }

}
