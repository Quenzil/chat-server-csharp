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

namespace ChatServerConsoleApp
{
    public class ClientInfo
    {
        public TcpClient client;
        public StreamReader reader;
        public StreamWriter writer;
        public Thread thread;
        public string nick;
        public Account linkedAccount;

        public ClientInfo(TcpClient Client, StreamReader Reader, StreamWriter Writer, Thread Thread, string NickName)
        {
            client = Client;
            reader = Reader;
            writer = Writer;
            thread = Thread;
            nick = NickName;
        }

    }
}
