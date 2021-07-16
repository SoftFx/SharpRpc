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
    partial class MessageDispatcher
    {
        private class NoThreading : MessageDispatcher
        {
            private readonly object _lockObj = new object();
            private readonly Dictionary<string, ITask> _callTasks = new Dictionary<string, ITask>();
            private bool _isProcessing;
            private bool _closed;
            private RpcResult _fault;
            private TaskCompletionSource<bool> _closeCompletion = new TaskCompletionSource<bool>();

            public override void Start()
            {
            }

            public override Task Stop(RpcResult fault)
            {
                List<ITask> tasksToCanel;

                lock (_callTasks)
                {
                    _closed = true;
                    _fault = fault;

                    tasksToCanel = _callTasks.Values.ToList();
                    _callTasks.Clear();

                    if (!_isProcessing)
                        CompleteClose();
                }

                foreach (var task in tasksToCanel)
                    task.Fail(_fault);

                return _closeCompletion.Task;
            }

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

                //lock (_lockObj)
                //    _allowFlag = true;

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
                    if (_closed)
                        return FwAdapter.AsyncVoid;

                    _isProcessing = true;
                }

                foreach (var msg in IncomingMessages)
                {
                    if (msg is IReqRespMessage)
                    {
                        if (msg is IResponse resp)
                            ProcessResponse(resp);
                        else if (msg is IRequest req)
                            ProcessRequest(req);
                    }
                    else
                        ProcessOneWayMessage(msg);
                }

                lock (_lockObj)
                {
                    _isProcessing = false;

                    if (_closed)
                        CompleteClose();
                }

                return FwAdapter.AsyncVoid;
            }

            protected override async void DoCall(IRequest requestMsg, ITask callTask)
            {
                var callId = Guid.NewGuid().ToString();

                requestMsg.CallId = callId;

                var result = RpcResult.Ok;

                lock (_lockObj)
                {
                    if (_closed)
                        result = _fault;
                    else
                        _callTasks.Add(callId, callTask);
                }

                if (result.Code == RpcRetCode.Ok)
                {
                    result = await Tx.TrySendAsync(requestMsg);

                    if (result.Code != RpcRetCode.Ok)
                    {
                        lock (_callTasks)
                        {
                            if (!_callTasks.Remove(callId))
                                return; // do not need to call Task.Fail() in this case, because it was called in Close() method
                        }
                    }
                }

                if (result.Code != RpcRetCode.Ok)
                    callTask.Fail(result);
            }

            private void ProcessResponse(IResponse resp)
            {
                ITask taskToComplete = null;

                lock (_lockObj)
                {
                    if (_callTasks.TryGetValue(resp.CallId, out taskToComplete))
                        _callTasks.Remove(resp.CallId);
                    else
                    {
                        // TO DO : signal protocol violation
                        return;
                    }
                }

                if (resp is IRequestFault faultMsg)
                    taskToComplete.Fail(faultMsg);
                else
                    taskToComplete.Complete(resp);
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

                _closeCompletion.SetResult(true);
            }

            private void CompleteClose()
            {
                TaskQueue.StartNew(InvokeOnStop);
            }
        }
    }
}
