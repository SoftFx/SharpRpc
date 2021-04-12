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
            private bool isClosing;
            private bool _isInitialized;
            private bool _isInitializing;
            private RpcResult _fault;
            private readonly int _bufferSizeThreshold;
            private readonly Queue<IPendingItem> _asyncQueue = new Queue<IPendingItem>();
            private readonly TaskCompletionSource<object> _completedEvent = new TaskCompletionSource<object>();
            private DateTime _lastTxTime = DateTime.MinValue;
            private readonly TimeSpan _idleThreshold;
            private readonly Timer _keepAliveTimer;
            private readonly IMessage _keepAliveMessage;

            public NoQueue(ContractDescriptor descriptor, Endpoint config, Func<Task<RpcResult<ByteTransport>>> transportRequestFunc)
                : base(transportRequestFunc)
            {
                _buffer = new TxBuffer(_lockObj, config.TxBufferSegmentSize, descriptor.SerializationAdapter);
                _bufferSizeThreshold = config.TxBufferSegmentSize * 2;
                _buffer.SpaceFreed += _buffer_SpaceFreed;

                if (config.IsKeepAliveEnabled)
                {
                    _keepAliveTimer = new Timer(OnKeepAliveTimerTick, null, 100, 100);
                    _buffer.OnDequeue += RefreshLastTxTime;
                    _idleThreshold = config.KeepAliveThreshold;
                    _keepAliveMessage = descriptor.SystemMessages.CreateHeartBeatMessage();
                }
            }

            private bool CanProcessNextMessage => _isInitialized &&!_isProcessingItem && HasRoomForNextMessage;
            private bool HasRoomForNextMessage => _buffer.DataSize < _bufferSizeThreshold;

            public override RpcResult TrySend(IMessage message)
            {
                lock (_lockObj)
                {
                    if (_fault.Code != RpcRetCode.Ok)
                        return _fault;

                    LazyInit();

                    while (!CanProcessNextMessage)
                    {
                        Monitor.Wait(_lockObj);

                        if (_fault.Code != RpcRetCode.Ok)
                            return _fault;
                    }

                    _isProcessingItem = true;
                    _buffer.Lock();
                }

                return ProcessMessage(message);
            }

            public override ValueTask<RpcResult> TrySendAsync(IMessage message)
            {
                lock (_lockObj)
                {
                    if (_fault.Code != RpcRetCode.Ok)
                        return new ValueTask<RpcResult>(_fault);

                    LazyInit();

                    if (!CanProcessNextMessage)
                    {
                        var waitItem = new AsyncTryItem(message);
                        _asyncQueue.Enqueue(waitItem);
                        return new ValueTask<RpcResult>(waitItem.Task);
                    }
                    else
                    {
                        _isProcessingItem = true;
                        _buffer.Lock();
                    }
                }

                return new ValueTask<RpcResult>(ProcessMessage(message));
            }

            public override ValueTask SendAsync(IMessage message)
            {
                lock (_lockObj)
                {
                    _fault.ThrowIfNotOk();

                    LazyInit();

                    if (!CanProcessNextMessage)
                    {
                        var waitItem = new AsyncThrowItem(message);
                        _asyncQueue.Enqueue(waitItem);
                        return new ValueTask(waitItem.Task);
                    }
                    else
                    {
                        _isProcessingItem = true;
                        _buffer.Lock();
                    }
                }

                ProcessMessage(message).ThrowIfNotOk();

                return new ValueTask();
            }

            public override void Send(IMessage message)
            {
                TrySend(message).ThrowIfNotOk();
            }

            private void LazyInit()
            {
                if (!_isInitialized && !_isInitializing)
                {
                    _isInitializing = true;
                    Initialize();
                }
            }

            private async void Initialize()
            {
                // exit lock
                await Task.Yield();

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

            private RpcResult ProcessMessage(IMessage msg)
            {
                try
                {
                    _buffer.WriteMessage(msg);
                    return RpcResult.Ok;
                }
                catch (RpcException rex)
                {
                    return OnTxError(rex.ToRpcResult());
                }
                catch (Exception ex)
                {
                    return OnTxError(new RpcResult(RpcRetCode.SerializationError, ex.Message));
                }
                finally
                {
                    lock (_lockObj)
                    {
                        _isProcessingItem = false;

                        if (!isClosing)
                            EnqueueNextItem();
                        else
                            CompleteClose();
                    }
                }
            }

            private RpcResult OnTxError(RpcResult error)
            {
                lock (_lockObj)
                {
                    // TO DO: stop TxPipeline immediately
                }

                SignalCommunicationError(error);

                return error;
            }

            protected override ValueTask<ArraySegment<byte>> DequeueNextSegment()
            {
                return _buffer.DequeueNext();
            }

            private void EnqueueNextItem()
            {
                if (_asyncQueue.Count > 0)
                {
                    _isProcessingItem = true;
                    _buffer.Lock();

                    var nextAsyncItem = _asyncQueue.Dequeue();

                    Task.Factory.StartNew(p =>
                    {
                        var task = (IPendingItem)p;
                        task.OnResult(ProcessMessage(task.Message));
                    }, nextAsyncItem);
                }
                else
                {
                    Monitor.Pulse(_lockObj);
                }
            }

            public override async Task Close(RpcResult fault)
            {
                await ClosePipeline(fault);
                await WaitTransportReadToEnd();
            }

            private Task ClosePipeline(RpcResult fault)
            {
                lock (_lockObj)
                {
                    if (!isClosing)
                    {
                        isClosing = true;
                        _fault = fault;

                        _keepAliveTimer?.Dispose();

                        Monitor.PulseAll(_lockObj);

                        while (_asyncQueue.Count > 0)
                            _asyncQueue.Dequeue().OnResult(_fault);

                        if (!_isProcessingItem)
                            _completedEvent.TrySetResult(true);

                        _buffer.Close();
                    }
                }

                return _completedEvent.Task;
            }

            private void CompleteClose()
            {
                lock (_lockObj)
                {
                    _completedEvent.TrySetResult(true);
                }
            }

            private void FailAllPendingItems()
            {
                while (_asyncQueue.Count > 0)
                    _asyncQueue.Dequeue().OnResult(_fault);
            }

            private void OnKeepAliveTimerTick(object state)
            {
                lock (_lockObj)
                {
                    if (DateTime.UtcNow - _lastTxTime > _idleThreshold)
                        TrySendAsync(_keepAliveMessage);
                }
            }

            private void RefreshLastTxTime()
            {
                _lastTxTime = DateTime.UtcNow;
            }

            private interface IPendingItem
            {
                IMessage Message { get; }
                void OnResult(RpcResult result);
            }

            private class AsyncThrowItem : TaskCompletionSource<RpcResult>, IPendingItem
            {
                public AsyncThrowItem(IMessage item)
                {
                    Message = item;
                }

                public IMessage Message { get; }

                public void OnResult(RpcResult result)
                {
                    SetResult(result);
                }
            }

            private class AsyncTryItem : TaskCompletionSource<RpcResult>, IPendingItem
            {
                public AsyncTryItem(IMessage item)
                {
                    Message = item;
                }

                public IMessage Message { get; }

                public void OnResult(RpcResult result)
                {
                    if (result.Code == RpcRetCode.Ok)
                        SetResult(result);
                    else
                        TrySetException(result.ToException());
                }
            }

            private void _buffer_SpaceFreed(TxBuffer sender)
            {
                if (CanProcessNextMessage)
                    EnqueueNextItem();
            }
        }
    }
}
