// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Lib;
using SharpRpc.Server;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace SharpRpc
{
    public class Channel
    {
        private readonly object _stateSyncObj = new object();
        private TxPipeline _tx;
        private RxPipeline _rx;
        private readonly Endpoint _endpoint;
        private readonly ServiceBinding _binding;
        private readonly MessageDispatcher _dispatcher;
        private readonly RpcCallHandler _callHandler;
        private readonly ContractDescriptor _descriptor;
        private readonly TaskCompletionSource<RpcResult> _connectEvent = new TaskCompletionSource<RpcResult>();
        private readonly TaskCompletionSource<RpcResult> _disconnectEvent = new TaskCompletionSource<RpcResult>();
        private readonly CancellationTokenSource _abortLoginSrc = new CancellationTokenSource();
        private readonly CancellationTokenSource _abortLogoutSrc = new CancellationTokenSource();
        private RpcResult _channelFault;
        private ByteTransport _transport;
        private SessionCoordinator _coordinator;
        private bool _closeFlag;
        private bool _isServerSide;
        private static int idSeed;

        public ChannelState State { get; private set; }
        public SessionState SessionState => _coordinator.State;
        public RpcResult Fault => _channelFault;
        public string Id { get; }

        internal MessageDispatcher Dispatcher => _dispatcher;
        internal Endpoint Endpoint => _endpoint;
        internal ServiceBinding Binding => _binding;
        internal TxPipeline Tx => _tx;
        internal ContractDescriptor Contract => _descriptor;
        internal IRpcLogger Logger { get; }

        internal event Action<Channel, RpcResult> InternalClosed;

        public event AsyncEventHandler<ChannelOpeningArgs> Opening;
        //public event EventHandler<ChannelOpenedArgs> Opened;
        public event AsyncEventHandler<ChannelClosingArgs> Closing;
        public event EventHandler<ChannelClosedArgs> Closed;

        internal Channel(ServiceBinding binding, Endpoint endpoint, ContractDescriptor descriptor, RpcCallHandler msgHandler)
        {
            _isServerSide = binding != null;
            _binding = binding;
            _endpoint = endpoint ?? throw new ArgumentNullException("endpoint");
            _descriptor = descriptor ?? throw new ArgumentNullException("descriptor");
            _callHandler = msgHandler;

            if (!_isServerSide)
            {
                _endpoint.AttachTo(this);
                _endpoint.Lock();
                _endpoint.Init();
            }

            Logger = endpoint.GetLogger();
            Id = nameof(Channel) + Interlocked.Increment(ref idSeed);

            _tx = new TxPipeline_NoQueue(Id, descriptor, endpoint, OnCommunicationError, OnConnectionRequested);
            //_tx = new TxPipeline_OneThread(descriptor, endpoint, OnCommunicationError, OnConnectionRequested);
            _dispatcher = MessageDispatcher.Create(endpoint.Dispatcher, this, msgHandler, _isServerSide);

            if (!_isServerSide)
                Init();
        }

        internal void Init(ByteTransport existingTransaport = null)
        {
            _transport = existingTransaport;

            if (_isServerSide)
            {
                var tranportInfo = GetTransportInfo();

                if (Logger.InfoEnabled)
                    Logger.Info(Id, $"Init, endpoint={_endpoint.Name}, service={_binding.ServiceName}");

                var sharedContex = new SessionContext(Id, tranportInfo);
                _coordinator = new ServerSessionCoordinator(sharedContex);

                if (_callHandler is ServiceCallHandler sch)
                    sch.Session.Init(this, sharedContex, tranportInfo);
            }
            else
            {
                _coordinator = new ClientSessionCoordinator();
            }

            if (_isServerSide)
            {
                lock (_stateSyncObj)
                    State = ChannelState.Connecting;

                DoConnect();
            }
        }

        private void StartPipelines(ByteTransport transport)
        {
            //if (_endpoint.AsyncMessageParsing)
            //    _rx = new RxPipeline.OneThread(transport, _endpoint, _descriptor.SerializationAdapter, _dispatcher, _coordinator);
            //else
                _rx = new RxPipeline.NoThreading(transport, _endpoint, _descriptor.SerializationAdapter, _dispatcher, _coordinator);

            _rx.CommunicationFaulted += OnCommunicationError;
            _rx.Start();

            _tx.Start(transport);
        }

#if NET5_0_OR_GREATER
        public ValueTask<RpcResult> TryConnectAsync()
#else
        public Task<RpcResult> TryConnectAsync()
#endif
        {
            bool invokeConnect = false;

            lock (_stateSyncObj)
            {
                if (State == ChannelState.New)
                {
                    State = ChannelState.Connecting;
                    invokeConnect = true;
                }
                else
                    return FwAdapter.WrappResult(new RpcResult(RpcRetCode.InvalidChannelState, "TryConnectAsync() cannot be called while channel in state: " + State));
            }

            if (invokeConnect)
                DoConnect();

            return FwAdapter.WrappResult(_connectEvent.Task);
        }

        public Task CloseAsync()
        {
            TriggerClose(new RpcResult(RpcRetCode.ChannelClosed, "Channel is closed locally."), out var completion);
            return completion;
        }

        internal void TriggerClose()
        {
            TriggerClose(new RpcResult(RpcRetCode.ChannelClosed, "Channel is closed locally."), out _);
        }

        internal void TriggerDisconnect(RpcResult reason)
        {
            TriggerClose(reason, out _);
        }

        private void TriggerClose(RpcResult reason, out Task closeCompletion)
        {
            lock (_stateSyncObj)
            {
                _closeFlag = true;

                if (State == ChannelState.Online)
                {
                    State = ChannelState.Disconnecting;
                    _channelFault = reason;
                }
                else if (State == ChannelState.Connecting)
                {
                    closeCompletion = _disconnectEvent.Task;
                    _channelFault = reason;
                    return;
                }
                else if (State == ChannelState.Disconnecting)
                {
                    closeCompletion = _disconnectEvent.Task;
                    return;
                }
                else if (State == ChannelState.New)
                {
                    State = ChannelState.Closed;
                    _channelFault = reason;
                    closeCompletion = Task.CompletedTask;
                    return;
                }
                else
                {
                    closeCompletion = Task.CompletedTask;
                    return;
                }
            }

            DoDisconnect();

            closeCompletion = _disconnectEvent.Task;
        }

        internal void OnCommunicationError(RpcResult fault)
        {
            if (Logger.VerboseEnabled)
            {
                if (fault.Code == RpcRetCode.ConnectionAbortedByPeer)
                    Logger.Verbose(Id, "The transport has been closed by the other side.");
                else
                    Logger.Verbose(Id, "Communication error: " + fault.Code);
            }

            Abort(fault);
        }

        private void UpdateFault(RpcResult fault)
        {
            if (_channelFault.Code == RpcRetCode.Ok) // only first fault counts
                _channelFault = fault;
        }

        private async void DoConnect()
        {
            if (!_isServerSide)
            {
                Logger.Info(Id, "Connecting...");

                try
                {
                    var connectResult = await ((ClientEndpoint)_endpoint).ConnectAsync();
                    if (connectResult.Code == RpcRetCode.Ok)
                        _transport = connectResult.Value;
                    else
                        UpdateFault(connectResult.GetResultInfo());
                }
                catch (Exception ex)
                {
                    UpdateFault(new RpcResult(RpcRetCode.UnknownError, "An unexpected error has been occured on transport level: " + ex.Message));
                }
            }

            if (_transport != null)
            {
                _coordinator.Init(this);

                // start the coordinator before the pipelines
                var startCoordinatorTask = _coordinator.OnConnect(_abortLoginSrc.Token);

                StartPipelines(_transport);

                // setup login timeout
                _abortLoginSrc.CancelAfter(_coordinator.LoginTimeout);

                // login handshake
                var loginResult = await startCoordinatorTask;

                if (loginResult.Code != RpcRetCode.Ok)
                    UpdateFault(loginResult);
                else
                    _tx.StartProcessingUserMessages();
            }

            bool abortConnect = false;

            lock (_stateSyncObj)
            {
                // Note: a communication fault may be already occured at this time
                if (_closeFlag || _channelFault.Code != RpcRetCode.Ok)
                    abortConnect = true;
                else
                    State = ChannelState.Online;
            }

            // exit transport thread
            await Task.Yield();

            if (abortConnect)
            {
                _tx.StopProcessingUserMessages(_channelFault);

                await CloseComponents();

                lock (_stateSyncObj)
                    State = ChannelState.Faulted;

                Logger.Warn(Id, "Connection failed! Code: {0}", _channelFault.Code);

                InternalClosed?.Invoke(this, _channelFault);

                //Faulted?.Invoke(this, new ChannelFaultedArgs(_channelFault));

                _connectEvent.SetResult(_channelFault);
                _disconnectEvent.SetResult(RpcResult.Ok);
            }
            else
            {
                Logger.Info(Id, "Connected.");
                _connectEvent.SetResult(RpcResult.Ok);
            }
        }

        private async Task CloseComponents()
        {
            Logger.Verbose(Id, "Stopping dispatcher...");

            await _dispatcher.Stop(_channelFault);

            try
            {
                Logger.Verbose(Id, "Stopping pipelines...");

                var rxCloseTask = _rx?.Close();
                var txCloseTask = _tx.Close(TimeSpan.FromSeconds(5));

                Logger.Verbose(Id, "Waiting stop of Tx pipeline...");

                await txCloseTask;

                Logger.Verbose(Id, "Stopping transport...");

                if (_transport != null)
                    await _transport.Shutdown();

                Logger.Verbose(Id, "Waiting stop of Rx pipeline ...");

                if (rxCloseTask != null)
                    await rxCloseTask;
            }
            catch (Exception ex)
            {
                Logger.Error(Id, "CloseComponents() failed!", ex);
            }

            _transport?.Dispose();
        }

        private void Abort(RpcResult fault)
        {
            TriggerClose(fault, out _);
            _abortLoginSrc.Cancel();
            _abortLogoutSrc.Cancel();
        }

        private async void DoDisconnect()
        {
            Logger.Info(Id, $"{_channelFault.FaultMessage} [{_channelFault.Code}] Disconnecting...");

            _abortLogoutSrc.CancelAfter(TimeSpan.FromMinutes(2));

            _tx.StopProcessingUserMessages(_channelFault);

            await _coordinator.OnDisconnect(_abortLogoutSrc.Token);
            await CloseComponents();

            var faultToRise = RpcResult.Ok;

            lock (_stateSyncObj)
            {
                if (_channelFault.Code != RpcRetCode.ChannelClosedByOtherSide
                    && _channelFault.Code != RpcRetCode.ChannelClosed)
                {
                    State = ChannelState.Faulted;
                    faultToRise = _channelFault;
                }
                else
                    State = ChannelState.Closed;
            }

            Logger.Info(Id, "Disconnected. Final state: " + State);

            InternalClosed?.Invoke(this, _channelFault);

            _disconnectEvent.SetResult(RpcResult.Ok);

            RiseClosedEvent(_channelFault, State == ChannelState.Faulted);
        }

        private void OnConnectionRequested()
        {
            bool invokeConnect = false;

            lock (_stateSyncObj)
            {
                if (State == ChannelState.New)
                {
                    State = ChannelState.Connecting;
                    invokeConnect = true;
                }
            }

            if (invokeConnect)
                DoConnect();
        }

        internal TransportInfo GetTransportInfo()
        {
            return _transport.GetInfo();
        }

        internal async Task<bool> RiseOpeningEvent()
        {
            try
            {
                var args = new ChannelOpeningArgs();
                await Opening.InvokeAsync(this, args);
                return !args.HasErrorOccurred;
            }
            catch (Exception ex)
            {
                Logger.Error(Id, ex, "An opening event handler threw an exception!");
                return false;
            }
        }

        internal async Task RiseClosingEvent(bool isFaulted)
        {
            try
            {
                var args = new ChannelClosingArgs(isFaulted);
                await Closing.InvokeAsync(this, args);
            }
            catch (Exception ex)
            {
                Logger.Error(Id, ex, "An opening event handler threw an exception!");
            }
        }

        internal void RiseClosedEvent(RpcResult closeResult, bool isFaulted)
        {
            try
            {
                Closed?.Invoke(this, new ChannelClosedArgs(closeResult, isFaulted));
            }
            catch (Exception ex)
            {
                Logger.Error(Id, ex, "An opening event handler threw an exception!");
            }
        }

#if PF_COUNTERS
        public double GetAverageRxChunkSize() => _rx.GetAvarageRxSize();
        public int GetRxMessagePageCount() => _rx.GetPageCount();
        public double GetAverageRxMessagePageSize() => _rx.GetAvaragePageSize();
#endif
    }

    public enum ChannelState
    {
        New,
        Connecting,
        Online,
        Disconnecting,
        Closed,
        Faulted
    }

    //public class ChannelOpenedArgs : EventArgs
    //{
    //}

    public class ChannelOpeningArgs : EventArgs
    {
        public ChannelOpeningArgs()
        {
        }

        public bool HasErrorOccurred { get; set; }
    }

    public class ChannelClosingArgs : EventArgs
    {
        public ChannelClosingArgs(bool isFaulted)
        {
            IsFaulted = isFaulted;
        }

        public bool IsFaulted { get; }
    }

    public class ChannelClosedArgs : EventArgs
    {
        internal ChannelClosedArgs(RpcResult reason, bool isFaulted)
        {
            Reason = reason;
        }

        public RpcResult Reason { get; }
        public bool IsFaulted { get; }
    }
}
