using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    partial class TxPipeline
    {
        public class NoQueue : TxPipeline
        {
            private readonly object _lockObj = new object();
            private readonly TxBuffer _buffer;
            private bool _isProcessingItem;
            private bool _isCompleted;
            private bool _isInitialized;
            private bool _isInitializing;
            private RpcResult _fault;
            private readonly int _bufferSizeThreshold;
            private readonly Queue<PendingItemTask> _asyncQueue = new Queue<PendingItemTask>();
            private readonly TaskCompletionSource<object> _completedEvent = new TaskCompletionSource<object>();

            public NoQueue(IRpcSerializer serializer, Endpoint config, Func<Task<RpcResult<ByteTransport>>> transportRequestFunc)
                : base(transportRequestFunc)
            {
                _buffer = new TxBuffer(_lockObj, config.TxSegmentSize, serializer);
                _bufferSizeThreshold = config.TxSegmentSize * 5;
                _buffer.SpaceFreed += _buffer_SpaceFreed;
            }

            private bool CanProcessNextMessage => !_isProcessingItem && HasRoomForNextMessage;
            private bool HasRoomForNextMessage => _buffer.DataSize < _bufferSizeThreshold;

            public override RpcResult TrySend(IMessage msg)
            {
                lock (_lockObj)
                {
                    if (_fault.Code != RpcRetCode.Ok)
                        return _fault;

                    if (!_isInitialized && !_isInitializing)
                    {
                        _isInitializing = true;
                        Initialize();
                    }

                    while (!CanProcessNextMessage)
                    {
                        Monitor.Wait(_lockObj);

                        if (_fault.Code != RpcRetCode.Ok)
                            return _fault;
                    }

                    _isProcessingItem = true;
                }

                ProcessMessage(msg);

                return RpcResult.Ok;
            }

            public override ValueTask<RpcResult> TrySendAsync(IMessage msg)
            {
                lock (_lockObj)
                {
                    if (_fault.Code != RpcRetCode.Ok)
                        return new ValueTask<RpcResult>(_fault);

                    if (!CanProcessNextMessage)
                    {
                        var waitItem = new PendingItemTask(msg);
                        _asyncQueue.Enqueue(waitItem);
                        return new ValueTask<RpcResult>(waitItem.Task);
                    }
                    else
                        _isProcessingItem = true;
                }

                ProcessMessage(msg);

                return new ValueTask<RpcResult>(RpcResult.Ok);
            }

            public override ValueTask SendAsync(IMessage message)
            {
                throw new NotImplementedException();
            }

            public override void Send(IMessage message)
            {
                TrySend(message).ThrowIfNotOk();
            }

            private async void Initialize()
            {
                var ret = await GetTransport();

                lock (_lockObj)
                {
                    _isInitialized = true;
                    _isInitializing = false;

                    Transport = ret.Result;

                    if (ret.Code != RpcRetCode.Ok)
                    {
                        _fault = new RpcResult(ret.Code, ret.Fault);
                        FailAllPendingItems();
                    }
                    else
                    {
                        StartTransportRead();
                        EnqueueNextItem();
                    }
                }
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

            private void EnqueueNextItem()
            {
                if (_asyncQueue.Count > 0)
                {
                    _isProcessingItem = true;

                    var nextAsyncItem = _asyncQueue.Dequeue();

                    Task.Factory.StartNew(p =>
                    {
                        var task = (PendingItemTask)p;
                        try
                        {
                            ProcessMessage(task.ItemToEnqueue);
                            task.SetResult(RpcResult.Ok);
                        }
                        catch (Exception ex)
                        {
                            task.SetException(ex);
                        }
                    }, nextAsyncItem);
                }
                else
                {
                    Monitor.Pulse(_lockObj);
                }
            }

            public override Task Close(RpcResult fault)
            {
                lock (_lockObj)
                {
                    if (!_isCompleted)
                    {
                        _isCompleted = true;
                        _fault = fault;

                        Monitor.PulseAll(_lockObj);

                        while (_asyncQueue.Count > 0)
                            _asyncQueue.Dequeue().SetResult(_fault);

                        if (!_isProcessingItem)
                            _completedEvent.TrySetResult(true);
                    }
                }

                return _completedEvent.Task;
            }   

            private void FailAllPendingItems()
            {
                while (_asyncQueue.Count > 0)
                    _asyncQueue.Dequeue().SetResult(_fault);
            }

            private class PendingItemTask : TaskCompletionSource<RpcResult>
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
