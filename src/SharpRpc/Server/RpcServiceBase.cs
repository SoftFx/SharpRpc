using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public abstract class RpcServiceBase : IUserMessageHandler
    {
        protected abstract ValueTask OnMessage(IMessage message);
        protected abstract ValueTask<IResponse> OnRequest(IRequest message);

        protected ValueTask OnUnknownMessage(IMessage message)
        {
            return new ValueTask();
        }

        protected ValueTask<IResponse> OnUnknownRequest(IRequest message)
        {
            throw new NotImplementedException();
        }

        protected IResponse CreateFaultResponse(Exception ex)
        {
            throw new NotImplementedException();
        }

        ValueTask IUserMessageHandler.ProcessMessage(IMessage message)
        {
            return OnMessage(message);
        }

        ValueTask<IResponse> IUserMessageHandler.ProcessRequest(IRequest message)
        {
            return OnRequest(message);
        }
    }
}
