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

        public ClientBase(ClientEndpoint endpoint, ContractDescriptor descriptor, RpcCallHandler callbackHandler)
        {
            Channel = new Channel(false, endpoint, descriptor, callbackHandler ?? throw new ArgumentNullException("callbackHandler"));
        }

        public ClientBase(Channel channel)
        {
            Channel = channel ?? throw new ArgumentNullException("channel");
        }

        public Channel Channel { get; }

        #region Messages

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

        #endregion

        #region Calls

        protected Task CallAsync<TResp>(IRequestMessage requestMessage)
            where TResp : IResponseMessage
        {
            return Channel.Dispatcher.Call<TResp>(requestMessage);
        }

        protected Task<T> CallAsync<T, TResp>(IRequestMessage requestMessage)
            where TResp : IResponseMessage
        {
            return Channel.Dispatcher.Call<TResp, T>(requestMessage);
        }

        protected Task<RpcResult> TryCallAsync<TResp>(IRequestMessage requestMsg)
            where TResp : IResponseMessage
        {
            return Channel.Dispatcher.TryCall<TResp>(requestMsg);
        }

        protected Task<RpcResult<T>> TryCallAsync<T, TResp>(IRequestMessage requestMsg)
            where TResp : IResponseMessage
        {
            return Channel.Dispatcher.TryCall<TResp, T>(requestMsg);
        }

        #endregion

        #region Streams

        protected OutputStreamCall<TOut> OpenOutputStream<TOut>(IOpenStreamRequest request, IStreamMessageFactory<TOut> factory)
        {
            return new StreamCall<object, TOut, object>(request, Channel, null, factory, false);
        }

        protected OutputStreamCall<TOut, TResult> OpenOutputStream<TOut, TResult>(IOpenStreamRequest request, IStreamMessageFactory<TOut> factory)
        {
            return new StreamCall<object, TOut, TResult>(request, Channel, null, factory, true);
        }

        protected InputStreamCall<TIn> OpenInputStream<TIn>(IOpenStreamRequest request, IStreamMessageFactory<TIn> factory)
        {
            return new StreamCall<TIn, object, object>(request, Channel, factory, null, false);
        }

        protected InputStreamCall<TIn, TResult> OpenInputStream<TIn, TResult>(IOpenStreamRequest request, IStreamMessageFactory<TIn> factory)
        {
            return new StreamCall<TIn, object, TResult>(request, Channel, factory, null, true);
        }

        protected DuplexStreamCall<TIn, TOut, object> OpenDuplexStream<TIn, TOut>(IOpenStreamRequest request,
            IStreamMessageFactory<TIn> inFactory, IStreamMessageFactory<TOut> outFactory)
        {
            return new StreamCall<TIn, TOut, object>(request, Channel, inFactory, outFactory, false);
        }

        protected DuplexStreamCall<TIn, TOut, TResult> OpenDuplexStream<TIn, TOut, TResult>(IOpenStreamRequest request,
            IStreamMessageFactory<TIn> inFactory, IStreamMessageFactory<TOut> outFactory)
        {
            return new StreamCall<TIn, TOut, TResult>(request, Channel, inFactory, outFactory, true);
        }

        #endregion

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
            public ValueTask<IResponseMessage> ProcessRequest(IRequestMessage message)
#else
            public Task<IResponseMessage> ProcessRequest(IRequestMessage message)
#endif
            {
                throw new RpcException("No message handler for " + message.GetType().Name, RpcRetCode.UnexpectedMessage);
            }
        }
    }
}
