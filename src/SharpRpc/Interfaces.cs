using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public abstract class ByteTransport
    {
        public abstract Task<int> Send(IList<ArraySegment<byte>> data);
        public abstract Task<int> Receive(ArraySegment<byte> buffer);
        public abstract Task<int> Receive(IList<ArraySegment<byte>> buffers);
    }
}
