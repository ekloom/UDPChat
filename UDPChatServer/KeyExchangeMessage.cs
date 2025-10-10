namespace UDPChatServer
{
    internal class KeyExchangeMessage
    {

        public string AESKey { get; }
        public string AESIV { get; }

        public KeyExchangeMessage(byte[] Key, byte[] IV)
        {
            this.AESKey = Convert.ToBase64String(Key);
            this.AESIV = Convert.ToBase64String(IV);
        }

    }
}