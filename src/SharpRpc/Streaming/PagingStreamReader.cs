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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.XPath;

namespace SharpRpc
{
    public interface IStreamEnumerator<T>
    {
        T Current { get; }
#if NET5_0_OR_GREATER
        ValueTask<bool> MoveNextAsync();
#else
        Task<bool> MoveNextAsync();
#endif
    }

#if NET5_0_OR_GREATER
    public class PagingStreamReader<T> : StreamReader<T>, IAsyncEnumerable<T>
#else
    public class PagingStreamReader<T> : StreamReader<T>
#endif
    {
        private object _lockObj = new object();
        private readonly Queue<IList<T>> _pages = new Queue<IList<T>>();
        private IList<T> _currentPage;
        private int _currentPageIndex;
        private INestedEnumerator _enumerator;
        private bool _isCompleted;
        private bool _isCompletionRequested;
        private readonly StreamReadCoordinator _coordinator;
        private readonly TxPipeline _tx;
        private readonly string _callId;
        private readonly IStreamMessageFactory<T> _factory;
        private RpcResult _fault;

        internal PagingStreamReader(string callId, TxPipeline tx, IStreamMessageFactory<T> factory)
        {
            _callId = callId;
            _tx = tx;
            _factory = factory;
            _coordinator = new StreamReadCoordinator(_lockObj, callId, factory);
        }

        private bool HasData => _currentPage != null;

        internal void OnRx(IStreamPage<T> page)
        {
            var wakeupListener = false;
            IStreamPageAck ack = null;

            if (page.Items == null || page.Items.Count == 0)
                return; // TO DO : signal protocol violation

            lock (_lockObj)
            {
                if (_isCompleted)
                    return; // TO DO : signal protocol violation

                if (_currentPage == null)
                {
                    Debug.Assert(_currentPageIndex == 0);
                    _currentPage = page.Items;
                }
                else
                    _pages.Enqueue(page.Items);

                wakeupListener = OnDataArrived(out ack);
            }

            if (ack != null)
                SendAck(ack);

            if (wakeupListener)
                _enumerator.WakeUpListener();
        }

        internal void OnRx(IStreamCompletionMessage msg)
        {
            CompleteStream(CompletionModes.InitiatedByWriter);
        }

        internal void Abort(RpcResult fault)
        {
            CompleteStream(CompletionModes.Abort, fault);
        }

        internal void Complete()
        {
            CompleteStream(CompletionModes.InitiatedByReader);
        }

        private void CompleteStream(CompletionModes mode, RpcResult fault = default)
        {
            var wakeupListener = false;
            IStreamPageAck ack = null;

            lock (_lockObj)
            {
                if (mode == CompletionModes.Abort)
                {
                    _pages.Clear();
                    _currentPage = null;
                    _currentPageIndex = 0;
                    _isCompleted = true;
                    _fault = fault;
                    wakeupListener = OnDataArrived(out ack);
                }
                else if (mode == CompletionModes.InitiatedByWriter)
                {
                    if (!_isCompleted) // TO DO : signal protocol violation if already completed
                    {
                        _isCompleted = true;
                        if (!HasData)
                            wakeupListener = OnDataArrived(out ack);
                    }
                }
                else if (mode == CompletionModes.InitiatedByReader)
                {
                    if (!_isCompletionRequested)
                    {
                        _isCompletionRequested = true;
                        SendCompletionMessage();
                    }
                }
            }

            if (ack != null) SendAck(ack);
            if (wakeupListener) _enumerator.WakeUpListener();
        }

        private bool OnDataArrived(out IStreamPageAck ack)
        {
            if (_enumerator != null)
                return _enumerator.OnDataArrived(out ack);

            ack = null;
            return false;
        }

        private T GetNextItem(out IStreamPageAck ack)
        {
            var item = _currentPage[_currentPageIndex++];

            if (_currentPageIndex >= _currentPage.Count)
            {
                _currentPageIndex = 0;

                var consumedPageSize = _currentPage.Count;

                if (_pages.Count > 0)
                    _currentPage = _pages.Dequeue();
                else
                    _currentPage = null;

                ack = _coordinator.OnPageConsume(consumedPageSize);
            }
            else
                ack = null;

            return item;
        }

        private NextItemCode TryGetNextItem(out T item, out IStreamPageAck ack)
        {
            if (_currentPage != null)
            {
                item = GetNextItem(out ack);
                return NextItemCode.Ok;
            }

            item = default(T);
            ack = null;

            if (_isCompleted)
                return NextItemCode.Completed;

            return NextItemCode.NoItems;
        }

        private void SendAck(IStreamPageAck ack)
        {
            _tx.TrySendAsync(ack, OnAckSent);
        }

        private void OnAckSent(RpcResult sendResult)
        {
            // TO DO : analyze sendResult ???

            IStreamPageAck nextAck = null;

            lock (_lockObj)
                nextAck = _coordinator.OnAckSent();

            if (nextAck != null)
                SendAck(nextAck);
        }

        private void SendCompletionMessage()
        {
            var complMessage = _factory.CreateCompletionRequestMessage(_callId);
            _tx.TrySendAsync(complMessage, OnCompletionMessageSent);
        }

        private void OnCompletionMessageSent(RpcResult result)
        {
        }

        private bool TryGetFault(out RpcResult fault)
        {
            fault = _fault;
            return !fault.IsOk;
        }

        public IStreamEnumerator<T> GetEnumerator(CancellationToken cancellationToken = default)
        {
            lock (_lockObj) return SetEnumerator(new AsyncEnumerator(this, cancellationToken));
        }

#if NET5_0_OR_GREATER
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            lock (_lockObj) return SetEnumerator(new AsyncEnumerator(this, cancellationToken));
        }
#endif

        private TEnum SetEnumerator<TEnum>(TEnum enumerator)
            where TEnum : INestedEnumerator
        {
            if (_enumerator != null)
                throw new InvalidOperationException("Multiple enumerators are not allowed!");

            _enumerator = enumerator;

            return enumerator;
        }

        private enum NextItemCode
        {
            Ok,
            NoItems,
            Completed,
            Aborted
        }

        private enum CompletionModes
        {
            Abort,
            InitiatedByReader,
            InitiatedByWriter
        }

        private interface INestedEnumerator
        {
            bool OnDataArrived(out IStreamPageAck ack);
            void WakeUpListener();
        }

#if NET5_0_OR_GREATER
        private class AsyncEnumerator : IAsyncEnumerator<T>, IStreamEnumerator<T>, INestedEnumerator
#else
        private class AsyncEnumerator : IStreamEnumerator<T>, INestedEnumerator
#endif
        {
            private readonly PagingStreamReader<T> _stream;
            private TaskCompletionSource<bool> _itemWaitSrc;
            //private TaskCompletionSource<bool> _closeWaitSrc;
            private bool _completed;
            private Exception _toThrow;

            public AsyncEnumerator(PagingStreamReader<T> stream, CancellationToken cancellationToken)
            {
                _stream = stream;
                cancellationToken.Register(Cancel);
            }

            public T Current { get; private set; }

#if NET5_0_OR_GREATER
            public ValueTask DisposeAsync()
            {
                // close stream ??? 
                //_stream.Abort();
                return new ValueTask();
            }
#endif

#if NET5_0_OR_GREATER
            public ValueTask<bool> MoveNextAsync()
#else
            public Task<bool> MoveNextAsync()
#endif
            {
                IStreamPageAck ack = null;
#if NET5_0_OR_GREATER
                ValueTask<bool> result;
#else
                Task<bool> result;
#endif

                lock (_stream._lockObj)
                {
                    var code = _stream.TryGetNextItem(out var nextItem, out ack);
                    Current = nextItem;

                    if (code == NextItemCode.Ok)
                        result = FwAdapter.AsyncTrue;
                    else if (code == NextItemCode.NoItems)
                    {
                        _itemWaitSrc = new TaskCompletionSource<bool>();
                        result = FwAdapter.WrappResult(_itemWaitSrc.Task);
                    }
                    else //NextItemCode.Completed
                    {
                        if(_stream.TryGetFault(out var fault))
                            throw fault.ToException();

                        result = FwAdapter.AsyncFalse;
                    }
                }

                if (ack != null)
                    _stream.SendAck(ack);

                return result;
            }

            public bool OnDataArrived(out IStreamPageAck ack)
            {
                if (_itemWaitSrc != null)
                {
                    var code = _stream.TryGetNextItem(out var nextItem, out ack);
                    Current = nextItem;

                    if (code == NextItemCode.Completed)
                    {
                        _completed = true;
                        if (_stream.TryGetFault(out var fault))
                            _toThrow = fault.ToException();
                    }

                    return true;
                }

                ack = null;
                return false;
            }

            public void WakeUpListener()
            {
                var eventCpy = _itemWaitSrc;
                _itemWaitSrc = null;

                if (_completed)
                {
                    if (_toThrow != null)
                        eventCpy.SetException(_toThrow);
                    else
                        eventCpy.SetResult(false);
                }
                else
                    eventCpy.SetResult(true);
            }

            private void Cancel()
            {
                _stream.Complete();
            }
        }
    }
}
