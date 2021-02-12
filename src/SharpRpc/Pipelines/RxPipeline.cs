﻿using System;
using System.Collections.Generic;
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

            public OneThread(ByteTransport transport, Endpoint config) : base(transport)
            {
                _buffer = new RxBuffer();
                _serializer = config.Serializer;

                var parseBlockOptions = new ExecutionDataflowBlockOptions();
                parseBlockOptions.BoundedCapacity = 5;
                parseBlockOptions.MaxDegreeOfParallelism = 1;
                _parseBlock = new ActionBlock<RxParseTask>((Action<RxParseTask>)Parse, parseBlockOptions);

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

                            if (_parser.MessageBody.Count == 1)
                            {
                                var msg = _serializer.Deserialize(_reader);
                            }
                            else
                            {
                                //var msg = _serializer.Deserialize(_reader);
                            }
                        }
                    }
                }

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