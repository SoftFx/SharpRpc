using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace SharpRpc
{
    internal abstract partial class RxPipeline
    {
        private readonly RxBuffer _buffer = new RxBuffer();
        private readonly MessageParser _parser = new MessageParser();
        private readonly RxMessageReader _reader = new RxMessageReader();
        private readonly ByteTransport _transport;
        private readonly IRpcSerializer _serializer;
        private readonly MessageBlock _msgConsumer;
        private readonly List<IMessage> _page = new List<IMessage>();
        private Task _rxLoop;

        public RxPipeline(ByteTransport transport, IRpcSerializer serializer, MessageBlock messageConsumer)
        {
            _transport = transport;
            _serializer = serializer;
            _msgConsumer = messageConsumer;
        }

        //protected abstract IList<ArraySegment<byte>> GetByteBuffer();
        protected abstract ValueTask<bool> OnBytesArrived(int count);
        protected MessageBlock MessageConsumer => _msgConsumer;

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
                var buffer = _buffer.Segments;
                int bytes;

                try
                {
                    bytes = await _transport.Receive(buffer);

                    if (bytes == 0)
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

                if (!await OnBytesArrived(bytes))
                    break;
            }
        }

        protected void SignalCommunicationError(RpcResult fault)
        {
            CommunicationFaulted?.Invoke(fault);
        }

        protected void Parse(RxParseTask task)
        {
            foreach (var segment in task)
            {
                _parser.SetNextSegment(segment);

                while (true)
                {
                    var pCode = _parser.ParseFurther();

                    if (pCode == MessageParser.RetCodes.EndOfSegment)
                        break;
                    else if (pCode == MessageParser.RetCodes.MessageParsed)
                    {
                        _reader.Init(_parser.MessageBody);

                        var msg = _serializer.Deserialize(_reader);
                        _msgConsumer.Consume(msg);
                    }
                }
            }

            _buffer.ReturnSegments(task);
        }

        protected void BatchParse(RxParseTask task)
        {
            _page.Clear();

            foreach (var segment in task)
            {
                _parser.SetNextSegment(segment);

                while (true)
                {
                    var pCode = _parser.ParseFurther();

                    if (pCode == MessageParser.RetCodes.EndOfSegment)
                        break;
                    else if (pCode == MessageParser.RetCodes.MessageParsed)
                    {
                        _reader.Init(_parser.MessageBody);

                        var msg = _serializer.Deserialize(_reader);
                        _page.Add(msg);
                    }
                }
            }

            if (_page.Count > 0)
                _msgConsumer.Consume(_page);

            _buffer.ReturnSegments(task);
        }

        internal class OneThread : RxPipeline
        {
            private readonly ActionBlock<RxParseTask> _parseBlock;
            private readonly CancellationTokenSource _stopSrc = new CancellationTokenSource();

            public OneThread(ByteTransport transport, Endpoint config, IRpcSerializer serializer, MessageBlock messageConsumer)
                : base(transport, serializer, messageConsumer)
            {
                var parseBlockOptions = new ExecutionDataflowBlockOptions();
                parseBlockOptions.BoundedCapacity = 5;
                parseBlockOptions.MaxDegreeOfParallelism = 1;
                parseBlockOptions.CancellationToken = _stopSrc.Token;

                if (_msgConsumer.SuportsBatching)
                    _parseBlock = new ActionBlock<RxParseTask>(BatchParse, parseBlockOptions);
                else
                    _parseBlock = new ActionBlock<RxParseTask>(Parse, parseBlockOptions);
            }

            public override void Start()
            {
                StartTransportRx();
            }

            //protected override IList<ArraySegment<byte>> GetByteBuffer()
            //{
            //    return _buffer.Segments;
            //}

            public override async Task Close()
            {
                try
                {
                    _stopSrc.Cancel();
                    _parseBlock.Complete();
                    await _parseBlock.Completion;

                    await WaitStopTransportRx();
                }
                catch (TaskCanceledException)
                {
                }
            }

            protected override ValueTask<bool> OnBytesArrived(int count)
            {
                var task = _buffer.Advance(count);

                return new ValueTask<bool>(_parseBlock.SendAsync(task));
            }
        }

        internal class NoThreading : RxPipeline
        {
            private volatile bool _isClosed;

            public NoThreading(ByteTransport transport, Endpoint config, IRpcSerializer serializer, MessageBlock messageConsumer)
                : base(transport, serializer, messageConsumer)
            {
                StartTransportRx();
            }

            //protected override IList<ArraySegment<byte>> GetByteBuffer()
            //{
            //    return _buffer.Segments;
            //}

            public override void Start()
            {
            }

            protected override ValueTask<bool> OnBytesArrived(int count)
            {
                if (_isClosed)
                    return new ValueTask<bool>(false);

                var task = _buffer.Advance(count);

                if (MessageConsumer.SuportsBatching)
                    BatchParse(task);
                else
                    Parse(task);

                return new ValueTask<bool>(true);
            }

            public override Task Close()
            {
                _isClosed = true;

                return Task.CompletedTask;
            }
        }
    }
}
