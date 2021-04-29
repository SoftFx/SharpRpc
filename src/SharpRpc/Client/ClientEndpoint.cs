using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public abstract class ClientEndpoint : Endpoint
    {
        private Credentials _creds = Credentials.None;

        public abstract Task<RpcResult<ByteTransport>> ConnectAsync();

        public Credentials Credentials
        {
            get => _creds;
            set
            {
                lock (_stateLockObj)
                {
                    ThrowIfImmutable();
                    _creds = value;
                }
            }
        }
    }
}
