// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Lib;
using SharpRpc.Streaming;
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
        private IStreamEnumerator _enumerator;
        private bool _completed;
        private readonly StreamReadCoordinator _coordinator;
        private readonly TxPipeline _tx;

        //private bool _isWating;

        internal PagingStreamReader(string callId, TxPipeline tx, IStreamMessageFactory<T> factory)
        {
            _tx = tx;
            _coordinator = new StreamReadCoordinator(callId, factory);
        }

        internal void OnRx(IStreamPage<T> page)
        {
            var wakeupListener = false;
            IStreamPageAck ack = null;

            if (page.Items == null || page.Items.Count == 0)
                return; // TO DO : signal protocol violation

            lock (_lockObj)
            {
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
            CompleteStream(false);
        }

        internal void Abort()
        {
            CompleteStream(true);
        }

        private void CompleteStream(bool clearQueue)
        {
            var wakeupListener = false;
            IStreamPageAck ack = null;

            lock (_lockObj)
            {
                _completed = true;

                if (clearQueue)
                {
                    _pages.Clear();
                    _currentPage = null;
                    _currentPageIndex = 0;
                }

                wakeupListener = OnDataArrived(out ack);
            }

            if (ack != null)
                SendAck(ack);

            if (wakeupListener)
                _enumerator.WakeUpListener();
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

                if (_pages.Count > 0)
                    _currentPage = _pages.Dequeue();
                else
                    _currentPage = null;

                ack = _coordinator.OnPageConsume();
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

            if (_completed)
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
            _coordinator.OnAckSent();
        }

#if NET5_0_OR_GREATER
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            lock (_lockObj) return SetEnumerator(new AsyncEnumerator(this));
        }
#endif

        private TEnum SetEnumerator<TEnum>(TEnum enumerator)
            where TEnum : IStreamEnumerator
        {
            if (_enumerator != null)
                throw new InvalidOperationException("Multiple enumerators are not allowed!");

            _enumerator = enumerator;

            return enumerator;
        }

        //public T Current => _currentPage[_currentPageIndex];

        //public ValueTask<bool> MoveNextAsync()
        //{
        //    lock (_lockObj)
        //    {
        //        if (_currentPage != null)
        //        {
        //            _currentPageIndex++;
        //            if (_currentPageIndex < _currentPage.Count)
        //                return new ValueTask<bool>(true);

        //            _currentPage = null;
        //            _currentPageIndex = 0;
        //        }


        //    }
        //}

        //public ValueTask DisposeAsync()
        //{
        //    return new ValueTask();
        //}

        private enum NextItemCode
        {
            Ok,
            NoItems,
            Completed
        }

        private interface IStreamEnumerator
        {
            bool OnDataArrived(out IStreamPageAck ack);
            void WakeUpListener();
        }

#if NET5_0_OR_GREATER
        private class AsyncEnumerator : IAsyncEnumerator<T>, IStreamEnumerator
        {
            private readonly PagingStreamReader<T> _stream;
            private TaskCompletionSource<bool> _itemWaitSrc;
            //private TaskCompletionSource<bool> _closeWaitSrc;
            private bool _completed;

            public AsyncEnumerator(PagingStreamReader<T> stream)
            {
                _stream = stream;
            }

            public T Current { get; private set; }

            public ValueTask DisposeAsync()
            {
                return new ValueTask();
            }

            public ValueTask<bool> MoveNextAsync()
            {
                IStreamPageAck ack = null;
                ValueTask<bool> result;

                lock (_stream._lockObj)
                {
                    var code = _stream.TryGetNextItem(out var nextItem, out ack);
                    Current = nextItem;

                    if (code == NextItemCode.Ok)
                        result = FwAdapter.AsyncTrue;
                    else if (code == NextItemCode.NoItems)
                    {
                        _itemWaitSrc = new TaskCompletionSource<bool>();
                        result = new ValueTask<bool>(_itemWaitSrc.Task);
                    }
                    else //NextItemCode.Completed
                        result = FwAdapter.AsyncFalse;
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
                        _completed = true;

                    return true;
                }

                ack = null;
                return false;
            }

            public void WakeUpListener()
            {
                var eventCpy = _itemWaitSrc;
                _itemWaitSrc = null;
                eventCpy.SetResult(!_completed);
            }
        }
#endif

        //private class SlimEnumerator : IStreamEnumerator
        //{
        //    public SlimEnumerator(TxStream stream)
        //    {
        //    }
        //}
    }
}
