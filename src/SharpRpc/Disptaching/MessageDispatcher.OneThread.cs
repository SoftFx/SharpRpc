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
            //private readonly CircularList<MessageTaskPair> _queue = new CircularList<MessageTaskPair>();
            //private List<MessageTaskPair> _batch = new List<MessageTaskPair>();
            //private List<MessageTaskPair> _queue = new List<MessageTaskPair>();
            private List<IMessage> _batch = new List<IMessage>();
            private List<IMessage> _queue = new List<IMessage>();
            private bool _isProcessing;
            //private TaskCompletionSource<bool> _dataAvaialableEvent;
            private bool _allowFlag;
            private bool _completed;
            private readonly int _maxBatchSize = 1000;
            private Task _workerTask;
            //private RpcResult _fault;

            public override void Start()
            {
            }

            protected bool HasMoreSpace => _queue.Count < _maxBatchSize;

            public override RpcResult OnSessionEstablished()
            {
                var fireResult = Core.FireOpened();

                if (!fireResult.IsOk)
                    return fireResult;

                //try
                //{
                //    ((RpcCallHandler)MessageHandler).Session.FireOpened(new SessionOpenedEventArgs());
                //}
                //catch (Exception)
                //{
                //    // TO DO : log or pass some more information about expcetion (stack trace)
                //    return new RpcResult(RpcRetCode.RequestCrashed, "An exception has been occured in ");
                //}

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

                    //Debug.Assert(_queue.Count == 0);

                    //var freeMessageBatch = _queue;
                    //_queue = IncomingMessagesContainer;
                    //IncomingMessagesContainer = freeMessageBatch;

                    //foreach (var msg in IncomingMessagesContainer)
                    //    MatchAndEnqueue(msg);

                    _queue.AddRange(IncomingMessages);
                    //_queue.Add(IncomingMessages[0]);

                    //if (_isProcessing)
                    //    return FwAdapter.WrappResult(_workerTask);
                    //else
                    //{
                    //    EnqueueNextBatch();
                    //    return FwAdapter.AsyncVoid;
                    //}


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

            protected override async void DoCall(IRequest requestMsg, MessageDispatcherCore.IInteropOperation callTask)
            {
                var callId = Guid.NewGuid().ToString();

                requestMsg.CallId = callId;

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
                    callTask.Fail(result);
            }

            public override RpcResult RegisterCallObject(string callId, MessageDispatcherCore.IInteropOperation callTask)
            {
                return Core.TryRegisterOperation(callId, callTask);
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
