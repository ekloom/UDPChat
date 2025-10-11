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
                await server.SendAsync(data, data.Length, iPEndPoint);
            }
            catch (SocketException)
            {
                throw;
            }
        }

    }
}
