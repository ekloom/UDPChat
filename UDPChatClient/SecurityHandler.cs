using System.Security.Cryptography;
using System.Text;


namespace UDPChatClient
{
    internal class SecurityHandler
    {
        static RSA rsa;  // asymmetrisch voor sessiesleutel
        static byte[] aesKey;
        static byte[] aesIV;
        public byte[] PublicKey => rsa.ExportRSAPublicKey();
        public byte[] PrivateKey => rsa.ExportRSAPrivateKey();

        public SecurityHandler()
        {
            rsa = RSA.Create(2048);
        }

        public void SetAESKeyAndIV(byte[] key, byte[] iv)
        {
            aesKey = key;
            aesIV = iv;
        }

        public byte[] DecryptWithPrivateKey(byte[] data)
        {
            return rsa.Decrypt(data, RSAEncryptionPadding.OaepSHA256);
        }

        public byte[] EncryptMessage(string plaintext)
        {
            using var aes = Aes.Create();
            aes.Key = aesKey;
            aes.IV = aesIV;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            return encryptor.TransformFinalBlock(Encoding.UTF8.GetBytes(plaintext), 0, plaintext.Length);
        }

        public string DecryptMessage(byte[] ciphertext)
        {
            using var aes = Aes.Create();
            aes.Key = aesKey;
            aes.IV = aesIV;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            return Encoding.UTF8.GetString(decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length));
        }


    }
}
