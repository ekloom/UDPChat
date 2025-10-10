using System.Security.Cryptography;
using System.Text;

namespace UDPChatServer
{
    internal class SecurityHandler
    {

        public static byte[] EncryptMessage(byte[] aesKey, byte[] aesIV, string plaintext)
        {
            using var aes = Aes.Create();
            aes.Key = aesKey;
            aes.IV = aesIV;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            return encryptor.TransformFinalBlock(Encoding.UTF8.GetBytes(plaintext), 0, plaintext.Length);
        }

        public static string DecryptMessage(byte[] aesKey, byte[] aesIV, byte[] ciphertext)
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
