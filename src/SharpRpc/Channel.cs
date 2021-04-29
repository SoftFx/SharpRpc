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

        public ChannelState State { get; private set; }
        public RpcResult Fault => _channelDisplayFault;
        public Guid Id { get; } = Guid.NewGuid();

        internal MessageDispatcher Dispatcher => _dispatcher;
        internal Endpoint Endpoint => _endpoint;
        internal TxPipeline Tx => _tx;
        internal ContractDescriptor Contract => _descriptor;

        internal event Action<Channel, RpcResult> Closed;

        internal Channel(Endpoint endpoint, ContractDescriptor descriptor, IUserMessageHandler msgHandler)
        {
            _endpoint = endpoint;
            _descriptor = descriptor;

            _tx = new TxPipeline.NoQueue(descriptor, endpoint);
            _tx.ConnectionRequested += OnConnectionRequested;
            _tx.CommunicationFaulted += OnCommunicationError;

            _dispatcher = MessageDispatcher.Create(_tx, msgHandler);   
        }

        internal void StartServerMode(ByteTransport transport)
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
                _coordinator = new ClientSideCoordinator(false);

            _coordinator.Init(this);

            _rx = new RxPipeline.NoThreading(transport, _endpoint, _descriptor.SerializationAdapter, _dispatcher, _coordinator);
            _rx.CommunicationFaulted += OnCommunicationError;
            _rx.Start();

            _tx.Start(transport);
        }

        public ValueTask<RpcResult> TryConnectAsync()
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
                    return new ValueTask<RpcResult>(new RpcResult(RpcRetCode.InvalidChannelState, "TryConnectAsync() cannot be called while channel in state: " + State));
            }

            if (invokeConnect)
                DoConnect();

            return new ValueTask<RpcResult>(_connectEvent.Task);
        }

        public Task CloseAsync()
        {
            lock (_stateSyncObj)
            {
                _closeFlag = true;

                if (State == ChannelState.Online)
                    State = ChannelState.Disconnecting;
                else if (State == ChannelState.Disconnecting)
                    return _disconnectEvent.Task;
                else if (State == ChannelState.New)
                {
                    State = ChannelState.Closed;
                    return Task.CompletedTask;
                }
                else if (State == ChannelState.Connecting)
                    return _disconnectEvent.Task;
                else
                    return Task.CompletedTask;
            }
            
            DoDisconnect(ChannelShutdownMode.Normal);

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

            DoDisconnect(ChannelShutdownMode.Abort);
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
                try
                {
                    var connectResult = await ((ClientEndpoint)_endpoint).ConnectAsync();
                    if (connectResult.Code == RpcRetCode.Ok)
                        _transport = connectResult.Result;
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

                Closed?.Invoke(this, _channelDisplayFault);

                _connectEvent.SetResult(_channelDisplayFault);
            }
            else
                _connectEvent.SetResult(RpcResult.Ok);
        }

        private async Task CloseComponents()
        {
            await _dispatcher.Stop(_channelOperationFault);

            try
            {
                var rxCloseTask = _rx?.Close();
                var txCloseTask = _tx.Close(TimeSpan.FromSeconds(5));

                await txCloseTask;
                
                if (_transport != null)
                    await _transport.Shutdown();

                if (rxCloseTask != null)
                    await rxCloseTask;
            }
            catch (Exception ex)
            {
                //TO DO : log
            }

            _transport?.Dispose();
        }

        private async void DoDisconnect(ChannelShutdownMode mode)
        {
            _tx.StopProcessingUserMessages(_channelOperationFault);

            if (mode == ChannelShutdownMode.Normal)
                await _coordinator.OnDisconnect();

            await CloseComponents();

            lock (_stateSyncObj)
            {
                if (_channelDisplayFault.Code != RpcRetCode.Ok)
                    State = ChannelState.Faulted;
                else
                    State = ChannelState.Closed;
            }

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
        Normal,
        Abort
    }
}
