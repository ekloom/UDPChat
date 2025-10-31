using System.Security.Cryptography;
using System.Text;

namespace UDPChatServer.Security
{
    internal class SecurityHandler
    {
        public const int NonceSize = 12;
        public const int TagSize = 16;

        /// <summary>
        /// Encrypts plaintext with AES-GCM
        /// </summary>
        public static byte[] EncryptMessage(byte[] aesKey, string plaintext, byte[]? associatedData = null)
        {
            if (aesKey is null) throw new ArgumentNullException(nameof(aesKey));
            if (plaintext is null) throw new ArgumentNullException(nameof(plaintext));

            byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
            byte[] plain = Encoding.UTF8.GetBytes(plaintext);
            byte[] cipher = new byte[plain.Length];
            byte[] tag = new byte[TagSize];

            using var gcm = new AesGcm(aesKey, TagSize);
            gcm.Encrypt(nonce, plain, cipher, tag, associatedData);

            byte[] output = new byte[NonceSize + cipher.Length + TagSize];
            Buffer.BlockCopy(nonce, 0, output, 0, NonceSize);
            Buffer.BlockCopy(cipher, 0, output, NonceSize, cipher.Length);
            Buffer.BlockCopy(tag, 0, output, NonceSize + cipher.Length, TagSize);
            return output;
        }

        /// <summary>
        /// Decrypts a blob produced by EncryptMessage
        /// Throws CryptographicException if authentication fails.
        /// </summary>
        public static string DecryptMessage(byte[] aesKey, byte[] blob, byte[]? associatedData = null)
        {
            if (aesKey is null) throw new ArgumentNullException(nameof(aesKey));
            if (blob is null) throw new ArgumentNullException(nameof(blob));
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
            // Will throw CryptographicException if tampered or wrong key
            gcm.Decrypt(nonce, cipher, tag, plain, associatedData);

            return Encoding.UTF8.GetString(plain);
        }
    }
}
