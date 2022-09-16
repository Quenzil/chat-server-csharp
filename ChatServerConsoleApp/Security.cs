using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace ChatServerConsoleApp
{
    public class Security
    {
        public string pubKeyAsString;
        private RSACryptoServiceProvider RSA;
        public RSAParameters RSAParams;

        public Security()
        {
            RSA = new RSACryptoServiceProvider();
            RSAParams = RSA.ExportParameters(false);
            pubKeyAsString = PublicKeyAsString();
        }

        public string PublicKeyAsString()
        {
            //Create separate string representations of Modulus and Exponent;
            StringBuilder sb = new StringBuilder();
            string modulus, exponent;
            foreach (var item in RSAParams.Modulus)
            {
                sb.Append(item + ",");
            }
            sb.Remove(sb.Length - 1, 1);
            modulus = sb.ToString();

            sb.Clear();
            foreach (var item in RSAParams.Exponent)
            {
                sb.Append(item + ",");
            }
            sb.Remove(sb.Length - 1, 1);
            exponent = sb.ToString();
            sb.Clear();

            return modulus + " " + exponent;
        }

        public string RSADecrypt(string DataToDecrypt)
        {
            try
            {
                //Split encrypted string message back into byte[];

                byte[] dataToDecrypt = Array.ConvertAll(DataToDecrypt.Split(','), Byte.Parse);
                byte[] decryptedData;

                //Decrypt the encrypted data using the server's RSA private key.
                decryptedData = RSA.Decrypt(dataToDecrypt, false);

                UnicodeEncoding ByteConverter = new UnicodeEncoding();

                string temp = ByteConverter.GetString(decryptedData);

                return temp;
            }
            catch (CryptographicException e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
                return null;
            }
        }

        public string RSAEncrypt(string DataToEncrypt, RSAParameters RSAKeyInfo)
        {
            try
            {
                string s;

                //Convert message to byte[];
                UnicodeEncoding ByteConverter = new UnicodeEncoding();

                byte[] dataToEncrypt = ByteConverter.GetBytes(DataToEncrypt);
                byte[] encryptedData;

                //Encrypt the data using the RSAKeyInfo (public key) of the sender;
                using (RSACryptoServiceProvider RSA = new RSACryptoServiceProvider())
                {
                    RSA.ImportParameters(RSAKeyInfo);
                    encryptedData = RSA.Encrypt(dataToEncrypt, false);
                }

                //Convert encrypted byte[] back to string.
                StringBuilder sb = new StringBuilder();
                foreach (var item in encryptedData)
                {
                    sb.Append(item + ",");
                }
                sb.Remove(sb.Length - 1, 1);
                s = sb.ToString();

                return s;
            }
            catch (CryptographicException e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
                return null;
            }
        }
    }
}
