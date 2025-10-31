namespace UDPChatServer.Security
{
    internal class KeyExchangeMessage
    {
        public string AESKey { get; }

        public KeyExchangeMessage(byte[] key)
        {
            AESKey = Convert.ToBase64String(key);
        }
    }
}
