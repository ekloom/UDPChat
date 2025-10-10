using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace UDPChatServer
{
    internal class ServerHandler
    {
        ConcurrentDictionary<IPEndPoint, ClientState> clients;
        ConcurrentDictionary<IPEndPoint, TaskCompletionSource<bool>> pendingPongs;
        public EncryptionKeyHandler encryptionKeyHandler { get; }
        ServerProtocolHandler protocol;
        UdpClient Server;
        public ServerHandler(UdpClient server)
        {
            clients = new ConcurrentDictionary<IPEndPoint, ClientState>();
            protocol = new ServerProtocolHandler(this);

            Server = server;
            pendingPongs = new ConcurrentDictionary<IPEndPoint, TaskCompletionSource<bool>>();
            encryptionKeyHandler = new();
        }

        public async Task StartAsync(CancellationTokenSource cts)
        {
            var ct = cts.Token;

            int pingInterval = 5000;


            _ = SendPings(pingInterval, ct);
            _ = MonitorClients(pingInterval, ct);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await Server.ReceiveAsync(ct);
                    string txt = Encoding.UTF8.GetString(result.Buffer);

                    Console.WriteLine($"Bericht ontvangen van {result.RemoteEndPoint} {txt}");

                    await Process(result.RemoteEndPoint, txt);
                }
                catch (SocketException ex)
                {

                }

            }

        }

        async Task Process(IPEndPoint iPEndPoint, string message)
        {

            if (!clients.ContainsKey(iPEndPoint))
            {
                clients.TryAdd(iPEndPoint, new ClientState());
                Console.WriteLine($"Nieuw client endpoint toegevoegd: {iPEndPoint}");
            }

            ClientState clientInfo = clients[iPEndPoint];

            string[] parsed = protocol.FormattedPrompt(message);
            await protocol.ExecuteCommand(iPEndPoint, clientInfo, parsed);
        }

        public void AddToClients(IPEndPoint iPEndPoint)
        {
            clients.TryAdd(iPEndPoint, new ClientState());
        }

        public void UpdateClientStatus(IPEndPoint iPEndPoint, ClientState clientInfo)
        {
            clients[iPEndPoint] = clientInfo;
        }

        private async Task SendPings(int intervalMs, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var tasks = new List<Task>();

                foreach (var client in clients)
                {

                    if (client.Value.clientStatus != ClientStatus.Offline)
                    {
                        var tcs = new TaskCompletionSource<bool>();
                        pendingPongs[client.Key] = tcs;
                        var pingTask = SendDataToClient(client.Key, "PING");
                        var pongTask = HandlePongs(tcs, intervalMs, client.Key);
                        client.Value.LastServerPing = DateTime.UtcNow;
                        Console.WriteLine($"Bericht gestuurd naar {client.Key} PING {client.Value.Username}");
                        tasks.Add(Task.WhenAll(pingTask, pongTask));
                    }
                }

                await Task.WhenAll(tasks);

                await Task.Delay(intervalMs, token);
            }
        }

        private async Task MonitorClients(int pingIntervalMs, CancellationToken token)
        {
            /*
                timeoutMs = maximale tijd zonder PONG. Wordt gebruikt door SendPings en MonitorClients.
                Interval wordt met 2.5 vermenigvuldigd om een consistente timing tussen pings en monitoring te krijgen.
                Task.Delay in SendPings = intervalMs, Task.Delay voor de task zelf (HandlePongs) = intervalMs
                Dus timeout is minimaal (intervalMs * 2) lang en om de timing consistent te krijgen doe ik nog + 2500
            */
            int timeoutMs = (pingIntervalMs * 2) + 2500;

            while (!token.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;
                foreach (var client in clients)
                {
                    var state = client.Value;
                    var lastPingTime = (now - state.LastPingTime).TotalMilliseconds;
                    var lastJoinTime = (now - state.JoinTime).TotalMilliseconds;
                    var lastServerPingTime = (now - state.LastServerPing).TotalMilliseconds;

                    if (lastPingTime > timeoutMs && lastJoinTime > timeoutMs && lastServerPingTime > timeoutMs)
                    {
                        Console.WriteLine($"{client.Key} lijkt offline. Verbinding wordt nu verbroken!");
                        await SendDataToClient(client.Key, $"MSG (SERVER) je bent nu offline!"); // Dit is voor als de client zelf de commando '/offline' heeft gebruikt
                        await SendDataToClients(client.Key, $"MSG (SERVER) {state.Username} is offline");
                        clients.TryRemove(client.Key, out _);
                    }
                }
                await Task.Delay(2500, token);
            }
        }

        public ConcurrentDictionary<IPEndPoint, TaskCompletionSource<bool>> GetPendingPongs()
        {
            return pendingPongs;
        }

        async Task HandlePongs(TaskCompletionSource<bool> tcs, int timeout, IPEndPoint senderEndPoint)
        {
            try
            {
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeout));

                if (completedTask != tcs.Task)
                {
                    if (clients.TryGetValue(senderEndPoint, out var state))
                    {
                        state.clientStatus = ClientStatus.Offline;
                    }
                }
            }
            finally
            {
                pendingPongs.TryRemove(senderEndPoint, out _);
            }
        }


        public async Task SendDataToClient(IPEndPoint clientEP, string data, bool encrypt = false, int pos = 0)
        {
            if (encrypt && encryptionKeyHandler.HasKeys(clientEP))
            {
                var keys = await encryptionKeyHandler.GetKeyExchangeMessage(clientEP);
                byte[] key = Convert.FromBase64String(keys.AESKey);
                byte[] iv = Convert.FromBase64String(keys.AESIV);

                byte[] msgBytes;

                if (pos == 0)
                {
                    // Encrypt entire message
                    msgBytes = SecurityHandler.EncryptMessage(key, iv, data);
                }
                else
                {
                    string[] parts = data.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    if (pos >= 0 && pos < parts.Length)
                    {
                        byte[] encryptedPart = SecurityHandler.EncryptMessage(key, iv, parts[pos]);
                        string encryptedBase64 = Convert.ToBase64String(encryptedPart);
                        parts[pos] = encryptedBase64;
                    }

                    // Recombine all parts back with spaces
                    string finalMessage = string.Join(" ", parts);
                    msgBytes = Encoding.UTF8.GetBytes(finalMessage);
                }

                await SendRawData(clientEP, msgBytes);
            }
            else
            {
                // No encryption, send as-is
                byte[] msgBytes = Encoding.UTF8.GetBytes(data);
                await SendRawData(clientEP, msgBytes);
            }
        }



        public async Task SendRawData(IPEndPoint iPEndPoint, byte[] data)
        {
            try
            {
                await Server.SendAsync(data, data.Length, iPEndPoint);
            }
            catch (SocketException)
            {
                throw;
            }
        }

        public async Task SendDataToClients(IPEndPoint senderEndpoint, string data, bool encrypt = false, int pos = 0)
        {
            var tasks = new List<Task>();

            foreach (var client in GetAllClients())
            {
                if (client.Key.Equals(senderEndpoint)) continue;
                var info = client.Value;
                if (info.clientStatus == ClientStatus.Offline) continue;

                tasks.Add(SendDataToClient(client.Key, data, encrypt, pos));
            }

            await Task.WhenAll(tasks);
        }



        public ConcurrentDictionary<IPEndPoint, ClientState> GetAllClients()
        {
            return clients;
        }


    }
}
