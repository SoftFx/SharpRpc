using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public abstract class ClientBase
    {
        public ClientBase(ClientEndpoint endpoint)
        {
            Channel = new Channel(endpoint);
        }

        public Channel Channel { get; }

        protected void SendMessage(IMessage message)
        {
            Channel.TrySend(message);
        }

        protected RpcResult TrySendMessage(IMessage message)
        {
            return Channel.TrySend(message);
        }

        protected ValueTask<RpcResult> TrySendMessageAsync(IMessage message)
        {
            return Channel.TrySendAsync(message);
        }

        protected ValueTask SendMessageAsync(IMessage message)
        {
            return Channel.SendAsync(message);
        }
    }
}
