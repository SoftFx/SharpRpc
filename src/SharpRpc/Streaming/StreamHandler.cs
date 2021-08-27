// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    //public interface OutputStreamHandler<TItem>
    //{
    //    PagingTxStream<TItem> OutputStream { get; }
    //}

    //public interface InputStreamHandler<TItem>
    //{
    //    PagingRxStream<TItem> InputStream { get; }
    //}

    //public interface DuplexStreamHandler<TInItem, TOutItem>
    //{
    //    PagingRxStream<TInItem> InputStream { get; }
    //    PagingTxStream<TOutItem> OutputStream { get; }
    //}

    public interface IStreamHandler
    {
        string CallId { get; }
        Task Close(Channel ch);
    }

    public class StreamHandler<TInItem, TOutItem> : IStreamHandler, MessageDispatcherCore.IInteropOperation //: InputStreamHandler<TInItem>, OutputStreamHandler<TOutItem>
    {
        public StreamHandler(IOpenStreamRequest request, Channel ch, IStreamMessageFactory<TInItem> inFactory, IStreamMessageFactory<TOutItem> outFactory)
        {
            CallId = request.CallId;

            System.Diagnostics.Debug.WriteLine("RX " + CallId + " RQ " + request.GetType().Name);

            if (inFactory != null)
                InputStream = new PagingStreamReader<TInItem>(CallId, ch.Tx, inFactory);

            if (outFactory != null)
                OutputStream = new PagingStreamWriter<TOutItem>(CallId, ch, outFactory, true, 100, 5);

            ch.Dispatcher.RegisterCallObject(request.CallId, this);
        }

        public string CallId { get; }
        public PagingStreamReader<TInItem> InputStream { get; }
        public PagingStreamWriter<TOutItem> OutputStream { get; }

        public async Task Close(Channel ch)
        {
            //System.Diagnostics.Debug.WriteLine("CLOSE " + CallId);

            InputStream?.Abort();

            if (OutputStream != null)
                await OutputStream.CompleteAsync();

            ch.Dispatcher.UnregisterCallObject(CallId);
        }

        RpcResult MessageDispatcherCore.IInteropOperation.Complete(IResponse respMessage)
        {
            return new RpcResult(RpcRetCode.ProtocolViolation, "");
        }

        void MessageDispatcherCore.IInteropOperation.Fail(RpcResult result)
        {
        }

        void MessageDispatcherCore.IInteropOperation.Fail(IRequestFault faultMessage)
        {
        }

        RpcResult MessageDispatcherCore.IInteropOperation.Update(IStreamAuxMessage auxMessage)
        {
            //System.Diagnostics.Debug.WriteLine("RX " + CallId + " A.MSG " + auxMessage.GetType().Name);

            if (auxMessage is IStreamPage<TInItem> page)
            {
                InputStream.OnRx(page);
                return RpcResult.Ok;
            }
            else if (auxMessage is IStreamCompletionMessage compl)
            {
                InputStream.OnRx(compl);
                return RpcResult.Ok;
            }
            else if (auxMessage is IStreamPageAck ack)
            {
                OutputStream.OnRx(ack);
                return RpcResult.Ok;
            }

            return new RpcResult(RpcRetCode.ProtocolViolation, "");
        }
    }
}
