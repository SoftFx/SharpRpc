using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public abstract class TcpServerSecurity
    {
        public static TcpServerSecurity None { get; } = new NullServerSecurity();

        internal abstract void Init();

        internal abstract ValueTask<ByteTransport> SecureTransport(Socket socket);

        private class NullServerSecurity : TcpServerSecurity
        {
            internal override void Init()
            {
            }

            internal override ValueTask<ByteTransport> SecureTransport(Socket socket)
            {
                return new ValueTask<ByteTransport>(new TcpTransport(socket));
            }
        }
    }
}
