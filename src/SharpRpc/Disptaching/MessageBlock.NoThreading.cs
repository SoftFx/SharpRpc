using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    partial class MessageBlock
    {
        private class NoThreading : MessageBlock
        {
            public NoThreading(IMessageHandler handler) : base(handler)
            {
            }

            public override bool SuportsBatching => false;

            public override void Consume(IMessage message)
            {
                var retVal = MessageHandler.ProcessMessage(message);

                if (!retVal.IsCompleted)
                    retVal.AsTask().Wait();
            }

            public override void Consume(IEnumerable<IMessage> messages)
            {
                throw new NotImplementedException();
            }

            public override Task Close(bool dropTheQueue)
            {
                return Task.CompletedTask;
            }
        }
    }
}
