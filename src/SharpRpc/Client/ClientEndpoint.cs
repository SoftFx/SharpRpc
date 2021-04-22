using SharpRpc.Security;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public abstract class ClientEndpoint : Endpoint
    {
        private ClientCredentials _creds = ClientCredentials.None;
        private ServerAuthenticator _authenticator = ServerAuthenticator.None;

        public abstract Task<RpcResult<ByteTransport>> ConnectAsync();

        public ClientCredentials Credentials
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

        public ServerAuthenticator Authenticator
        {
            get => _authenticator;
            set
            {
                lock (_stateLockObj)
                {
                    ThrowIfImmutable();
                    _authenticator = value;
                }
            }
        }
    }
}
