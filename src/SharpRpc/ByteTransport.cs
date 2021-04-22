using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    public abstract class ByteTransport
    {
        public abstract ValueTask Send(ArraySegment<byte> data, CancellationToken cToken);
        public abstract ValueTask<int> Receive(ArraySegment<byte> buffer, CancellationToken cToken);
        public abstract RpcResult TranslateException(Exception ex);

        public abstract Task Shutdown();
        public abstract void Dispose();
    }
}
