using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace SharpRpc
{
    internal class MessageBlock
    {
        private readonly ActionBlock<IMessage> _actionBlock;
        private readonly IMessageHandler handler;

        public MessageBlock(int degreeOfParallelism, IMessageHandler handler)
        {
            var options = new ExecutionDataflowBlockOptions();
            options.MaxDegreeOfParallelism = degreeOfParallelism;

            _actionBlock = new ActionBlock<IMessage>(handler.ProcessMessage, options);
        }

        public void Consume(IMessage message)
        {
            _actionBlock.Post(message);
        }
    }

    internal interface IMessageHandler
    {
        Task ProcessMessage(IMessage message);
    }
}
