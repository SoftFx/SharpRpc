// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Streaming;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SharpRpc
{
    public sealed class PagingStreamWriter<T> : StreamWriter<T>
    {
        private readonly object _lockObj = new object();
        //private readonly Queue<IStreamPage<T>> _queueCompletePages = new Queue<IStreamPage<T>>();
        private readonly Queue<IStreamPage<T>> _unsuedPagesCache = new Queue<IStreamPage<T>>();
        private IStreamPage<T> _queue;
        private IStreamPage<T> _pageToSend;
        private readonly bool _canImmediatelyReusePages;
        private readonly Channel _ch;
        private readonly Queue<EnqueueAwaiter> _enqueueAwaiters = new Queue<EnqueueAwaiter>();
        //private readonly List<EnqueueAwaiter> _awaitersToRelease = new List<EnqueueAwaiter>();
        private readonly TaskCompletionSource<RpcResult> _completionEventSrc = new TaskCompletionSource<RpcResult>();
        private bool _isClosed;
        private bool _isAbroted;
        private bool _isSendingEnabled;
        private RpcResult _closeFault;
        private bool _isSedning;
        private int _maxPageSize;
        private int _windowSize;
        private readonly IStreamMessageFactory<T> _factory;
        private readonly StreamWriteCoordinator _coordinator;

        internal PagingStreamWriter(string callId, Channel channel, IStreamMessageFactory<T> factory, bool allowSendInitialValue, StreamOptions options)
        {
            CallId = callId;
            _ch = channel;
            _factory = factory;
            //_maxPageSize = maxPageSize;
            _windowSize = options?.WindowSize ?? StreamOptions.DefaultWindowsSize;

            if (_windowSize < 1)
                _windowSize = StreamOptions.DefaultWindowsSize;

            _maxPageSize = options.WindowSize / 8;

            if (_maxPageSize < 1)
                _maxPageSize = 1;

            _isSendingEnabled = allowSendInitialValue;

            _canImmediatelyReusePages = channel.Tx.ImmidiateSerialization;
            
            _queue = CreatePage();
            if (_canImmediatelyReusePages)
                _pageToSend = CreatePage(); 

            _coordinator = new StreamWriteCoordinator(_lockObj, _windowSize);
        }

        private bool DataIsAvailable => _queue.Items.Count > 0;
        private bool HasSpaceInQueue => _queue.Items.Count < _maxPageSize;

        public string CallId { get; }

        public Task<RpcResult> CompleteAsync()
        {
            MarkAsCompleted();
            return _completionEventSrc.Task;
        }

#if NET5_0_OR_GREATER
        public ValueTask<RpcResult> WriteAsync(T item)
#else
        public Task<RpcResult> WriteAsync(T item)
#endif
        {
            bool sendNextPage = false;

            lock (_lockObj)
            {
                if (_isClosed)
                    return FwAdapter.WrappResult(_closeFault);

                if (HasSpaceInQueue)
                {
                    EnqueueItem(item);
                    sendNextPage = OnDataArrived();
                }
                else
                {
                    var waitHandler = new EnqueueAwaiter(item);
                    _enqueueAwaiters.Enqueue(waitHandler);
                    return FwAdapter.WrappResult(waitHandler.Task);
                }       
            }

            if (sendNextPage)
                SendNextPage();

            return FwAdapter.AsyncRpcOk;
        }

        public void MarkAsCompleted()
        {
            lock (_lockObj)
            {
                if (!_isClosed)
                    CloseStream(false, new RpcResult(RpcRetCode.StreamCompleted, "The stream is completed and does not accept additions."));
            }
        }

        #region Control methods

        internal void AllowSend()
        {
            lock (_lockObj)
            {
                _isSendingEnabled = true;

                if (!_isSedning && DataIsAvailable && !_coordinator.IsBlocked)
                {
                    _isSedning = true;
                    _pageToSend = DequeuePage();
                }
                else
                    return;
            }

            SendNextPage();
        }

        internal void Close(RpcResult fault)
        {
            lock (_lockObj)
            {
                if (!_isClosed)
                    CloseStream(false, fault);
            }
        }

        internal void Abort(RpcResult fault)
        {
            lock (_lockObj)
            {
                if (!_isClosed)
                    CloseStream(true, fault);
            }
        }

        internal void Cancel()
        {
            lock (_lockObj)
            {
                if (!_isClosed)
                    CloseStream(true, new RpcResult(RpcRetCode.OperationCanceled, "The operation was canceled by the user!"));
            }
        }

        #endregion

        internal void OnRx(IStreamPageAck ack)
        {
            lock (_lockObj)
            {
                _coordinator.OnAcknowledgementRx(ack);

                if (DataIsAvailable && !_isSedning && !_coordinator.IsBlocked)
                {
                    _isSedning = true;
                    _pageToSend = DequeuePage();
                }
                else
                    return;
            }

            SendNextPage();
        }

        private void EnqueueItem(T item)
        {
            _queue.Items.Add(item);
        }

        private bool OnDataArrived()
        {
            if (!_isSedning && !_coordinator.IsBlocked && _isSendingEnabled)
            {
                _isSedning = true;
                _pageToSend = DequeuePage();
                return true;
            }

            return false;
        }

        private void SendNextPage()
        {
            //_pageToSend = DequeuePage();

            _ch.Tx.TrySendAsync(_pageToSend, OnSendCompleted);
        }

        private void OnSendCompleted(RpcResult sendResult)
        {
            bool sendNextPage = false;
            int pageSize = 0;

            lock (_lockObj)
            {
                if (_pageToSend != null)
                {
                    pageSize = _pageToSend.Items.Count;

                    if (_canImmediatelyReusePages)
                        _pageToSend.Items.Clear();
                    else
                        _pageToSend = null;
                }
                
                if (sendResult.IsOk)
                {
                    _coordinator.OnPageSent(pageSize);

                    if(_enqueueAwaiters.Count > 0)
                        ProcessAwaiters();

                    if (DataIsAvailable && !_coordinator.IsBlocked)
                    {
                        _pageToSend = DequeuePage();
                        sendNextPage = true;
                    }
                    else
                    {
                        _isSedning = false;

                        if (_isClosed)
                            CompleteStream();
                    }
                }
                else
                    CloseStream(true, sendResult);
            }

            if (sendNextPage)
                SendNextPage();
        }

        private void ProcessAwaiters()
        {
            while (_enqueueAwaiters.Count > 0)
            {
                var awaiter = _enqueueAwaiters.Dequeue();
                EnqueueItem(awaiter.Item);
                awaiter.Result = RpcResult.Ok;
                Task.Factory.StartNew(awaiter.Fire);

                if (!HasSpaceInQueue)
                    break;
            }
        }

        private void AbortAwaiters(RpcResult result)
        {
            while (_enqueueAwaiters.Count > 0)
            {
                var awaiter = _enqueueAwaiters.Dequeue();
                awaiter.Result = result;
                Task.Factory.StartNew(awaiter.Fire);
            }
        }

        private void CloseStream(bool abortMode, RpcResult fault)
        {
            _closeFault = fault;
            _isClosed = true;

            AbortAwaiters(_closeFault);

            if (abortMode)
            {
                _queue.Items.Clear();
                _isAbroted = true;
            }

            if (!_isSedning && !DataIsAvailable)
                CompleteStream();
        }

        private void CompleteStream()
        {
            if (!_isAbroted)
            {
                var complMessage = _factory.CreateCompletionMessage(CallId);
                _ch.Tx.TrySendAsync(complMessage, OnCompletionMessageSent);
            }
            else
                Task.Factory.StartNew(FireCompletion);
        }

        private void OnCompletionMessageSent(RpcResult result)
        {
            FireCompletion();
        }

        private void FireCompletion()
        {
            _completionEventSrc.TrySetResult(RpcResult.Ok);
        }

        private IStreamPage<T> CreatePage()
        {
            var page = _factory.CreatePage(CallId);
            page.Items = new List<T>();
            return page;
        }

        private IStreamPage<T> AllocatePage()
        {
            if (_unsuedPagesCache.Count > 0)
                return _unsuedPagesCache.Dequeue();
            else
                return CreatePage();
        }

        private void FreePage(IStreamPage<T> page)
        {
            _unsuedPagesCache.Enqueue(page);
        }

        private IStreamPage<T> DequeuePage()
        {
            var page = _queue;

            if (_canImmediatelyReusePages)
                _queue = _pageToSend;
            else
                _queue = AllocatePage();

            return page;
        }

        private class EnqueueAwaiter : TaskCompletionSource<RpcResult>
        {
            public EnqueueAwaiter(T item)
            {
                Item = item;
            }

            public T Item { get; }
            public RpcResult Result { get; set; } = RpcResult.Ok;

            public void Fire()
            {
                SetResult(Result);
            }
        }
    }
}
