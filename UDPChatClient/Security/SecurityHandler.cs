using System.Security.Cryptography;
using System.Text;

namespace UDPChatClient.Security
{
    internal class SecurityHandler
    {
        static RSA rsa;
        static byte[] aesKey;

        public byte[] PublicKey => rsa.ExportRSAPublicKey();
        public byte[] PrivateKey => rsa.ExportRSAPrivateKey();


        private const int NonceSize = 12; // 96-bit nonce
        private const int TagSize = 16; // 128-bit tag

        public SecurityHandler()
        {
            rsa = RSA.Create(2048);
        }

        public void SetKey(string base64EncryptedKey)
        {
            byte[] encryptedKey = Convert.FromBase64String(base64EncryptedKey);
            byte[] rawAesKey = rsa.Decrypt(encryptedKey, RSAEncryptionPadding.OaepSHA256);
            aesKey = rawAesKey;
        }

        public byte[] DecryptWithPrivateKey(byte[] data)
        {
            return rsa.Decrypt(data, RSAEncryptionPadding.OaepSHA256);
        }

        /// <summary>
        /// Encrypts plaintext and returns Base64([nonce|cipher|tag]).
        /// </summary>
        public string EncryptForWire(string plaintext, byte[]? aad = null)
        {
            if (aesKey == null) throw new InvalidOperationException("AES key not set.");
            if (plaintext == null) throw new ArgumentNullException(nameof(plaintext));

            byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
            byte[] plain = Encoding.UTF8.GetBytes(plaintext);
            byte[] cipher = new byte[plain.Length];
            byte[] tag = new byte[TagSize];

            using var gcm = new AesGcm(aesKey, TagSize);
            gcm.Encrypt(nonce, plain, cipher, tag, aad);

            byte[] blob = new byte[NonceSize + cipher.Length + TagSize];
            Buffer.BlockCopy(nonce, 0, blob, 0, NonceSize);
            Buffer.BlockCopy(cipher, 0, blob, NonceSize, cipher.Length);
            Buffer.BlockCopy(tag, 0, blob, NonceSize + cipher.Length, TagSize);

            return Convert.ToBase64String(blob);
        }

        /// <summary>
        /// Decrypts a blob produced by EncryptForWire input is raw bytes [nonce|cipher|tag].
        /// </summary>
        public string DecryptMessage(byte[] blob, byte[]? aad = null)
        {
            if (aesKey == null) throw new InvalidOperationException("AES key not set.");
            if (blob == null) throw new ArgumentNullException(nameof(blob));
            if (blob.Length < NonceSize + TagSize) throw new ArgumentException("Blob too small.", nameof(blob));

            int cipherLen = blob.Length - NonceSize - TagSize;

            byte[] nonce = new byte[NonceSize];
            byte[] cipher = new byte[cipherLen];
            byte[] tag = new byte[TagSize];

            Buffer.BlockCopy(blob, 0, nonce, 0, NonceSize);
            Buffer.BlockCopy(blob, NonceSize, cipher, 0, cipherLen);
            Buffer.BlockCopy(blob, NonceSize + cipherLen, tag, 0, TagSize);

            byte[] plain = new byte[cipherLen];

            using var gcm = new AesGcm(aesKey, TagSize);
            gcm.Decrypt(nonce, cipher, tag, plain, aad); // throws CryptographicException on tamper
            return Encoding.UTF8.GetString(plain);
        }
    }
}
