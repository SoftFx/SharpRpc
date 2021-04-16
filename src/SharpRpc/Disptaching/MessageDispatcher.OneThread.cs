using SharpRpc.Lib;
using System;
using System.Collections.Generic;
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
            private bool _completed;
            private readonly int _pageSize = 50;
            private Task _workerTask;

            public OneThread()
            {
            }

            protected override void OnInit()
            {
                _workerTask = InvokeMessageHandlerLoop();
            }

            public override void OnMessages(IEnumerable<IMessage> messages)
            {
                lock (_lockObj)
                {
                    if (_completed)
                        return;

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

            public override Task Close(bool dropTheQueue)
            {
                lock (_lockObj)
                {
                    _completed = true;

                    if (dropTheQueue)
                        _queue.Clear();

                    if (_queue.Count == 0)
                        SignalCompleted();

                    return _workerTask;
                }
            }

            protected override async void DoCall(IRequest requestMsg, ITask callTask)
            {
                var callId = Guid.NewGuid().ToString();

                requestMsg.CallId = callId;

                var result = RpcResult.Ok;

                lock (_lockObj)
                {
                    if (_completed)
                        result = RpcResult.ChannelClose;
                    else
                        _callTasks.Add(callId, callTask);
                }

                if (result.Code == RpcRetCode.Ok)
                {
                    var sendResult = await Tx.TrySendAsync(requestMsg);

                    if (sendResult.Code != RpcRetCode.Ok)
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

            private async Task InvokeMessageHandlerLoop()
            {
                while (true)
                {
                    if (!await TryDequeueNextPage())
                        break;

                    foreach (var item in _batch)
                    {
                        if (item.Message is ISystemMessage sysMsg)
                        {
                            // ?????
                        }
                        else if (item.Message is IResponse resp)
                        {
                            item.Task.Complete(resp);
                        }
                        else if (item.Message is IRequest req)
                        {
                            var respToSend = await MessageHandler.ProcessRequest(req);
                            respToSend.CallId = req.CallId;
                            await Tx.TrySendAsync(respToSend);
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
