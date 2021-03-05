using SharpRpc.Lib;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    partial class MessageBlock
    {
        private class OneThread : MessageBlock
        {
            private readonly object _lockObj = new object();
            private readonly CircularList<IMessage> _queue = new CircularList<IMessage>();
            private readonly List<IMessage> _batch = new List<IMessage>();
            private TaskCompletionSource<bool> _dataAvaialableEvent;
            private TaskCompletionSource<object> _completionEvent = new TaskCompletionSource<object>();
            private bool _completed;
            private readonly int _pageSize = 50;

            public OneThread(IMessageHandler handler) : base(handler)
            {
                InvokeMessageHandlerLoop();
            }

            public override bool SuportsBatching => true;

            public override void Consume(IMessage message)
            {
                lock (_lockObj)
                {
                    if (_completed)
                        return;

                    _queue.Enqueue(message);
                    SignalDataReady();
                }
            }

            public override void Consume(IEnumerable<IMessage> messages)
            {
                lock (_lockObj)
                {
                    if (_completed)
                        return;

                    _queue.AddRange(messages);
                    SignalDataReady();
                }
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

                    return _completionEvent.Task;
                }
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

            private async void InvokeMessageHandlerLoop()
            {
                while (true)
                {
                    if (!await TryDequeueNextPage())
                        break;

                    foreach (var msg in _batch)
                    {
                        try
                        {
                            await MessageHandler.ProcessMessage(msg);
                        }
                        catch (Exception ex)
                        {
                        }
                    }
                }

                _completionEvent.SetResult(true);
            }
        }
    }
}
