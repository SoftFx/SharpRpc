﻿// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
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
            private bool _isClosing;
            private bool _isStarted;
            private bool _isRequestedConnection;
            private bool _isUserMessagesEnabled;
            private RpcResult _fault;
            private readonly int _bufferSizeThreshold;
            private readonly Queue<IPendingItem> _asyncQueue = new Queue<IPendingItem>();
            private readonly Queue<IPendingItem> _systemQueue = new Queue<IPendingItem>();
            private readonly TaskCompletionSource<object> _completedEvent = new TaskCompletionSource<object>();
            private DateTime _lastTxTime = DateTime.MinValue;
            private readonly TimeSpan _idleThreshold;
            private readonly Timer _keepAliveTimer;
            private readonly ISystemMessage _keepAliveMessage;

            public NoQueue(ContractDescriptor descriptor, Endpoint config)
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

            private bool CanProcessUserMessage => _isUserMessagesEnabled && !_isProcessingItem && HasRoomForNextMessage;
            private bool CanProcessSystemMessage => _isStarted && !_isProcessingItem && HasRoomForNextMessage;
            private bool HasRoomForNextMessage => _buffer.DataSize < _bufferSizeThreshold;

            public override RpcResult TrySend(IMessage message)
            {
                lock (_lockObj)
                {
                    if (_fault.Code != RpcRetCode.Ok)
                        return _fault;

                    CheckConnectionFlags();

                    while (!CanProcessUserMessage)
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

#if NET5_0_OR_GREATER
            public override ValueTask<RpcResult> TrySendAsync(IMessage message)
#else
            public override Task<RpcResult> TrySendAsync(IMessage message)
#endif
            {
                lock (_lockObj)
                {
                    if (_fault.Code != RpcRetCode.Ok)
                        return FwAdapter.WrappResult(_fault);

                    CheckConnectionFlags();

                    if (!CanProcessUserMessage)
                    {
                        var waitItem = new AsyncTryItem(message);
                        _asyncQueue.Enqueue(waitItem);
                        return FwAdapter.WrappResult(waitItem.Task);
                    }
                    else
                    {
                        _isProcessingItem = true;
                        _buffer.Lock();
                    }
                }

                return FwAdapter.WrappResult(ProcessMessage(message));
            }


#if NET5_0_OR_GREATER
            public override ValueTask<RpcResult> SendSystemMessage(ISystemMessage message)
#else
            public override Task<RpcResult> SendSystemMessage(ISystemMessage message)
#endif
            {
                lock (_lockObj)
                {
                    if (_isClosing)
                        return FwAdapter.WrappResult(_fault);

                    CheckConnectionFlags();

                    if (!CanProcessSystemMessage)
                    {
                        var waitItem = new AsyncTryItem(message);
                        _systemQueue.Enqueue(waitItem);
                        return FwAdapter.WrappResult(waitItem.Task);
                    }
                    else
                    {
                        _isProcessingItem = true;
                        _buffer.Lock();
                    }
                }

                return FwAdapter.WrappResult(ProcessMessage(message));
            }

            private void CasuallySend(ISystemMessage message)
            {
                lock (_lockObj)
                {
                    if (_isClosing || !CanProcessSystemMessage)
                        return;

                    _isProcessingItem = true;
                    _buffer.Lock();
                }

                ProcessMessage(message);
            }

#if NET5_0_OR_GREATER
            public override ValueTask SendAsync(IMessage message)
#else
            public override Task SendAsync(IMessage message)
#endif
            {
                lock (_lockObj)
                {
                    _fault.ThrowIfNotOk();

                    CheckConnectionFlags();

                    if (!CanProcessUserMessage)
                    {
                        var waitItem = new AsyncThrowItem(message);
                        _asyncQueue.Enqueue(waitItem);
                        return FwAdapter.WrappResult((Task)waitItem.Task);
                    }
                    else
                    {
                        _isProcessingItem = true;
                        _buffer.Lock();
                    }
                }

                ProcessMessage(message).ThrowIfNotOk();

                return FwAdapter.AsyncVoid;
            }

            public override void Send(IMessage message)
            {
                TrySend(message).ThrowIfNotOk();
            }

            private void CheckConnectionFlags()
            {
                if (!_isStarted && !_isRequestedConnection)
                {
                    _isRequestedConnection = true;
                    RequestConnection();
                }
            }

            private async void RequestConnection()
            {
                // exit lock
                await Task.Yield();

                SignalConnectionRequest();
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

                        if (!_isClosing)
                            EnqueueNextItem();
                        else
                        {
                            if (_asyncQueue.Count == 0)
                                CompleteClose();
                        }
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

#if NET5_0_OR_GREATER
            protected override ValueTask<ArraySegment<byte>> DequeueNextSegment()
#else
            protected override Task<ArraySegment<byte>> DequeueNextSegment()
#endif
            {
                return _buffer.DequeueNext();
            }

            private void EnqueueNextItem()
            {
                var nextPending = DequeuePending();

                if (nextPending != null)
                {
                    _isProcessingItem = true;
                    _buffer.Lock();

                    Task.Factory.StartNew(p =>
                    {
                        var task = (IPendingItem)p;
                        task.OnResult(ProcessMessage(task.Message));
                    }, nextPending);
                }
                else
                {
                    Monitor.PulseAll(_lockObj);
                }
            }

            private IPendingItem DequeuePending()
            {
                if (_systemQueue.Count > 0)
                    return _systemQueue.Dequeue();
                else if (_isUserMessagesEnabled && _asyncQueue.Count > 0)
                    return _asyncQueue.Dequeue();
                else
                    return null;
            }

            public override void Start(ByteTransport transport)
            {
                lock (_lockObj)
                {
                    _isStarted = true;

                    Transport = transport;
                    StartTransportWrite();
                    EnqueueNextItem();
                }
            }

            public override void StartProcessingUserMessages()
            {
                lock (_lockObj)
                {
                    if (_isStarted && !_isClosing)
                    {
                        _isUserMessagesEnabled = true;
                        EnqueueNextItem();
                    }
                    else
                        throw new Exception("Invalid state!");
                }
            }

            public override void StopProcessingUserMessages(RpcResult fault)
            {
                lock (_lockObj)
                {
                    if (!_isClosing)
                    {
                        _isUserMessagesEnabled = false;
                        _fault = fault;

                        Monitor.PulseAll(_lockObj);

                        while (_asyncQueue.Count > 0)
                            _asyncQueue.Dequeue().OnResult(_fault);
                    }
                }
            }

            public override async Task Close(TimeSpan gracefulCloseTimeout)
            {
                await ClosePipeline(gracefulCloseTimeout);
                await WaitTransportWaitToEnd();
            }

            private Task ClosePipeline(TimeSpan gracefulCloseTimeout)
            {
                bool gracefulClose = gracefulCloseTimeout > TimeSpan.Zero;

                lock (_lockObj)
                {
                    if (!_isClosing)
                    {
                        _isClosing = true;

                        _keepAliveTimer?.Dispose();

                        Monitor.PulseAll(_lockObj);

                        if (!gracefulClose)
                        {
                            while (_systemQueue.Count > 0)
                                _asyncQueue.Dequeue().OnResult(_fault);
                        }

                        if (!_isProcessingItem)
                            CompleteClose();

                        if (gracefulClose)
                            AbortTransportWriteAfter(gracefulCloseTimeout);
                        else
                            AbortTransportRead();
                    }
                }

                return _completedEvent.Task;
            }

            private void CompleteClose()
            {
                _buffer.Close();
                _completedEvent.TrySetResult(true);
            }

            private void OnKeepAliveTimerTick(object state)
            {
                lock (_lockObj)
                {
                    if (DateTime.UtcNow - _lastTxTime > _idleThreshold)
                        CasuallySend(_keepAliveMessage);
                }
            }

            private void RefreshLastTxTime()
            {
                _lastTxTime = DateTime.UtcNow;
            }

            private void _buffer_SpaceFreed(TxBuffer sender)
            {
                if (_isStarted && !_isProcessingItem)
                    EnqueueNextItem();
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
                    if (result.Code == RpcRetCode.Ok)
                        SetResult(result);
                    else
                        TrySetException(result.ToException());
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
                    SetResult(result);
                }
            }
        }
    }
}
