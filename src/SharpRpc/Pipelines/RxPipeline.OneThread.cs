using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    partial class RxPipeline
    {
        public class OneThread : RxPipeline
        {
            private object _lockObj = new object();
            private readonly RxBuffer _buffer;
            private bool _isClosing;
            private TaskCompletionSource<bool> _enqeueuWaitHandler;
            private TaskCompletionSource<bool> _closeWaitHandler;
            private bool _isBusy;
            private ArraySegment<byte> _segmentToParse;
            private ArraySegment<byte> _awaitingSegment;

            public OneThread(ByteTransport transport, Endpoint config, IRpcSerializer serializer, MessageDispatcher messageConsumer, SessionCoordinator coordinator)
                : base(transport, config, serializer, messageConsumer, coordinator)
            {
                _buffer = new RxBuffer(config.RxBufferSegmentSize);
            }

            protected override ArraySegment<byte> AllocateRxBuffer()
            {
                lock (_lockObj)
                    return _buffer.GetRxSegment();
            }

            protected override ValueTask<bool> OnBytesArrived(int count)
            {
                lock (_lockObj)
                {
                    if (_isClosing)
                        return new ValueTask<bool>(false);

                    var arrivedData = _buffer.CommitDataRx(count);

                    if (_isBusy)
                    {
                        Debug.Assert(_enqeueuWaitHandler == null);

                        _awaitingSegment = arrivedData;
                        _enqeueuWaitHandler = new TaskCompletionSource<bool>();
                        return new ValueTask<bool>(_enqeueuWaitHandler.Task);
                    }
                    else
                    {
                        LaunchWorker(arrivedData);
                        return new ValueTask<bool>(true);
                    }
                }
            }

            private void LaunchWorker(ArraySegment<byte> dataToProcess)
            {
                _isBusy = true;

                _segmentToParse = dataToProcess;
                Task.Factory.StartNew(ParseSegment);
            }

            public override void Start()
            {
                StartTransportRx();
            }

            public override Task Close()
            {
                lock (_lockObj)
                {
                    _isClosing = true;

                    if (!_isBusy)
                    {
                        CompleteClose();
                        return WaitStopTransportRx();
                    }
                    else
                    {
                        _closeWaitHandler = new TaskCompletionSource<bool>();
                        return Task.WhenAll(_closeWaitHandler.Task, WaitStopTransportRx());
                    }
                }
            }

            private void ParseSegment()
            {
                var parseRes = ParseAndDeserialize(_segmentToParse, out var bytesConsumed);

                if (parseRes.Code == RpcRetCode.Ok)
                    OnParseCompleted(bytesConsumed);
                else
                    OnParseFailed(parseRes);       
            }

            private void OnParseCompleted(int dataSize)
            {
                TaskCompletionSource<bool> toSignal = null;
                bool isNotClosed;

                lock (_lockObj)
                {
                    _isBusy = false;
                    _segmentToParse = default;

                    _buffer.CommitDataConsume(dataSize);

                    if (_enqeueuWaitHandler != null)
                    {
                        toSignal = _enqeueuWaitHandler;
                        _enqeueuWaitHandler = null;

                        if (!_isClosing)
                        {
                            LaunchWorker(_awaitingSegment);
                            _awaitingSegment = default;
                        }
                        else
                            CompleteClose();
                    }

                    isNotClosed = !_isClosing;
                }

                toSignal?.SetResult(isNotClosed);
            }

            private void OnParseFailed(RpcResult result)
            {
                TaskCompletionSource<bool> toSignal = null;

                lock (_lockObj)
                {
                    _isClosing = true;
                    _isBusy = false;

                    toSignal = _enqeueuWaitHandler;
                    _enqeueuWaitHandler = null;
                }

                toSignal?.SetResult(false);

                SignalCommunicationError(result);
            }

            private void CompleteClose()
            {
                _buffer.Dispose();
                _closeWaitHandler?.TrySetResult(true);
            }
        }
    }
}
