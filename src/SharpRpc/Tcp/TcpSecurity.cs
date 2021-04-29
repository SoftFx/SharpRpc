using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public abstract class TcpSecurity
    {
        public static TcpSecurity None { get; } = new NullSecurity();

        internal abstract ValueTask<ByteTransport> SecureTransport(Socket socket, string targetHost);

        private class NullSecurity : TcpSecurity
        {
            internal override ValueTask<ByteTransport> SecureTransport(Socket socket, string targetHost)
            {
                return new ValueTask<ByteTransport>(new TcpTransport(socket));
            }
        }
    }
}
