using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Configuration;

namespace Quote_To_Deal.Common
{
    public class EncryptDecrypt
    {

        private static byte[] _key;
        public EncryptDecrypt(string key)
        {
            _key = Encoding.ASCII.GetBytes(key);
        }

        public static string Encrypt(string originalString)
        {
            byte[] bytes = _key;
            try
            {
                if (String.IsNullOrEmpty(originalString))
                {
                    throw new ArgumentNullException("The string which needs to be encrypted can not be null.");
                }

                DESCryptoServiceProvider cryptoProvider = new DESCryptoServiceProvider();
                MemoryStream memoryStream = new MemoryStream();
                CryptoStream cryptoStream = new CryptoStream(memoryStream, cryptoProvider.CreateEncryptor(bytes, bytes), CryptoStreamMode.Write);

                StreamWriter writer = new StreamWriter(cryptoStream);
                writer.Write(originalString);
                writer.Flush();
                cryptoStream.FlushFinalBlock();
                writer.Flush();

                return Convert.ToBase64String(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
            }
            catch (Exception ex)
            { throw ex; }
        }
        public static string Decrypt(string cryptedString)
        {
            byte[] bytes = _key;
            try
            {
                cryptedString = cryptedString.Replace(" ", "+");

                if (string.IsNullOrEmpty(cryptedString))
                {
                    throw new ArgumentNullException("The string which needs to be decrypted can not be null.");
                }

                DESCryptoServiceProvider cryptoProvider = new DESCryptoServiceProvider();
                MemoryStream memoryStream = new MemoryStream(Convert.FromBase64String(cryptedString));
                CryptoStream cryptoStream = new CryptoStream(memoryStream, cryptoProvider.CreateDecryptor(bytes, bytes), CryptoStreamMode.Read);
                StreamReader reader = new StreamReader(cryptoStream);

                return reader.ReadToEnd();
            }
            catch (Exception ex)
            { throw ex; }
        }
    }
}