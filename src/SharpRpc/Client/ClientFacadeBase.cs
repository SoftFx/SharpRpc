// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    public class ClientFacadeBase
    {
        public ClientFacadeBase(Channel channel)
        {
            Channel = channel;
        }

        protected Channel Channel { get; }

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
    }
}
