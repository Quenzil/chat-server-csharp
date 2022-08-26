using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace ChatServerConsoleApp
{
    public class ClientInfo
    {
        public TcpClient client;
        public StreamReader reader;
        public StreamWriter writer;
        public Thread thread;
        public string nick;
        public string email;
        public Account linkedAccount;

        public RSAParameters RSAParams;

        public ClientInfo(TcpClient Client, StreamReader Reader, StreamWriter Writer, Thread Thread, string NickName, string Email, byte[] Modulus, byte[] Exponent)
        {
            client = Client;
            reader = Reader;
            writer = Writer;
            thread = Thread;
            nick = NickName;
            email = Email;

            RSAParams.Modulus = Modulus;
            RSAParams.Exponent = Exponent;
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

    }
}
