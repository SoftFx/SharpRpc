using SharpRpc.Lib;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace SharpRpc
{
    internal abstract partial class MessageBlock
    {
        public static MessageBlock Create(IMessageHandler handler, ConcurrencyMode mode)
        {
            switch (mode)
            {
                case ConcurrencyMode.NoQueue: return new NoThreading(handler);
                case ConcurrencyMode.DataflowX1: return new Dataflow(1, handler);
                case ConcurrencyMode.DataflowX2: return new Dataflow(2, handler);
                case ConcurrencyMode.PagedQueueX1: return new OneThread(handler);
                default: throw new InvalidOperationException();
            }
        }

        protected MessageBlock(IMessageHandler handler)
        {
            MessageHandler = handler;
        }

        protected IMessageHandler MessageHandler { get; }

        protected void OnError(RetCode code, string message)
        {
            ErrorOccured?.Invoke(new RpcResult(code, message));
        }

        public event Action<RpcResult> ErrorOccured;

        public abstract bool SuportsBatching { get; }
        public abstract void Consume(IMessage message);
        public abstract void Consume(IEnumerable<IMessage> messages);
        public abstract Task Close(bool dropTheQueue);
    }

    internal interface IMessageHandler
    {
        ValueTask ProcessMessage(IMessage message);
    }

    public enum ConcurrencyMode
    {
        NoQueue,
        DataflowX1,
        DataflowX2,
        PagedQueueX1,
    }
}
