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
        public enum States { Online, Cancellation, Completed  }

        private object _lockObj = new object();
        private readonly IRpcLogger _logger;
        private readonly Queue<IList<T>> _pages = new Queue<IList<T>>();
        private IList<T> _currentPage;
        private int _currentPageIndex;
        private INestedEnumerator _enumerator;
        private readonly StreamReadCoordinator _coordinator;
        private readonly TxPipeline _tx;
        private readonly string _callId;
        private readonly IStreamMessageFactory<T> _factory;
        private RpcResult _fault;
        private readonly TaskCompletionSource<bool> _closed = new TaskCompletionSource<bool>();
        private string _name;

        internal PagingStreamReader(string callId, TxPipeline tx, IStreamMessageFactory<T> factory, IRpcLogger logger)
        {
            _callId = callId;
            _tx = tx;
            _factory = factory;
            _logger = logger;
            _coordinator = new StreamReadCoordinator(_lockObj, callId, factory);

            if (_logger.VerboseEnabled)
                _logger.Verbose(GetName(), "[Opened]");
        }

        private bool HasData => _currentPage != null;

        public States State {get; private set; }
        public Task Closed => _closed.Task;

        private void ChangeState(States newState)
        {
            State = newState;
        }

        internal void OnRx(IStreamPage<T> page)
        {
            var wakeupListener = false;
            IStreamPageAck ack = null;

            if (page.Items == null || page.Items.Count == 0)
                return; // TO DO : signal protocol violation

            lock (_lockObj)
            {
                if (State == States.Completed)
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

        // graceful close
        internal void OnRx(IStreamCloseMessage msg)
        {
            IStreamCloseAckMessage closeAck = null;
            var wakeupListener = false;

            lock (_lockObj)
            {
                if (State == States.Completed) // TO DO : signal protocol violation if already completed
                    return;

                State = States.Completed;
                //_isCloseAckRequested = (msg.Options & StreamCloseOptions.SendAcknowledgment) != 0;

                if (_logger.VerboseEnabled)
                    _logger.Verbose(GetName(), "Received a close message. [Completed]");

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
        internal void Abort(RpcResult fault)
        {
            var wakeupListener = false;

            lock (_lockObj)
            {
                _pages.Clear();
                _currentPage = null;
                _currentPageIndex = 0;
                ChangeState(States.Completed);
                _fault = fault;
                wakeupListener = OnDataArrived(out _);

                if (_logger.VerboseEnabled)
                    _logger.Verbose(GetName(), $"[Aborted]");
            }

            if (wakeupListener) _enumerator.WakeUpListener();

            _closed.TrySetResult(true);
        }

        internal void Cancel(bool dropRemItems)
        {
            //CompleteStream(CompletionModes.InitiatedByReader);

            lock (_lockObj)
            {
                if (State == States.Online)
                {
                    ChangeState(States.Cancellation);

                    if (_logger.VerboseEnabled)
                        _logger.Verbose(GetName(), $"Cancellation is requested.{(dropRemItems ? "[Drop] " : " ")} [Cancellation]");

                    SendCancelMessage(dropRemItems);
                }
            }
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

        private NextItemCode TryGetNextItem(out T item, out IStreamPageAck pageAck)
        {
            if (_currentPage != null)
            {
                item = GetNextItem(out pageAck);
                return NextItemCode.Ok;
            }

            item = default(T);
            pageAck = null;
            
            if (State == States.Completed)
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

        private string GetName()
        {
            if (_name == null)
                _name = $"{_tx.ChannelId}-SR-{_callId}";
            return _name;
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
            private CancellationTokenRegistration _cancelReg;

            public AsyncEnumerator(PagingStreamReader<T> stream, CancellationToken cancellationToken)
            {
                _stream = stream;
                _cancelReg = cancellationToken.Register(Cancel);
            }

            public T Current { get; private set; }

#if NET5_0_OR_GREATER
            public ValueTask DisposeAsync()
            {
                // close stream ??? 
                //_stream.Abort();
                _cancelReg.Dispose();
                return new ValueTask();
            }
#endif

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

                lock (_stream._lockObj)
                {
                    var code = _stream.TryGetNextItem(out var nextItem, out pageAck);
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
                        closeAck = _stream._factory.CreateCloseAcknowledgement(_stream._callId);

                        if (_stream.TryGetFault(out var fault))
                            toThrow = fault.ToException();

                        result = FwAdapter.AsyncFalse;
                    }
                }

                if (pageAck != null) _stream.SendAck(pageAck);
                if (closeAck != null) _stream.SendCloseAck(closeAck);
                if (toThrow != null) throw toThrow;

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
                // Notify the stream writer to stop. Keep all already enqueued items to process.
                _stream.Cancel(false);
            }
        }
    }
}
