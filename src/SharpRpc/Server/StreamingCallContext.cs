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
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    public interface IStreamContext : CallContext
    {
        //string CallId { get; }
        Task Close(Channel ch);
    }

    public class ServiceStreamingCallContext<TInItem, TOutItem> : IStreamContext, IDispatcherOperation
    {
        private readonly CancellationTokenSource _cancelSrc;

        internal ServiceStreamingCallContext(IOpenStreamRequest request, TxPipeline msgTx, IDispatcher dispatcher,
            IStreamMessageFactory<TInItem> inFactory, IStreamMessageFactory<TOutItem> outFactory)
        {
            RequestMessage = request;
            CallId = request.CallId;

            //System.Diagnostics.Debug.WriteLine("RX " + CallId + " RQ " + request.GetType().Name);

            if ((request.Options & RequestOptions.CancellationEnabled) != 0)
            {
                _cancelSrc = new CancellationTokenSource();
                CancellationToken = _cancelSrc.Token;
            }
            else
                CancellationToken = CancellationToken.None;

            if (inFactory != null)
                InputStream = new ObjectStreamReader<TInItem>(CallId, msgTx, inFactory, dispatcher.Logger);

            if (outFactory != null)
            {
                OutputStream = new ObjectStreamWriter<TOutItem>(CallId, msgTx, outFactory, true,
                    new StreamOptions(request), dispatcher.Logger);
            }

            dispatcher.Register(this);
        }

        public string CallId { get; }
        public ObjectStreamReader<TInItem> InputStream { get; }
        public ObjectStreamWriter<TOutItem> OutputStream { get; }
        public IRequestMessage RequestMessage { get; }
        public CancellationToken CancellationToken { get; }

        public void StartCancellation() { }

        public async Task Close(Channel ch)
        {
            // Cancel the stream reader. Since the execution of the handler is ended, and no one is reading the stream.
            InputStream?.Cancel(true);

            if (OutputStream != null)
                await OutputStream.CompleteAsync();

            ch.Dispatcher.Unregister(this);
        }

        public void Abort(RpcResult fault)
        {
            InputStream?.Abort(fault);
            OutputStream?.Abort(fault);
        }

        RpcResult IDispatcherOperation.OnUpdate(IInteropMessage auxMessage)
        {
            //System.Diagnostics.Debug.WriteLine("RX " + CallId + " A.MSG " + auxMessage.GetType().Name);

            if (auxMessage is IStreamPage<TInItem> page)
            {
                InputStream.OnRx(page);
                return RpcResult.Ok;
            }
            else if (auxMessage is IStreamCloseMessage closeMsg)
            {
                InputStream.OnRx(closeMsg);
                return RpcResult.Ok;
            }
            else if (auxMessage is IStreamCancelMessage cancelMsg)
            {
                OutputStream.OnRx(cancelMsg);
                return RpcResult.Ok;
            }
            else if (auxMessage is IStreamPageAck ack)
            {
                OutputStream.OnRx(ack);
                return RpcResult.Ok;
            }
            else if (auxMessage is ICancelRequestMessage)
            {
                _cancelSrc?.Cancel();
                InputStream?.Abort(RpcResult.Ok);
            }

            return new RpcResult(RpcRetCode.ProtocolViolation, "");
        }

        void IDispatcherOperation.OnRequestCancelled() { }

        RpcResult IDispatcherOperation.OnResponse(IResponseMessage respMessage)
            => RpcResult.UnexpectedMessage(respMessage.GetType(), GetType());

        void IDispatcherOperation.OnFault(RpcResult result) { }
        void IDispatcherOperation.OnFaultResponse(IRequestFaultMessage faultMessage) { }
    }
}
