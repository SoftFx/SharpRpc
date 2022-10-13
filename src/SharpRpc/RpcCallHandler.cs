// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Server;
using System;
using System.Threading.Tasks;

namespace SharpRpc
{
    public abstract class RpcCallHandler : IUserMessageHandler
    {
        private Channel _ch;

#if NET5_0_OR_GREATER
        protected abstract ValueTask OnMessage(IMessage message);
        protected abstract ValueTask<IResponseMessage> OnRequest(IRequestMessage message);
#else
        protected abstract Task OnMessage(IMessage message);
        protected abstract Task<IResponseMessage> OnRequest(IRequestMessage message);
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
        protected ValueTask<IResponseMessage> OnUnknownRequest(IRequestMessage message)
#else
        protected Task<IResponseMessage> OnUnknownRequest(IRequestMessage message)
#endif
        {
            throw new NotImplementedException();
        }

        protected IResponseMessage OnRegularFault(IRequestFaultMessage faultMessage, string exceptionMessage)
        {
            faultMessage.Code = RequestFaultCode.Fault;
            faultMessage.Text = exceptionMessage;
            return faultMessage;
        }

        protected IResponseMessage OnCustomFault(IRequestFaultMessage faultMessage, string exceptionMessage)
        {
            faultMessage.Code = RequestFaultCode.Fault;
            faultMessage.Text = exceptionMessage;
            return faultMessage;
        }
        
        protected IResponseMessage OnUnexpectedFault(IRequestFaultMessage faultMessage, Exception ex)
        {
            // TO DO : log!

            faultMessage.Code = RequestFaultCode.Crash;
            return faultMessage;
        }

        protected virtual void OnInit(Channel channel) { }

        internal void InvokeInit(Channel channel)
        {
            _ch = channel;
            OnInit(channel);
        }

        protected ServiceCallContext CreateCallContext(IRequestMessage requestMessage)
        {
            return new ServiceCallContext(requestMessage, _ch.Dispatcher);
        }

        protected ServiceStreamingCallContext<object, T> CreateOutputStreamContext<T>(IOpenStreamRequest request, IStreamMessageFactory<T> factory)
        {
            return new ServiceStreamingCallContext<object, T>(request, _ch.Tx, _ch.Dispatcher, null, factory);
        }

        protected ServiceStreamingCallContext<T, object> CreateInputStreamContext<T>(IOpenStreamRequest request, IStreamMessageFactory<T> factory)
        {
            return new ServiceStreamingCallContext<T, object>(request, _ch.Tx, _ch.Dispatcher, factory, null);
        }

        protected ServiceStreamingCallContext<TIn, TOut> CreateDuplexStreamContext<TIn, TOut>(IOpenStreamRequest request,
            IStreamMessageFactory<TIn> inFactory, IStreamMessageFactory<TOut> outFactory)
        {
            return new ServiceStreamingCallContext<TIn, TOut>(request, _ch.Tx, _ch.Dispatcher, inFactory, outFactory);
        }

        protected Task CloseStreamContext(IStreamContext context)
        {
            return context.Close(_ch);
        }

        protected void CloseContext(ServiceCallContext context)
        {
            context.Close(_ch);
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
        ValueTask<IResponseMessage> IUserMessageHandler.ProcessRequest(IRequestMessage message)
#else
        Task<IResponseMessage> IUserMessageHandler.ProcessRequest(IRequestMessage message)
#endif
        {
            return OnRequest(message);
        }
    }
}
