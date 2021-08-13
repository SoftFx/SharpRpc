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
        //public ByteTransport Transport { get; protected set; }

        //event Action ConnectionRequested;
        //event Action<RpcResult> CommunicationFaulted;

        TaskFactory TaskQueue { get; }

        RpcResult TrySend(IMessage message);
        void Send(IMessage message);
        void TrySendAsync(IMessage message, Action<RpcResult> onSendCompletedCallback);
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
        Task Close(TimeSpan gracefulCloseTimeout);

        //protected void SignalCommunicationError(RpcResult fault)
        //{
        //    CommunicationFaulted.Invoke(fault);
        //}

        //protected void SignalConnectionRequest()
        //{
        //    ConnectionRequested.Invoke();
        //}
    }
}
