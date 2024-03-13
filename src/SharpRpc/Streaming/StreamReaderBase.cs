// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Disptaching;
using SharpRpc.Streaming;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static SharpRpc.TxAsyncGate;

namespace SharpRpc
{
    public abstract class StreamReaderBase<T, TPage> : IStreamReaderFixture<T>
    {
        public enum States
        {
            Online,
            Cancelled,
            Terminated,
            Completed
        }

        private readonly IRpcLogger _logger;
        private readonly Queue<TPage> _pages = new Queue<TPage>();
        private TPage _currentPage;
        private int _currentPageIndex;
        //private bool _isPageConsumed;
        private INestedEnumerator _enumerator;
        private readonly StreamReadCoordinator _coordinator;
        private readonly TxPipeline _tx;
        private readonly string _callId;
        private readonly IStreamMessageFactory _factory;
        private RpcResult _fault;
        private readonly TaskCompletionSource<bool> _closed = new TaskCompletionSource<bool>();
        private string _name;
        //private TaskCompletionSource<bool> _readWaitSrc;

        internal StreamReaderBase(string callId, TxPipeline tx, IStreamMessageFactory factory, IRpcLogger logger)
        {
            _callId = callId;
            _tx = tx;
            _factory = factory;
            _logger = logger;
            _coordinator = new StreamReadCoordinator(LockObj, callId, factory);

            if (_logger.IsVerboseEnabled)
                _logger.Verbose(GetName(), "Opened");
        }

        private bool HasData => !IsNull(_currentPage);

        public States State { get; private set; }
        public Task Closed => _closed.Task;

        protected object LockObj { get; } = new object();

        protected abstract int GetItemsCount(TPage page);
        protected abstract T GetItem(TPage page, int index);
        protected abstract bool IsNull(TPage page);
        protected virtual void FreePage(TPage page) { }
        protected abstract void CopyItems(TPage page, int pageIndex, T[] destArray, int destIndex, int count);

        private void ChangeState(States newState)
        {
            State = newState;
        }

        internal abstract bool OnMessage(IInteropMessage auxMessage, out RpcResult result);

        bool IStreamReaderFixture<T>.OnMessage(IInteropMessage auxMessage, out RpcResult result) => OnMessage(auxMessage, out result);

        internal void OnRx(TPage page)
        {
            var wakeupListener = false;
            IStreamPageAck ack = null;

            if (GetItemsCount(page) == 0)
                return; // TO DO : signal protocol violation

            lock (LockObj)
            {
                if (State == States.Completed || State == States.Terminated)
                    return; // TO DO : signal protocol violation

                if (IsNull(_currentPage))
                {
                    Debug.Assert(_currentPageIndex == 0);
                    _currentPage = page;
                }
                else
                    _pages.Enqueue(page);

                wakeupListener = OnDataArrived(out ack);
            }

            if (ack != null)
                SendAck(ack);

            if (wakeupListener)
                _enumerator.WakeUpListener();
        }

        // graceful close
        internal void OnRx(IStreamCloseMessage msg)
        {
            IStreamCloseAckMessage closeAck = null;
            var wakeupListener = false;

            lock (LockObj)
            {
                if (State == States.Completed) 
                    return; // TO DO : signal protocol violation

                if (State == States.Terminated)
                    return;

                State = States.Completed;

                if (_logger.IsVerboseEnabled)
                    _logger.Verbose(GetName(), "Completed (Received a close message)");

                if (!HasData)
                {
                    wakeupListener = OnDataArrived(out _);
                    closeAck = _factory.CreateCloseAcknowledgement(_callId);
                }
            }

            if (closeAck != null) SendCloseAck(closeAck);
            if (wakeupListener) _enumerator.WakeUpListener();

            _closed.TrySetResult(true);
        }

        // The call ended (may happen before the stream is gracefully closed).
        void IStreamReaderFixture<T>.Terminate(RpcResult fault)
        {
            var wakeupListener = false;

            lock (LockObj)
            {
                //DropAllItems();
                ChangeState(States.Terminated);
                _fault = fault;

                if (!HasData)
                    wakeupListener = OnDataArrived(out _);
            }

            if (_logger.IsVerboseEnabled)
                _logger.Verbose(GetName(), $"Terminated (due to {_fault.Code})");

            if (wakeupListener) _enumerator.WakeUpListener();

            _closed.TrySetResult(true);
        }

        private void Cancel(bool dropRemItems)
        {
            lock (LockObj)
            {
                if (State == States.Online)
                {
                    if (dropRemItems)
                    {
                        ChangeState(States.Cancelled);
                        DropAllItems();
                    }
                    else
                        ChangeState(States.Cancelled);

                    if (_logger.IsVerboseEnabled)
                        _logger.Verbose(GetName(), $"Cancellation is requested.{(dropRemItems ? "[Drop] " : " ")}");

                    SendCancelMessage(dropRemItems);
                }
            }
        }

#if NET5_0_OR_GREATER
        internal ValueTask CloseByReader()
#else
        internal Task CloseByReader()
#endif
        {
            lock (LockObj)
            {
                if (_logger.IsVerboseEnabled)
                    _logger.Verbose(GetName(), $"Closing...");

                if (State == States.Online)
                {
                    ChangeState(States.Cancelled);
                    SendCancelMessage(true);
                }

#if NET5_0_OR_GREATER
                return new ValueTask(Closed);
#else
                return Closed;
#endif
            }
        }

        void IStreamReaderFixture<T>.Cancel(bool dropRemItems) => Cancel(dropRemItems);

        private void DropAllItems()
        {
            while (_pages.Count > 0)
                FreePage(_pages.Dequeue());
            if (!IsNull(_currentPage))
                FreePage(_currentPage);
            _currentPage = default;
            _currentPageIndex = 0;
        }

        private bool OnDataArrived(out IStreamPageAck ack)
        {
            if (_enumerator != null)
                return _enumerator.OnDataArrived(out ack);

            ack = null;
            return false;
        }

        private void IncreasePageIndexBy(int readSize, bool keepPages, out IStreamPageAck ack)
        {
            _currentPageIndex += readSize;

            if (_currentPageIndex >= GetItemsCount(_currentPage))
            {
                _currentPageIndex = 0;

                var consumedPageSize = GetItemsCount(_currentPage);
                if (!keepPages)
                    FreePage(_currentPage);

                if (_pages.Count > 0)
                    _currentPage = _pages.Dequeue();
                else
                    _currentPage = default;

                ack = _coordinator.OnPageConsume(consumedPageSize);
            }
            else
                ack = null;
        }

        private NextItemCode TryGetNextItem(out T item, out IStreamPageAck pageAck)
        {
            if (!IsNull(_currentPage))
            {
                item = GetItem(_currentPage, _currentPageIndex);
                IncreasePageIndexBy(1, false, out pageAck);
                return NextItemCode.Ok;
            }

            item = default(T);
            pageAck = null;

            if (State == States.Completed || State == States.Terminated)
                return NextItemCode.Completed;

            return NextItemCode.NoItems;
        }

        internal NextItemCode TryGetNextPage(out TPage page, out IStreamPageAck pageAck)
        {
            if (!IsNull(_currentPage))
            {
                page = _currentPage;
                IncreasePageIndexBy(GetItemsCount(page), true, out pageAck);
                return NextItemCode.Ok;
            }

            page = default;
            pageAck = null;

            if (State == States.Completed || State == States.Terminated)
                return NextItemCode.Completed;

            return NextItemCode.NoItems;
        }

        private NextItemCode TryBulkRead(ArraySegment<T> buffer, out int count, out IStreamPageAck pageAck)
        {
            if (!IsNull(_currentPage))
            {
                // copy
                count = Math.Min(buffer.Count, GetItemsCount(_currentPage) - _currentPageIndex);
                CopyItems(_currentPage, _currentPageIndex, buffer.Array, buffer.Offset, count);
                IncreasePageIndexBy(count, false, out pageAck);
                return NextItemCode.Ok;
            }

            count = 0;
            pageAck = null;

            if (State == States.Completed || State == States.Terminated)
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

            lock (LockObj)
                nextAck = _coordinator.OnAckSent();

            if (nextAck != null)
                SendAck(nextAck);
        }

        private void SendCancelMessage(bool dropRemItems)
        {
            var canelMessage = _factory.CreateCancelMessage(_callId);
            if (dropRemItems)
                canelMessage.Options |= StreamCancelOptions.DropRemainingItems;
            _tx.TrySendAsync(canelMessage, OnCompletionMessageSent);
        }

        private void SendCloseAck(IStreamCloseAckMessage msg)
        {
            _tx.TrySendAsync(msg, OnCloseAckMessageSent);
        }

        private void OnCompletionMessageSent(RpcResult result)
        {
        }

        private void OnCloseAckMessageSent(RpcResult result)
        {
        }

        private bool TryGetFault(out RpcResult fault)
        {
            fault = _fault;
            return !fault.IsOk;
        }

        internal void OnPageConsumed(TPage page) => FreePage(page);

        private string GetName()
        {
            if (_name == null)
                _name = $"{_tx.ChannelId}-SR-{_callId}";
            return _name;
        }

        public IStreamEnumerator<T> GetEnumerator(CancellationToken cancellationToken = default)
        {
            lock (LockObj) return SetEnumerator(new AsyncEnumerator(this, cancellationToken));
        }

        internal IStreamBulkEnumerator<T> GetBulkEnumeratorInternal(CancellationToken cancellationToken = default)
        {
            lock (LockObj) return SetEnumerator(new BulkEnumerator(this, cancellationToken));
        }


#if NET5_0_OR_GREATER
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            lock (LockObj) return SetEnumerator(new AsyncEnumerator(this, cancellationToken));
        }
#endif

        protected TEnum SetEnumerator<TEnum>(TEnum enumerator)
            where TEnum : INestedEnumerator
        {
            if (_enumerator != null)
                throw new InvalidOperationException("Multiple enumerators are not allowed!");

            _enumerator = enumerator;

            return enumerator;
        }

        internal enum NextItemCode
        {
            Ok,
            NoItems,
            Completed
        }

        protected interface INestedEnumerator
        {
            bool OnDataArrived(out IStreamPageAck ack);
            void WakeUpListener();
        }

        internal abstract class AsyncEnumeratorBase : INestedEnumerator
        {
            private TaskCompletionSource<bool> _itemWaitSrc;
            private bool _completed;
            private Exception _toThrow;
            private CancellationTokenRegistration _cancelReg;

            public AsyncEnumeratorBase(StreamReaderBase<T, TPage> stream, CancellationToken cancellationToken)
            {
                Stream = stream;
                _cancelReg = cancellationToken.Register(Cancel);
            }

            public StreamReaderBase<T, TPage> Stream { get; }

            public abstract NextItemCode GetNextItem(out IStreamPageAck pageAck);
            public void Dispose() => DisposeAsync().Wait();

#if NET5_0_OR_GREATER
            public virtual ValueTask DisposeAsync()
#else
            public virtual Task DisposeAsync()
#endif
            {
                _cancelReg.Dispose();
                return Stream.CloseByReader();
            }

#if NET5_0_OR_GREATER
            public ValueTask<bool> MoveNextAsync()
#else
            public Task<bool> MoveNextAsync()
#endif
            {
                IStreamPageAck pageAck = null;
                IStreamCloseAckMessage closeAck = null;
                Exception toThrow = null;
#if NET5_0_OR_GREATER
                ValueTask<bool> result;
#else
                Task<bool> result;
#endif

                lock (Stream.LockObj)
                {
                    //var code = _stream.TryGetNextItem(out var nextItem, out pageAck);
                    //Current = nextItem;

                    var code = GetNextItem(out pageAck);

                    if (code == NextItemCode.Ok)
                        result = FwAdapter.AsyncTrue;
                    else if (code == NextItemCode.NoItems)
                    {
                        _itemWaitSrc = new TaskCompletionSource<bool>();
                        result = FwAdapter.WrappResult(_itemWaitSrc.Task);
                    }
                    else //NextItemCode.Completed
                    {
                        closeAck = Stream._factory.CreateCloseAcknowledgement(Stream._callId);

                        if (Stream.TryGetFault(out var fault))
                            toThrow = fault.ToException();

                        result = FwAdapter.AsyncFalse;
                    }
                }

                if (pageAck != null) Stream.SendAck(pageAck);
                if (closeAck != null) Stream.SendCloseAck(closeAck);
                if (toThrow != null) throw toThrow;

                return result;
            }

            public bool OnDataArrived(out IStreamPageAck ack)
            {
                if (_itemWaitSrc != null)
                {
                    var code = GetNextItem(out ack);

                    if (code == NextItemCode.Completed)
                    {
                        _completed = true;
                        if (Stream.TryGetFault(out var fault))
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
                // Notify the stream writer to stop. Keep all already enqueued items to process.
                Stream.Cancel(false);
            }
        }

#if NET5_0_OR_GREATER
        private class AsyncEnumerator : AsyncEnumeratorBase, IAsyncEnumerator<T>, IStreamEnumerator<T>
#else
        private class AsyncEnumerator : AsyncEnumeratorBase, IStreamEnumerator<T>
#endif
        {
            public AsyncEnumerator(StreamReaderBase<T, TPage> stream, CancellationToken cancellationToken) : base(stream, cancellationToken)
            {
            }

            public T Current { get; private set; }

            public override NextItemCode GetNextItem(out IStreamPageAck pageAck)
            {
                var code =  Stream.TryGetNextItem(out T item, out pageAck);
                Current = item;
                return code;
            }
        }

        internal class BulkEnumerator : INestedEnumerator, IStreamBulkEnumerator<T>
        {
            private readonly StreamReaderBase<T, TPage> _stream;
            private TaskCompletionSource<RpcResult<int>> _itemWaitSrc;
            private ArraySegment<T> _bufferToFill;
            private RpcResult<int> _resultToFire;
            private CancellationTokenRegistration _cancelReg;

            public BulkEnumerator(StreamReaderBase<T, TPage> stream, CancellationToken cancellationToken)
            {
                _stream = stream;
                _cancelReg = cancellationToken.Register(Cancel);
            }

            public bool OnDataArrived(out IStreamPageAck ack)
            {
                if (_itemWaitSrc != null)
                {
                    var code = _stream.TryBulkRead(_bufferToFill, out var count, out ack);
                    _bufferToFill = default;

                    if (code == NextItemCode.Completed)
                        _resultToFire = GetCompletionResult();
                    else if (code == NextItemCode.Ok)
                        _resultToFire = new RpcResult<int>(count);
                    else
                        throw new Exception("Unexpected code: " + count);

                    return true;
                }

                ack = null;
                return false;
            }

            public void WakeUpListener()
            {
                var eventCpy = _itemWaitSrc;
                _itemWaitSrc = null;

                eventCpy.SetResult(_resultToFire);
            }

#if NET5_0_OR_GREATER
            public async ValueTask<RpcResult<int>> GreedyRead(ArraySegment<T> buffer)
#else
            public async Task<RpcResult<int>> GreedyRead(ArraySegment<T> buffer)
#endif
            {
                var leftToFill = buffer.Count;
                var array = buffer.Array;
                var offset = buffer.Offset;

                while (leftToFill > 0)
                {
                    var readResult = await Read(new ArraySegment<T>(array, offset, leftToFill));

                    if (!readResult.IsOk)
                        return readResult;

                    if (readResult.Value == 0)
                        break;

                    leftToFill -= readResult.Value;
                    offset += readResult.Value;
                }

                return RpcResult.Result(buffer.Count - leftToFill);
            }

#if NET5_0_OR_GREATER
            public ValueTask<RpcResult<int>> Read(ArraySegment<T> buffer)
#else
            public Task<RpcResult<int>> Read(ArraySegment<T> buffer)
#endif
            {
                IStreamPageAck pageAck = null;
                IStreamCloseAckMessage closeAck = null;
                Exception toThrow = null;
#if NET5_0_OR_GREATER
                ValueTask<RpcResult<int>> result;
#else
                Task<RpcResult<int>> result;
#endif

                lock (_stream.LockObj)
                {
                    var code = _stream.TryBulkRead(buffer, out var count, out pageAck);

                    if (code == NextItemCode.Ok)
                        result = FwAdapter.WrappResult(new RpcResult<int>(count));
                    else if (code == NextItemCode.NoItems)
                    {
                        if (_itemWaitSrc != null)
                            throw new InvalidOperationException("Concurrent reads from a stream are prohibited!");

                        _bufferToFill = buffer;
                        _itemWaitSrc = new TaskCompletionSource<RpcResult<int>>();
                        result = FwAdapter.WrappResult(_itemWaitSrc.Task);
                    }
                    else //NextItemCode.Completed
                    {
                        closeAck = _stream._factory.CreateCloseAcknowledgement(_stream._callId);

                        if (_stream.TryGetFault(out var fault))
                            toThrow = fault.ToException();

                        result = FwAdapter.WrappResult(GetCompletionResult());
                    }
                }

                if (pageAck != null) _stream.SendAck(pageAck);
                if (closeAck != null) _stream.SendCloseAck(closeAck);
                if (toThrow != null) throw toThrow;

                return result;
            }

            private RpcResult<int> GetCompletionResult()
            {
                var fault = _stream._fault;

                if (fault.IsOk)
                    return new RpcResult<int>(0);
                else
                    return new RpcResult<int>(fault.Code, fault.FaultMessage, fault.CustomFaultData);
            }

            public void Dispose()
            {
                DisposeAsync().Wait();
            }

#if NET5_0_OR_GREATER
            public ValueTask DisposeAsync()
#else
            public Task DisposeAsync()
#endif
            {
                _cancelReg.Dispose();
                return _stream.CloseByReader();
            }

            private void Cancel()
            {
                // Notify the stream writer to stop. Keep all already enqueued items to process.
                _stream.Cancel(false);
            }
        }
    }
}
