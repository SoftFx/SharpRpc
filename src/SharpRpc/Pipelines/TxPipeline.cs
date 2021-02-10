using SharpRpc.Lib;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace SharpRpc
{
    internal abstract partial class TxPipeline
    {
        //private readonly BatchingActionBlock<IMessage> _msgQueue;
        //private readonly MessageQueue _msgQueue;
        //private readonly TxBuffer _buffer;
        private readonly ByteTransport _transport;

        public TxPipeline(ByteTransport transport)
        {
            _transport = transport;
        }

        //public TxPipeline(ByteTransport transport)
        //{
        //    _transport = transport;

        //    var options = new ExecutionDataflowBlockOptions();
        //    options.BoundedCapacity = 100;
        //    options.EnsureOrdered = true;
        //    options.MaxDegreeOfParallelism = 1;
        //    //_msgQueue = new BatchingActionBlock<IMessage>(SerializeMessages, 500, 500);

        //    _buffer = new TxBuffer(ushort.MaxValue);
        //    _msgQueue = new MessageQueue(_buffer, ushort.MaxValue * 2);

        //    TxBytesLoop();
        //}

        public abstract bool Send(IMessage message);
        public abstract ValueTask<bool> SendAsync(IMessage message);

        protected abstract ValueTask ReturnSegmentAndDequeue(List<ArraySegment<byte>> container);

        protected async void TxBytesLoop()
        {
            try
            {
                var segmentList = new List<ArraySegment<byte>>();

                while (true)
                {
                    await ReturnSegmentAndDequeue(segmentList);
                    await Task.Yield();
                    await _transport.Send(segmentList);
                }
            }
            catch (Exception ex)
            {
            }
        }

        //public bool Send(IMessage msg)
        //{
        //    return _msgQueue.TryEnqueue(msg);
        //}

        //public ValueTask<bool> SendAsync(IMessage msg)
        //{
        //    return _msgQueue.TryEnqueueAsync(msg);
        //    //SerializeMessage(msg);
        //    //return Task.CompletedTask;
        //}

        //private void SerializeMessage(IMessage msg)
        //{
        //    _buffer.StartMessageWrite(new MessageHeader());
        //    msg.Serialize(_buffer);
        //    _buffer.EndMessageWrite();
        //}

        //private void SerializeMessages(IList<IMessage> msgList)
        //{
        //    _buffer.LockForWrite();

        //    for (int i = 0; i < msgList.Count; i++)
        //    {
        //        _buffer.StartMessageWrite(new MessageHeader()); 
        //        msgList[i].Serialize(_buffer);
        //        _buffer.EndMessageWrite();
        //    }

        //    _buffer.ReleaseLock();
        //}
        
    }
}
