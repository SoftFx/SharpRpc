// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    public class Channel
    {
        private readonly object _stateSyncObj = new object();
        private TxPipeline _tx;
        private RxPipeline _rx;
        private readonly Endpoint _endpoint;
        private readonly MessageDispatcher _dispatcher;
        private readonly ContractDescriptor _descriptor;
        private readonly TaskCompletionSource<RpcResult> _connectEvent = new TaskCompletionSource<RpcResult>();
        private readonly TaskCompletionSource<RpcResult> _disconnectEvent = new TaskCompletionSource<RpcResult>();
        private readonly CancellationTokenSource _loginCancelSrc = new CancellationTokenSource();
        private RpcResult _channelDisplayFault = RpcResult.Ok;
        private RpcResult _channelOperationFault = RpcResult.ChannelClose;
        private ByteTransport _transport;
        private SessionCoordinator _coordinator;
        private bool _closeFlag;
        private bool _isServerSide;
        private static int idSeed;

        public ChannelState State { get; private set; }
        public RpcResult Fault => _channelDisplayFault;
        public string Id { get; }

        internal MessageDispatcher Dispatcher => _dispatcher;
        internal Endpoint Endpoint => _endpoint;
        internal TxPipeline Tx => _tx;
        internal ContractDescriptor Contract => _descriptor;
        internal LoggerFacade Logger { get; }

        internal event Action<Channel, RpcResult> Closed;

        internal Channel(bool serverSide, Endpoint endpoint, ContractDescriptor descriptor, RpcCallHandler msgHandler)
        {
            _isServerSide = serverSide;

            _endpoint = endpoint ?? throw new ArgumentNullException("endpoint");
            _descriptor = descriptor ?? throw new ArgumentNullException("descriptor");

            if (!_isServerSide)
                _endpoint.LockTo(this);

            Logger = endpoint.LoggerAdapter;
            Id = nameof(Channel) + Interlocked.Increment(ref idSeed);

            msgHandler.InvokeInit(this);

            _tx = new TxPipeline_NoQueue(descriptor, endpoint, OnCommunicationError, OnConnectionRequested);
            //_tx = new TxPipeline_OneThread(descriptor, endpoint, OnCommunicationError, OnConnectionRequested);
            _dispatcher = MessageDispatcher.Create(endpoint.Dispatcher, _tx, msgHandler, serverSide);

            Logger.Verbose(Id, "Created. Endpoint '{0}'.", endpoint.Name);
        }

        internal void StartIncomingSession(ByteTransport transport)
        {
            _isServerSide = true;
            _transport = transport;
            _coordinator = new ServerSideCoordinator();

            lock (_stateSyncObj)
                State = ChannelState.Connecting;

            DoConnect();
        }

        private void StartPipelines(ByteTransport transport)
        {
            if (_coordinator == null)
                _coordinator = new ClientSideCoordinator();

            _coordinator.Init(this);

            if (_endpoint.AsyncMessageParsing)
                _rx = new RxPipeline.OneThread(transport, _endpoint, _descriptor.SerializationAdapter, _dispatcher, _coordinator);
            else
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
            lock (_stateSyncObj)
            {
                _closeFlag = true;

                if (State == ChannelState.Online)
                    State = ChannelState.Disconnecting;
                else if (State == ChannelState.Disconnecting || State == ChannelState.Connecting)
                    return _disconnectEvent.Task;
                else if (State == ChannelState.New)
                {
                    State = ChannelState.Closed;
                    return Task.CompletedTask;
                }
                else
                    return Task.CompletedTask;
            }
            
            DoDisconnect(ChannelShutdownMode.Normal, LogoutOption.Immidiate);

            return _disconnectEvent.Task;
        }

        internal void OnCommunicationError(RpcResult fault)
        {
            lock (_stateSyncObj)
            {
                if (State == ChannelState.Online)
                {
                    State = ChannelState.Disconnecting;
                    UpdateFault(fault);
                }
                else if (State == ChannelState.Connecting)
                {
                    UpdateFault(fault);
                    _loginCancelSrc.Cancel();
                    return;
                }
                else
                    return;
            }

            Logger.Info(Id, "Communication error: " + fault.Code);

            DoDisconnect(ChannelShutdownMode.Abort, LogoutOption.Immidiate);
        }

        private void UpdateFault(RpcResult fault)
        {
            if (_channelDisplayFault.Code == RpcRetCode.Ok) // only first fault counts
            {
                _channelOperationFault = fault;
                _channelDisplayFault = fault;
            }
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
            else
                Logger.Info(Id, "Initializing connection...");


            if (_transport != null)
            {
                StartPipelines(_transport);

                // setup login timeout
                _loginCancelSrc.CancelAfter(_coordinator.LoginTimeout);

                // login handshake
                var loginResult = await _coordinator.OnConnect(_loginCancelSrc.Token);

                if (loginResult.Code != RpcRetCode.Ok)
                    UpdateFault(loginResult);
                else
                    _tx.StartProcessingUserMessages();
            }

            bool abortConnect = false;

            lock (_stateSyncObj)
            {
                // Note: a communication fault may be already occured at this time
                if (_closeFlag || _channelDisplayFault.Code != RpcRetCode.Ok)
                    abortConnect = true;
                else
                    State = ChannelState.Online;
            }

            if (abortConnect)
            {
                _tx.StopProcessingUserMessages(_channelOperationFault);

                await CloseComponents();

                lock (_stateSyncObj)
                    State = ChannelState.Faulted;

                Logger.Warn(Id, "Connection failed! Code: {0}", _channelDisplayFault.Code);

                Closed?.Invoke(this, _channelDisplayFault);

                _connectEvent.SetResult(_channelDisplayFault);
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

            await _dispatcher.Stop(_channelOperationFault);

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
            catch (Exception)
            {
                //TO DO : log
            }

            _transport?.Dispose();
        }

        private async void DoDisconnect(ChannelShutdownMode closeMode, LogoutOption logoutMode)
        {
            Logger.Info(Id, "Disconnecting... Mode: {0}, logout: {1}", closeMode, logoutMode);

            _tx.StopProcessingUserMessages(_channelOperationFault);

            if (closeMode == ChannelShutdownMode.Normal)
                await _coordinator.OnDisconnect(logoutMode);

            await CloseComponents();

            lock (_stateSyncObj)
            {
                if (_channelDisplayFault.Code != RpcRetCode.Ok)
                    State = ChannelState.Faulted;
                else
                    State = ChannelState.Closed;
            }

            Logger.Info(Id, "Disconnected. Final state: " + State);

            Closed?.Invoke(this, _channelDisplayFault);

            _disconnectEvent.SetResult(RpcResult.Ok);
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

    internal enum ChannelShutdownMode
    {
        /// <summary>
        /// Close channel with full logout sequnce.
        /// </summary>
        Normal,

        /// <summary>
        /// Close channel immediately without logout sequence. This option is typically used in fault situations.
        /// </summary>
        Abort
    }

    public enum LogoutOption
    {
        /// <summary>
        /// Close channel immidietly; Do not wait for completion of started calls; Do not require logout response from other side;
        /// </summary>
        Immidiate,

        /// <summary>
        /// Wait for all started calls to complete; Require logout response from other side
        /// </summary>
        EnsureCompletion
    }
}
