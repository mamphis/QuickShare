namespace qs.Utils
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;

    internal static class Encryption
    {
        internal static (string privateKey, string publicKey) GetKeyPair()
        {
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            return (rsa.ToXmlString(true), rsa.ToXmlString(false));
        }

        internal static (string encrData, byte[] key, byte[] iv) GetEncryptedSymmetricKey(string publicKey)
        {
            Aes aes = Aes.Create("AesManaged");
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            rsa.FromXmlString(publicKey);

            byte[] key = aes.Key;
            byte[] iv = aes.IV;

            List<byte> vs = new List<byte>
            {
                (byte)key.Length
            };
            vs.AddRange(key);
            vs.Add((byte)iv.Length);
            vs.AddRange(iv);

            return (Convert.ToBase64String(rsa.Encrypt(vs.ToArray(), false)), key, iv);
        }

        internal static (byte[] key, byte[] iv) GetDecryptedSymmetricKey(string encryptedData, string privateKey)
        {
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            rsa.FromXmlString(privateKey);

            byte[] vs = rsa.Decrypt(Convert.FromBase64String(encryptedData), false);

            byte[] key = vs.Skip(1).Take(vs[0]).ToArray();
            byte[] iv = vs.Skip(1 + vs[0] + 1).ToArray();

            return (key, iv);
        }

        internal static byte[] Encrypt(byte[] data, (byte[] key, byte[] iv) aesParams)
        {
            return AesCryptographyService.Encrypt(data, aesParams.key, aesParams.iv);
        }

        internal static byte[] Decrypt(byte[] data, (byte[] key, byte[] iv) aesParams)
        {
            return AesCryptographyService.Decrypt(data, aesParams.key, aesParams.iv);
        }

        internal static byte[] GetHash(byte[] data)
        {
            return SHA256.Create().ComputeHash(data);
        }

        internal static class AesCryptographyService
        {
            internal static byte[] Encrypt(byte[] data, byte[] key, byte[] iv)
            {
                using (Aes aes = Aes.Create())
                {
                    aes.KeySize = 128;
                    aes.BlockSize = 128;
                    aes.Padding = PaddingMode.PKCS7;

                    aes.Key = key;
                    aes.IV = iv;

                    using (ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                    {
                        return PerformCryptography(data, encryptor);
                    }
                }
            }

            internal static byte[] Decrypt(byte[] data, byte[] key, byte[] iv)
            {
                using (Aes aes = Aes.Create())
                {
                    aes.KeySize = 128;
                    aes.BlockSize = 128;
                    aes.Padding = PaddingMode.PKCS7;

                    aes.Key = key;
                    aes.IV = iv;

                    using (ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                    {
                        return PerformCryptography(data, decryptor);
                    }
                }
            }

            private static byte[] PerformCryptography(byte[] data, ICryptoTransform cryptoTransform)
            {
                using (MemoryStream ms = new MemoryStream())
                using (CryptoStream cryptoStream = new CryptoStream(ms, cryptoTransform, CryptoStreamMode.Write))
                {
                    cryptoStream.Write(data, 0, data.Length);
                    cryptoStream.FlushFinalBlock();

                    return ms.ToArray();
                }
            }
        }
    }
}
