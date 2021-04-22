using SharpRpc.Lib;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace SharpRpc
{
    internal abstract partial class MessageDispatcher
    {
        public static MessageDispatcher Create(TxPipeline sender, IUserMessageHandler handler, ConcurrencyMode mode)
        {
            switch (mode)
            {
                //case ConcurrencyMode.NoQueue: return new NoThreading().Init(sender, handler);
                case ConcurrencyMode.PagedQueue: return new OneThread().Init(sender, handler);
                default: throw new InvalidOperationException();
            }
        }

        protected MessageDispatcher Init(TxPipeline tx, IUserMessageHandler handler)
        {
            Tx = tx;
            MessageHandler = handler;
            return this;
        }

        protected IUserMessageHandler MessageHandler { get; private set; }
        protected TxPipeline Tx { get; private set; }

        protected void OnError(RpcRetCode code, string message)
        {
            ErrorOccured?.Invoke(new RpcResult(code, message));
        }

        public event Action<RpcResult> ErrorOccured;

        public abstract void Start();
        public abstract void AllowMessages();
        public abstract void OnMessages(IEnumerable<IMessage> messages);
        public abstract Task Stop(RpcResult fault);

        protected abstract void DoCall(IRequest requestMsg, ITask callTask);

        public Task Call<TResp>(IRequest requestMsg)
            where TResp : IResponse
        {
            var task = new CallTask<TResp>();
            DoCall(requestMsg, task);
            return task.Task;
        }

        public Task<TReturn> Call<TResp, TReturn>(IRequest requestMsg)
            where TResp : IResponse
        {
            var task = new CallTask<TResp, TReturn>();
            DoCall(requestMsg, task);
            return task.Task;
        }

        public Task<RpcResult> TryCall<TResp>(IRequest requestMsg)
            where TResp : IResponse
        {
            var task = new TryCallTask<TResp>();
            DoCall(requestMsg, task);
            return task.Task;
        }

        public Task<RpcResult<TReturn>> TryCall<TResp, TReturn>(IRequest requestMsg)
            where TResp : IResponse
        {
            var task = new TryCallTask<TResp, TReturn>();
            DoCall(requestMsg, task);
            return task.Task;
        }

        protected interface ITask
        {
            void Complete(IResponse respMessage);
            void Fail(RpcResult result);
        }

        private class CallTask<TResp> : TaskCompletionSource<RpcResult>, ITask
            where TResp : IResponse
        {
            public void Complete(IResponse respMessage)
            {
                SetResult(RpcResult.Ok);
            }

            public void Fail(RpcResult result)
            {
                SetException(result.ToException());
            }
        }

        private class TryCallTask<TResp> : TaskCompletionSource<RpcResult>, ITask
            where TResp : IResponse
        {
            public void Complete(IResponse respMessage)
            {
                SetResult(RpcResult.Ok);
            }

            public void Fail(RpcResult result)
            {
                SetResult(result);
            }
        }

        private class CallTask<TResp, TReturn> : TaskCompletionSource<TReturn>, ITask
            where TResp : IResponse
        {
            public void Complete(IResponse respMessage)
            {
                var result = ((IResponse<TReturn>)respMessage).Result;
                SetResult(result);
            }

            public void Fail(RpcResult result)
            {
                SetException(result.ToException());
            }
        }

        private class TryCallTask<TResp, TReturn> : TaskCompletionSource<RpcResult<TReturn>>, ITask
            where TResp : IResponse
        {
            public void Complete(IResponse respMessage)
            {
                var result = ((IResponse<TReturn>)respMessage).Result;
                SetResult(new RpcResult<TReturn>(result));
            }

            public void Fail(RpcResult result)
            {
                SetResult(new RpcResult<TReturn>(result.Code, result.Fault));
            }
        }
    }

    internal interface IUserMessageHandler
    {
        ValueTask ProcessMessage(IMessage message);
        ValueTask<IResponse> ProcessRequest(IRequest message);
    }

    public enum ConcurrencyMode
    {
        NoQueue,
        PagedQueue,
    }
}
