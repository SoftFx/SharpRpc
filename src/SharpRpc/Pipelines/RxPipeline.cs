using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal abstract partial class RxPipeline
    {
        private readonly MessageParser _parser = new MessageParser();
        private readonly RxMessageReader _reader = new RxMessageReader();
        private readonly ByteTransport _transport;
        private readonly IRpcSerializer _serializer;
        private readonly MessageDispatcher _msgConsumer;
        private readonly List<IMessage> _page = new List<IMessage>();
        private Task _rxLoop;

        public RxPipeline(ByteTransport transport, Endpoint config, IRpcSerializer serializer, MessageDispatcher messageConsumer)
        {
            _transport = transport;
            _serializer = serializer;
            _msgConsumer = messageConsumer;
        }

        protected abstract ArraySegment<byte> AllocateRxBuffer();
        protected abstract ValueTask<bool> OnBytesArrived(int count);
        protected MessageDispatcher MessageConsumer => _msgConsumer;

        public event Action<RpcResult> CommunicationFaulted;

        public abstract void Start();
        public abstract Task Close();

        protected void StartTransportRx()
        {
            _rxLoop = RxLoop();
        }

        protected Task WaitStopTransportRx()
        {
            return _rxLoop;
        }

        private async Task RxLoop()
        {
            while (true)
            {
                int byteCount;

                try
                {
                    var buffer = AllocateRxBuffer();

                    byteCount = await _transport.Receive(buffer);

                    if (byteCount == 0)
                    {
                        SignalCommunicationError(new RpcResult(RpcRetCode.ConnectionShutdown, ""));
                        break;
                    }

                }
                catch (RpcException rex)
                {
                    SignalCommunicationError(rex.ToRpcResult());
                    return;
                }
                catch (Exception ex)
                {
                    var fault = _transport.TranslateException(ex);
                    SignalCommunicationError(fault);
                    return;
                }

                if (!await OnBytesArrived(byteCount))
                    break;
            }
        }

        protected void SignalCommunicationError(RpcResult fault)
        {
            CommunicationFaulted?.Invoke(fault);
        }

        protected RpcResult ParseAndDeserialize(ArraySegment<byte> segment, out int bytesConsumed)
        {
            if (_msgConsumer.SuportsBatching)
                return BatchParseAndDeserialize(segment, out bytesConsumed);
            else
                return ParseAndDeserializeOneByOne(segment, out bytesConsumed);
        }

        protected RpcResult ParseAndDeserializeOneByOne(ArraySegment<byte> segment, out int bytesConsumed)
        {
            bytesConsumed = 0;

            _parser.SetNextSegment(segment);

            while (true)
            {
                var pCode = _parser.ParseFurther();

                if (pCode == MessageParser.RetCodes.EndOfSegment)
                    break;
                else if (pCode == MessageParser.RetCodes.MessageParsed)
                {
                    _reader.Init(_parser.MessageBody);

                    IMessage msg;

                    try
                    {
                        msg = _serializer.Deserialize(_reader);
                    }
                    catch (Exception ex)
                    {
                        return new RpcResult(RpcRetCode.DeserializationError, ex.Message);
                    }

                    _msgConsumer.OnMessage(msg);

                    bytesConsumed += _reader.MsgSize;
                    _reader.Clear();
                }
                else
                    return new RpcResult(RpcRetCode.ProtocolViolation, "A violation of message markup protocol has been detected. Code: " + pCode);
            }

            return RpcResult.Ok;
        }

        protected RpcResult BatchParseAndDeserialize(ArraySegment<byte> segment, out int bytesConsumed)
        {
            bytesConsumed = 0;

            _page.Clear();
            _parser.SetNextSegment(segment);

            while (true)
            {
                var pCode = _parser.ParseFurther();

                if (pCode == MessageParser.RetCodes.EndOfSegment)
                    break;
                else if (pCode == MessageParser.RetCodes.MessageParsed)
                {
                    _reader.Init(_parser.MessageBody);

                    try
                    {
                        var msg = _serializer.Deserialize(_reader);
                        _page.Add(msg);
                    }
                    catch (Exception ex)
                    {
                        return new RpcResult(RpcRetCode.DeserializationError, ex.Message);
                    }

                    bytesConsumed += _reader.MsgSize;

                    _reader.Clear();
                }
            }

            if (_page.Count > 0)
                _msgConsumer.OnMessages(_page);

            return RpcResult.Ok;
        }
    }
}
