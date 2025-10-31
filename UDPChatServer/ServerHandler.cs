using System.Net;
using System.Net.Sockets;
using System.Text;
using UDPChatServer.DataInterfaces;
using UDPChatServer.Datamodels;
using UDPChatServer.Security;

namespace UDPChatServer
{
    internal class ServerHandler
    {
        private UdpClient Server;
        public EncryptionKeyHandler EncryptionKeyHandler { get; }
        public ClientDataInterface ClientDataInterface { get; }
        public MessageDataInterface MessageDataInterface { get; }

        private ServerProtocolHandler protocol;

        public ServerHandler(UdpClient server)
        {

            protocol = new ServerProtocolHandler(this);

            Server = server;

            EncryptionKeyHandler = new();
            ClientDataInterface = new ClientDataInterface();
            MessageDataInterface = new MessageDataInterface(Server, EncryptionKeyHandler);
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


            // FIX: Initialize timestamps for new clients to the current time.
            // Without this, the default DateTime.MinValue makes MonitorClients think
            // the client has already timed out and marks it as offline immediately.
            if (!ClientDataInterface.clients.ContainsKey(iPEndPoint))
            {
                var now = DateTime.UtcNow;
                var s = new ClientState
                {
                    Username = "",
                    clientStatus = ClientStatus.Available,
                    JoinTime = now,
                    LastSeen = now,
                    LastPingTime = now,
                    LastServerPing = now
                };

                ClientDataInterface.clients.TryAdd(iPEndPoint, s);
                Console.WriteLine($"Nieuw client endpoint toegevoegd: {iPEndPoint}");
            }

            ClientState clientInfo = ClientDataInterface.clients[iPEndPoint];

            await protocol.ExecuteCommand(iPEndPoint, clientInfo, message);
        }


        private async Task SendPings(int intervalMs, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var tasks = new List<Task>();

                Console.WriteLine("[PING LOOP] tick in");

                foreach (var client in ClientDataInterface.clients)
                {

                    if (client.Value.clientStatus != ClientStatus.Offline)
                    {
                        var tcs = new TaskCompletionSource<bool>();
                        ClientDataInterface.pendingPongs[client.Key] = tcs;
                        var pingTask = MessageDataInterface.SendPacketToClient(client.Key, "PING");
                        var pongTask = ClientDataInterface.HandlePongs(tcs, intervalMs, client.Key);
                        client.Value.LastServerPing = DateTime.UtcNow;
                        Console.WriteLine($"Bericht gestuurd naar {client.Key} PING {client.Value.Username}");
                        tasks.Add(Task.WhenAll(pingTask, pongTask));
                    }
                }
                try
                {
                    await Task.WhenAll(tasks);
                    Console.WriteLine("[PING LOOP] tick out");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }

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
                foreach (var client in ClientDataInterface.clients)
                {
                    var state = client.Value;
                    var lastPingTime = (now - state.LastPingTime).TotalMilliseconds;
                    var lastJoinTime = (now - state.JoinTime).TotalMilliseconds;
                    var lastServerPingTime = (now - state.LastServerPing).TotalMilliseconds;

                    if (lastPingTime > timeoutMs && lastJoinTime > timeoutMs && lastServerPingTime > timeoutMs)
                    {
                        Console.WriteLine($"{client.Key} lijkt offline. Verbinding wordt nu verbroken!");
                        await MessageDataInterface.SendPacketToClient(client.Key, $"MSG (SERVER) je bent nu offline!"); // Dit is voor als de client zelf de commando '/offline' heeft gebruikt
                        await SendPacketToClients(client.Key, $"MSG (SERVER) {state.Username} is offline");
                        ClientDataInterface.clients.TryRemove(client.Key, out _);
                    }
                }
                await Task.Delay(2500, token);
            }
        }


        public async Task SendPacketToClients(IPEndPoint senderEndpoint, string data, bool encrypt = false, int pos = 0)
        {
            await MessageDataInterface.SendPacketToClients(this.ClientDataInterface, senderEndpoint, data, encrypt, pos);
        }


    }
}
