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
    public abstract class RpcServiceBase : IUserMessageHandler
    {
        private Channel _ch;

        protected abstract ValueTask OnMessage(IMessage message);
        protected abstract ValueTask<IResponse> OnRequest(IRequest message);

        protected ValueTask OnUnknownMessage(IMessage message)
        {
            return new ValueTask();
        }

        protected ValueTask<IResponse> OnUnknownRequest(IRequest message)
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
            OnInit(channel);
        }

        ValueTask IUserMessageHandler.ProcessMessage(IMessage message)
        {
            return OnMessage(message);
        }

        ValueTask<IResponse> IUserMessageHandler.ProcessRequest(IRequest message)
        {
            return OnRequest(message);
        }
    }
}
