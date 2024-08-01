// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    public abstract class ClientBase
    {
        public ClientBase(ClientEndpoint endpoint, ContractDescriptor descriptor)
        {
            Channel = new Channel(Channel.GenerateId(), null, endpoint, descriptor, new NullHandler());
        }

        public ClientBase(ClientEndpoint endpoint, ContractDescriptor descriptor, RpcCallHandler callbackHandler)
        {
            Channel = new Channel(Channel.GenerateId(), null, endpoint, descriptor, callbackHandler ?? throw new ArgumentNullException("callbackHandler"));
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

        protected Task CallAsync<TResp>(IRequestMessage requestMessage, CancellationToken cToken)
            where TResp : IResponseMessage
        {
            return Channel.Dispatcher.Call<TResp>(requestMessage, cToken);
        }

        protected Task<T> CallAsync<T, TResp>(IRequestMessage requestMessage, CancellationToken cToken)
            where TResp : IResponseMessage
        {
            return Channel.Dispatcher.Call<TResp, T>(requestMessage, cToken);
        }

        protected Task<RpcResult> TryCallAsync<TResp>(IRequestMessage requestMsg, CancellationToken cToken)
            where TResp : IResponseMessage
        {
            return Channel.Dispatcher.TryCall<TResp>(requestMsg, cToken);
        }

        protected Task<RpcResult<T>> TryCallAsync<T, TResp>(IRequestMessage requestMsg, CancellationToken cToken)
            where TResp : IResponseMessage
        {
            return Channel.Dispatcher.TryCall<TResp, T>(requestMsg, cToken);
        }

        #endregion

        #region Streams

        protected OutputStreamCall<TOut> OpenOutputStream<TOut>(IOpenStreamRequest request, StreamOptions options, IStreamMessageFactory<TOut> factory)
        {
            return new StreamCall<object, TOut, object>(request, null, options, Channel.Tx, Channel.Dispatcher, null, factory, false);
        }

        protected OutputStreamCall<TOut, TResult> OpenOutputStream<TOut, TResult>(IOpenStreamRequest request, StreamOptions options, IStreamMessageFactory<TOut> factory)
        {
            return new StreamCall<object, TOut, TResult>(request, null, options, Channel.Tx, Channel.Dispatcher, null, factory, true);
        }

        protected InputStreamCall<TIn> OpenInputStream<TIn>(IOpenStreamRequest request, StreamOptions options, IStreamMessageFactory<TIn> factory)
        {
            return new StreamCall<TIn, object, object>(request, options, null, Channel.Tx, Channel.Dispatcher, factory, null, false);
        }

        protected InputStreamCall<TIn, TResult> OpenInputStream<TIn, TResult>(IOpenStreamRequest request, StreamOptions options, IStreamMessageFactory<TIn> factory)
        {
            return new StreamCall<TIn, object, TResult>(request, options, null, Channel.Tx, Channel.Dispatcher, factory, null, true);
        }

        protected DuplexStreamCall<TIn, TOut> OpenDuplexStream<TIn, TOut>(IOpenStreamRequest request, DuplexStreamOptions options,
            IStreamMessageFactory<TIn> inFactory, IStreamMessageFactory<TOut> outFactory)
        {
            return new StreamCall<TIn, TOut, object>(request, options.GetInputOptions(), options.GetOutputOptions(), Channel.Tx, Channel.Dispatcher, inFactory, outFactory, false);
        }

        protected DuplexStreamCall<TIn, TOut, TResult> OpenDuplexStream<TIn, TOut, TResult>(IOpenStreamRequest request, DuplexStreamOptions options,
            IStreamMessageFactory<TIn> inFactory, IStreamMessageFactory<TOut> outFactory)
        {
            return new StreamCall<TIn, TOut, TResult>(request, options.GetInputOptions(), options.GetOutputOptions(), Channel.Tx, Channel.Dispatcher, inFactory, outFactory, true);
        }

        #endregion

        private class NullHandler : RpcCallHandler
        {

#if NET5_0_OR_GREATER
            protected override ValueTask OnMessage(IMessage message)
#else
            protected override Task OnMessage(IMessage message)
#endif
            {
                throw new RpcException("No message handler for " + message.GetType().Name, RpcRetCode.UnexpectedMessage);
            }

#if NET5_0_OR_GREATER
            protected override ValueTask<IResponseMessage> OnRequest(IRequestMessage message)
#else
            protected override Task<IResponseMessage> OnRequest(IRequestMessage message)
#endif
            {
                throw new RpcException("No message handler for " + message.GetType().Name, RpcRetCode.UnexpectedMessage);
            }
        }
    }
}
