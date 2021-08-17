// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public sealed class PagingTxStream<T> : OutputStream<T>
    {
        private readonly object _lockObj = new object();
        private readonly Queue<IStreamPage<T>> _queueCompletePages = new Queue<IStreamPage<T>>();
        private readonly Queue<IStreamPage<T>> _unsuedPagesCache = new Queue<IStreamPage<T>>();
        private IStreamPage<T> _queueTopPage;
        private IStreamPage<T> _pageToSend;
        private readonly Channel _ch;
        private TaskCompletionSource<RpcResult> _queueWaitHandler;
        private bool _isClosed;
        private bool _sendingEnabled;
        private RpcResult _closeFault;
        private bool _isSedning;
        private int _maxPageSize;
        private int _maxWindowSize;
        private readonly IStreamMessageFactory<T> _factory;

        internal PagingTxStream(string callId, Channel channel, IStreamMessageFactory<T> factory, int maxPageSize, int maxWindowSize)
        {
            CallId = callId;
            _ch = channel;
            _factory = factory;
            _maxPageSize = maxPageSize;

            _queueTopPage = AllocatePage();
            _maxWindowSize = maxWindowSize;
        }

        private bool DataIsAvailable => _queueTopPage.Items.Count > 0 || _queueCompletePages.Count > 0;

        public string CallId { get; }
        public int QueueSize { get; private set; }

#if NET5_0_OR_GREATER
        public ValueTask<RpcResult> WriteAsync(T item)
#else
        public Task<RpcResult> WriteAsync(T item)
#endif
        {
            lock (_lockObj)
            {
                if (_isClosed)
                    return FwAdapter.WrappResult(_closeFault);

                if (_queueWaitHandler != null)
                    throw new InvalidOperationException("");

                if (_queueTopPage.Items.Count >= _maxPageSize)
                {
                    _queueWaitHandler = new TaskCompletionSource<RpcResult>();
                    return FwAdapter.WrappResult(_queueWaitHandler.Task);
                }
                else
                {
                    _queueTopPage.Items.Add(item);
                    if (_queueTopPage.Items.Count >= _maxPageSize)
                    {
                        _queueCompletePages.Enqueue(_queueTopPage);
                        _queueTopPage = AllocatePage();
                    }

                    OnDataArrived();
                    return FwAdapter.AsyncRpcOk;
                }       
            }
        }

        #region Control methods

        internal void AllowSend()
        {
            lock (_lockObj)
            {
                _sendingEnabled = true;
                if (_isSedning)
                    SendNextPageNoCheck();
            }
        }

        internal void Close(RpcResult fault)
        {
        }

        #endregion

        private void OnDataArrived()
        {
            if (!_isSedning)
                SendNextPage();
        }

        private void SendNextPage()
        {
            _isSedning = true;
            if (_sendingEnabled)
                SendNextPageNoCheck();
        }

        private void SendNextPageNoCheck()
        {
            _pageToSend = DequeuePage();
            _ch.Tx.TrySendAsync(_pageToSend, OnSendCompleted);
        }

        private void OnSendCompleted(RpcResult result)
        {
            lock (_lockObj)
            {
                FreePage(_pageToSend);

                if (DataIsAvailable)
                    SendNextPage();
                else
                    _isSedning = false;

                if (_queueWaitHandler != null)
                {
                    var cpy = _queueWaitHandler;
                    _queueWaitHandler = null;
                    Task.Factory.StartNew(s => ((TaskCompletionSource<bool>)s).SetResult(true), cpy);
                }
            }
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

        //public SlimAwaitable<bool> WriteAsyncSlim(T item)
        //{

        //}
    }
}
