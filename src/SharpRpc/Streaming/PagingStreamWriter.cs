// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Lib;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public sealed class PagingStreamWriter<T> : StreamWriter<T>
    {
        private readonly object _lockObj = new object();
        private readonly Queue<IStreamPage<T>> _queueCompletePages = new Queue<IStreamPage<T>>();
        private readonly Queue<IStreamPage<T>> _unsuedPagesCache = new Queue<IStreamPage<T>>();
        private IStreamPage<T> _queueTopPage;
        private IStreamPage<T> _pageToSend;
        private readonly bool _isPageCacheEnabled;
        private readonly Channel _ch;
        private readonly Queue<EnqueueAwaiter> _enqueueAwaiters = new Queue<EnqueueAwaiter>();
        //private readonly List<EnqueueAwaiter> _awaitersToRelease = new List<EnqueueAwaiter>();
        private readonly TaskCompletionSource<RpcResult> _completionEventSrc = new TaskCompletionSource<RpcResult>();
        private bool _isClosed;
        private bool _isSendingEnabled;
        private RpcResult _closeFault;
        private bool _isSedning;
        private int _maxPageSize;
        private int _maxWindowSize;
        private readonly IStreamMessageFactory<T> _factory;

        internal PagingStreamWriter(string callId, Channel channel, IStreamMessageFactory<T> factory, bool allowSendInitialValue, int maxPageSize, int maxWindowSize)
        {
            CallId = callId;
            _ch = channel;
            _factory = factory;
            _maxPageSize = maxPageSize;

            _isPageCacheEnabled = channel.Tx.ProvidesImmidiateSerialization;
            _isSendingEnabled = allowSendInitialValue;

            _queueTopPage = AllocatePage();
            _maxWindowSize = maxWindowSize;
        }

        private bool DataIsAvailable => _queueTopPage.Items.Count > 0 || _queueCompletePages.Count > 0;
        private bool HasSpaceInQueue => _queueCompletePages.Count < _maxWindowSize;

        public string CallId { get; }
        public int QueueSize { get; private set; }

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
                {
                    _isClosed = true;
                    _closeFault = new RpcResult(RpcRetCode.StreamCompleted, "Stream is completed and does not accept additions.");

                    if (!_isSedning && !DataIsAvailable)
                        CompleteStream();
                }
            }
        }

        public Task<RpcResult> CompleteAsync()
        {
            MarkAsCompleted();
            return _completionEventSrc.Task;
        }

        #region Control methods

        internal void AllowSend()
        {
            bool invokeSend = false;

            lock (_lockObj)
            {
                _isSendingEnabled = true;
                invokeSend = _isSedning;
            }

            if (invokeSend)
                SendNextPage();
        }

        internal void Close(RpcResult fault)
        {
            lock (_lockObj)
                AbortStream(fault);
        }

        #endregion

        private void EnqueueItem(T item)
        {
            _queueTopPage.Items.Add(item);
            if (_queueTopPage.Items.Count >= _maxPageSize)
            {
                _queueCompletePages.Enqueue(_queueTopPage);
                _queueTopPage = AllocatePage();
            }
        }

        private bool OnDataArrived()
        {
            if (!_isSedning)
            {
                _isSedning = true;
                _pageToSend = DequeuePage();
                return _isSendingEnabled;
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

            lock (_lockObj)
            {
                if (_pageToSend != null)
                {
                    if (_isPageCacheEnabled)
                    {
                        _pageToSend.Items.Clear();
                        FreePage(_pageToSend);
                    }
                    _pageToSend = null;
                }
                
                if (sendResult.IsOk)
                {
                    if (DataIsAvailable || _enqueueAwaiters.Count > 0)
                    {
                        _pageToSend = DequeuePage();
                        sendNextPage = true;
                        ProcessAwaiters();
                    }
                    else
                    {
                        _isSedning = false;

                        if (_isClosed)
                            CompleteStream();
                    }
                }
                else
                    AbortStream(sendResult);
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

        private void AbortStream(RpcResult fault)
        {
            _closeFault = fault;
            _isClosed = true;

            AbortAwaiters(_closeFault);
        }

        private void CompleteStream()
        {
            var complMessage = _factory.CreateCompletionMessage(CallId);
            _ch.Tx.TrySendAsync(complMessage, OnCompletionMessageSent);
        }

        private void OnCompletionMessageSent(RpcResult result)
        {
            _completionEventSrc.TrySetResult(result);
        }

        private IStreamPage<T> AllocatePage()
        {
            if (_unsuedPagesCache.Count > 0)
                return _unsuedPagesCache.Dequeue();

            var page = _factory.CreatePage(CallId);
            page.Items = new List<T>();
            return page;
        }

        private void FreePage(IStreamPage<T> page)
        {
            _unsuedPagesCache.Enqueue(page);
        }

        private IStreamPage<T> DequeuePage()
        {
            if (_queueCompletePages.Count > 0)
                return _queueCompletePages.Dequeue();

            var page = _queueTopPage;
            _queueTopPage = AllocatePage();
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
