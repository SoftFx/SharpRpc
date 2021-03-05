using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public abstract class RpcServiceBase : IMessageHandler
    {
        protected abstract ValueTask OnMessage(IMessage message);
        //protected abstract Task OnRequest(IRequest request);

        protected ValueTask OnUnknownMessage(IMessage message)
        {
            return new ValueTask();
        }

        ValueTask IMessageHandler.ProcessMessage(IMessage message)
        {
            return OnMessage(message);
        }
    }
}
