// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public abstract class ClientBase
    {
        public ClientBase(ClientEndpoint endpoint, ContractDescriptor descriptor)
        {
            Channel = new Channel(false, endpoint, descriptor, new NullHandler());
        }

        public ClientBase(ClientEndpoint endpoint, ContractDescriptor descriptor, RpcServiceBase callbackHandler)
        {
            Channel = new Channel(false, endpoint, descriptor, callbackHandler ?? throw new ArgumentNullException("callbackHandler"));
        }

        public ClientBase(Channel channel)
        {
            Channel = channel ?? throw new ArgumentNullException("channel");
        }

        public Channel Channel { get; }

        protected void SendMessage(IMessage message)
        {
            Channel.Tx.Send(message);
        }

        protected RpcResult TrySendMessage(IMessage message)
        {
            return Channel.Tx.TrySend(message);
        }

#if NET5_0_OR_GREATER
        protected ValueTask<RpcResult> TrySendMessageAsync(IMessage message)
#else
        protected Task<RpcResult> TrySendMessageAsync(IMessage message)
#endif
        {
            return Channel.Tx.TrySendAsync(message);
        }

#if NET5_0_OR_GREATER
        protected ValueTask SendMessageAsync(IMessage message)
#else
        protected Task SendMessageAsync(IMessage message)
#endif
        {
            return Channel.Tx.SendAsync(message);
        }

        protected Task CallAsync<TResp>(IRequest requestMessage)
            where TResp : IResponse
        {
            return Channel.Dispatcher.Call<TResp>(requestMessage);
        }

        protected Task<T> CallAsync<T, TResp>(IRequest requestMessage)
            where TResp : IResponse
        {
            return Channel.Dispatcher.Call<TResp, T>(requestMessage);
        }

        protected Task<RpcResult> TryCallAsync<TResp>(IRequest requestMsg)
            where TResp : IResponse
        {
            return Channel.Dispatcher.TryCall<TResp>(requestMsg);
        }

        protected Task<RpcResult<T>> TryCallAsync<T, TResp>(IRequest requestMsg)
            where TResp : IResponse
        {
            return Channel.Dispatcher.TryCall<TResp, T>(requestMsg);
        }

        private class NullHandler : IUserMessageHandler
        {
            public void Init(Channel ch)
            {
            }

#if NET5_0_OR_GREATER
            public ValueTask ProcessMessage(IMessage message)
#else
            public Task ProcessMessage(IMessage message)
#endif
            {
                throw new RpcException("No message handler for " + message.GetType().Name, RpcRetCode.UnexpectedMessage);
            }

#if NET5_0_OR_GREATER
            public ValueTask<IResponse> ProcessRequest(IRequest message)
#else
            public Task<IResponse> ProcessRequest(IRequest message)
#endif
            {
                throw new RpcException("No message handler for " + message.GetType().Name, RpcRetCode.UnexpectedMessage);
            }
        }
    }
}
