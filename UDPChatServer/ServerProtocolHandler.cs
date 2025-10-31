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

        /// <summary>
        /// ProtocolMessage gets formatted to an array
        /// </summary>
        /// <param name="ProtocolMessage"></param>
        /// <returns></returns>
        public string[] FormattedPrompt(string ProtocolMessage)
        {
            string[] formattedMessage = ProtocolMessage.Split(" ");

            // Command en name must always be given
            string command = formattedMessage[0];
            string name = formattedMessage[1];

            string messageId = null;
            string data;



            if (command == "MSG" && formattedMessage.Length >= 4)
            {
                // MSG <name> <messageId> <text...>
                messageId = formattedMessage[2];
                data = string.Join(" ", formattedMessage.Skip(3));

                return new string[] { command, name, messageId, data };
            }
            else if (command == "PONG")
            {
                return new string[] { command, name };
            }
            else
            {
                // (andere commands) <name> <data...>
                data = string.Join(" ", formattedMessage.Skip(2));
                return new string[] { command, name, data };
            }

        }

        public async Task ExecuteCommand(IPEndPoint senderEndpoint, ClientState clientInfo, string[] parsedData)
        {
            string command = parsedData[0];
            string clientName = parsedData[1];
            string content = parsedData.Length > 3 ? parsedData[3] : parsedData.Length > 2 ? parsedData[2] : "";

            if (NonEncryptedCommands.Contains(command))
            {
                switch (command)
                {
                    case "CONNECT":
                        await HandleConnect(senderEndpoint, clientInfo, parsedData);
                        return;
                    case "PUBKEY":
                        await HandlePubKey(senderEndpoint, clientInfo, content);
                        return;
                    case "PONG":
                        if (ServerHandler.ClientDataInterface.GetPendingPongs().TryRemove(senderEndpoint, out var tcs))
                            tcs.SetResult(true);
                        clientInfo.LastPingTime = DateTime.UtcNow;
                        return;
                    case "STATUS":
                        await HandleStatus(senderEndpoint, clientInfo, content);
                        break;

                }
            }

            if (EncryptedCommands.Contains(command))
            {
                // Get raw AES key that was stored during key exchange
                byte[] aesKey = await ServerHandler.EncryptionKeyHandler.GetAesKey(senderEndpoint);

                // content now holds Base64(nonce|ciphertext|tag)
                byte[] blob = Convert.FromBase64String(content);
                string plaintext;

                try
                {
                    plaintext = SecurityHandler.DecryptMessage(aesKey, blob);
                }
                catch (CryptographicException)
                {
                    Console.WriteLine($"[WARN] Message authentication failed from {senderEndpoint}");
                    return;
                }

                switch (command)
                {
                    case "MSG":
                        await HandleMessage(senderEndpoint, clientInfo, command, clientName, parsedData[2], plaintext);
                        break;
                }
            }
            else
            {
                Console.WriteLine($"Onbekend commando ontvangen: {command}");
            }


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
            string Username = parsedData[1];
            string ackId = parsedData[2];

            string formattedName = Username.ToLower();

            if (formattedName == "server" || formattedName.Contains("(") || formattedName.Contains(")"))
            {
                await ServerHandler.MessageDataInterface.SendPacketToClient(senderEndpoint, $"MSG (SERVER) VERBODEN NAAM!");
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
