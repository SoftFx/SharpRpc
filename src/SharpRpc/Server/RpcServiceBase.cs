using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public abstract class RpcServiceBase : IMessageHandler
    {
        protected abstract Task OnMessage(IMessage message);
        //protected abstract Task OnRequest(IRequest request);

        protected Task OnUnknownMessage(IMessage message)
        {
            return Task.CompletedTask;
        }

        Task IMessageHandler.ProcessMessage(IMessage message)
        {
            try
            {
                return OnMessage(message)
                    .ContinueWith(t =>
                    {

                    });
            }
            catch (Exception ex)
            {
                return Task.CompletedTask;
            }
        }
    }
}
