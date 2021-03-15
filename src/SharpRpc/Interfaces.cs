using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    public abstract class ByteTransport
    {
        public abstract Task<int> Send(IList<ArraySegment<byte>> data, CancellationToken cancelToken);
        public abstract Task<int> Receive(IList<ArraySegment<byte>> buffers);
        public abstract RpcResult TranslateException(Exception ex);

        public abstract Task Shutdown();
        public abstract void Dispose();
    }
}
