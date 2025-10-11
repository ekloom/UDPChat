using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;

namespace UDPChatServer.Security
{
    internal class EncryptionKeyHandler
    {


        private ConcurrentDictionary<IPEndPoint, KeyExchangeMessage> keys;

        /// <summary>
        /// Deze klasse zorgt ervoor dat de AES(symmetrisch) key en IV veilig naar de client gestuurd kan worden
        /// </summary>
        public EncryptionKeyHandler()
        {
            keys = new();
        }

        /// <summary>
        /// Genereert een AES(symmetrisch) key en IV en encrypte het met de RSA(asymmetrisch) PublicKey zodat allpeen de client deze key kan decrypten
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="clientPubKey"></param>
        /// <returns></returns>
        public async Task<KeyExchangeMessage> GenAESKeyandIV(IPEndPoint sender, byte[] clientPubKey)
        {
            using var rsa = RSA.Create();
            rsa.ImportRSAPublicKey(clientPubKey, out _);

            using var aes = Aes.Create();
            aes.GenerateKey();
            aes.GenerateIV();

            byte[] encKey = rsa.Encrypt(aes.Key, RSAEncryptionPadding.OaepSHA256); // encrypted key met publieke key van client
            byte[] encIV = rsa.Encrypt(aes.IV, RSAEncryptionPadding.OaepSHA256); // encrypted key met publieke key van client

            keys[sender] = new KeyExchangeMessage(aes.Key, aes.IV);

            return new KeyExchangeMessage(encKey, encIV);
        }


        public async Task<KeyExchangeMessage> GetKeyExchangeMessage(IPEndPoint sender)
        {
            return keys[sender];
        }

        public bool HasKeys(IPEndPoint sender) => keys.ContainsKey(sender);
    }
}
