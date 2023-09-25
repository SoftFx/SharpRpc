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
using System.Threading.Tasks;

namespace SharpRpc.MsTest.MockObjects
{
    public class MockMessageTransmitter : TxPipeline
    {
        public TaskFactory TaskQueue => Task.Factory;

        public MockMessageTransmitter()
        {
            //MessageFactory = msgFactory;
        }

        public string ChannelId { get; } = "Ch1";
        public IMessageFactory MessageFactory { get; }
        public bool ImmediateSerialization => true;
        public Queue<IMessage> MessagesToSend { get; } = new Queue<IMessage>(); 

        public Task Close(TimeSpan gracefulCloseTimeout)
        {
            return Task.CompletedTask;
        }

        public void Send(IMessage message)
        {
            TrySend(message).ThrowIfNotOk();
        }

        public ValueTask SendAsync(IMessage message)
        {
            Send(message);
            return new ValueTask();
        }

        public ValueTask<RpcResult> TrySendAsync(IMessage message)
        {
            return ValueTask.FromResult(TrySend(message));
        }

        public RpcResult TrySend(IMessage message)
        {
            if (message is ICloneOnSendMessage cMsg)
                MessagesToSend.Enqueue(cMsg.Clone());
            else
                MessagesToSend.Enqueue(message);

            return RpcResult.Ok;
        }

        public ValueTask<RpcResult> SendSystemMessage(ISystemMessage message)
        {
            return ValueTask.FromResult(TrySend(message));
        }

        public void TrySendAsync(IMessage message, Action<RpcResult> onSendCompletedCallback)
        {
            var result = TrySend(message);
            onSendCompletedCallback(result);
        }

        public void Start(ByteTransport transport)
        {
        }

        public void StartProcessingUserMessages()
        {
        }

        public void StopProcessingUserMessages(RpcResult fault)
        {
        }

        public bool TryCancelSend(IMessage message)
        {
            return true;
        }

        public void TrySendBytePage(string callId, ArraySegment<byte> page, Action<RpcResult> onSendCompletedCallback)
        {
            
        }
    }
}
