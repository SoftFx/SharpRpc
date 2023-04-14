// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Disptaching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    public interface CallContext
    {
        string CallId { get; }
        CancellationToken CancellationToken { get; }
    }

    public class ServiceCallContext : CallContext, IDispatcherOperation
    {
        private readonly CancellationTokenSource _cancelSrc;

        internal ServiceCallContext(IRequestMessage reqMessage, MessageDispatcher dispatcher)
        {
            CallId = reqMessage.CallId;
            RequestMessage = reqMessage;

            if ((reqMessage.Options & RequestOptions.CancellationEnabled) != 0)
            {
                _cancelSrc = new CancellationTokenSource();
                CancellationToken = _cancelSrc.Token;
                dispatcher.Register(this);
            }
            else
                CancellationToken = CancellationToken.None;
        }

        public string CallId { get; }
        public IRequestMessage RequestMessage { get; }
        public CancellationToken CancellationToken { get; }

        string IDispatcherOperation.CallId => throw new NotImplementedException();

        IRequestMessage IDispatcherOperation.RequestMessage => throw new NotImplementedException();

        public void StartCancellation() { }

        public void Close(Channel ch)
        {
        }

        RpcResult IDispatcherOperation.OnUpdate(IInteropMessage auxMessage)
        {
            if (auxMessage is ICancelRequestMessage)
            {
                if (_cancelSrc != null)
                {
                    _cancelSrc.Cancel();
                    return RpcResult.Ok;
                }
                else
                    return new RpcResult(RpcRetCode.UnexpectedMessage, "");
            }
            else
                return new RpcResult(RpcRetCode.UnexpectedMessage, "");
        }

        void IDispatcherOperation.OnRequestCancelled() { }

        RpcResult IDispatcherOperation.OnResponse(IResponseMessage respMessage)
            => RpcResult.UnexpectedMessage(typeof(IResponseMessage), typeof(CallContext));

        void IDispatcherOperation.OnFault(RpcResult result) { }
        void IDispatcherOperation.OnFaultResponse(IRequestFaultMessage faultMessage) { }

        void IDispatcherOperation.Abort(RpcResult fault) { }
    }
}
