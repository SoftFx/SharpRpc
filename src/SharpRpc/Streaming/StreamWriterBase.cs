// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Streaming;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static SharpRpc.TxAsyncGate;
using static System.Net.Mime.MediaTypeNames;

namespace SharpRpc
{
    public abstract class StreamWriterBase2<T> : IStreamWriterFixture<T>, IStreamCoordinatorContext
    {
        public enum States { Online, Completed, Closed }

        private readonly object _lockObj = new object();
        private readonly IRpcLogger _logger;
        private readonly Queue<IAsyncAwaiter> _enqueueAwaiters = new Queue<IAsyncAwaiter>();
        private readonly TaskCompletionSource<RpcResult> _closedEventSrc = new TaskCompletionSource<RpcResult>();
        private bool _isSendingEnabled;
        private RpcResult _closeFault;
        private bool _isSedning;
        private int _windowSize;
        private readonly IStreamMessageFactory _factory;
        private readonly StreamWriteCoordinator _coordinator;
        private CancellationTokenRegistration _cancelReg;
        private bool _isCancellationEnabled;
        private string _name;
        private bool _isBulkWrite;

        internal StreamWriterBase2(string callId, TxPipeline msgTransmitter, IStreamMessageFactory factory,
            bool allowSending, StreamOptions options, IRpcLogger logger)
        {
            CallId = callId;
            _logger = logger;
            Tx = msgTransmitter;
            _factory = factory;
            _windowSize = options?.WindowSize ?? StreamOptions.DefaultWindowsSize;

            if (_windowSize < 1)
                _windowSize = StreamOptions.DefaultWindowsSize;

            MaxPageCount = 8;
            MaxPageSize = options.WindowSize / MaxPageCount;

            if (MaxPageSize < 1)
                MaxPageSize = 1;

            _isSendingEnabled = allowSending;

            _coordinator = new StreamWriteCoordinator.Realtime().Init(this);

            if (_logger.VerboseEnabled)
                _logger.Verbose(GetName(), $"[opened] {options}");
        }

        protected abstract bool DataIsAvailable { get; }
        protected abstract bool HasSpaceInQueue { get; }

        protected abstract void SendNextPage();
        protected abstract void EnqueueItem(T item);
        protected abstract void DropQueue();
        protected abstract void FillSendBuffer();
        protected abstract void FreeSendBuffer(out int sentDataSize);
        protected abstract ArraySegment<T> ReserveBulkWriteBuffer();
        protected abstract void CommitBulkWriteBuffer(int writeSize);
        //protected abstract void AllocateQueueBuffer();

        public abstract int QueueSize { get; }

        object IStreamCoordinatorContext.SyncObj => _lockObj;
        //int IStreamCoordinatorContext.QueueSize => GetItemsCount(_queue);
        bool IStreamCoordinatorContext.IsCompleted => State != States.Online;

        protected object LockObj => _lockObj;

        public string CallId { get; }
        public States State { get; private set; }
        public int MaxPageSize { get; }
        public int MaxPageCount { get; }
        internal TxPipeline Tx { get; }

        public Task Closed => _closedEventSrc.Task;

        public Task<RpcResult> CompleteAsync()
        {
            MarkAsCompleted();
            return _closedEventSrc.Task;
        }

        protected abstract bool OnMessage(IInteropMessage auxMessage, out RpcResult result);

#if NET5_0_OR_GREATER
        public ValueTask<RpcResult> WriteAsync(T item)
#else
        public Task<RpcResult> WriteAsync(T item)
#endif
        {
            bool sendNextPage = false;

            lock (_lockObj)
            {
                if (State != States.Online)
                    return FwAdapter.WrappResult(_closeFault);

                if (_isBulkWrite)
                {
                    return FwAdapter.WrappResult(new RpcResult(RpcRetCode.InvalidOperation,
                        "Cannot perform any write actions while the stream is busy with a bulk operation!"));
                }

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
            CloseStream(false, false, new RpcResult(RpcRetCode.StreamCompleted, "The stream is completed and does not accept additions."),
                "Completion is requested.");
        }

        public void EnableCancellation(CancellationToken cancelToken)
        {
            lock (_lockObj)
            {
                if (_isCancellationEnabled)
                    throw new InvalidOperationException("Cancellation has been already enabled!");

                _isCancellationEnabled = true;
                _cancelReg = cancelToken.Register(Cancel);
            }
        }

#if NET5_0_OR_GREATER
        public ValueTask<RpcResult<ArraySegment<T>>> StartBulkWrite()
#else
        public Task<RpcResult<ArraySegment<T>>> StartBulkWrite()
#endif
        {
            lock (_lockObj)
            {
                if (State != States.Online)
                    return FwAdapter.WrappResult((RpcResult<ArraySegment<T>>)_closeFault);

                if (_isBulkWrite)
                {
                    return FwAdapter.WrappResult(new RpcResult<ArraySegment<T>>(RpcRetCode.InvalidOperation,
                        "Cannot perform any write actions while the stream is busy with a bulk operation!"));
                }

                _isBulkWrite = true;

                if (HasSpaceInQueue)
                {
                    var buffer = ReserveBulkWriteBuffer();
                    return FwAdapter.WrappResult(RpcResult.FromResult(buffer));
                }
                else
                {
                    var waitHandler = new BulkWriteAwaiter();
                    _enqueueAwaiters.Enqueue(waitHandler);

                    return FwAdapter.WrappResult(waitHandler.Task);
                }
            }
        }

        public void CommitBulkWrite(int byteCount)
        {
            bool sendNextPage = false;

            lock (_lockObj)
            {
                if (State != States.Online)
                    return;

                _isBulkWrite = false;

                if (byteCount > 0)
                {
                    CommitBulkWriteBuffer(byteCount);
                    sendNextPage = OnDataArrived();
                }
            }

            if (sendNextPage)
                SendNextPage();
        }

        #region Control methods

        bool IStreamWriterFixture<T>.OnMessage(IInteropMessage auxMessage, out RpcResult result) => OnMessage(auxMessage, out result);

        void IStreamWriterFixture<T>.AllowSend()
        {
            lock (_lockObj)
            {
                _isSendingEnabled = true;

                if (!_isSedning && DataIsAvailable && _coordinator.CanSend())
                {
                    _isSedning = true;
                    FillSendBuffer();
                }
                else
                    return;
            }

            SendNextPage();
        }

        // Complete writes and close communication gracefully (send Close -> wait for CloseAck);
        //internal void Close(RpcResult fault)
        //{
        //    CloseStream(false, false, fault);
        //}

        // Close the writer immediately without any further messaging.
        void IStreamWriterFixture<T>.Abort(RpcResult fault)
        {
            CloseStream(true, true, fault, "Aborted.");
        }

        internal void Cancel()
        {
            CloseStream(false, false, new RpcResult(RpcRetCode.OperationCanceled, "The operation was canceled by the user!"),
                "Cancellation is requested.");
        }

        private void CloseStream(bool abort, bool dropQueue, RpcResult fault, string closeReason)
        {
            var sendNextPage = false;
            var sendCloseMessage = false;

            lock (_lockObj)
            {
                if (State == States.Online || abort) // allow abortion when normal completion is already being in the process
                    CloseStreamInternal(abort, dropQueue, fault, closeReason, out sendNextPage, out sendCloseMessage);
            }

            if (sendNextPage)
                SendNextPage();

            if (sendCloseMessage)
                SendCloseMessage();
        }

        #endregion

        private void ChangeState(States newState)
        {
            State = newState;
        }

        internal void OnRx(IStreamPageAck ack)
        {
            lock (_lockObj)
            {
                _coordinator.OnAcknowledgementRx(ack);

                if (DataIsAvailable && !_isSedning && _coordinator.CanSend())
                {
                    _isSedning = true;
                    FillSendBuffer();
                }
                else
                    return;
            }

            SendNextPage();
        }

        internal void OnRx(IStreamCancelMessage cancelMsg)
        {
            CloseStream(false, cancelMsg.Options.HasFlag(StreamCancelOptions.DropRemainingItems), RpcResult.OperationCanceled,
                "Received a cancel message.");
        }

        internal RpcResult OnRx(IStreamCloseAckMessage closeAckMsg)
        {
            lock (_lockObj)
            {
                if (State != States.Completed)
                    return RpcResult.UnexpectedMessage(closeAckMsg.GetType(), GetType()); // signal protocol violation

                ChangeState(States.Closed);

                if (_logger.VerboseEnabled)
                    _logger.Verbose(GetName(), "Received a close acknowledgment. [Closed]");

                return RpcResult.Ok;
            }
        }

        //private void EnqueueItem(T item)
        //{
        //    AddItem(ref _queue, item);
        //}

        private bool OnDataArrived()
        {
            if (!_isSedning && _coordinator.CanSend() && _isSendingEnabled)
            {
                _isSedning = true;
                FillSendBuffer();
                return true;
            }

            return false;
        }

        //private void SendNextPage()
        //{
        //    SendPageAsync(_pageToSend, OnPageSendCompleted);
        //}

        protected void OnPageSendCompleted(RpcResult sendResult)
        {
            Debug.Assert(!Monitor.IsEntered(_lockObj));

            bool sendNextPage = false;
            bool sendCompletion = false;

            lock (_lockObj)
            {
                FreeSendBuffer(out var pageSize);

                if (sendResult.IsOk)
                {
                    _coordinator.OnPageSent(pageSize);

                    if (DataIsAvailable)
                    {
                        if (_coordinator.CanSend())
                        {
                            FillSendBuffer();
                            sendNextPage = true;
                        }
                        else
                            _isSedning = false;
                    }
                    else
                    {
                        _isSedning = false;

                        if (State == States.Completed)
                            sendCompletion = true;
                    }

                    ProcessAwaiters();
                }
                else
                    CloseStreamInternal(true, true, sendResult, "Communication is faulted.", out sendNextPage, out sendCompletion);
            }

            if (sendNextPage)
                SendNextPage();

            if (sendCompletion)
                SendCloseMessage();
        }

        private void ProcessAwaiters()
        {
            while (_enqueueAwaiters.Count > 0 && HasSpaceInQueue)
            {
                var awaiter = _enqueueAwaiters.Dequeue();
                if (!awaiter.WasCanceled)
                    awaiter.Confirm(this);
            }
        }

        private void AbortAwaiters(RpcResult result)
        {
            while (_enqueueAwaiters.Count > 0)
            {
                var awaiter = _enqueueAwaiters.Dequeue();
                awaiter.Cancel(result);
            }
        }

        private void CloseStreamInternal(bool abort, bool dropItems, RpcResult fault, string closeReason,
            out bool sendNextPage, out bool sendCompletionMessage)
        {
            Debug.Assert(Monitor.IsEntered(_lockObj));

            sendNextPage = false;
            sendCompletionMessage = false;

            _closeFault = fault;
            _cancelReg.Dispose();
            AbortAwaiters(_closeFault);

            if (abort || dropItems)
                DropQueue();

            if (abort)
            {
                OnClosed();

                if (_logger.VerboseEnabled)
                    _logger.Verbose(GetName(), $"Aborted. [Closed]");
            }
            else
            {
                ChangeState(States.Completed);

                // A greedy coordinator may allow to send data page on stream completion.
                if (DataIsAvailable && !_isSedning && _coordinator.CanSend())
                {
                    _isSedning = true;
                    FillSendBuffer();
                    sendNextPage = true;
                }

                sendCompletionMessage = !_isSedning && !DataIsAvailable;

                if (_logger.VerboseEnabled)
                    _logger.Verbose(GetName(), $"{closeReason} [Completed]");
            }
        }

        private void OnClosed()
        {
            ChangeState(States.Closed);
            Task.Factory.StartNew(FireOnClose);
        }

        private void SendCloseMessage()
        {
            var closeMessage = _factory.CreateCloseMessage(CallId);
            Tx.TrySendAsync(closeMessage, OnCompletionMessageSent);
        }

        private void OnCompletionMessageSent(RpcResult result)
        {
            FireOnClose();
        }

        private void FireOnClose()
        {
            _closedEventSrc.TrySetResult(RpcResult.Ok);
        }

        //private TPage DequeuePage()
        //{
        //    var page = _queue;

        //    if (_canImmediatelyReusePages)
        //        _queue = _pageToSend;
        //    else
        //        _queue = AllocatePage();

        //    return page;
        //}

        //private void CancelReadAwait(object param)
        //{
        //    lock (_lockObj) ((EnqueueAwaiter)param).Cancel();
        //}

        private string GetName()
        {
            if (_name == null)
                _name = $"{Tx.ChannelId}-SW-{CallId}";
            return _name;
        }

        private interface IAsyncAwaiter
        {
            bool WasCanceled { get; }
            void Confirm(StreamWriterBase2<T> writer);
            void Cancel(RpcResult retValue);
        }

        private class BulkWriteAwaiter : TaskCompletionSource<RpcResult<ArraySegment<T>>>, IAsyncAwaiter
        {
            public RpcResult<ArraySegment<T>> Result { get; private set; } = RpcResult.Ok;
            public bool WasCanceled { get; private set; }

            public void Confirm(StreamWriterBase2<T> writer)
            {
                Result = RpcResult.FromResult(writer.ReserveBulkWriteBuffer());
                System.Threading.Tasks.Task.Factory.StartNew(Signal);
            }

            public void Cancel(RpcResult retValue)
            {
                Result = retValue;
                WasCanceled = true;
                System.Threading.Tasks.Task.Factory.StartNew(Signal);
            }

            private void Signal()
            {
                TrySetResult(Result);
            }
        }

        private class EnqueueAwaiter : TaskCompletionSource<RpcResult>, IAsyncAwaiter
        {
            public EnqueueAwaiter(T item)
            {
                Item = item;
            }

            public T Item { get; }
            public RpcResult Result { get; private set; } = RpcResult.Ok;
            public bool WasCanceled { get; private set; }

            public void Confirm(StreamWriterBase2<T> writer)
            {
                writer.EnqueueItem(Item);
                System.Threading.Tasks.Task.Factory.StartNew(Signal);
            }

            public void Cancel(RpcResult retValue)
            {
                Result = retValue;
                WasCanceled = true;
                System.Threading.Tasks.Task.Factory.StartNew(Signal);
            }

            private void Signal()
            {
                TrySetResult(Result);
            }
        }
    }
}
