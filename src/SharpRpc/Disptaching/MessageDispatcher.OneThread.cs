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
            private readonly CircularList<MessageTaskPair> _queue = new CircularList<MessageTaskPair>();
            private readonly Dictionary<string, ITask> _callTasks = new Dictionary<string, ITask>();
            private readonly List<MessageTaskPair> _batch = new List<MessageTaskPair>();
            private TaskCompletionSource<bool> _dataAvaialableEvent;
            private bool _allowFlag;
            private bool _completed;
            private readonly int _pageSize = 50;
            private Task _workerTask;
            private RpcResult _fault;

            public override void Start()
            {
                lock (_lockObj)
                    _workerTask = MessageHandlerLoop();
            }

            public override void AllowMessages()
            {
                lock (_lockObj)
                    _allowFlag = true;
            }

            public override void OnMessages(IEnumerable<IMessage> messages)
            {
                lock (_lockObj)
                {
                    if (_completed)
                        return;

                    if (!_allowFlag)
                        OnError(RpcRetCode.ProtocolViolation, "A violation of handshake protocol has been detected!");

                    foreach (var msg in messages)
                        MatchAndEnqueue(msg);

                    SignalDataReady();
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
                else
                    _queue.Add(new MessageTaskPair(incomingMessage));
            }

            public override Task Stop(RpcResult fault)
            {
                Task stopWaithandle;
                List<ITask> tasksToCanel;

                lock (_lockObj)
                {
                    _completed = true;
                    _queue.Clear();
                    _fault = fault;

                    if (_queue.Count == 0)
                        SignalCompleted();

                    tasksToCanel = _callTasks.Values.ToList();
                    _callTasks.Clear();

                    stopWaithandle = _workerTask ?? Task.CompletedTask;
                }

                foreach (var task in tasksToCanel)
                    task.Fail(_fault);

                return stopWaithandle;
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

            private void SignalDataReady()
            {
                var eventCpy = _dataAvaialableEvent;
                if (eventCpy != null)
                {
                    _dataAvaialableEvent = null;
                    _queue.DequeueRange(_batch, _pageSize);
                    Task.Factory.StartNew(p => ((TaskCompletionSource<bool>)p).SetResult(true), eventCpy);
                }
            }

            private void SignalCompleted()
            {
                var eventCpy = _dataAvaialableEvent;
                _dataAvaialableEvent = null;
                eventCpy?.SetResult(false);
            }

            private ValueTask<bool> TryDequeueNextPage()
            {
                lock (_lockObj)
                {
                    _batch.Clear();

                    if (_queue.Count > 0)
                    {
                        _queue.DequeueRange(_batch, _pageSize);
                        return new ValueTask<bool>(true);
                    }
                    else if (_completed)
                        return new ValueTask<bool>(false);

                    _dataAvaialableEvent = new TaskCompletionSource<bool>();
                    return new ValueTask<bool>(_dataAvaialableEvent.Task);
                }
            }

            private async Task MessageHandlerLoop()
            {
                while (true)
                {
                    if (!await TryDequeueNextPage())
                        break;

                    foreach (var item in _batch)
                    {
                        if (item.Message is IResponse resp)
                        {
                            if (resp is IRequestFault faultMsg)
                            {
                                //var result = default(RpcResult);

                                //if (faultMsg.Code == RequestFaultCode.RegularFault)
                                //    result = new RpcResult(RpcRetCode.RequestFaulted, new RpcFaultStub(faultMsg.Text));
                                ////else if(faultMsg.Code == RequestFaultCode.CustomFault)
                                ////    result = new RpcResult(RpcRetCode.RequestFaulted, faultMsg
                                //else
                                //    result = new RpcResult(RpcRetCode.RequestCrashed, new RpcFaultStub("Request fauled due to unexpected exception in request handler."));

                                item.Task.Fail(faultMsg);
                            }
                            else
                                item.Task.Complete(resp);
                        }
                        else if (item.Message is IRequest req)
                        {
                            ProcessRequest(req);
                        }
                        else
                        {
                            try
                            {
                                await MessageHandler.ProcessMessage(item.Message);
                            }
                            catch (Exception ex)
                            {
                                OnError(RpcRetCode.MessageHandlerFailure, "Message handler threw an exception: " + ex.Message);
                            }
                        }
                    }
                }
            }

            private async void ProcessRequest(IRequest request)
            {
                var respToSend = await MessageHandler.ProcessRequest(request);
                respToSend.CallId = request.CallId;
                await Tx.TrySendAsync(respToSend);
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
