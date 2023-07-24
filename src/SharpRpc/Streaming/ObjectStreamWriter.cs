// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    public sealed class ObjectStreamWriter<T> : StreamWriter<T>, IStreamCoordinatorContext
    {
        public enum States { Online, Completed, Closed  }

        private readonly object _lockObj = new object();
        private readonly IRpcLogger _logger;
        //private readonly Queue<IStreamPage<T>> _queueCompletePages = new Queue<IStreamPage<T>>();
        //private readonly Queue<IStreamPage<T>> _unsuedPagesCache = new Queue<IStreamPage<T>>();
        private IStreamPage<T> _queue;
        private IStreamPage<T> _pageToSend;
        private readonly bool _canImmediatelyReusePages;
        private readonly TxPipeline _msgTransmitter;
        private readonly Queue<EnqueueAwaiter> _enqueueAwaiters = new Queue<EnqueueAwaiter>();
        //private readonly List<EnqueueAwaiter> _awaitersToRelease = new List<EnqueueAwaiter>();
        private readonly TaskCompletionSource<RpcResult> _closedEventSrc = new TaskCompletionSource<RpcResult>();
        //private bool _isClosed;
        //private bool _isAbroted;
        private bool _isSendingEnabled;
        private RpcResult _closeFault;
        private bool _isSedning;
        private int _windowSize;
        private readonly IStreamMessageFactory<T> _factory;
        private readonly StreamWriteCoordinator _coordinator;
        private CancellationTokenRegistration _cancelReg;
        private bool _isCancellationEnabled;
        private string _name;

        internal ObjectStreamWriter(string callId, TxPipeline msgTransmitter, IStreamMessageFactory<T> factory,
            bool allowSending, StreamOptions options, IRpcLogger logger)
        {
            CallId = callId;
            _logger = logger;
            _msgTransmitter = msgTransmitter;
            _factory = factory;
            //_maxPageSize = maxPageSize;
            _windowSize = options?.WindowSize ?? StreamOptions.DefaultWindowsSize;

            if (_windowSize < 1)
                _windowSize = StreamOptions.DefaultWindowsSize;

            MaxPageCount = 8;
            MaxPageSize = options.WindowSize / MaxPageCount;

            if (MaxPageSize < 1)
                MaxPageSize = 1;

            _isSendingEnabled = allowSending;

            _canImmediatelyReusePages = _msgTransmitter.ImmediateSerialization;
            
            _queue = CreatePage();
            if (_canImmediatelyReusePages)
                _pageToSend = CreatePage();

            _coordinator = new StreamWriteCoordinator.Realtime().Init(this);

            if (_logger.VerboseEnabled)
                _logger.Verbose(GetName(), $"[opened] {options}");
        }

        private bool DataIsAvailable => _queue.Items.Count > 0;
        private bool HasSpaceInQueue => _queue.Items.Count < MaxPageSize;

        object IStreamCoordinatorContext.SyncObj => _lockObj;
        int IStreamCoordinatorContext.PageSize => _queue.Items.Count;
        bool IStreamCoordinatorContext.IsCompleted => State != States.Online;

        public string CallId { get; }
        public States State { get; private set; }

        public int MaxPageSize { get; }
        public int MaxPageCount { get; }

        public Task Closed => _closedEventSrc.Task;

        public Task<RpcResult> CompleteAsync()
        {
            MarkAsCompleted();
            return _closedEventSrc.Task;
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
                if (State != States.Online)
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

        #region Control methods

        internal void AllowSend()
        {
            lock (_lockObj)
            {
                _isSendingEnabled = true;

                if (!_isSedning && DataIsAvailable && _coordinator.CanSend())
                {
                    _isSedning = true;
                    _pageToSend = DequeuePage();
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
        internal void Abort(RpcResult fault)
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
                    _pageToSend = DequeuePage();
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

        private void EnqueueItem(T item)
        {
            _queue.Items.Add(item);
        }

        private bool OnDataArrived()
        {
            if (!_isSedning && _coordinator.CanSend() && _isSendingEnabled)
            {
                _isSedning = true;
                _pageToSend = DequeuePage();
                return true;
            }

            return false;
        }

        private void SendNextPage()
        {
            _msgTransmitter.TrySendAsync(_pageToSend, OnPageSendCompleted);
        }

        private void OnPageSendCompleted(RpcResult sendResult)
        {
            Debug.Assert(!Monitor.IsEntered(_lockObj));

            bool sendNextPage = false;
            bool sendCompletion = false;
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

                    if (DataIsAvailable)
                    {
                        if (_coordinator.CanSend())
                        {
                            _pageToSend = DequeuePage();
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
                {
                    EnqueueItem(awaiter.Item);
                    awaiter.Result = RpcResult.Ok;
                    Task.Factory.StartNew(awaiter.Fire);
                }
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
                _queue.Items.Clear();

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
                    _pageToSend = DequeuePage();
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
            _msgTransmitter.TrySendAsync(closeMessage, OnCompletionMessageSent);
        }

        private void OnCompletionMessageSent(RpcResult result)
        {
            FireOnClose();
        }

        private void FireOnClose()
        {
            _closedEventSrc.TrySetResult(RpcResult.Ok);
        }

        private IStreamPage<T> CreatePage()
        {
            var page = _factory.CreatePage(CallId);
            page.Items = new List<T>();
            return page;
        }

        private IStreamPage<T> AllocatePage()
        {
            return CreatePage();
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

        //private void CancelReadAwait(object param)
        //{
        //    lock (_lockObj) ((EnqueueAwaiter)param).Cancel();
        //}

        private string GetName()
        {
            if (_name == null)
                _name = $"{_msgTransmitter.ChannelId}-SW-{CallId}";
            return _name;
        }

        private class EnqueueAwaiter : TaskCompletionSource<RpcResult>
        {
            public EnqueueAwaiter(T item)
            {
                Item = item;
            }

            public T Item { get; }
            public RpcResult Result { get; set; } = RpcResult.Ok;
            public bool WasCanceled { get; private set; }

            public void Cancel()
            {
                WasCanceled = true;
                TrySetResult(RpcResult.OperationCanceled);
            }

            public void Fire()
            {
                TrySetResult(Result);
            }
        }
    }
}
