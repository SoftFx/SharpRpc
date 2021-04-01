using System;
using System.Collections.Generic;
using System.Text;
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
        private readonly TaskCompletionSource<RpcResult<ByteTransport>> _requestConnectEvent = new TaskCompletionSource<RpcResult<ByteTransport>>();
        private readonly TaskCompletionSource<RpcResult> _disconnectEvent = new TaskCompletionSource<RpcResult>();
        private RpcResult _channelDisplayFault = RpcResult.Ok;
        private RpcResult _channelOperationFault = RpcResult.ChannelClose;
        private ByteTransport _transport;
        private bool _closeFlag;

        public ChannelState State { get; private set; }
        public RpcResult Fault => _channelDisplayFault;
        public Guid Id { get; } = Guid.NewGuid();

        internal MessageDispatcher Dispatcher => _dispatcher;
        internal TxPipeline Tx => _tx;

        internal event Action<Channel, RpcResult> Closed;

        internal Channel(ClientEndpoint endpoint, ContractDescriptor descriptor, IUserMessageHandler msgHandler)
            : this(null, endpoint, descriptor, msgHandler)
        {
        }

        internal Channel(ByteTransport transport, Endpoint endpoint, ContractDescriptor descriptor, IUserMessageHandler msgHandler)
        {
            _transport = transport;
            _endpoint = endpoint;
            _descriptor = descriptor;

            _tx = new TxPipeline.NoQueue(descriptor, endpoint, OnConnectionRequest);
            _tx.CommunicationFaulted += OnCommunicationError;

            _dispatcher = MessageDispatcher.Create(_tx, msgHandler, endpoint.RxConcurrencyMode);

            if (transport != null)
            {
                State = ChannelState.Online;
                StartRxPipeline(transport);
            }
        }

        private void StartRxPipeline(ByteTransport transport)
        {
            _rx = new RxPipeline.Dataflow(transport, _endpoint, _descriptor.SerializationAdapter, _dispatcher);
            _rx.CommunicationFaulted += OnCommunicationError;
            _rx.Start();
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
            
            DoDisconnect();

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
                else if (State == ChannelState.Connecting && _channelDisplayFault.Code == RpcRetCode.Ok)
                {
                    UpdateFault(fault);
                    return;
                }
                else
                    return;
            }

            DoDisconnect();
        }

        private void UpdateFault(RpcResult fault)
        {
            _channelOperationFault = fault;
            _channelDisplayFault = fault;
        }

        private async void DoConnect()
        {
            bool abortConnect = false;

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

            if (_transport != null)
                StartRxPipeline(_transport);

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
                await CloseComponents();

                lock (_stateSyncObj)
                    State = ChannelState.Faulted;

                Closed?.Invoke(this, _channelDisplayFault);

                _connectEvent.SetResult(_channelDisplayFault);
                _requestConnectEvent.SetResult(new RpcResult<ByteTransport>(_channelDisplayFault.Code, _channelDisplayFault.Fault));
            }
            else
            {
                _connectEvent.SetResult(RpcResult.Ok);
                _requestConnectEvent.SetResult(new RpcResult<ByteTransport>(_transport));
            }
        }

        private async Task CloseComponents()
        {
            if (_transport != null)
                await _transport.Shutdown();

            try
            {
                var rxCloseTask = _rx?.Close();
                var txCloseTask = _tx.Close(_channelOperationFault);

                if (rxCloseTask != null)
                    await rxCloseTask;
                await txCloseTask;
            }
            catch (Exception)
            {
                //TO DO : log
            }

            _transport?.Dispose();
        }

        private async void DoDisconnect()
        {
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

        private Task<RpcResult<ByteTransport>> OnConnectionRequest()
        {
            bool invokeConnect = false;

            lock (_stateSyncObj)
            {
                if (State == ChannelState.Online)
                    return Task.FromResult(new RpcResult<ByteTransport>(_transport));
                else if (State == ChannelState.New)
                {
                    State = ChannelState.Connecting;
                    invokeConnect = true;
                }
                else if (State != ChannelState.Connecting)
                {
                    if (_channelDisplayFault.Code == RpcRetCode.Ok)
                        return Task.FromResult(new RpcResult<ByteTransport>(RpcRetCode.ChannelClosed, "Closed"));
                    else
                        return Task.FromResult(new RpcResult<ByteTransport>(_channelDisplayFault.Code, _channelDisplayFault.Fault));
                }
            }

            if (invokeConnect)
                DoConnect();

            return _requestConnectEvent.Task;
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
}
