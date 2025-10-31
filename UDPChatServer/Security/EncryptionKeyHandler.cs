using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;

namespace UDPChatServer.Security
{
    internal class EncryptionKeyHandler
    {
        private readonly ConcurrentDictionary<IPEndPoint, byte[]> aesKeys = new();
        private readonly SecurityHandler securityHandler = new();

        public async Task<KeyExchangeMessage> GenAESKey(IPEndPoint endpoint, byte[] clientPublicKey)
        {
            // Generate 256-bit AES key
            byte[] aesKey = RandomNumberGenerator.GetBytes(32);
            aesKeys[endpoint] = aesKey;

            //   Encrypt key with client's RSA pubkey
            using var rsa = RSA.Create();
            rsa.ImportRSAPublicKey(clientPublicKey, out _);
            byte[] encryptedKey = rsa.Encrypt(aesKey, RSAEncryptionPadding.OaepSHA256);


            return new KeyExchangeMessage(encryptedKey);
        }

        public async Task<byte[]> GetAesKey(IPEndPoint endpoint)
        {
            if (!aesKeys.TryGetValue(endpoint, out var key))
                throw new InvalidOperationException("No AES key for this endpoint.");
            return key;
        }
    }
}
