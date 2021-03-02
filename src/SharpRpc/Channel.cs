using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public class Channel
    {
        private TxPipeline _tx;
        private RxPipeline _rx;
        private readonly ClientEndpoint _endpoint;
        private readonly MessageBlock _msgHandleBlock;

        public ChannelState State { get; private set; }

        internal Channel(ByteTransport transport, Endpoint serverEndpoint, IMessageHandler msgHandler)
        {
            _msgHandleBlock = new MessageBlock(1, msgHandler);
            _rx = new RxPipeline.OneThread(transport, serverEndpoint, _msgHandleBlock);
            //_tx = new TxPipeline(transport);
        }

        internal Channel(ClientEndpoint endpoint, IMessageHandler msgHandler)
        {
            _endpoint = endpoint ?? throw new ArgumentNullException("endpoint");
            _msgHandleBlock = new MessageBlock(1, msgHandler);
        }

        public async Task ConnectAsync()
        {
            if (_endpoint == null)
                throw new InvalidOperationException();

            var transport = await _endpoint.ConnectAsync();
            
            _tx = new TxPipeline.OneLock(transport, _endpoint);
            _rx = new RxPipeline.OneThread(transport, _endpoint, _msgHandleBlock);
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
