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

namespace SharpRpc
{
#if NET5_0_OR_GREATER
    public class PagingRxStream<T> : InputStream<T>, IAsyncEnumerable<T>
#else
    public class PagingRxStream<T> : InputStream<T>
#endif
    {
        private object _lockObj = new object();
        private readonly Queue<IList<T>> _pages = new Queue<IList<T>>();
        private IList<T> _currentPage;
        private int _currentPageIndex;
        private IStreamEnumerator _enumerator;

        //private bool _isWating;

        internal PagingRxStream(IStreamMessageFactory<T> factory)
        {
        }

        internal void OnRx(IStreamPage<T> page)
        {
            var wakeupListener = false;

            lock (_lockObj)
            {
                if (_currentPage == null)
                {
                    Debug.Assert(_currentPageIndex == 0);

                    _currentPage = page.Items;
                    wakeupListener = true;
                }
                else
                    _pages.Enqueue(page.Items);

                OnDataArrived();
            }

            if (wakeupListener)
                _enumerator.WakeUpListener();
        }

        private bool OnDataArrived()
        {
            if (_enumerator != null)
                return _enumerator.OnDataArrived();

            return false;
        }

        private T GetNextItem()
        {
            var item = _currentPage[_currentPageIndex++];

            if (_currentPageIndex >= _currentPage.Count)
            {
                _currentPageIndex = 0;

                if (_pages.Count > 0)
                    _currentPage = _pages.Dequeue();
                else
                    _currentPage = null;
            }

            return item;
        }

        private bool TryGetNextItem(out T item)
        {
            if (_currentPage != null)
            {
                item = GetNextItem();
                return true;
            }

            item = default(T);
            return false;
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

        private interface IStreamEnumerator
        {
            bool OnDataArrived();
            void WakeUpListener();
            void OnClose();
        }

#if NET5_0_OR_GREATER
        private class AsyncEnumerator : IAsyncEnumerator<T>, IStreamEnumerator
        {
            private readonly PagingRxStream<T> _stream;
            private TaskCompletionSource<bool> _itemWaitSrc;
            //private TaskCompletionSource<bool> _closeWaitSrc;

            public AsyncEnumerator(PagingRxStream<T> stream)
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
                lock (_stream._lockObj)
                {
                    if (_stream.TryGetNextItem(out var nextItem))
                    {
                        Current = nextItem;
                        return FwAdapter.AsyncTrue;
                    }
                    else
                    {
                        _itemWaitSrc = new TaskCompletionSource<bool>();
                        return new ValueTask<bool>(_itemWaitSrc.Task);
                    }
                }
            }

            public bool OnDataArrived()
            {
                Current = _stream.GetNextItem();
                return _itemWaitSrc != null;
            }

            public void WakeUpListener()
            {
                var eventCpy = _itemWaitSrc;
                _itemWaitSrc = null;
                eventCpy.SetResult(true);
            }

            public void OnClose()
            {
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
