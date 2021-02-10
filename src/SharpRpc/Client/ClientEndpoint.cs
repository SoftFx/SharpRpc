using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public abstract class ClientEndpoint : Endpoint
    {
        public abstract Task<ByteTransport> ConnectAsync();
    }
}
