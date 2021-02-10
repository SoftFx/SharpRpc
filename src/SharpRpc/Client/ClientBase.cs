using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    public abstract class ClientBase
    {
        public ClientBase(ClientEndpoint endpoint)
        {
            Channel = new Channel(endpoint);
        }

        public Channel Channel { get; }
    }
}
