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
            //MessageHandler = handler;
            TaskQueue = tx.TaskQueue;
            Core = new MessageDispatcherCore(tx, handler, OnError);
            return this;
        }

        //protected IUserMessageHandler MessageHandler { get; private set; }
        protected TxPipeline Tx { get; private set; }
        protected TaskFactory TaskQueue { get; private set; }
        protected MessageDispatcherCore Core { get; private set; }

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

        protected abstract void DoCall(IRequest requestMsg, MessageDispatcherCore.ITask callTask);

        public Task Call<TResp>(IRequest requestMsg)
            where TResp : IResponse
        {
            var task = new MessageDispatcherCore.CallTask<TResp>();
            DoCall(requestMsg, task);
            return task.Task;
        }

        public Task<TReturn> Call<TResp, TReturn>(IRequest requestMsg)
            where TResp : IResponse
        {
            var task = new MessageDispatcherCore.CallTask<TResp, TReturn>();
            DoCall(requestMsg, task);
            return task.Task;
        }

        public Task<RpcResult> TryCall<TResp>(IRequest requestMsg)
            where TResp : IResponse
        {
            var task = new MessageDispatcherCore.TryCallTask<TResp>();
            DoCall(requestMsg, task);
            return task.Task;
        }

        public Task<RpcResult<TReturn>> TryCall<TResp, TReturn>(IRequest requestMsg)
            where TResp : IResponse
        {
            var task = new MessageDispatcherCore.TryCallTask<TResp, TReturn>();
            DoCall(requestMsg, task);
            return task.Task;
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
