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

        public ChannelState State { get; private set; }

        internal Channel(ByteTransport transport, Endpoint serverEndpoint)
        {
            _rx = new RxPipeline.OneThread(transport, serverEndpoint);
            //_tx = new TxPipeline(transport);
        }

        internal Channel(ClientEndpoint endpoint)
        {
            _endpoint = endpoint ?? throw new ArgumentNullException("endpoint");
        }

        public async Task ConnectAsync()
        {
            if (_endpoint == null)
                throw new InvalidOperationException();

            var transport = await _endpoint.ConnectAsync();
            _tx = new TxPipeline.OneLock(transport, _endpoint);
            _rx = new RxPipeline.OneThread(transport, _endpoint);
        }

        public bool TrySend(IMessage msg)
        {
            return _tx.Send(msg);
        }

        public ValueTask<bool> TrySendAsync(IMessage msg)
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
