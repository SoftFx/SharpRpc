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
            Channel = new Channel(endpoint, new MsgHandler());
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

        protected void Call(IRequest request)
        {
            throw new NotImplementedException();
        }

        protected T Call<T>(IRequest request)
        {
            throw new NotImplementedException();
        }

        protected RpcResult TryCall(IRequest request, out RpcResult result)
        {
            throw new NotImplementedException();
        }

        protected RpcResult<T> TryCall<T>(IRequest request, out RpcResult result)
        {
            throw new NotImplementedException();
        }

        protected Task CallAsync(IRequest request)
        {
            throw new NotImplementedException();
        }

        protected Task<T> CallAsync<T>(IRequest request)
        {
            throw new NotImplementedException();
        }

        protected Task<RpcResult> TryCallAsync(IRequest request)
        {
            throw new NotImplementedException();
        }

        protected Task<RpcResult<T>> TryCallAsync<T>(IRequest request)
        {
            throw new NotImplementedException();
        }

        private class MsgHandler : IMessageHandler
        {
            public Task ProcessMessage(IMessage message)
            {
                return Task.CompletedTask;
            }
        }
    }
}
