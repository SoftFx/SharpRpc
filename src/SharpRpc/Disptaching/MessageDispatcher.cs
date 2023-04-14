// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Disptaching;
using SharpRpc.Lib;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal abstract partial class MessageDispatcher : IDispatcher
    {
        private string _opIdPrefix;
        private int _opIdSeed;

        public static MessageDispatcher Create(MessageDispatcherConfig config, Channel ch, RpcCallHandler handler, bool serverSide)
        {
            return new NoThreading().Init(serverSide, ch, handler);

            //switch (config.RxConcurrencyMode)
            //{
            //    case DispatcherConcurrencyMode.None: return new NoThreading().Init(serverSide, sender, handler);
            //    case DispatcherConcurrencyMode.Single: return new OneThread().Init(serverSide, sender, handler);
            //    default: throw new NotSupportedException("Conccurency mode is not supported: " + config.RxConcurrencyMode);
            //}
        }

        protected MessageDispatcher Init(bool serverSide, Channel ch, RpcCallHandler handler)
        {
            Channel = ch;
            Tx = ch.Tx;
            Logger = ch.Logger;
            _opIdPrefix = serverSide ? "S" : "C";
            //MessageHandler = handler;
            TaskQueue = Tx.TaskQueue;
            Core = new MessageDispatcherCore(Tx, handler, OnError);
            return this;
        }

        //protected IUserMessageHandler MessageHandler { get; private set; }
        protected TxPipeline Tx { get; private set; }
        protected Channel Channel { get; private set; }
        protected TaskFactory TaskQueue { get; private set; }
        protected MessageDispatcherCore Core { get; private set; }

        public IRpcLogger Logger { get; private set; }

        public List<IMessage> IncomingMessages { get; protected set; } = new List<IMessage>();

        public event Action<RpcResult> ErrorOccured;

        protected void OnError(RpcRetCode code, string message)
        {
            ErrorOccured?.Invoke(new RpcResult(code, message));
        }

        public abstract RpcResult Start();
#if NET5_0_OR_GREATER
        public abstract ValueTask OnMessages();
#else
        public abstract Task OnMessages();
#endif
        public abstract Task Stop(RpcResult fault);
        public abstract RpcResult Register(IDispatcherOperation operation);
        public abstract void Unregister(IDispatcherOperation operation);
        protected abstract void CancelOutgoingCall(IDispatcherOperation callObj);
        protected abstract void DoCall(IRequestMessage requestMsg, IDispatcherOperation callObj, CancellationToken cToken);

        public string GenerateOperationId()
        {
            return _opIdPrefix + Interlocked.Increment(ref _opIdSeed);
        }

        public void CancelOutgoingCall(object state)
        {
            CancelOutgoingCall((IDispatcherOperation)state);
        }

        public Task Call<TResp>(IRequestMessage requestMsg, CancellationToken cToken)
            where TResp : IResponseMessage
        {
            var task = new MessageDispatcherCore.CallTask<TResp>(requestMsg);
            DoCall(requestMsg, task, cToken);
            return task.Task;
        }

        public Task<TReturn> Call<TResp, TReturn>(IRequestMessage requestMsg, CancellationToken cToken)
            where TResp : IResponseMessage
        {
            var task = new MessageDispatcherCore.CallTask<TResp, TReturn>(requestMsg);
            DoCall(requestMsg, task, cToken);
            return task.Task;
        }

        public Task<RpcResult> TryCall<TResp>(IRequestMessage requestMsg, CancellationToken cToken)
            where TResp : IResponseMessage
        {
            var task = new MessageDispatcherCore.TryCallTask<TResp>(requestMsg);
            DoCall(requestMsg, task, cToken);
            return task.Task;
        }

        public Task<RpcResult<TReturn>> TryCall<TResp, TReturn>(IRequestMessage requestMsg, CancellationToken cToken)
            where TResp : IResponseMessage
        {
            var task = new MessageDispatcherCore.TryCallTask<TResp, TReturn>(requestMsg);
            DoCall(requestMsg, task, cToken);
            return task.Task;
        }
    }

//    internal interface IUserMessageHandler
//    {
//#if NET5_0_OR_GREATER
//        ValueTask ProcessMessage(IMessage message);
//        ValueTask<IResponseMessage> ProcessRequest(IRequestMessage message);
//#else
//        Task ProcessMessage(IMessage message);
//        Task<IResponseMessage> ProcessRequest(IRequestMessage message);
//#endif
//    }

    //public enum ConcurrencyMode
    //{
    //    NoQueue,
    //    PagedQueue,
    //}
}
