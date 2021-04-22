using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    partial class RxPipeline
    {
        public class NoThreading : RxPipeline
        {
            private readonly RxBuffer _buffer;
            private volatile bool _isClosed;

            public NoThreading(ByteTransport transport, Endpoint config, IRpcSerializer serializer, MessageDispatcher messageConsumer, SessionCoordinator coordinator)
                : base(transport, config, serializer, messageConsumer, coordinator)
            {
                _buffer = new RxBuffer(config.RxBufferSegmentSize);
            }

            public override void Start()
            {
                StartTransportRx();
            }

            protected override void OnCommunicationError(RpcResult fault)
            {
                SignalCommunicationError(fault);
            }

            protected override ArraySegment<byte> AllocateRxBuffer()
            {
                return _buffer.GetRxSegment();
            }

            protected override ValueTask<bool> OnBytesArrived(int count)
            {
                if (_isClosed)
                    return new ValueTask<bool>(false);

                var rxData = _buffer.CommitDataRx(count);
                var parseRet = ParseAndDeserialize(rxData, out var bytesConsumed);

                if (parseRet.Code != RpcRetCode.Ok)
                {
                    SignalCommunicationError(parseRet);
                    return new ValueTask<bool>(false);
                }

                _buffer.CommitDataConsume(bytesConsumed);

                return new ValueTask<bool>(true);
            }

            public override Task Close()
            {
                _isClosed = true;

                return StopTransportRx();
            }
        }
    }
}
