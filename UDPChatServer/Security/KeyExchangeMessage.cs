namespace UDPChatServer.Security
{
    internal class KeyExchangeMessage
    {

        public string AESKey { get; }
        public string AESIV { get; }

        public KeyExchangeMessage(byte[] Key, byte[] IV)
        {
            AESKey = Convert.ToBase64String(Key);
            AESIV = Convert.ToBase64String(IV);
        }

    }
}