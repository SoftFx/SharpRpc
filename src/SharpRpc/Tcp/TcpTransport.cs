using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal class TcpTransport : ByteTransport
    {
        private readonly Socket _socket;

        public TcpTransport(Socket socket)
        {
            _socket = socket;
        }

        public override Task<int> Receive(ArraySegment<byte> buffer)
        {
            return _socket.ReceiveAsync(buffer, SocketFlags.None);
        }

        public override Task<int> Receive(IList<ArraySegment<byte>> buffers)
        {
            return _socket.ReceiveAsync(buffers, SocketFlags.None);
        }

        public override Task<int> Send(IList<ArraySegment<byte>> data)
        {
            return _socket.SendAsync(data, SocketFlags.None);
        }
    }
}
