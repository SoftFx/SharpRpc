using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    partial class TxPipeline
    {
        public class OneLock : TxPipeline
        {
            private readonly object _lockObj = new object();
            private readonly TxBuffer _buffer;
            private bool _isProcessingItem;
            private bool _isCompleted;
            private readonly int _bufferSizeThreshold;
            private readonly Queue<PendingItemTask> _asyncQueue = new Queue<PendingItemTask>();
            private readonly TaskCompletionSource<object> _completedEvent = new TaskCompletionSource<object>();

            public OneLock(ByteTransport transport, Endpoint config) : base(transport)
            {
                _buffer = new TxBuffer(_lockObj, config.TxSegmentSize, config.Serializer);
                _bufferSizeThreshold = config.TxSegmentSize * 5;
                _buffer.SpaceFreed += _buffer_SpaceFreed;

                TxBytesLoop();
            }

            private bool CanProcessNextMessage => !_isProcessingItem && HasRoomForNextMessage;
            private bool HasRoomForNextMessage => _buffer.DataSize < _bufferSizeThreshold;

            public override bool Send(IMessage msg)
            {
                lock (_lockObj)
                {
                    if (_isCompleted)
                        return false;

                    while (!CanProcessNextMessage)
                    {
                        Monitor.Wait(_lockObj);

                        if (_isCompleted)
                            return false;
                    }

                    _isProcessingItem = true;
                }

                ProcessMessage(msg);

                return true;
            }

            public override ValueTask<bool> SendAsync(IMessage msg)
            {
                lock (_lockObj)
                {
                    if (_isCompleted)
                        return new ValueTask<bool>(false);

                    if (!CanProcessNextMessage)
                    {
                        var waitItem = new PendingItemTask(msg);
                        _asyncQueue.Enqueue(waitItem);
                        return new ValueTask<bool>(waitItem.Task);
                    }
                    else
                        _isProcessingItem = true;
                }

                ProcessMessage(msg);

                return new ValueTask<bool>(true);
            }

            private void ProcessMessage(IMessage msg)
            {
                try
                {
                    _buffer.WriteMessage(new MessageHeader { MsgType = MessageType.User }, msg);
                }
                finally
                {
                    lock (_lockObj)
                    {
                        _isProcessingItem = false;
                        EnqueueNextItem();
                    }
                }
            }

            protected override ValueTask ReturnSegmentAndDequeue(List<ArraySegment<byte>> container)
            {
                return _buffer.ReturnAndDequeue(container);
            }

            //private void SerializeMessage(IMessage item)
            //{
            //    _buffer.StartMessageWrite(new MessageHeader());
            //    item.Serialize(_buffer);
            //    _buffer.EndMessageWrite();
            //}

            private void EnqueueNextItem()
            {
                if (_asyncQueue.Count > 0)
                {
                    _isProcessingItem = true;

                    var nextAsyncItem = _asyncQueue.Dequeue();

                    Task.Factory.StartNew(p =>
                    {
                        var task = (PendingItemTask)p;
                        ProcessMessage(task.ItemToEnqueue);
                        task.SetResult(true);

                    }, nextAsyncItem);
                }
                else
                {
                    Monitor.Pulse(_lockObj);
                }
            }

            public Task CloseAsync()
            {
                lock (_lockObj)
                {
                    if (!_isCompleted)
                    {
                        _isCompleted = true;

                        Monitor.PulseAll(_lockObj);

                        while (_asyncQueue.Count > 0)
                            _asyncQueue.Dequeue().SetResult(false);

                        if (!_isProcessingItem)
                            _completedEvent.TrySetResult(true);
                    }
                }

                return _completedEvent.Task;
            }

            private class PendingItemTask : TaskCompletionSource<bool>
            {
                public PendingItemTask(IMessage item)
                {
                    ItemToEnqueue = item;
                }

                public IMessage ItemToEnqueue { get; }
            }

            private void _buffer_SpaceFreed(TxBuffer sender)
            {
                if (CanProcessNextMessage)
                    EnqueueNextItem();
            }
        }
    }
}
