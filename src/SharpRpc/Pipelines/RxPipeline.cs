using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace SharpRpc
{
    internal abstract partial class RxPipeline
    {
        private readonly ByteTransport _transport;

        public RxPipeline(ByteTransport transport)
        {
            _transport = transport;
        }

        protected abstract IList<ArraySegment<byte>> GetByteBuffer();
        protected abstract Task OnBytesArrived(int count);

        protected void StartTransportRx()
        {
            RxLoop();
        }

        private async void RxLoop()
        {
            while (true)
            {
                var buffer = GetByteBuffer();
                var bytes = await _transport.Receive(buffer);
                await OnBytesArrived(bytes);
            }
        }

        internal class OneThread : RxPipeline
        {
            private readonly RxBuffer _buffer;
            private readonly ActionBlock<RxParseTask> _parseBlock;
            private readonly MessageParser _parser = new MessageParser();
            private readonly IRpcSerializer _serializer;
            private readonly RxMessageReader _reader = new RxMessageReader();
            private readonly MessageBlock _msgConsumer;
            private readonly List<IMessage> _page = new List<IMessage>();

            public OneThread(ByteTransport transport, Endpoint config, IRpcSerializer serializer, MessageBlock messageConsumer) : base(transport)
            {
                _buffer = new RxBuffer();
                _serializer = serializer;
                _msgConsumer = messageConsumer;

                var parseBlockOptions = new ExecutionDataflowBlockOptions();
                parseBlockOptions.BoundedCapacity = 5;
                parseBlockOptions.MaxDegreeOfParallelism = 1;

                if (_msgConsumer.SuportsBatching)
                    _parseBlock = new ActionBlock<RxParseTask>(BatchParse, parseBlockOptions);
                else
                    _parseBlock = new ActionBlock<RxParseTask>(Parse, parseBlockOptions);

                StartTransportRx();
            }

            private void Parse(RxParseTask task)
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

            private void BatchParse(RxParseTask task)
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

            protected override IList<ArraySegment<byte>> GetByteBuffer()
            {
                return _buffer.Segments;
            }

            protected override Task OnBytesArrived(int count)
            {
                var task = _buffer.Advance(count);

                //_buffer.ReturnSegments(task);

                return _parseBlock.SendAsync(task);
                //return Task.CompletedTask;
            }
        }
    }
}
