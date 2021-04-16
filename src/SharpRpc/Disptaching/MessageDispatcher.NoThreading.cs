using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    partial class MessageDispatcher
    {
        //private class NoThreading : MessageDispatcher
        //{
        //    public NoThreading()
        //    {
        //    }

        //    public override bool SuportsBatching => false;

        //    public override void OnMessage(IMessage message)
        //    {
        //        var retVal = MessageHandler.ProcessMessage(message);

        //        if (!retVal.IsCompleted)
        //            retVal.AsTask().Wait();
        //    }

        //    protected override void DoCall(IRequest requestMsg, ITask callTask)
        //    {
        //        throw new NotImplementedException();
        //    }

        //    public override void OnMessages(IEnumerable<IMessage> messages)
        //    {
        //        throw new NotImplementedException();
        //    }

        //    public override Task Close(bool dropTheQueue)
        //    {
        //        return Task.CompletedTask;
        //    }
        //}
    }
}
