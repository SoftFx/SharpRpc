// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    partial class MessageDispatcher
    {
        private class NoThreading : MessageDispatcher
        {
            private readonly object _lockObj = new object();
            private bool _isProcessing;
            private bool _closed;
            private TaskCompletionSource<bool> _closeCompletion = new TaskCompletionSource<bool>();

            public override void Start()
            {
            }

            public override Task Stop(RpcResult fault)
            {
                lock (_lockObj)
                {
                    _closed = true;

                    Core.OnStop(fault);

                    if (!_isProcessing)
                        CompleteClose();
                }

                Core.CompleteStop();

                return _closeCompletion.Task;
            }

            public override RpcResult OnSessionEstablished()
            {
                return Core.FireOpened();
            }

#if NET5_0_OR_GREATER
            public override ValueTask OnMessages()
#else
            public override Task OnMessages()
#endif
            {
                lock (_lockObj)
                {
                    if (_closed)
                        return FwAdapter.AsyncVoid;

                    _isProcessing = true;
                }

                foreach (var msg in IncomingMessages)
                    Core.ProcessMessage(msg);

                lock (_lockObj)
                {
                    _isProcessing = false;

                    if (_closed)
                        CompleteClose();
                }

                return FwAdapter.AsyncVoid;
            }

            protected override async void DoCall(IRequestMessage requestMsg, MessageDispatcherCore.IInteropOperation callTask, CancellationToken cToken)
            {
                var callId = GenerateOperationId(); // Guid.NewGuid().ToString();

                requestMsg.CallId = callId;

                if (cToken.CanBeCanceled)
                    requestMsg.Options |= RequestOptions.CancellationEnabled;

                var result = Core.TryRegisterOperation(callId, callTask);

                if (result.Code == RpcRetCode.Ok)
                {
                    var sendTask = Tx.TrySendAsync(requestMsg);

                    cToken.Register(CancelOperation, callTask);

                    result = await sendTask;

                    if (result.Code != RpcRetCode.Ok)
                    {
                        if (!Core.UnregisterOperation(callId))
                            return; // do not need to call Task.Fail() in this case, because it was called in Close() method
                    }
                }

                if (result.Code != RpcRetCode.Ok)
                    callTask.OnFail(result);
            }

            public override RpcResult RegisterCallObject(string callId, MessageDispatcherCore.IInteropOperation callTask)
            {
                lock (_lockObj)
                    return Core.TryRegisterOperation(callId, callTask);
            }

            public override void UnregisterCallObject(string callId)
            {
                lock (_lockObj)
                    Core.UnregisterOperation(callId);
            }

            protected override void CancelOperation(MessageDispatcherCore.IInteropOperation opObject)
            {
                var sendWasCanceled = true;

                lock (_lockObj)
                {
                    sendWasCanceled = Core.Tx.TryCancelSend(opObject.RequestMessage);
                }

                if (sendWasCanceled)
                    opObject.OnFail(new RpcResult(RpcRetCode.OperationCanceled, "Canceled by user."));
                else
                {
                    var cancelMessage = Tx.MessageFactory.CreateCancelRequestMessage();
                    cancelMessage.CallId = opObject.RequestMessage.CallId;

                    opObject.StartCancellation();

                    Tx.TrySendAsync(cancelMessage);
                }
            }

            private void InvokeOnStop()
            {
                Core.FireClosed();

                _closeCompletion.SetResult(true);
            }

            private void CompleteClose()
            {
                TaskQueue.StartNew(InvokeOnStop);
            }
        }
    }
}
