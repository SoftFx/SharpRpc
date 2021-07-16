// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Lib;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal abstract partial class MessageDispatcher
    {
        public static MessageDispatcher Create(MessageDispatcherConfig config, TxPipeline sender, IUserMessageHandler handler)
        {
            switch (config.RxConcurrencyMode)
            {
                case DispatcherConcurrencyMode.None: return new NoThreading().Init(sender, handler);
                case DispatcherConcurrencyMode.Single: return new OneThread().Init(sender, handler);
                default: throw new NotSupportedException("Conccurency mode is not supported: " + config.RxConcurrencyMode);
            }
        }

        protected MessageDispatcher Init(TxPipeline tx, IUserMessageHandler handler)
        {
            Tx = tx;
            MessageHandler = handler;
            TaskQueue = tx.TaskQueue;
            return this;
        }

        protected IUserMessageHandler MessageHandler { get; private set; }
        protected TxPipeline Tx { get; private set; }
        protected TaskFactory TaskQueue { get; private set; }

        public List<IMessage> IncomingMessages { get; protected set; } = new List<IMessage>();

        public event Action<RpcResult> ErrorOccured;

        protected void OnError(RpcRetCode code, string message)
        {
            ErrorOccured?.Invoke(new RpcResult(code, message));
        }

        public abstract void Start();
        public abstract RpcResult OnSessionEstablished();
#if NET5_0_OR_GREATER
        public abstract ValueTask OnMessages();
#else
        public abstract Task OnMessages();
#endif
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
            void Fail(IRequestFault faultMessage);
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

            public void Fail(IRequestFault faultMessage)
            {
                SetException(faultMessage.CreateException());
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

            public void Fail(IRequestFault faultMessage)
            {
                var result = new RpcResult(faultMessage.Code.ToRetCode(), faultMessage.GetFault());
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

            public void Fail(IRequestFault faultMessage)
            {
                SetException(faultMessage.CreateException());
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

            public void Fail(IRequestFault faultMessage)
            {
                var result = new RpcResult<TReturn>(faultMessage.Code.ToRetCode(), faultMessage.GetFault());
                SetResult(result);
            }
        }
    }

    internal interface IUserMessageHandler
    {
#if NET5_0_OR_GREATER
        ValueTask ProcessMessage(IMessage message);
        ValueTask<IResponse> ProcessRequest(IRequest message);
#else
        Task ProcessMessage(IMessage message);
        Task<IResponse> ProcessRequest(IRequest message);
#endif
    }

    //public enum ConcurrencyMode
    //{
    //    NoQueue,
    //    PagedQueue,
    //}
}
