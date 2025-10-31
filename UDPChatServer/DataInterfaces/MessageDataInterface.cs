using System.Net;
using System.Net.Sockets;
using System.Text;
using UDPChatServer.Security;

namespace UDPChatServer.DataInterfaces
{
    internal class MessageDataInterface
    {
        private readonly UdpClient server;
        private readonly EncryptionKeyHandler encryptionKeyHandler;


        public MessageDataInterface(UdpClient server, EncryptionKeyHandler encryptionKeyHandler)
        {
            this.server = server;
            this.encryptionKeyHandler = encryptionKeyHandler;
        }

        public async Task SendPacketToClient(IPEndPoint clientEP, string data, bool encrypt = false, int pos = 0)
        {
            if (encrypt)
            {
                var key = await encryptionKeyHandler.GetAesKey(clientEP);

                string[] parts = data.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                // Guard the index
                if (pos >= 0 && pos < parts.Length)
                {
                    // Encrypt ONLY the target field (e.g., parts[2] for "MSG <name> <payload>")
                    byte[] blob = SecurityHandler.EncryptMessage(key, parts[pos]);
                    parts[pos] = Convert.ToBase64String(blob);
                }

                // Recombine
                string finalMessage = string.Join(" ", parts);
                byte[] msgBytes = Encoding.UTF8.GetBytes(finalMessage);
                await SendRawData(clientEP, msgBytes);

            }
            else
            {
                // No encryption, send as it is
                byte[] msgBytes = Encoding.UTF8.GetBytes(data);
                await SendRawData(clientEP, msgBytes);
            }
        }



        public async Task SendRawData(IPEndPoint iPEndPoint, byte[] data)
        {
            try
            {
                await server.SendAsync(data, data.Length, iPEndPoint);
            }
            catch (SocketException)
            {
                throw;
            }
        }

    }
}
