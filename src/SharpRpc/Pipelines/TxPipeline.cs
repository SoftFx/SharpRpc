// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Lib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal interface TxPipeline
    {
        TaskFactory TaskFactory { get; }
        IMessageFactory MessageFactory { get; }
        bool ImmediateSerialization { get; }
        string ChannelId { get; }

        RpcResult TrySend(IMessage message);
        void Send(IMessage message);
        void TrySendAsync(IMessage message, Action<RpcResult> onSendCompletedCallback);
        void TrySendSystemMessage(IMessage message, Action<RpcResult> onSendCompletedCallback);
        void TrySendBytePage(string callId, ArraySegment<byte> page, Action<RpcResult> onSendCompletedCallback);
        bool TryCancelSend(IMessage message);
#if NET5_0_OR_GREATER
        ValueTask<RpcResult> TrySendAsync(IMessage message);
        ValueTask<RpcResult> SendSystemMessage(ISystemMessage message);
        ValueTask SendAsync(IMessage message);
#else
        Task<RpcResult> TrySendAsync(IMessage message);
        Task<RpcResult> SendSystemMessage(ISystemMessage message);
        Task SendAsync(IMessage message);
#endif
        void Start(ByteTransport transport);
        void StartProcessingUserMessages();
        void StopProcessingUserMessages(RpcResult fault);
        Task Close(RpcResult fault);
    }
}
