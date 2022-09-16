using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChatServerConsoleApp
{
    //Wrapper class to expose the "Active" boolean.
    public class TcpListenerWrapper : TcpListener
    {
        public TcpListenerWrapper(IPEndPoint localEP) : base(localEP)
        {

        }

        public TcpListenerWrapper(IPAddress localaddr, int port) : base(localaddr, port)
        {

        }

        public new bool Active
        {
            get { return base.Active; }
        }
    }
}
