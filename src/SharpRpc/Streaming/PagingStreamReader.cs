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

        //private bool _isWating;

        internal PagingStreamReader(IStreamMessageFactory<T> factory)
        {
        }

        internal void OnRx(IStreamPage<T> page)
        {
            var wakeupListener = false;

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

                wakeupListener = OnDataArrived();
            }

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

            lock (_lockObj)
            {
                _completed = true;

                if (clearQueue)
                {
                    _pages.Clear();
                    _currentPage = null;
                    _currentPageIndex = 0;
                }

                wakeupListener = OnDataArrived();
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

        private NextItemCode TryGetNextItem(out T item)
        {
            if (_currentPage != null)
            {
                item = GetNextItem();
                return NextItemCode.Ok;
            }

            item = default(T);

            if (_completed)
                return NextItemCode.Completed;

            return NextItemCode.NoItems;
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
            bool OnDataArrived();
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
                lock (_stream._lockObj)
                {
                    var code = _stream.TryGetNextItem(out var nextItem);
                    Current = nextItem;

                    if (code == NextItemCode.Ok)
                        return FwAdapter.AsyncTrue;
                    else if (code == NextItemCode.NoItems)
                    {
                        _itemWaitSrc = new TaskCompletionSource<bool>();
                        return new ValueTask<bool>(_itemWaitSrc.Task);
                    }
                    else //NextItemCode.Completed
                        return FwAdapter.AsyncFalse;
                }
            }

            public bool OnDataArrived()
            {
                if (_itemWaitSrc != null)
                {
                    var code = _stream.TryGetNextItem(out var nextItem);
                    Current = nextItem;

                    if (code == NextItemCode.Completed)
                        _completed = true;

                    return true;
                }

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
