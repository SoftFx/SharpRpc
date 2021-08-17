// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public abstract class RpcCallHandler : IUserMessageHandler
    {
        private Channel _ch;

        public SessionInfo Session { get; } = new SessionInfo();

#if NET5_0_OR_GREATER
        protected abstract ValueTask OnMessage(IMessage message);
        protected abstract ValueTask<IResponse> OnRequest(IRequest message);
#else
        protected abstract Task OnMessage(IMessage message);
        protected abstract Task<IResponse> OnRequest(IRequest message);
#endif

#if NET5_0_OR_GREATER
        protected ValueTask OnUnknownMessage(IMessage message)
#else
        protected Task OnUnknownMessage(IMessage message)
#endif
        {
            return FwAdapter.AsyncVoid;
        }

#if NET5_0_OR_GREATER
        protected ValueTask<IResponse> OnUnknownRequest(IRequest message)
#else
        protected Task<IResponse> OnUnknownRequest(IRequest message)
#endif
        {
            throw new NotImplementedException();
        }

        protected IResponse OnCustomFault<T>(string callId, T fault)
            where T : RpcFault
        {
            var resp = _ch.Contract.SystemMessages.CreateFaultMessage<T>(fault);
            resp.CallId = callId;
            resp.FaultData = fault;
            return resp;
        }

        protected IResponse OnRegularFault(string callId, string exceptionMessage)
        {
            // TO DO : log!

            var faultMsg = _ch.Contract.SystemMessages.CreateFaultMessage();
            faultMsg.CallId = callId;
            faultMsg.Code = RequestFaultCode.RegularFault;
            faultMsg.Text = exceptionMessage;
            return faultMsg;
        }

        protected IResponse OnUnexpectedFault(string callId, Exception ex)
        {
            var faultMsg = _ch.Contract.SystemMessages.CreateFaultMessage();
            faultMsg.CallId = callId;
            faultMsg.Code = RequestFaultCode.UnexpectedFault;
            return faultMsg;
        }

        protected virtual void OnInit(Channel channel) { }

        internal void InvokeInit(Channel channel)
        {
            _ch = channel;
            Session.Init(channel);
            OnInit(channel);
        }

        protected StreamHandler<object, T> CreateOutputStreamHandler<T>(IOpenStreamRequest request, IStreamMessageFactory<T> factory)
        {
            var handler = new StreamHandler<object, T>(request, _ch, null, factory);
            _ch.Dispatcher.RegisterCallObject(request.CallId, handler);
            return handler;
        }

        protected StreamHandler<T, object> CreateInputStreamHandler<T>(IOpenStreamRequest request, IStreamMessageFactory<T> factory)
        {
            var handler = new StreamHandler<T, object>(request, _ch, factory, null);
            _ch.Dispatcher.RegisterCallObject(request.CallId, handler);
            return handler;
        }

        protected StreamHandler<TIn, TOut> CreateDuplexStreamHandler<TIn, TOut>(IOpenStreamRequest request,
            IStreamMessageFactory<TIn> inFactory, IStreamMessageFactory<TOut> outFactory)
        {
            var handler = new StreamHandler<TIn, TOut>(request, _ch, inFactory, outFactory);
            _ch.Dispatcher.RegisterCallObject(request.CallId, handler);
            return handler;
        }

#if NET5_0_OR_GREATER
        ValueTask IUserMessageHandler.ProcessMessage(IMessage message)
#else
        Task IUserMessageHandler.ProcessMessage(IMessage message)
#endif
        {
            return OnMessage(message);
        }

#if NET5_0_OR_GREATER
        ValueTask<IResponse> IUserMessageHandler.ProcessRequest(IRequest message)
#else
        Task<IResponse> IUserMessageHandler.ProcessRequest(IRequest message)
#endif
        {
            return OnRequest(message);
        }
    }
}
