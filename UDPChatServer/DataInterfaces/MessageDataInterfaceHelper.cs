using System.Net;
using UDPChatServer.Datamodels;

namespace UDPChatServer.DataInterfaces
{
    internal static class MessageDataInterfaceHelper
    {
        public static async Task SendPacketToClients(this MessageDataInterface messageDataInterface, ClientDataInterface clientDataInterface, IPEndPoint senderEndpoint, string data, bool encrypt = false, int pos = 0)
        {
            var tasks = new List<Task>();

            foreach (var client in clientDataInterface.GetAllClients())
            {
                if (client.Key.Equals(senderEndpoint)) continue;
                var info = client.Value;
                if (info.clientStatus == ClientStatus.Offline) continue;

                tasks.Add(messageDataInterface.SendPacketToClient(client.Key, data, encrypt, pos));
            }

            await Task.WhenAll(tasks);
        }
    }
}
