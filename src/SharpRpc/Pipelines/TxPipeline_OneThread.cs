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
//    internal class TxPipeline_OneThread : TxPipeline
//    {
//        private readonly object _lockObj = new object();
//        private readonly object _bufferLock = new object();
//        private readonly TxBuffer _buffer;
//        private bool _isClosing;
//        private bool _isStarted;
//        private bool _isRequestedConnection;
//        private bool _isUserMessagesEnabled;
//        private RpcResult _fault;
//        private readonly int _bufferSizeThreshold;
//        private readonly TxAsyncGate _asyncGate = new TxAsyncGate();
//        private readonly TaskCompletionSource<object> _closeCompletedEvent = new TaskCompletionSource<object>();
//        private DateTime _lastTxTime = DateTime.MinValue;
//        private readonly Action<RpcResult> _commErrorHandler;
//        private readonly Action _connectionRequestHandler;
//        private readonly TxTransportFeed _feed;
//        private readonly bool _useDelayedWorker = false;

//        public TxPipeline_OneThread(string channelId, ContractDescriptor descriptor, Endpoint config, Action<RpcResult> commErrorHandler, Action connectionRequestHandler)
//        {
//            ChannelId = channelId;

//            _commErrorHandler = commErrorHandler;
//            _connectionRequestHandler = connectionRequestHandler;

//            TaskQueue = config.TaskQueue;
//            MessageFactory = descriptor.SystemMessages;

//            _buffer = new TxBuffer(_bufferLock, config.TxBufferSegmentSize, descriptor.SerializationAdapter);
//            _bufferSizeThreshold = config.TxBufferSegmentSize * 2;
//            _buffer.SpaceFreed += _buffer_SpaceFreed;

//            _feed = new TxTransportFeed(_buffer, commErrorHandler);

//            var workerDelay = TimeSpan.Zero; // TimeSpan.FromMilliseconds(5);
//            _useDelayedWorker = workerDelay > TimeSpan.Zero;

//            if (_useDelayedWorker)
//                _workerExecutor = new TxExecDelay(ProcessBatch, TaskQueue, workerDelay, _lockObj);

//            //if (config.IsKeepAliveEnabled)
//            //{
//            //    _keepAliveTimer = new Timer(OnKeepAliveTimerTick, null, 100, 100);
//            //    _buffer.OnDequeue += RefreshLastTxTime;
//            //    _idleThreshold = config.KeepAliveThreshold;
//            //    _keepAliveMessage = descriptor.SystemMessages.CreateHeartBeatMessage();
//            //}
//        }

//        private bool CanEnqueueUserMessage => _isUserMessagesEnabled && HasRoomInQueue;
//        private bool CanEnqueueSystemMessage => _isStarted && HasRoomInQueue;
//        private bool HasRoomInQueue => _queue.Count < 220;
//        private bool HasRoomInBuffer => _buffer.DataSize < _bufferSizeThreshold;

//        public string ChannelId { get; }

//        #region Consumer impl

//        private bool _isProcessingBatch;
//        private bool _isWaitingForSomeSpaceInBuffer;
//        private Queue<IMessage> _batch = new Queue<IMessage>();
//        private Queue<IMessage> _queue = new Queue<IMessage>();
//        private readonly TxExecDelay _workerExecutor;

//        private void Enqueue(IMessage message)
//        {
//            Debug.Assert(message != null);

//            _queue.Enqueue(message);
//            OnDataArrived();
//        }

//        private void OnDataArrived()
//        {
//            if (!_isProcessingBatch)
//                LaunchBatchTask();
//            else if (_useDelayedWorker && !HasRoomInQueue)
//                _workerExecutor.Force();
//        }

//        private void OnBatchCompleted()
//        {
//            _isProcessingBatch = false;
//            SignalAllAwaiters();
//            if (_queue.Count > 0)
//                LaunchBatchTask();
//            else if (_isClosing)
//                CompleteClose();
//        }

//        private void SignalAllAwaiters()
//        {
//            while (true)
//            {
//                var nextAwaiter = _asyncGate.Dequeue(_isUserMessagesEnabled);
//                if (nextAwaiter == null)
//                    break;
//                _queue.Enqueue(nextAwaiter.Message);
//                TaskQueue.StartNew(a => ((TxAsyncGate.Item)a).OnResult(RpcResult.Ok), nextAwaiter);
//            }

//            Monitor.PulseAll(_lockObj);
//        }

//        private void LaunchBatchTask()
//        {
//            Debug.Assert(_batch.Count == 0);
//            Debug.Assert(!_isProcessingBatch);

//            _isProcessingBatch = true;

//            var cpy = _batch;
//            _batch = _queue;
//            _queue = cpy;

//            if (_useDelayedWorker)
//                _workerExecutor.TriggerOn();
//            else
//                TaskQueue.StartNew(ProcessBatch);
//        }

//        private void _buffer_SpaceFreed(TxBuffer sender)
//        {
//            if (_isWaitingForSomeSpaceInBuffer)
//            {
//                _isWaitingForSomeSpaceInBuffer = false;
//                TaskQueue.StartNew(ProcessBatch);
//            }
//        }

//        #endregion

//        private void ProcessBatch()
//        {
//            while (_batch.Count > 0)
//            {
//                lock (_bufferLock)
//                {
//                    if (HasRoomInBuffer)
//                        _buffer.Lock();
//                    else
//                    {
//                        Debug.Assert(!_isWaitingForSomeSpaceInBuffer);
//                        _isWaitingForSomeSpaceInBuffer = true;
//                        return;
//                    }
//                }

//                var result = ProcessMessage(_batch.Dequeue());

//                if (!result.IsOk)
//                {
//                    _commErrorHandler(result);

//                    lock (_lockObj)
//                    {
//                        _isProcessingBatch = false;
//                        _batch.Clear();

//                        //  TO DO : Stop all processing!
//                        OnBatchCompleted();
//                        return;
//                    }
//                }
//            }

//            lock (_lockObj)
//                OnBatchCompleted();
//        }

//        private RpcResult ProcessMessage(IMessage msg)
//        {
//            try
//            {
//                _buffer.WriteMessage(msg);
//                return RpcResult.Ok;
//            }
//            catch (RpcException rex)
//            {
//                return rex.ToRpcResult();
//            }
//            catch (Exception ex)
//            {
//                return new RpcResult(RpcRetCode.SerializationError, ex.Message);
//            }
//            finally
//            {
//                //lock (_lockObj)
//                //{
//                //    _isProcessingItem = false;

//                //    if (!_isClosing)
//                //        EnqueueNextItem();
//                //    else
//                //    {
//                //        if (_asyncGate.UserQueueSize == 0)
//                //            CompleteClose();
//                //    }
//                //}
//            }
//        }

//        #region TxPipeline impl

//        public TaskFactory TaskQueue { get; }
//        public IMessageFactory MessageFactory { get; }
//        public bool ImmediateSerialization => false;

//        public void Start(ByteTransport transport)
//        {
//            lock (_lockObj)
//            {
//                _isStarted = true;
//                _feed.StartTransportWrite(transport);
//                SignalAllAwaiters();
//                if (_queue.Count > 0)
//                    OnDataArrived();
//            }
//        }

//        public void StartProcessingUserMessages()
//        {
//            lock (_lockObj)
//            {
//                if (_isStarted && !_isClosing)
//                {
//                    _isUserMessagesEnabled = true;
//                    SignalAllAwaiters();
//                    if (_queue.Count > 0)
//                        OnDataArrived();
//                }
//                else
//                    throw new Exception("Invalid state!");
//            }
//        }

//        public void StopProcessingUserMessages(RpcResult fault)
//        {
//            lock (_lockObj)
//            {
//                if (!_isClosing)
//                {
//                    _isUserMessagesEnabled = false;
//                    _fault = fault;
//                }
//            }
//        }

//        public async Task Close(TimeSpan gracefulCloseTimeout)
//        {
//            await ClosePipeline(gracefulCloseTimeout);
//            await _feed.WaitTransportWaitToEnd();
//        }

//        private Task ClosePipeline(TimeSpan gracefulCloseTimeout)
//        {
//            bool gracefulClose = gracefulCloseTimeout > TimeSpan.Zero;

//            lock (_lockObj)
//            {
//                if (!_isClosing)
//                {
//                    _isClosing = true;

//                    //_keepAliveTimer?.Dispose();

//                    Monitor.PulseAll(_lockObj);

//                    if (!gracefulClose)
//                        _asyncGate.CancelUserItems(_fault);

//                    if (!_isProcessingBatch)
//                        CompleteClose();

//                    if (gracefulClose)
//                        _feed.AbortTransportWriteAfter(gracefulCloseTimeout);
//                    else
//                        _feed.AbortTransportWrite();
//                }
//            }

//            return _closeCompletedEvent.Task;
//        }

//        private void CompleteClose()
//        {
//            _buffer.Close();
//            _closeCompletedEvent.TrySetResult(true);
//        }

//        public void Send(IMessage message)
//        {
//            TrySend(message).ThrowIfNotOk();
//        }

//#if NET5_0_OR_GREATER
//        public ValueTask SendAsync(IMessage message)
//#else
//        public Task SendAsync(IMessage message)
//#endif
//        {
//            lock (_lockObj)
//            {
//                _fault.ThrowIfNotOk();

//                CheckConnectionFlags();

//                if (CanEnqueueUserMessage)
//                {
//                    Enqueue(message);
//                    return FwAdapter.AsyncVoid;
//                }
//                else
//                    return FwAdapter.WrappResult((Task)_asyncGate.Enqueue(message, true, false));
//            }
//        }

//#if NET5_0_OR_GREATER
//        public ValueTask<RpcResult> SendSystemMessage(ISystemMessage message)
//#else
//        public Task<RpcResult> SendSystemMessage(ISystemMessage message)
//#endif
//        {
//            lock (_lockObj)
//            {
//                if (_isClosing)
//                    return FwAdapter.WrappResult(_fault);

//                CheckConnectionFlags();

//                if (CanEnqueueSystemMessage)
//                {
//                    Enqueue(message);
//                    return FwAdapter.AsyncRpcOk;
//                }
//                else
//                    return FwAdapter.WrappResult(_asyncGate.Enqueue(message, false, false));
//            }
//        }

//        public RpcResult TrySend(IMessage message)
//        {
//            lock (_lockObj)
//            {
//                if (_fault.Code != RpcRetCode.Ok)
//                    return _fault;

//                CheckConnectionFlags();

//                while (!CanEnqueueUserMessage)
//                {
//                    Monitor.Wait(_lockObj);

//                    if (_fault.Code != RpcRetCode.Ok)
//                        return _fault;
//                }

//                Enqueue(message);
//                return RpcResult.Ok;
//            }
//        }

//#if NET5_0_OR_GREATER
//        public ValueTask<RpcResult> TrySendAsync(IMessage message)
//#else
//        public Task<RpcResult> TrySendAsync(IMessage message)
//#endif
//        {
//            lock (_lockObj)
//            {
//                if (_fault.Code != RpcRetCode.Ok)
//                    return FwAdapter.WrappResult(_fault);

//                CheckConnectionFlags();

//                if (CanEnqueueUserMessage)
//                {
//                    Enqueue(message);
//                    return FwAdapter.AsyncRpcOk;
//                }
//                else
//                    return FwAdapter.WrappResult(_asyncGate.Enqueue(message, false, false));
//            }
//        }

//        public void TrySendAsync(IMessage message, Action<RpcResult> onSendCompletedCallback)
//        {
//            lock (_lockObj)
//            {
//                if (_fault.Code != RpcRetCode.Ok)
//                    onSendCompletedCallback(_fault);

//                CheckConnectionFlags();

//                if (CanEnqueueUserMessage)
//                {
//                    Enqueue(message);
//                    onSendCompletedCallback(RpcResult.Ok);
//                }
//                else
//                    _asyncGate.Enqueue(message, onSendCompletedCallback);
//            }
//        }

//        public bool TryCancelSend(IMessage message)
//        {
//            return false;
//        }

//        #endregion

//        private void CheckConnectionFlags()
//        {
//            if (!_isStarted && !_isRequestedConnection)
//            {
//                _isRequestedConnection = true;
//                _connectionRequestHandler();
//            }
//        }
//    }
}
