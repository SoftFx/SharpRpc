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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    partial class MessageDispatcher
    {
        private class OneThread : MessageDispatcher
        {
            private readonly object _lockObj = new object();
            private List<IMessage> _batch = new List<IMessage>();
            private List<IMessage> _queue = new List<IMessage>();
            private bool _isProcessing;
            private bool _allowFlag;
            private bool _completed;
            private readonly int _maxBatchSize = 1000;
            private Task _workerTask;

            public override void Start()
            {
            }

            protected bool HasMoreSpace => _queue.Count < _maxBatchSize;

            public override RpcResult OnSessionEstablished()
            {
                var fireResult = Core.FireOpened();

                if (!fireResult.IsOk)
                    return fireResult;

                lock (_lockObj)
                    _allowFlag = true;

                return RpcResult.Ok;
            }

#if NET5_0_OR_GREATER
            public override ValueTask OnMessages()
#else
            public override Task OnMessages()
#endif
            {
                lock (_lockObj)
                {
                    if (_completed)
                        return FwAdapter.AsyncVoid;

                    if (!_allowFlag)
                        OnError(RpcRetCode.ProtocolViolation, "A violation of handshake protocol has been detected!");

                    _queue.AddRange(IncomingMessages);

                    if (!_isProcessing)
                        EnqueueNextBatch();

                    if (!HasMoreSpace)
                        return FwAdapter.WrappResult(_workerTask);

                    return FwAdapter.AsyncVoid;
                }
            }

            public override Task Stop(RpcResult fault)
            {
                Task stopTask;

                lock (_lockObj)
                {
                    _completed = true;
                    //_fault = fault;

                    _queue.Clear();

                    Core.OnStop(fault);

                    if (_isProcessing)
                        stopTask = _workerTask.ContinueWith(t => InvokeOnStop());
                    else
                        stopTask = TaskQueue.StartNew(InvokeOnStop);
                }

                Core.CompleteStop();

                return stopTask;
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
                    result = await Tx.TrySendAsync(requestMsg);

                    if (result.Code != RpcRetCode.Ok)
                    {
                        if (!Core.UnregisterOperation(callId))
                            return; // do not need to call Task.Fail() in this case, because it was called in Close() method
                    }
                }

                if (result.Code != RpcRetCode.Ok)
                    callTask.OnFail(result);
            }

            protected override void CancelOperation(MessageDispatcherCore.IInteropOperation opObject)
            {
                throw new NotImplementedException();
            }

            public override RpcResult RegisterCallObject(string callId, MessageDispatcherCore.IInteropOperation callTask)
            {
                return Core.TryRegisterOperation(callId, callTask);
            }

            public override void UnregisterCallObject(string callId)
            {
                Core.UnregisterOperation(callId);
            }

            private void EnqueueNextBatch()
            {
                _isProcessing = true;

                Debug.Assert(_batch.Count == 0);

                var cpy = _queue;
                _queue = _batch;
                _batch = cpy;

                _workerTask = ProcessMessages();
            }

            private void SignalCompleted()
            {
                _isProcessing = false;

                if (_queue.Count > 0)
                    EnqueueNextBatch();
            }

            private async Task ProcessMessages()
            {
                // move processing to another thread (and exit the lock)
                await TaskQueue.Dive();

                foreach (var message in _batch)
                    Core.ProcessMessage(message);

                _batch.Clear();

                lock (_lockObj)
                    SignalCompleted();
            }

            private void InvokeOnStop()
            {
                Core.FireClosed();
            }
        }
    }
}
