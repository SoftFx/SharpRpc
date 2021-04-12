using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc.Tcp
{
    internal class SslTransport : ByteTransport
    {
        private readonly SslStream _stream;

        public SslTransport(SslStream stream)
        {
            _stream = stream;
        }

        public override ValueTask<int> Receive(ArraySegment<byte> buffer)
        {
            return _stream.ReadAsync(buffer);
        }

        public override ValueTask Send(ArraySegment<byte> data)
        {
            return _stream.WriteAsync(data);
        }

        public override RpcResult TranslateException(Exception ex)
        {
            return TcpTransport.ToRpcResult(ex);
        }

        public override async Task Shutdown()
        {
            try
            {
                await _stream.ShutdownAsync();
            }
            catch (Exception)
            {
                // TO DO: log
            }
        }

        public override void Dispose()
        {
            _stream.Dispose();
        }
    }
}
