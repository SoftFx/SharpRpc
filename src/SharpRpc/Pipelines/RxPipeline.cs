using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
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
        private readonly SessionCoordinator _coordinator;
        //private readonly CancellationTokenSource _rxCancelSrc = new CancellationTokenSource();
        private Task _rxLoop;

        public RxPipeline(ByteTransport transport, Endpoint config, IRpcSerializer serializer, MessageDispatcher messageConsumer, SessionCoordinator coordinator)
        {
            _transport = transport;
            _serializer = serializer;
            _msgConsumer = messageConsumer;
            _coordinator = coordinator;
        }

        protected MessageDispatcher MessageConsumer => _msgConsumer;

        public event Action<RpcResult> CommunicationFaulted;

        protected abstract ArraySegment<byte> AllocateRxBuffer();
        protected abstract ValueTask<bool> OnBytesArrived(int count);
        protected abstract void OnCommunicationError(RpcResult fault);

        public abstract void Start();
        public abstract Task Close();

        protected void StartTransportRx()
        {
            _rxLoop = RxLoop();
        }

        protected Task StopTransportRx()
        {
            //_rxCancelSrc.Cancel();
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

                    byteCount = await _transport.Receive(buffer, CancellationToken.None); // _rxCancelSrc.Token);

                    if (byteCount == 0)
                    {
                        OnCommunicationError(new RpcResult(RpcRetCode.ConnectionAbortedByPeer, "Connection is closed by foreign host."));
                        break;
                    }

                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (RpcException rex)
                {
                    OnCommunicationError(rex.ToRpcResult());
                    return;
                }
                catch (Exception ex)
                {
                    var fault = _transport.TranslateException(ex);
                    OnCommunicationError(fault);
                    return;
                }

                await OnBytesArrived(byteCount);

                //if (!await OnBytesArrived(byteCount))
                //    break;
            }
        }

        protected void SignalCommunicationError(RpcResult fault)
        {
            CommunicationFaulted?.Invoke(fault);
        }

        protected RpcResult ParseAndDeserialize(ArraySegment<byte> segment, out int bytesConsumed)
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

                        if (msg is ISystemMessage sysMsg)
                        {
                            var sysMsgResult = OnSystemMessage(sysMsg);
                            if (sysMsgResult.Code != RpcRetCode.Ok)
                                return sysMsgResult;
                        }
                        else
                            _page.Add(msg);
                    }
                    catch (Exception ex)
                    {
                        return new RpcResult(RpcRetCode.DeserializationError, ex.Message);
                    }

                    bytesConsumed += _parser.MessageBrutto;

                    _reader.Clear();
                }
                else
                    return new RpcResult(RpcRetCode.ProtocolViolation, "A violation of message markup protocol has been detected! Code: " + pCode);
            }

            if (_page.Count > 0)
                _msgConsumer.OnMessages(_page);

            return RpcResult.Ok;
        }

        private RpcResult OnSystemMessage(ISystemMessage msg)
        {
            // ignore hartbeat (it did his job by just arriving)
            if (msg is IHeartbeatMessage)
                return RpcResult.Ok;

            var result = _coordinator.OnMessage(msg);

            if (result.Code != RpcRetCode.Ok)
                return result;

            //if (isLoggedIn)
            //    _isLoggedIn = true;

            return RpcResult.Ok;
        }
    }
}
