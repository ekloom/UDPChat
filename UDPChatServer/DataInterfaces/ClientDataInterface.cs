using System.Collections.Concurrent;
using System.Net;
using UDPChatServer.Datamodels;

namespace UDPChatServer.DataInterfaces
{
    internal class ClientDataInterface
    {
        public ConcurrentDictionary<IPEndPoint, ClientState> clients { get; }
        public ConcurrentDictionary<IPEndPoint, TaskCompletionSource<bool>> pendingPongs { get; }

        public ClientDataInterface()
        {
            clients = new ConcurrentDictionary<IPEndPoint, ClientState>();
            pendingPongs = new ConcurrentDictionary<IPEndPoint, TaskCompletionSource<bool>>();

        }

        public void AddToClients(IPEndPoint iPEndPoint)
        {
            clients.TryAdd(iPEndPoint, new ClientState());
        }

        public void UpdateClientStatus(IPEndPoint iPEndPoint, ClientState clientInfo)
        {
            clients[iPEndPoint] = clientInfo;
        }


        public ConcurrentDictionary<IPEndPoint, TaskCompletionSource<bool>> GetPendingPongs()
        {
            return pendingPongs;
        }

        public ConcurrentDictionary<IPEndPoint, ClientState> GetAllClients()
        {
            return clients;
        }

        public async Task HandlePongs(TaskCompletionSource<bool> tcs, int timeout, IPEndPoint senderEndPoint)
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


    }
}
