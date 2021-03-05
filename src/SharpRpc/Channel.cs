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
        private readonly MessageBlock _msgHandleBlock;
        private readonly IRpcSerializer _serializer;

        public ChannelState State { get; private set; }

        internal Channel(ClientEndpoint endpoint, IRpcSerializer serializer, IMessageHandler msgHandler)
            : this(null, endpoint, serializer, msgHandler)
        {
        }

        internal Channel(ByteTransport transport, Endpoint endpoint, IRpcSerializer serializer, IMessageHandler msgHandler)
        {
            _endpoint = endpoint;
            _serializer = serializer;

            _msgHandleBlock = MessageBlock.Create(msgHandler, endpoint.RxConcurrencyMode);

            if (transport != null)
                CreatePipelines(transport);
        }
        
        private void CreatePipelines(ByteTransport transport)
        {
            _tx = new TxPipeline.OneLock(transport, _serializer, _endpoint);
            _rx = new RxPipeline.OneThread(transport, _endpoint, _serializer, _msgHandleBlock);
        }

        public async Task ConnectAsync()
        {
            if (_endpoint == null)
                throw new InvalidOperationException();

            var transport = await ((ClientEndpoint)_endpoint).ConnectAsync();

            CreatePipelines(transport);
        }

        public RpcResult TrySend(IMessage msg)
        {
            return _tx.TrySend(msg);
        }

        public ValueTask<RpcResult> TrySendAsync(IMessage msg)
        {
            return _tx.TrySendAsync(msg);
        }

        public void Send(IMessage msg)
        {
            _tx.Send(msg);
        }

        public ValueTask SendAsync(IMessage msg)
        {
            return _tx.SendAsync(msg);
        }
    }

    public enum ChannelState
    {
        New,
        Connecting,
        Disconnecting,
        Closed,
        Faulted
    }
}
