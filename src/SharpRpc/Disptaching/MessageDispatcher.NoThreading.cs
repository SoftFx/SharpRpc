// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Disptaching;
using SharpRpc.Lib;
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
            private bool _isStarted;
            private bool _isProcessing;
            private bool _closed;
            private TaskCompletionSource<bool> _startedEvent = new TaskCompletionSource<bool>();
            private TaskCompletionSource<bool> _closeCompletion = new TaskCompletionSource<bool>();

            public override RpcResult Start()
            {
                TaskCompletionSource<bool> _toFire = null;

                if (!Core.TryInvokeInit(Channel, out var error))
                    return error;

                lock (_lockObj)
                {
                    if (!_isStarted)
                    {
                        _isStarted = true;
                        _toFire = _startedEvent;
                    }
                }

                _toFire?.SetResult(true);
                return RpcResult.Ok;
            }

            public override Task Stop(RpcResult fault)
            {
                lock (_lockObj)
                {
                    if (_closed)
                        return _closeCompletion.Task;

                    _closed = true;

                    if (!_isStarted)
                    {
                        _isStarted = true;
                        _startedEvent.SetResult(true);
                    }

                    Core.OnStop(fault);

                    if (!_isProcessing)
                        CompleteClose();
                }

                Core.CompleteStop();

                return _closeCompletion.Task;
            }

#if NET5_0_OR_GREATER
            public override async ValueTask OnMessages()
#else
            public override async Task OnMessages()
#endif
            {
                TaskCompletionSource<bool> toWait = null;

                lock (_lockObj)
                {
                    if (_closed)
                        return;

                    if (!_isStarted)
                        toWait = _startedEvent;

                    _isProcessing = true;
                }

                if (toWait != null)
                {
                    await toWait.Task.ConfigureAwait(false);
                    await TaskFactory.Dive();

                    lock (_lockObj)
                    {
                        if (_closed)
                        {
                            _isProcessing = false;
                            CompleteClose();
                            return;
                        }
                    }
                }

                foreach (var msg in IncomingMessages)
                    Core.ProcessMessage(msg);

                lock (_lockObj)
                {
                    _isProcessing = false;

                    if (_closed)
                        CompleteClose();
                }
            }

            protected override async void DoCall(IRequestMessage requestMsg, IDispatcherOperation callTask, CancellationToken cToken)
            {
                var callId = GenerateOperationId(); // Guid.NewGuid().ToString();

                requestMsg.CallId = callId;

                if (cToken.CanBeCanceled)
                    requestMsg.Options |= RequestOptions.CancellationEnabled;

                var result = Core.TryRegisterOperation(callTask);

                if (result.Code == RpcRetCode.Ok)
                {
                    var sendTask = Tx.TrySendAsync(requestMsg);

                    cToken.Register(CancelOutgoingCall, callTask);

                    result = await sendTask.ConfigureAwait(false);

                    if (result.Code != RpcRetCode.Ok)
                    {
                        if (!Core.UnregisterOperation(callTask))
                            return; // do not need to call Task.Fail() in this case, because it was called in Close() method
                    }
                }

                if (result.Code != RpcRetCode.Ok)
                    callTask.OnFault(result);
            }

            public override RpcResult Register(IDispatcherOperation operation)
            {
                lock (_lockObj)
                    return Core.TryRegisterOperation(operation);
            }

            public override void Unregister(IDispatcherOperation operation)
            {
                lock (_lockObj)
                    Core.UnregisterOperation(operation);
            }

            protected override void CancelOutgoingCall(IDispatcherOperation callObj)
            {
                var sendWasCanceled = true;

                lock (_lockObj)
                {
                    sendWasCanceled = Core.Tx.TryCancelSend(callObj.RequestMessage);
                }

                // OnFault(new RpcResult(RpcRetCode.OperationCanceled, "Canceled by user."));
                if (sendWasCanceled)
                    callObj.OnRequestCancelled();
                else
                {
                    var cancelMessage = Tx.MessageFactory.CreateCancelRequestMessage();
                    cancelMessage.CallId = callObj.RequestMessage.CallId;

                    Tx.TrySendAsync(cancelMessage);
                }
            }

            private void InvokeOnStop()
            {
                Core.InvokeOnClose();

                _closeCompletion.SetResult(true);
            }

            private void CompleteClose()
            {
                TaskFactory.StartNew(InvokeOnStop);
            }
        }
    }
}