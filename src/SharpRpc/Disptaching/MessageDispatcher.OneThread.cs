// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    partial class MessageDispatcher
    {
        private class OneThread : MessageDispatcher
        {
            private readonly object _lockObj = new object();
            //private readonly CircularList<MessageTaskPair> _queue = new CircularList<MessageTaskPair>();
            private readonly Dictionary<string, ITask> _callTasks = new Dictionary<string, ITask>();
            private List<MessageTaskPair> _batch = new List<MessageTaskPair>();
            private List<MessageTaskPair> _queue = new List<MessageTaskPair>();
            private bool _isProcessing;
            //private TaskCompletionSource<bool> _dataAvaialableEvent;
            private bool _allowFlag;
            private bool _completed;
            private readonly int _maxBatchSize = 200;
            private Task _workerTask;
            private RpcResult _fault;

            public override void Start()
            {
            }

            protected bool HasMoreSpace => _queue.Count < _maxBatchSize;

            public override RpcResult OnSessionEstablished()
            {
                try
                {
                    ((RpcServiceBase)MessageHandler).Session.FireOpened(new SessionOpenedEventArgs());
                }
                catch (Exception)
                {
                    // TO DO : log or pass some more information about expcetion (stack trace)
                    return new RpcResult(RpcRetCode.RequestCrashed, "An exception has been occured in ");
                }

                lock (_lockObj)
                    _allowFlag = true;

                return RpcResult.Ok;
            }

#if NET5_0_OR_GREATER
            public override ValueTask OnMessages(IEnumerable<IMessage> messages)
#else
            public override Task OnMessages(IEnumerable<IMessage> messages)
#endif
            {
                lock (_lockObj)
                {
                    if (_completed)
                        return FwAdapter.AsyncVoid;

                    if (!_allowFlag)
                        OnError(RpcRetCode.ProtocolViolation, "A violation of handshake protocol has been detected!");

                    foreach (var msg in messages)
                        MatchAndEnqueue(msg);

                    if (!_isProcessing)
                        EnqueueNextBatch();

                    if (!HasMoreSpace)
                        return FwAdapter.WrappResult(_workerTask);

                    return FwAdapter.AsyncVoid;
                }
            }

            private void MatchAndEnqueue(IMessage incomingMessage)
            {
                if (incomingMessage is IResponse resp)
                {
                    if (_callTasks.TryGetValue(resp.CallId, out ITask task))
                        _callTasks.Remove(resp.CallId);

                    _queue.Add(new MessageTaskPair(incomingMessage, task));
                }
                else //if (incomingMessage is IRequest)
                    _queue.Add(new MessageTaskPair(incomingMessage));
            }

            public override Task Stop(RpcResult fault)
            {
                Task stopTask;
                List<ITask> tasksToCanel;

                lock (_lockObj)
                {
                    _completed = true;
                    _fault = fault;

                    _queue.Clear();

                    tasksToCanel = _callTasks.Values.ToList();
                    _callTasks.Clear();

                    if (_isProcessing)
                        stopTask = _workerTask.ContinueWith(t => InvokeOnStop());
                    else
                        stopTask = Task.Factory.StartNew(InvokeOnStop);
                }

                foreach (var task in tasksToCanel)
                    task.Fail(_fault);

                return stopTask;
            }

            protected override async void DoCall(IRequest requestMsg, ITask callTask)
            {
                var callId = Guid.NewGuid().ToString();

                requestMsg.CallId = callId;

                var result = RpcResult.Ok;

                lock (_lockObj)
                {
                    if (_completed)
                        result = _fault;
                    else
                        _callTasks.Add(callId, callTask);
                }

                if (result.Code == RpcRetCode.Ok)
                {
                    result = await Tx.TrySendAsync(requestMsg);

                    if (result.Code != RpcRetCode.Ok)
                    {
                        lock (_lockObj)
                        {
                            if (!_callTasks.Remove(callId))
                                return; // do not need to call Task.Fail() in this case, because it was called in Close() method
                        }
                    }
                }

                if (result.Code != RpcRetCode.Ok)
                    callTask.Fail(result);
            }

            private void EnqueueNextBatch()
            {
                _isProcessing = true;

                var cpy = _queue;
                _queue = _batch;
                _batch = cpy;

                _workerTask = ProcessMessages();
            }

            private void SignalCompleted()
            {
                _isProcessing = false;
                _batch.Clear();

                if (_queue.Count > 0)
                    EnqueueNextBatch();
            }

            private async Task ProcessMessages()
            {
                // move processing to another thread (and exit the lock)
                await Task.Yield();

                ProcessBatch();

                lock (_lockObj)
                    SignalCompleted();
            }

            private void ProcessBatch()
            {
                foreach (var item in _batch)
                {
                    if (item.Message is IResponse resp)
                    {
                        if (resp is IRequestFault faultMsg)
                            item.Task.Fail(faultMsg);
                        else
                            item.Task.Complete(resp);
                    }
                    else if (item.Message is IRequest req)
                        ProcessRequest(req);
                    else
                        ProcessOneWayMessage(item.Message);
                }
            }

            private async void ProcessRequest(IRequest request)
            {
                var respToSend = await MessageHandler.ProcessRequest(request);
                respToSend.CallId = request.CallId;
                await Tx.TrySendAsync(respToSend);
            }

            private void ProcessOneWayMessage(IMessage message)
            {
                try
                {
                    var result = MessageHandler.ProcessMessage(message);
                    if (result.IsCompleted)
                    {
                        if (result.IsFaulted)
                            OnError(RpcRetCode.MessageHandlerFailure, "Message handler threw an exception: " + result.ToTask().Exception.Message);
                    }
                    else
                    {
                        result.ToTask().ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                                OnError(RpcRetCode.MessageHandlerFailure, "Message handler threw an exception: " + t.Exception.Message);
                        });
                    }
                }
                catch (Exception ex)
                {
                    // TO DO : stop processing here ???
                    OnError(RpcRetCode.MessageHandlerFailure, "Message handler threw an exception: " + ex.Message);
                }
            }

            private void InvokeOnStop()
            {
                try
                {
                    ((RpcServiceBase)MessageHandler).Session.FireClosed(new SessionClosedEventArgs());
                }
                catch (Exception)
                {
                    // TO DO : log or pass some more information about expcetion (stack trace)
                }
            }

            private struct MessageTaskPair
            {
                public MessageTaskPair(IMessage message, ITask task = null)
                {
                    Message = message;
                    Task = task;
                }

                public IMessage Message { get; }
                public ITask Task { get; }
            }
        }
    }
}
