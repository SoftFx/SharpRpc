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
using System.Diagnostics;
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
        private readonly CancellationTokenSource _connectCancellationSrc = new CancellationTokenSource();
        private RpcResult _channelFault;
        private ByteTransport _transport;
        private SessionCoordinator _coordinator;
        private bool _closeFlag;
        private bool _isServerSide;
        //private bool _isTransportDisposed;
        private Task _closeComponentsTask;

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
        internal object StateLockObject => _stateSyncObj;

        internal event Action<Channel, RpcResult> InternalClosed;

        public event AsyncEventHandler<ChannelOpeningArgs> Opening;
        //public event EventHandler<ChannelOpenedArgs> Opened;
        public event AsyncEventHandler<ChannelClosingArgs> Closing;
        public event EventHandler<ChannelClosedArgs> Closed;
        public event EventHandler<ChannelFailedToConnectArgs> FailedToConnect;

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

                if (Logger.IsInfoEnabled)
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

                ConnectRoutine();
            }
        }

        private void StartPipelines(ByteTransport transport)
        {
            //if (_endpoint.AsyncMessageParsing)
            //    _rx = new RxPipeline.OneThread(transport, _endpoint, _descriptor.SerializationAdapter, _dispatcher, _coordinator);
            //else
                _rx = new RxPipeline.NoThreading(Id, transport, _endpoint, _descriptor.SerializationAdapter, _dispatcher, _coordinator);

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
                ConnectRoutine();

            return FwAdapter.WrappResult(_connectEvent.Task);
        }

        public Task CloseAsync()
        {
            TriggerClose(new RpcResult(RpcRetCode.ChannelClosed, "Channel is closed."), false, out var completion);
            return completion;
        }

        internal void TriggerClose()
        {
            TriggerClose(new RpcResult(RpcRetCode.ChannelClosed, "Channel is closed."), false, out _);
        }

        internal void TriggerDisconnect(RpcResult reason)
        {
            TriggerClose(reason, false, out _);
        }

        private void TriggerClose(RpcResult reason, bool isConnectionLost, out Task closeCompletion)
        {
            lock (_stateSyncObj)
            {
                _closeFlag = true;

                if (isConnectionLost)
                    BeginCloseComponents(true);

                if (State == ChannelState.Online)
                {
                    State = ChannelState.Disconnecting;
                    UpdateFault(reason);
                }
                else if (State == ChannelState.Connecting)
                {
                    State = ChannelState.Disconnecting;
                    _connectCancellationSrc.Cancel();
                    UpdateFault(reason);
                    closeCompletion = _connectEvent.Task;
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
            if (fault.Code == RpcRetCode.ConnectionAbortedByPeer)
            {
                if (Logger.IsVerboseEnabled)
                    Logger.Verbose(Id, "The transport has been closed by the other side.");
            }
            else
                Logger.Error(Id, "Communication error: " + fault.Code);

            TriggerClose(fault, true, out _);
        }

        internal void UpdateFault(RpcResult fault)
        {
            if (_channelFault.Code == RpcRetCode.Ok) // only first fault counts
                _channelFault = fault;
        }

        private async void ConnectRoutine()
        {
            if (!_isServerSide)
            {
                Logger.Info(Id, "Connecting...");

                try
                {
                    var connectResult = await ((ClientEndpoint)_endpoint).ConnectAsync(_connectCancellationSrc.Token);
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

            if (_transport == null)
            {
                lock (_stateSyncObj)
                    State = ChannelState.Faulted;
                Logger.Warn(Id, "Failed to establish transport connection! Code: {0}", _channelFault.Code);
                _connectEvent.SetResult(_channelFault);
                await _dispatcher.Stop(_channelFault);
                RiseFailedToConnectEvent(_channelFault);
                return;
            }

            _transport.Init(this);
            _coordinator.Init(this);

            using (var loginTimeoutSrc = new CancellationTokenSource(_endpoint.LoginTimeout))
            {
                // start the coordinator before the pipelines
                Task startCoordinatorTask;
                lock (StateLockObject)
                {
                    startCoordinatorTask = _coordinator.OnConnect(loginTimeoutSrc.Token);
                    StartPipelines(_transport);
                }

                // login
                await startCoordinatorTask;
            }

            bool isLoggedIn = true;
            bool isAbortion = false;

            lock (_stateSyncObj)
            {
                // Note: a communication fault may be already occured at this time
                if (_closeFlag || _channelFault.Code != RpcRetCode.Ok)
                {
                    isLoggedIn = false;
                    isAbortion = _coordinator.IsCoordinationBroken;
                    State = ChannelState.Disconnecting;
                }
                else
                    State = ChannelState.Online;
            }

            // exit transport thread
            await Task.Factory.Dive();

            if (!isLoggedIn)
            {
                Logger.Warn(Id, "Failed to open a session! Code: {0}", _channelFault.Code);
                await DisconnectRoutine(isAbortion);
                lock (StateLockObject)
                    SetClosedState();
                Logger.Info(Id, "Disconnected. Final state: " + State);
                _connectEvent.SetResult(_channelFault);
                RiseFailedToConnectEvent(_channelFault);
            }
            else
            {
                Logger.Info(Id, "Connected.");
                _connectEvent.SetResult(RpcResult.Ok);
            }
        }

        private void OnLogoutTimeout()
        {
            lock (StateLockObject)
            {
                _coordinator.AbortCoordination();
                Logger.Warn(Id, "Logout operation timed out!");
                BeginCloseComponents(true);
            }
        }

        private Task BeginCloseComponents(bool isAbortion)
        {
            lock (StateLockObject)
            {
                if (_closeComponentsTask == null)
                {
                    _coordinator.AbortCoordination();
                    _closeComponentsTask = CloseComponents(isAbortion);
                }

                return _closeComponentsTask;
            }
        }

        private async Task CloseComponents(bool isAbortion)
        {
            Logger.Verbose(Id, "Stopping the dispatcher...");

            await _dispatcher.Stop(_channelFault);

            try
            {
                Logger.Verbose(Id, "Sopping Tx pipeline...");

                await _tx.Close(TimeSpan.FromSeconds(5));

                if (_transport != null)
                {
                    if (!isAbortion)
                    {
                        Logger.Verbose(Id, "Disconnecting the transport...");
                        await _transport.Shutdown();
                    }
                    else
                        DisposeTransport();
                }

                if (_rx != null)
                {
                    Logger.Verbose(Id, "Sopping Rx pipeline ...");
                    await _rx.Close();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(Id, "CloseComponents() failed!", ex);
            }

            if (!isAbortion)
                DisposeTransport();
        }

        private void DisposeTransport()
        {

            Logger.Verbose(Id, "Disposing the transport...");

            try
            {
                _transport?.Dispose();
            }
            catch (Exception)
            {
                // TO DO
            }
        }

        private async void DoDisconnect()
        {
            await _connectEvent.Task.ConfigureAwait(false);

            Logger.Info(Id, $"{_channelFault.FaultMessage} [{_channelFault.Code}] Disconnecting...");

            await DisconnectRoutine(false);

            lock (_stateSyncObj)
                SetClosedState();

            Logger.Info(Id, "Disconnected. Final state: " + State);

            InternalClosed?.Invoke(this, _channelFault);

            _disconnectEvent.SetResult(RpcResult.Ok);

            RiseClosedEvent(_channelFault, State == ChannelState.Faulted);
        }

        private void SetClosedState()
        {
            if (_channelFault.Code != RpcRetCode.ChannelClosedByOtherSide
                    && _channelFault.Code != RpcRetCode.ChannelClosed)
                State = ChannelState.Faulted;
            else
                State = ChannelState.Closed;
        }

        private async Task DisconnectRoutine(bool isAbortion)
        {
            if (!isAbortion)
            {
                using (var logoutTimeoutSrc = new CancellationTokenSource(Endpoint.LogoutTimeout))
                {
                    logoutTimeoutSrc.Token.Register(OnLogoutTimeout);
                    await _coordinator.OnDisconnect();
                    await BeginCloseComponents(false);
                }
            }
            else
                await BeginCloseComponents(true);
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
                ConnectRoutine();
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
                Logger.Error(Id, ex, "An Closing event handler threw an exception!");
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
                Logger.Error(Id, ex, "An Closed event handler threw an exception!");
            }
        }

        internal void RiseFailedToConnectEvent(RpcResult fault)
        {
            try
            {
                FailedToConnect?.Invoke(this, new ChannelFailedToConnectArgs(fault));
            }
            catch (Exception ex)
            {
                Logger.Error(Id, ex, "An FailedToConnect event handler threw an exception!");
            }
        }

#if PF_COUNTERS
        public double GetAverageRxBufferSize() => _rx.GetAvarageBufferRxSize();
        public double GetAverageRxMessageBatchSize() => _rx.GetAvarageMessagesPerBuffer();
        public int GetRxMessageCount()=> _rx.GetMessageCount();
        public double GetAverageRxMessageSize()=> _rx.GetAverageMessageSize();
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
            IsFaulted = isFaulted;
        }

        public RpcResult Reason { get; }
        public bool IsFaulted { get; }
    }

    public class ChannelFailedToConnectArgs : EventArgs
    {
        public ChannelFailedToConnectArgs(RpcResult reason)
        {
            Reason = reason;
        }

        public RpcResult Reason { get; }
    }
}
